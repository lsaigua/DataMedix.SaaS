namespace DataMedix.Application.DTOs
{
    public class ImportacionResultadoDto
    {
        public Guid LoteId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int TotalFilas { get; set; }
        public int FilasValidas { get; set; }
        public int FilasError { get; set; }
        public int FilasDuplicadas { get; set; }
        public decimal PorcentajeExito => TotalFilas > 0
            ? Math.Round((decimal)FilasValidas / TotalFilas * 100, 1) : 0;
        public List<ErrorImportacionDto> Errores { get; set; } = new();
        public List<LabRowDto> FilasPrevisualizacion { get; set; } = new();
        public bool Exitoso => Estado == "COMPLETADO";
        public string? MensajeError { get; set; }
    }

    public class ErrorImportacionDto
    {
        public int NumeroFila { get; set; }
        public string? Campo { get; set; }
        public string TipoError { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string? ValorRecibido { get; set; }
    }

    public class PreVisualizacionDto
    {
        public List<LabRowDto> Filas { get; set; } = new();
        public List<ErrorImportacionDto> Errores { get; set; } = new();
        public int TotalFilas { get; set; }
        public int FilasValidas { get; set; }
        public int FilasConError { get; set; }
        public bool PuedeConfirmar => FilasValidas > 0;
    }
}
