namespace DataMedix.Application.DTOs.HojaEpo
{
    public class HojaEpoFiltroDto
    {
        public string? BusquedaPaciente { get; set; }
        public Guid? ParametroClinicoId { get; set; }
        public int AnioDe { get; set; } = DateTime.UtcNow.Year;
        public int MesDe { get; set; } = DateTime.UtcNow.Month;
        public int AnioHasta { get; set; } = DateTime.UtcNow.Year;
        public int MesHasta { get; set; } = DateTime.UtcNow.Month;

        public DateTime FechaDesde => new DateTime(AnioDe, MesDe, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime FechaHasta => new DateTime(AnioHasta, MesHasta, 1, 0, 0, 0, DateTimeKind.Utc);

        public List<DateTime> Meses
        {
            get
            {
                var result = new List<DateTime>();
                var d = FechaDesde;
                var hasta = FechaHasta;
                while (d <= hasta && result.Count < 24)
                {
                    result.Add(d);
                    d = d.AddMonths(1);
                }
                return result;
            }
        }
    }
}
