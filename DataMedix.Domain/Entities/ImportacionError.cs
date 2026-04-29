namespace DataMedix.Domain.Entities
{
    public class ImportacionError
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid LoteId { get; set; }
        public Guid? ImportacionDetalleId { get; set; }
        public int? NumeroFila { get; set; }
        public string? Campo { get; set; }
        public string TipoError { get; set; } = null!;
        // REQUERIDO | FORMATO | DUPLICADO | PARAMETRO_DESCONOCIDO | PACIENTE_INVALIDO
        public string Mensaje { get; set; } = null!;
        public string? ValorRecibido { get; set; }
        public bool EsIgnorado { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public LoteImportacion Lote { get; set; } = null!;
        public ImportacionDetalle? Detalle { get; set; }
    }

    public static class TipoErrorImportacion
    {
        public const string Requerido = "REQUERIDO";
        public const string Formato = "FORMATO";
        public const string Duplicado = "DUPLICADO";
        public const string ParametroDesconocido = "PARAMETRO_DESCONOCIDO";
        public const string PacienteInvalido = "PACIENTE_INVALIDO";
    }
}
