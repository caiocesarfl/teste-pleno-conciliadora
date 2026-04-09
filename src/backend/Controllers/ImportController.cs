
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;
using Parking.Api.Models;
using Parking.Api.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Parking.Api.Controllers
{
    [ApiController]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PlacaService _placa;
        public ImportController(AppDbContext db, PlacaService placa) { _db = db; _placa = placa; }

        [HttpPost("csv")]
        public async Task<IActionResult> ImportCsvDetalhado(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Envie um arquivo CSV.");

            var resultados = new List<object>();
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            int linhaAtual = 0;
            string? header = await reader.ReadLineAsync(); 

            while (!reader.EndOfStream)
            {
                linhaAtual++;
                var raw = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    resultados.Add(new { Linha = linhaAtual, Status = "Erro", Mensagem = "Linha vazia" });
                    continue;
                }

                try
                {
                    var colunas = raw.Split(',');
                    if (colunas.Length != 9)
                        throw new Exception("Colunas insuficientes");

                    String placa = colunas[0].Trim().ToUpperInvariant();
                    var modelo = colunas[1].Trim();
                    var ano = int.Parse(colunas[2]);
                    var clienteId = colunas[3].Trim();
                    var nome = colunas[4].Trim();
                    var telefone = Regex.Replace(colunas[5], @"\D", "");
                    var endereco = colunas[6].Trim();
                    var mensalista = bool.Parse(colunas[7]);
                    var valorMensalidade = decimal.Parse(colunas[8], CultureInfo.InvariantCulture);                   

                    resultados.Add(new
                    {
                        Linha = linhaAtual,
                        Status = "Sucesso",
                        Placa = placa,
                        Modelo = modelo,
                        Ano = ano,
                        Cliente = nome
                    });
                }
                catch (Exception ex)
                {
                    resultados.Add(new
                    {
                        Linha = linhaAtual,
                        Status = "Erro",
                        Mensagem = ex.Message,
                        Conteudo = raw
                    });
                }
            }

            return Ok(resultados);
        }
    }
}
