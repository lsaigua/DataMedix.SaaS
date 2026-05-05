using DataMedix.Application.Interfaces;
using DataMedix.Application.RuleEngine;
using DataMedix.Application.Services;
using DataMedix.Infrastructure.Excel;
using DataMedix.Infrastructure.Persistence;
using DataMedix.Infrastructure.Repositories;
using DataMedix.Infrastructure.Security;
using DataMedix.Infrastructure.Seed;
using DataMedix.Infrastructure.Services;
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
                    npgsql =>
                    {
                        npgsql.CommandTimeout(120);
                        npgsql.MaxBatchSize(50);   // Evita lotes masivos que detonan el bug MRES de Npgsql
                    }));

            // ── Repositorios (Scoped) ──────────────────────────────────────────
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
            services.AddScoped<IReglaClinicaRepository, ReglaClinicaRepository>();
            services.AddScoped<IReporteService, ReporteService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IExcelReader, ExcelReader>();
            services.AddScoped<IExcelExporter, ExcelExporter>();

            // ── Motor de reglas (Singleton — stateless, thread-safe) ───────────
            // IMemoryCache está registrado desde AddMemoryCache() en Program.cs
            services.AddSingleton<RuleConditionEvaluator>();
            services.AddSingleton<IRuleCache, RuleCache>();
            services.AddSingleton<IRuleEngine, RuleEngine>();

            // Persistir claves de DataProtection en PostgreSQL
            services.AddDataProtection()
                .PersistKeysToDbContext<DataMedixDbContext>()
                .SetApplicationName("DataMedix");

            return services;
        }

        /// <summary>
        /// Ejecuta todas las operaciones de startup contra la BD usando un único scope/conexión.
        /// Npgsql 10.x tiene un bug donde devolver la conexión al pool entre llamadas deja el
        /// ManualResetEventSlim interno en estado disposed. Un solo scope evita el reciclado.
        /// </summary>
        public static async Task EnsureStartupAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataMedixDbContext>();

            // 1. Tabla DataProtection
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS data_protection_keys " +
                "(id SERIAL PRIMARY KEY, friendly_name TEXT, xml TEXT);");

            // 2. Tabla reglas_clinicas
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS reglas_clinicas (
                    id               UUID         NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                    codigo           VARCHAR(50)  NOT NULL UNIQUE,
                    nombre           VARCHAR(300) NOT NULL,
                    tipo             VARCHAR(20)  NOT NULL,
                    prioridad        INT          NOT NULL,
                    severidad        VARCHAR(20),
                    condiciones_json TEXT         NOT NULL,
                    accion_json      TEXT         NOT NULL,
                    version          INT          NOT NULL DEFAULT 1,
                    activo           BOOLEAN      NOT NULL DEFAULT TRUE,
                    tenant_id        UUID,
                    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                    updated_at       TIMESTAMPTZ,
                    created_by       UUID
                );");

            // 3. Seed reglas clínicas (idempotente)
            var hayReglas = await db.ReglasClinicas.AnyAsync();
            if (!hayReglas)
            {
                var reglas = ReglasSeed.GetReglas();
                await db.ReglasClinicas.AddRangeAsync(reglas);
                await db.SaveChangesAsync();
            }
        }
    }
}
