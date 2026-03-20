using DataMedix.Application.DTOs;
using DataMedix.Application.DTOs.Auth;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application.UseCases.Auth
{
    public class LoginUseCase : IAuthService
    {
        private readonly IUsuarioRepository _usuarioRepo;
        private readonly IJwtTokenGenerator _jwt;

        public LoginUseCase(
            IUsuarioRepository usuarioRepo,
            IJwtTokenGenerator jwt)
        {
            _usuarioRepo = usuarioRepo;
            _jwt = jwt;
        }

        public async Task<string?> LoginAsync(LoginRequest request)
        {
            var user = await _usuarioRepo.GetByEmailAsync(request.Email);

            if (user is null || !user.Activo)
                return null;
            
            var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!valid)
                return null;

            return _jwt.Generate(user);
        }
        public async Task<Usuario?> ValidateUserAsync(LoginRequest request)
        {
            var user = await _usuarioRepo.GetByEmailAsync(request.Email);

            if (user is null || !user.Activo)
                return null;

            var valid = BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash);

            if (!valid)
                return null;

            return valid ? user : null;
        }
    
    }
}
