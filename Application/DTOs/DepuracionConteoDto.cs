using System.Globalization;

namespace DataMedix.Application.DTOs
{
    public class DepuracionConteoDto
    {
        public int Año { get; set; }
        public int Mes { get; set; }
        public string NombrePeriodo =>
            new DateTime(Año, Mes, 1).ToString("MMMM yyyy", new CultureInfo("es-EC"));

        public int ResultadosLaboratorio { get; set; }
        public int Snapshots { get; set; }
        public int SnapshotDetalles { get; set; }
        public int PrescripcionesSugeridas { get; set; }
        public int PrescripcionesFinales { get; set; }
        public int LotesImportacion { get; set; }
        public int DetallesImportacion { get; set; }
        public int ErroresImportacion { get; set; }

        // Cronograma de medicación del período
        public int Cronogramas { get; set; }
        public int CronogramaDias { get; set; }
        public int AplicacionesHierro { get; set; }
        public int EventosDosisPendiente { get; set; }
        public int CronogramaAuditorias { get; set; }

        public int TotalRegistros =>
            ResultadosLaboratorio + Snapshots + SnapshotDetalles +
            PrescripcionesSugeridas + PrescripcionesFinales +
            LotesImportacion + DetallesImportacion + ErroresImportacion +
            Cronogramas + CronogramaDias + AplicacionesHierro +
            EventosDosisPendiente + CronogramaAuditorias;

        public bool TieneDatos => TotalRegistros > 0;

        // ── Impacto sobre el padrón de pacientes ──────────────────────────────
        /// <summary>
        /// Pacientes que quedarían sin ningún dato clínico en ningún período y
        /// por tanto se darán de baja (Activo = false). La fila se conserva.
        /// </summary>
        public int PacientesADarDeBaja { get; set; }

        // ── Estado de facturación del período ─────────────────────────────────
        /// <summary>Si el período ya tiene cierre de facturación registrado.</summary>
        public bool FacturacionCerrada { get; set; }
        /// <summary>Pacientes que se facturarán / ya se facturaron por este período.</summary>
        public int PacientesFacturables { get; set; }
    }
}
