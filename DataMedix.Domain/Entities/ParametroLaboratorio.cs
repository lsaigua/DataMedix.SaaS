// Entidad removida del modelo. Reemplazada por ParametroClinico + AliasParametro.
namespace DataMedix.Domain.Entities
{
    [Obsolete("Use ParametroClinico instead")]
    public class ParametroLaboratorio
    {
        public Guid IdParametroLaboratorio { get; set; }
        public Guid IdEmpresa { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
        public string TipoDato { get; set; } = string.Empty;
        public double? ValorMinimo { get; set; }
        public double? ValorMaximo { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; }
    }
}
