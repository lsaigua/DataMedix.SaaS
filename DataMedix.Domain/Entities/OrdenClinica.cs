using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataMedix.Domain.Entities
{
    [Table("ordenclinica")]
    public class OrdenClinica
    {
        public Guid IdOrdenClinica { get; set; }

        public Guid IdEmpresa { get; private set; }
        public Guid IdPaciente { get; private set; }
        public string NumeroOrden { get; set; } = null!;
        public DateTime FechaOrden { get; set; } 

        public string Estado { get; set; } = null!;
        public string? Prioridad { get; set; }   // NULLABLE

         public DateTime FechaCreacion { get; set; }
        private OrdenClinica() { }

        public OrdenClinica(Guid empresaId, Guid pacienteId, DateTime fecha)
        {
            IdOrdenClinica = Guid.NewGuid();
            IdEmpresa = empresaId;
            IdPaciente = pacienteId;
            FechaOrden = fecha;
        }
    }
}
