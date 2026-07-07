using DataMedix.Application.Interfaces;
using DataMedix.Application.RuleEngine;
using DataMedix.Application.Services;
using DataMedix.Infrastructure.Excel;
using DataMedix.Infrastructure.Persistence;
using DataMedix.Infrastructure.Repositories;
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
            // ── TenantContext (scoped, mutable por middleware) ─────────────────
            // TenantContext (concrete) es inyectado en DataMedixDbContext para los
            // Global Query Filters. ITenantContext es la vista readonly para el resto.
            services.AddScoped<TenantContext>();
            services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

            // ── DbContext (depende de TenantContext para global filters) ───────
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
            services.AddScoped<ICronogramaRepository, CronogramaRepository>();
            services.AddScoped<IConfiguracionMedicamentoRepository, ConfiguracionMedicamentoRepository>();
            services.AddScoped<IAplicacionHierroRepository, AplicacionHierroRepository>();
            services.AddScoped<IHierroSchedulerService, HierroSchedulerService>();
            services.AddScoped<IPrecioEpoDosisRepository, PrecioEpoDosisRepository>();
            services.AddScoped<IReporteService, ReporteService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IExcelReader, ExcelReader>();
            services.AddScoped<IExcelExporter, ExcelExporter>();
            services.AddScoped<IDepuracionService, DepuracionService>();
            services.AddScoped<IEntradaManualService, EntradaManualService>();
            services.AddScoped<IUsageMeter, UsageMeter>();

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
        /// Ejecuta todas las operaciones de startup contra la BD con la conexión abierta
        /// explícitamente durante toda la secuencia.  Npgsql 10.x dispone el
        /// ManualResetEventSlim interno del conector al devolver la conexión al pool;
        /// al mantenerla abierta (OpenConnectionAsync) se evita ese reciclado.
        /// </summary>
        public static async Task EnsureStartupAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataMedixDbContext>();

            // Mantener la conexión abierta durante toda la secuencia de startup para que
            // Npgsql no la devuelva al pool (y no disponga el MRES) entre operaciones.
            await db.Database.OpenConnectionAsync();
            try
            {
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

                // 3. Seed reglas clínicas — idempotente por código.
                // Inserta solo las reglas que aún no existen (por codigo UNIQUE).
                // Las reglas ya existentes no se modifican, preservando ediciones manuales en producción.
                // Para MODIFICAR una regla existente, ejecutar 004_sync_reglas_clinicas.sql manualmente.
                var codigosExistentes = await db.ReglasClinicas
                    .Select(r => r.Codigo)
                    .ToHashSetAsync();

                var reglasNuevas = ReglasSeed.GetReglas()
                    .Where(r => !codigosExistentes.Contains(r.Codigo))
                    .ToList();

                if (reglasNuevas.Count > 0)
                {
                    await db.ReglasClinicas.AddRangeAsync(reglasNuevas);
                    await db.SaveChangesAsync();
                }

                // 4. Columnas ausente/motivo_ausencia en cronograma_medicamento (idempotente)
                await db.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE cronograma_medicamento
                        ADD COLUMN IF NOT EXISTS ausente          BOOLEAN  NOT NULL DEFAULT FALSE,
                        ADD COLUMN IF NOT EXISTS motivo_ausencia  TEXT,
                        ADD COLUMN IF NOT EXISTS sala             VARCHAR(20),
                        ADD COLUMN IF NOT EXISTS modo             SMALLINT NOT NULL DEFAULT 0,
                        ADD COLUMN IF NOT EXISTS fecha_inicio_flex DATE;
                ");

                // 5. Tabla aplicacion_hierro — registro individual por aplicación Fe IV (idempotente)
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS aplicacion_hierro (
                        id                UUID         NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                        tenant_id         UUID         NOT NULL,
                        paciente_id       UUID         NOT NULL,
                        cronograma_id     UUID         NOT NULL
                            REFERENCES cronograma_medicamento(id) ON DELETE CASCADE,
                        fecha_programada  DATE         NOT NULL,
                        dosis_mg          DECIMAL(10,2) NOT NULL DEFAULT 100,
                        estado            VARCHAR(20)  NOT NULL DEFAULT 'PENDIENTE',
                        observaciones     TEXT,
                        created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                        updated_at        TIMESTAMPTZ,
                        created_by        UUID,
                        updated_by        UUID
                    );
                    CREATE INDEX IF NOT EXISTS ix_aplicacion_hierro_cronograma
                        ON aplicacion_hierro (tenant_id, cronograma_id, fecha_programada);
                ");

                // 6. Tabla precio_epo_dosis — precio por dosis administrada de EPO (extensible sin código)
                await db.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS precio_epo_dosis (
                        id          UUID          NOT NULL DEFAULT uuid_generate_v4() PRIMARY KEY,
                        tenant_id   UUID          NOT NULL,
                        dosis_ui    DECIMAL(10,2) NOT NULL,
                        precio      DECIMAL(12,4) NOT NULL DEFAULT 0,
                        activo      BOOLEAN       NOT NULL DEFAULT TRUE,
                        created_at  TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                        updated_at  TIMESTAMPTZ
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ix_precio_epo_dosis_tenant_dosis
                        ON precio_epo_dosis (tenant_id, dosis_ui);
                ");
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
