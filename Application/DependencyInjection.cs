using DataMedix.Application.Interfaces;
using DataMedix.Application.UseCases.Auth;
using DataMedix.Application.UseCases.Laboratorio;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMedix.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<ProcesarArchivoLaboratorioUseCase>();

            return services;
        }
    }
}
