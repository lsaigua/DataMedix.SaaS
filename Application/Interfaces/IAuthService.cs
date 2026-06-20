using DataMedix.Application.DTOs;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.Interfaces
{
    public interface IAuthService
    {
        Task<Usuario?> ValidateUserAsync(LoginRequest request);
    }
}
        