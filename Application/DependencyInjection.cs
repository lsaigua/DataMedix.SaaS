using DataMedix.Application.Interfaces;
using DataMedix.Application.Services;
using DataMedix.Application.UseCases.Auth;
using DataMedix.Application.UseCases.Laboratorio;
using Microsoft.Extensions.DependencyInjection;

namespace DataMedix.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, LoginUseCase>();
            services.AddScoped<ImportacionService>();
            services.AddScoped<PrescripcionService>();
            services.AddScoped<ProcesarArchivoLaboratorioUseCase>();

            return services;
        }
    }
}
