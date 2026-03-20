using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.DTOs.Auth
{
    public class UsuarioLoginResult
    {
        public Guid UsuarioId { get; set; }
        public Guid EmpresaId { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
    }
}
