using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.DTOs
{
    public class LabRowDto
    {
        public int LineNumber { get; set; }
        public DateTime FechaOrden { get; set; }
        public string? PlanSalud { get; set; }
        public string? TipoAtencion { get; set; }
        public string? Identificacion { get; set; }
        public string? PrimerNombre { get; set; }
        public string? PrimerApellido { get; set; }
        public string? Examen { get; set; }
        public string? Parametro { get; set; }
        public string? ResultadoTexto { get; set; }
        public string? UnidadMedidad { get; set; }

        public DateTime FechaExamen { get; set; }
      
    }
}
