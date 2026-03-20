using DataMedix.Application.DTOs;
using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.Interfaces
{
    public interface IAuthService
    {
        Task<string?> LoginAsync(LoginRequest request);
        Task<Usuario?> ValidateUserAsync(LoginRequest request);
    }
}
        