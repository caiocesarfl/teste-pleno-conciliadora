
using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;
using Parking.Api.Models;

namespace Parking.Api.Services
{
    public class FaturamentoService
    {
        private readonly AppDbContext _db;
        public FaturamentoService(AppDbContext db) => _db = db;

        // BUG proposital: usa dono ATUAL do veículo em vez do dono NA DATA DE CORTE
        public async Task<List<Fatura>> GerarAsync(string competencia, CancellationToken ct = default)
        {
            // competencia formato yyyy-MM
            var part = competencia.Split('-');
            var ano = int.Parse(part[0]);
            var mes = int.Parse(part[1]);
            var ultimoDia = DateTime.DaysInMonth(ano, mes);
            var corte = new DateTime(ano, mes, ultimoDia, 23, 59, 59, DateTimeKind.Utc);

            var mensalistas = await _db.Clientes
                .Where(c => c.Mensalista)
                .AsNoTracking()
                .ToListAsync(ct);

            var criadas = new List<Fatura>();

            foreach (var cli in mensalistas)
            {
                var existente = await _db.Faturas
                    .FirstOrDefaultAsync(f => f.ClienteId == cli.Id && f.Competencia == competencia, ct);
                if (existente != null) continue; // idempotência simples

                // Busca veículos que estavam associados ao cliente na data de corte
                var veiculosNaDataCorte = await _db.VeiculoClientes
                    .Where(vc => vc.ClienteId == cli.Id &&
                                 vc.DataInicio <= corte &&
                                 (vc.DataFim == null || vc.DataFim >= corte))
                    .Select(vc => vc.VeiculoId)
                    .ToListAsync(ct);

                var fat = new Fatura
                {
                    Competencia = competencia,
                    ClienteId = cli.Id,
                    Valor = cli.ValorMensalidade ?? 0m,
                    Observacao = "Faturamento correto: dono na data de corte"
                };

                foreach (var id in veiculosNaDataCorte)
                    fat.Veiculos.Add(new FaturaVeiculo { FaturaId = fat.Id, VeiculoId = id });

                _db.Faturas.Add(fat);
                criadas.Add(fat);
            }

            await _db.SaveChangesAsync(ct);
            return criadas;
        }
        public async Task<List<Fatura>> GerarFaturamentoParcialAsync(
            string competencia,
            DateTime dataInicial,
            DateTime dataFinal,
            CancellationToken ct = default)
        {
            var part = competencia.Split('-');
            var ano = int.Parse(part[0]);
            var mes = int.Parse(part[1]);
            var primeiroDiaMes = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var ultimoDiaMes = new DateTime(ano, mes, DateTime.DaysInMonth(ano, mes), 23, 59, 59, DateTimeKind.Utc);

            var inicioFaturamento = dataInicial < primeiroDiaMes ? primeiroDiaMes : dataInicial;
            var fimFaturamento = dataFinal > ultimoDiaMes ? ultimoDiaMes : dataFinal;

            var mensalistas = await _db.Clientes
                .Where(c => c.Mensalista)
                .AsNoTracking()
                .ToListAsync(ct);

            var criadas = new List<Fatura>();

            foreach (var cli in mensalistas)
            {
                var existente = await _db.Faturas
                    .FirstOrDefaultAsync(f => f.ClienteId == cli.Id && f.Competencia == competencia, ct);
                if (existente != null) continue;

                var veiculos = await _db.VeiculoClientes
                    .Where(vc => vc.ClienteId == cli.Id &&
                                 vc.DataInicio <= fimFaturamento &&
                                 (vc.DataFim == null || vc.DataFim >= inicioFaturamento))
                    .ToListAsync(ct);

                decimal valorTotal = 0m;
                var fat = new Fatura
                {
                    Competencia = competencia,
                    ClienteId = cli.Id,
                    Valor = 0m,
                    Observacao = $"Faturamento parcial de {inicioFaturamento:yyyy-MM-dd} até {fimFaturamento:yyyy-MM-dd}"
                };

                foreach (var assoc in veiculos)
                {
                    var inicio = assoc.DataInicio > inicioFaturamento ? assoc.DataInicio : inicioFaturamento;
                    var fim = assoc.DataFim != null && assoc.DataFim < fimFaturamento ? assoc.DataFim.Value : fimFaturamento;
                    var dias = (fim - inicio).Days + 1;
                    var diasNoMes = (ultimoDiaMes - primeiroDiaMes).Days + 1;

                    // Proporcional ao período
                    var valorProporcional = (cli.ValorMensalidade ?? 0m) * dias / diasNoMes;
                    valorTotal += valorProporcional;

                    fat.Veiculos.Add(new FaturaVeiculo { FaturaId = fat.Id, VeiculoId = assoc.VeiculoId });
                }

                fat.Valor = valorTotal;
                _db.Faturas.Add(fat);
                criadas.Add(fat);
            }

            await _db.SaveChangesAsync(ct);
            return criadas;
        }
    }
}
