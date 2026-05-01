namespace DataMedix.Application.RuleEngine
{
    public class AlertaClinica
    {
        public string Codigo { get; set; } = "";             // ALERT-HB-CRIT
        public string Flag { get; set; } = "";               // HB_CRITICA
        public string Severidad { get; set; } = "MEDIA";     // CRITICA | ALTA | MEDIA
        public string Mensaje { get; set; } = "";
        public bool RequiereRevisionMedica { get; set; }
        public decimal? SugerenciaDosisMax { get; set; }
    }
}
