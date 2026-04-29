namespace DataMedix.Domain.Entities
{
    public class LoteImportacion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string NombreArchivo { get; set; } = null!;
        public string NombreArchivoOriginal { get; set; } = null!;
        public string? StoragePath { get; set; }
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        public DateTime PeriodDate { get; set; }        // Siempre primer día del mes
        public int TotalFilas { get; set; } = 0;
        public int FilasValidas { get; set; } = 0;
        public int FilasError { get; set; } = 0;
        public int FilasDuplicadas { get; set; } = 0;
        public string Estado { get; set; } = EstadoLote.Pendiente;
        public string? MensajeError { get; set; }
        public DateTime? FechaInicioProceso { get; set; }
        public DateTime? FechaFinProceso { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }

        public Tenant Tenant { get; set; } = null!;
        public ICollection<ImportacionDetalle> Detalles { get; set; } = new List<ImportacionDetalle>();
        public ICollection<ImportacionError> Errores { get; set; } = new List<ImportacionError>();
        public ICollection<ResultadoLaboratorio> Resultados { get; set; } = new List<ResultadoLaboratorio>();

        public void IniciarProcesamiento()
        {
            Estado = EstadoLote.Procesando;
            FechaInicioProceso = DateTime.UtcNow;
        }

        public void CompletarProcesamiento()
        {
            Estado = EstadoLote.Completado;
            FechaFinProceso = DateTime.UtcNow;
        }

        public void MarcarError(string mensaje)
        {
            Estado = EstadoLote.Error;
            MensajeError = mensaje;
            FechaFinProceso = DateTime.UtcNow;
        }

        public void Cancelar()
        {
            Estado = EstadoLote.Cancelado;
            Activo = false;
            FechaFinProceso ??= DateTime.UtcNow;
        }
    }

    public static class EstadoLote
    {
        public const string Pendiente = "PENDIENTE";
        public const string Procesando = "PROCESANDO";
        public const string Completado = "COMPLETADO";
        public const string Error = "ERROR";
        public const string Cancelado = "CANCELADO";
    }
}
