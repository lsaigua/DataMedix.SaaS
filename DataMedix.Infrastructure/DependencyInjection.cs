using DataMedix.Application.Interfaces;
using DataMedix.Infrastructure.Excel;
using DataMedix.Infrastructure.Persistence;
using DataMedix.Infrastructure.Repositories;
using DataMedix.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
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

            // Persistir claves de DataProtection en PostgreSQL
            // Sin esto, cada redeploy en Railway genera claves nuevas → falla antiforgery
            services.AddDataProtection()
                .PersistKeysToDbContext<DataMedixDbContext>()
                .SetApplicationName("DataMedix");

            return services;
        }

        public static async Task EnsureDataProtectionTableAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataMedixDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS data_protection_keys " +
                "(id SERIAL PRIMARY KEY, friendly_name TEXT, xml TEXT);");
        }
    }
}
