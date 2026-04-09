namespace Parking.Api.Models
{
    public class VeiculoCliente
    {
        public Guid Id { get; set; }
        public Guid VeiculoId { get; set; }
        public Guid ClienteId { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public decimal? ValorMensalidade { get; set; }

        // Navegação (opcional)
        public Veiculo? Veiculo { get; set; }
        public Cliente? Cliente { get; set; }
    }
}
