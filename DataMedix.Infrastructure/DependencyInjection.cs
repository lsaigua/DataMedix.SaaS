using DataMedix.Application.Interfaces;
using DataMedix.Infrastructure.Excel;
using DataMedix.Infrastructure.Persistence;
using DataMedix.Infrastructure.Repositories;
using DataMedix.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataMedix.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<DataMedixDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DatabasePostgres"),
                    npgsql => npgsql.CommandTimeout(60)));

            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IPacienteRepository, PacienteRepository>();
            services.AddScoped<ILoteImportacionRepository, LoteImportacionRepository>();
            services.AddScoped<IParametroClinicoRepository, ParametroClinicoRepository>();
            services.AddScoped<IResultadoLaboratorioRepository, ResultadoLaboratorioRepository>();
            services.AddScoped<ISnapshotMensualRepository, SnapshotMensualRepository>();
            services.AddScoped<IRangoPreescribaRepository, RangoPreescribaRepository>();
            services.AddScoped<IPrescripcionRepository, PrescripcionRepository>();
            services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IExcelReader, ExcelReader>();

            return services;
        }
    }
}
