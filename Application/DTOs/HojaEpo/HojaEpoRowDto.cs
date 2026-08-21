namespace DataMedix.Application.DTOs.HojaEpo
{
    public class HojaEpoRowDto
    {
        public Guid PacienteId { get; set; }
        public string Identificacion { get; set; } = "";
        public string NombrePaciente { get; set; } = "";
        public int TiempoDialisisMeses { get; set; }
        /// <summary>Modalidad del paciente: alimenta el filtro de tipo de atención.</summary>
        public string? TipoAtencion { get; set; }
        /// <summary>Turno de sesión del paciente, en código canónico.</summary>
        public string? Turno { get; set; }
        public Dictionary<DateTime, HojaEpoCeldaDto> Meses { get; set; } = new();

        // Resumen calculado
        public decimal? PromedioHb { get; set; }
        public decimal? PromedioEpo { get; set; }
        public decimal? PromedioHierro { get; set; }
        public decimal? SumaHierro { get; set; }
    }
}
