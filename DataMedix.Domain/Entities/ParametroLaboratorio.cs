using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataMedix.Domain.Entities
{
    [Table("parametrolaboratorio")]
    public class ParametroLaboratorio
    {
        public Guid IdParametroLaboratorio { get; set; }

        public Guid IdEmpresa { get; private set; }

        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string UnidadMedida { get; set; } = null!;
        public string TipoDato { get; set; } = null!;
        public double ValorMinimo { get; set; }
        public double ValorMaximo { get; set; } 
        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; }
    }
}
