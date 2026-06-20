using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;

namespace DataMedix.Application.UseCases.Auth
{
    public class LoginUseCase : IAuthService
    {
        private readonly IUsuarioRepository _usuarioRepo;

        public LoginUseCase(IUsuarioRepository usuarioRepo)
        {
            _usuarioRepo = usuarioRepo;
        }

        public async Task<Usuario?> ValidateUserAsync(LoginRequest request)
        {
            var user = await _usuarioRepo.GetByEmailAsync(request.Email);

            if (user is null || !user.Activo)
                return null;

            return BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash) ? user : null;
        }
    }
}
