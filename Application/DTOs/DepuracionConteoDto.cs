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

        public int TotalRegistros =>
            ResultadosLaboratorio + Snapshots + SnapshotDetalles +
            PrescripcionesSugeridas + PrescripcionesFinales +
            LotesImportacion + DetallesImportacion + ErroresImportacion;

        public bool TieneDatos => TotalRegistros > 0;
    }
}
