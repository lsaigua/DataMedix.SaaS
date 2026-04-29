namespace DataMedix.Application.DTOs
{
    public class ResultadoFiltro
    {
        public string? BusquedaPaciente { get; set; }
        public Guid? ParametroClinicoId { get; set; }
        public int? AnioDe { get; set; }
        public int? MesDe { get; set; }
        public int? AnioHasta { get; set; }
        public int? MesHasta { get; set; }
        public Guid? LoteId { get; set; }
        public int Pagina { get; set; } = 1;
        public int Tamano { get; set; } = 50;

        public DateTime? FechaDesde => (AnioDe.HasValue && MesDe.HasValue)
            ? new DateTime(AnioDe.Value, MesDe.Value, 1, 0, 0, 0, DateTimeKind.Utc)
            : null;

        public DateTime? FechaHasta => (AnioHasta.HasValue && MesHasta.HasValue)
            ? new DateTime(AnioHasta.Value, MesHasta.Value, 1, 0, 0, 0, DateTimeKind.Utc)
            : null;

        public bool HayFiltrosActivos =>
            !string.IsNullOrWhiteSpace(BusquedaPaciente) ||
            ParametroClinicoId.HasValue ||
            AnioDe.HasValue || MesDe.HasValue ||
            AnioHasta.HasValue || MesHasta.HasValue ||
            LoteId.HasValue;
    }
}
