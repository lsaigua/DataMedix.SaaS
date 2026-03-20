using DataMedix.Application.DTOs.Auth;
using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IUsuarioRepository
    {
        //Task<UsuarioLoginResult> ExecuteAsync(string email, string password);
        Task<Usuario?> GetByEmailAsync(string email);
    }

}
