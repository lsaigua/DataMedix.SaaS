using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataMedix.Domain.Entities
{
    [Table("usuario")]
    public class Usuario
    {
        public Guid IdUsuario { get; set; }

        public string CodigoUsuario { get; set; } = null!;
        public string Identificacion { get; set; } = null!;

        public string PrimerNombre { get; set; } = null!;
        public string? SegundoNombre { get; set; }   // NULLABLE

        public string PrimerApellido { get; set; } = null!;
        public string SegundoApellido { get; set; } = null!;

        public string? Email { get; set; }            //NULLABLE
        public string? Telefono { get; set; }         // NULLABLE

        public string TipoUsuario { get; set; } = null!;

        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; }

        public string Password { get; set; } = null!;
        public string PasswordHash { get; set; }= null!;


    }
}
