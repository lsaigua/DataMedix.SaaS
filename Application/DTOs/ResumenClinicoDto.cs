namespace DataMedix.Application.DTOs
{
    public class ResumenClinicoDto
    {
        // ── Meta ────────────────────────────────────────────────────────────────
        public DateTime Periodo { get; set; }
        public int TotalPrescripciones { get; set; }

        // ── KPIs ────────────────────────────────────────────────────────────────
        public int TotalPacientesEvaluados { get; set; }
        public int PacientesConEpo { get; set; }
        public int PacientesConHierro { get; set; }
        public int AlertasCriticas { get; set; }
        public int AlertasAltas { get; set; }
        public int PrescripcionesPendientes { get; set; }
        public int PrescripcionesAprobadas { get; set; }
        public decimal PorcentajeAprobacion { get; set; }

        // ── Zonas Hb (panorama clínico) ─────────────────────────────────────────
        public int HbCritico { get; set; }      // Hb < 8  g/dL
        public int HbBajo { get; set; }          // 8 ≤ Hb < 10
        public int HbObjetivo { get; set; }      // 10 ≤ Hb ≤ 13
        public int HbAlto { get; set; }          // Hb > 13
        public int HbSinDato { get; set; }

        // ── Distribuciones ──────────────────────────────────────────────────────
        public List<ReglaDistribucionDto> DistribucionEpo { get; set; } = [];
        public List<ReglaDistribucionDto> DistribucionHierro { get; set; } = [];

        // ── Alertas ─────────────────────────────────────────────────────────────
        public List<AlertaTipoDto> ResumenAlertas { get; set; } = [];
        public List<PacienteAlertaDto> PacientesAlerta { get; set; } = [];

        // ── Tendencia Hb promedio últimos 6 meses ───────────────────────────────
        public List<TendenciaHbDto> TendenciaHb { get; set; } = [];
    }

    public class ReglaDistribucionDto
    {
        public string Codigo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public int Pacientes { get; set; }
        public decimal? DosisPromedio { get; set; }
        public decimal Porcentaje { get; set; }
        /// <summary>critica | baja | atencion | objetivo | alto</summary>
        public string Zona { get; set; } = "";
    }

    public class AlertaTipoDto
    {
        public string Codigo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Severidad { get; set; } = "";
        public int Total { get; set; }
    }

    public class PacienteAlertaDto
    {
        public Guid PacienteId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Identificacion { get; set; } = "";
        public decimal? Hb { get; set; }
        public List<AlertaItemDto> Alertas { get; set; } = [];
        public string SeveridadMaxima { get; set; } = "";
        public string EstadoPrescripcion { get; set; } = "";
    }

    public class AlertaItemDto
    {
        public string Codigo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Severidad { get; set; } = "";
        public string Mensaje { get; set; } = "";
    }

    public class TendenciaHbDto
    {
        public DateTime Periodo { get; set; }
        public string PeriodoLabel { get; set; } = "";
        public decimal HbPromedio { get; set; }
        public int Pacientes { get; set; }
    }
}
