namespace DataMedix.Domain.Entities
{
    public class ParametroClinico
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Codigo { get; set; } = null!;         // HB, FE, FERR, ISAT
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string? UnidadMedidaDefault { get; set; }
        public string TipoDato { get; set; } = "numerico";   // numerico | texto
        public decimal? ValorMinReferencia { get; set; }
        public decimal? ValorMaxReferencia { get; set; }
        public bool EsParametroClave { get; set; } = false;  // HB, Fe, Ferr, ISAT
        public int OrdenVisualizacion { get; set; } = 99;
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AliasParametro> Aliases { get; set; } = new List<AliasParametro>();
        public ICollection<ResultadoLaboratorio> Resultados { get; set; } = new List<ResultadoLaboratorio>();
        public ICollection<RangoPrescriba> Rangos { get; set; } = new List<RangoPrescriba>();
    }
}
