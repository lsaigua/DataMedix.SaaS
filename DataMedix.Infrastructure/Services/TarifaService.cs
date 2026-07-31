using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Consola comercial del dueño del SaaS.
    ///
    /// A diferencia del resto de la aplicación, este servicio SÍ atraviesa
    /// tenants: usa IgnoreQueryFilters() de forma explícita y acotada. El acceso
    /// está protegido por el permiso de plataforma "tarifas.configurar", que la
    /// migración marca como solo_superadmin y por tanto ningún tenant puede
    /// concederse a sí mismo desde su matriz de permisos.
    /// </summary>
    public class TarifaService : ITarifaService
    {
        private readonly DataMedixDbContext _db;
        private readonly IAuditoriaRepository _auditoria;

        public TarifaService(DataMedixDbContext db, IAuditoriaRepository auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        public async Task<List<TarifaTenantDto>> ListarTenantsAsync()
        {
            var tenants = await _db.Tenants
                .Where(t => t.DeletedAt == null)
                .OrderBy(t => t.Nombre)
                .AsNoTracking()
                .ToListAsync();

            var tramos = await _db.TenantTarifaTramos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync();

            var porTenant = tramos.GroupBy(x => x.TenantId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DesdePacientes).ToList());

            return tenants.Select(t => Map(t, porTenant.GetValueOrDefault(t.Id))).ToList();
        }

        public async Task<TarifaTenantDto?> ObtenerAsync(Guid tenantId)
        {
            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant is null) return null;

            var tramos = await _db.TenantTarifaTramos
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId)
                .OrderBy(x => x.DesdePacientes)
                .AsNoTracking()
                .ToListAsync();

            return Map(tenant, tramos);
        }

        public async Task GuardarAsync(TarifaTenantDto dto, Guid usuarioId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == dto.TenantId)
                ?? throw new InvalidOperationException("El cliente indicado no existe.");

            var modelo = ModeloCobro.Todos.Contains(dto.ModeloCobro)
                ? dto.ModeloCobro
                : ModeloCobro.Mixto;

            tenant.PlanNombre           = dto.PlanNombre;
            tenant.ModeloCobro          = modelo;
            tenant.TarifaBase           = dto.TarifaBase;
            tenant.TarifaPaciente       = dto.TarifaPaciente;
            tenant.TarifaSoporteMensual = dto.TarifaSoporteMensual;
            tenant.Moneda               = string.IsNullOrWhiteSpace(dto.Moneda) ? "USD" : dto.Moneda.Trim();
            tenant.FacturacionNotas     = dto.Notas;
            tenant.UpdatedAt            = DateTime.UtcNow;

            // Los tramos se reemplazan completos: es una lista corta y así no
            // quedan huecos ni solapes de una edición anterior.
            await _db.TenantTarifaTramos
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == dto.TenantId)
                .ExecuteDeleteAsync();

            foreach (var tramo in dto.Tramos.OrderBy(t => t.DesdePacientes))
            {
                _db.TenantTarifaTramos.Add(new TenantTarifaTramo
                {
                    TenantId       = dto.TenantId,
                    DesdePacientes = tramo.DesdePacientes,
                    HastaPacientes = tramo.HastaPacientes,
                    PrecioPaciente = tramo.PrecioPaciente
                });
            }

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = dto.TenantId,
                UsuarioId   = usuarioId,
                Accion      = "UPDATE",
                Entidad     = "TenantTarifa",
                EntidadId   = dto.TenantId,
                Descripcion = $"Tarifas actualizadas: modelo {modelo}, base {dto.TarifaBase:N2}, " +
                              $"{dto.Tramos.Count} tramo(s), soporte {dto.TarifaSoporteMensual:N2}",
                CreatedAt   = DateTime.UtcNow
            });
        }

        public async Task<List<CargoUnicoDto>> ListarCargosAsync(Guid tenantId)
            => await _db.TenantCargosUnicos
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId)
                .OrderByDescending(c => c.PeriodoAnio).ThenByDescending(c => c.PeriodoMes)
                .Select(c => new CargoUnicoDto
                {
                    Id            = c.Id,
                    Concepto      = c.Concepto,
                    Monto         = c.Monto,
                    PeriodoAnio   = c.PeriodoAnio,
                    PeriodoMes    = c.PeriodoMes,
                    Aplicado      = c.Aplicado,
                    Observaciones = c.Observaciones
                })
                .AsNoTracking()
                .ToListAsync();

        public async Task AgregarCargoAsync(Guid tenantId, CargoUnicoDto cargo, Guid usuarioId)
        {
            _db.TenantCargosUnicos.Add(new TenantCargoUnico
            {
                TenantId      = tenantId,
                Concepto      = cargo.Concepto.Trim(),
                Monto         = cargo.Monto,
                PeriodoAnio   = cargo.PeriodoAnio,
                PeriodoMes    = cargo.PeriodoMes,
                Aplicado      = cargo.Aplicado,
                Observaciones = cargo.Observaciones,
                CreatedBy     = usuarioId
            });

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "CREATE",
                Entidad     = "TenantCargoUnico",
                Descripcion = $"Cargo «{cargo.Concepto}» de {cargo.Monto:N2} " +
                              $"para {cargo.PeriodoAnio}-{cargo.PeriodoMes:D2}",
                CreatedAt   = DateTime.UtcNow
            });
        }

        public async Task EliminarCargoAsync(Guid tenantId, Guid cargoId, Guid usuarioId)
        {
            var borrados = await _db.TenantCargosUnicos
                .IgnoreQueryFilters()
                .Where(c => c.Id == cargoId && c.TenantId == tenantId)
                .ExecuteDeleteAsync();

            if (borrados == 0) return;

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "DELETE",
                Entidad     = "TenantCargoUnico",
                EntidadId   = cargoId,
                Descripcion = "Cargo puntual eliminado",
                CreatedAt   = DateTime.UtcNow
            });
        }

        public async Task<List<FacturacionGlobalDto>> ConsolidadoAsync(int anio, int mes)
        {
            var period = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);

            var tenants = await _db.Tenants
                .Where(t => t.DeletedAt == null && t.Activo)
                .OrderBy(t => t.Nombre)
                .AsNoTracking()
                .ToListAsync();

            var tenantIds = tenants.Select(t => t.Id).ToList();

            // Períodos ya cerrados: se leen del libro, nunca se recalculan
            var cerrados = await _db.FacturacionPeriodos
                .IgnoreQueryFilters()
                .Where(f => tenantIds.Contains(f.TenantId) &&
                            f.PeriodoAnio == anio && f.PeriodoMes == mes &&
                            f.Estado == EstadoFacturacion.Cerrado)
                .AsNoTracking()
                .ToDictionaryAsync(f => f.TenantId);

            var tramos = (await _db.TenantTarifaTramos
                    .IgnoreQueryFilters()
                    .Where(x => tenantIds.Contains(x.TenantId))
                    .AsNoTracking()
                    .ToListAsync())
                .GroupBy(x => x.TenantId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DesdePacientes).ToList());

            var cargos = (await _db.TenantCargosUnicos
                    .IgnoreQueryFilters()
                    .Where(c => tenantIds.Contains(c.TenantId) && c.Aplicado &&
                                c.PeriodoAnio == anio && c.PeriodoMes == mes)
                    .AsNoTracking()
                    .ToListAsync())
                .GroupBy(c => c.TenantId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Monto));

            // Pacientes con actividad en el período, por tenant y en una sola
            // pasada por tabla: recorrer tenant por tenant sería N×5 consultas.
            var actividad = await PacientesConActividadPorTenantAsync(tenantIds, anio, mes, period);

            var resultado = new List<FacturacionGlobalDto>(tenants.Count);

            foreach (var t in tenants)
            {
                if (cerrados.TryGetValue(t.Id, out var f))
                {
                    resultado.Add(new FacturacionGlobalDto
                    {
                        TenantId            = t.Id,
                        TenantNombre        = t.Nombre,
                        Subdomain           = t.Subdomain,
                        PlanNombre          = f.PlanNombre,
                        ModeloCobro         = f.ModeloCobro,
                        Estado              = f.Estado,
                        PacientesFacturados = f.PacientesFacturados,
                        TarifaBase          = f.TarifaBase,
                        CostoPacientes      = f.PacientesFacturados * f.TarifaPaciente,
                        CostoSoporte        = f.CostoSoporte,
                        CostoCargos         = f.CostoCargos,
                        Total               = f.Total,
                        Moneda              = f.Moneda,
                        CerradoAt           = f.CerradoAt
                    });
                    continue;
                }

                var pacientes = actividad.GetValueOrDefault(t.Id)?.Count ?? 0;

                var cobro = CalculadoraCobro.Calcular(
                    t.ModeloCobro, t.TarifaBase, t.TarifaPaciente, t.TarifaSoporteMensual,
                    tramos.GetValueOrDefault(t.Id), pacientes, cargos.GetValueOrDefault(t.Id));

                resultado.Add(new FacturacionGlobalDto
                {
                    TenantId            = t.Id,
                    TenantNombre        = t.Nombre,
                    Subdomain           = t.Subdomain,
                    PlanNombre          = t.PlanNombre,
                    ModeloCobro         = t.ModeloCobro,
                    Estado              = EstadoFacturacion.Abierto,
                    PacientesFacturados = pacientes,
                    TarifaBase          = cobro.TarifaBase,
                    CostoPacientes      = cobro.CostoPacientes,
                    CostoSoporte        = cobro.CostoSoporte,
                    CostoCargos         = cobro.CostoCargos,
                    Total               = cobro.Total,
                    Moneda              = t.Moneda
                });
            }

            return resultado;
        }

        // ──────────────────────────────────────────────────────────────────────

        private async Task<Dictionary<Guid, HashSet<Guid>>> PacientesConActividadPorTenantAsync(
            List<Guid> tenantIds, int anio, int mes, DateTime period)
        {
            var mapa = new Dictionary<Guid, HashSet<Guid>>();

            async Task AcumularAsync(IQueryable<ParTenantPaciente> consulta)
            {
                foreach (var fila in await consulta.Distinct().AsNoTracking().ToListAsync())
                {
                    if (!mapa.TryGetValue(fila.TenantId, out var set))
                        mapa[fila.TenantId] = set = new HashSet<Guid>();
                    set.Add(fila.PacienteId);
                }
            }

            await AcumularAsync(_db.ResultadosLaboratorio.IgnoreQueryFilters()
                .Where(r => tenantIds.Contains(r.TenantId) && r.PeriodoAnio == anio && r.PeriodoMes == mes)
                .Select(r => new ParTenantPaciente(r.TenantId, r.PacienteId)));

            await AcumularAsync(_db.SnapshotsMensuales.IgnoreQueryFilters()
                .Where(s => tenantIds.Contains(s.TenantId) && s.PeriodoAnio == anio && s.PeriodoMes == mes)
                .Select(s => new ParTenantPaciente(s.TenantId, s.PacienteId)));

            await AcumularAsync(_db.PrescripcionesSugeridas.IgnoreQueryFilters()
                .Where(p => tenantIds.Contains(p.TenantId) && p.PeriodDate == period)
                .Select(p => new ParTenantPaciente(p.TenantId, p.PacienteId)));

            await AcumularAsync(_db.PrescripcionesFinales.IgnoreQueryFilters()
                .Where(p => tenantIds.Contains(p.TenantId) && p.PeriodDate == period)
                .Select(p => new ParTenantPaciente(p.TenantId, p.PacienteId)));

            await AcumularAsync(_db.CronogramasMedicamento.IgnoreQueryFilters()
                .Where(c => tenantIds.Contains(c.TenantId) && c.PeriodoAnio == anio && c.PeriodoMes == mes)
                .Select(c => new ParTenantPaciente(c.TenantId, c.PacienteId)));

            return mapa;
        }

        /// <summary>Proyección mínima para agrupar actividad por tenant y paciente.</summary>
        private sealed record ParTenantPaciente(Guid TenantId, Guid PacienteId);

        private static TarifaTenantDto Map(Tenant t, List<TenantTarifaTramo>? tramos) => new()
        {
            TenantId             = t.Id,
            TenantNombre         = t.Nombre,
            Subdomain            = t.Subdomain,
            Activo               = t.Activo,
            PlanNombre           = t.PlanNombre,
            ModeloCobro          = t.ModeloCobro,
            TarifaBase           = t.TarifaBase,
            TarifaPaciente       = t.TarifaPaciente,
            TarifaSoporteMensual = t.TarifaSoporteMensual,
            Moneda               = t.Moneda,
            Notas                = t.FacturacionNotas,
            Tramos               = (tramos ?? new List<TenantTarifaTramo>())
                .Select(x => new TramoTarifaDto
                {
                    Id             = x.Id,
                    DesdePacientes = x.DesdePacientes,
                    HastaPacientes = x.HastaPacientes,
                    PrecioPaciente = x.PrecioPaciente
                })
                .ToList()
        };
    }
}
