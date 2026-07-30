namespace DataMedix.Application.Interfaces
{
    /// <summary>
    /// Registra eventos de uso para facturación por consumo.
    /// Desacoplado de la lógica clínica: la lógica de negocio solo llama a RecordAsync
    /// sin saber dónde ni cómo se persiste el evento.
    /// </summary>
    public interface IUsageMeter
    {
        /// <summary>
        /// Registra un evento de uso. Fire-and-forget seguro: no lanza excepciones
        /// hacia el caller aunque falle la escritura en BD de facturación.
        /// </summary>
        /// <param name="eventType">Identificador del evento (ej. "prescription_suggested").</param>
        /// <param name="metadata">Datos adicionales serializados como JSON (pacienteId, periodo, etc.).</param>
        Task RecordAsync(string eventType, object? metadata = null);

        /// <summary>
        /// Registra muchos eventos del mismo tipo en una sola conexión.
        /// Necesario para procesos por lote: un evento por paciente con
        /// RecordAsync abriría una conexión por paciente.
        /// </summary>
        Task RecordManyAsync(string eventType, IEnumerable<object> metadatas);
    }

    /// <summary>Constantes de tipos de eventos para evitar magic strings.</summary>
    public static class UsageEventTypes
    {
        public const string PrescripcionSugerida = "prescription_suggested";
        public const string ImportacionLote       = "batch_import";
        public const string EntradaManual         = "manual_entry";
    }
}
