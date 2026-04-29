// Entidad removida del modelo. Reemplazada por LoteImportacion + ResultadoLaboratorio.
// Mantenida como stub para evitar errores de compilación en código legacy.
namespace DataMedix.Domain.Entities
{
    [Obsolete("Use LoteImportacion instead")]
    public class OrdenClinica
    {
        public Guid IdOrdenClinica { get; set; }
        public Guid IdEmpresa { get; set; }
        public Guid IdPaciente { get; set; }
        public DateTime FechaOrden { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
