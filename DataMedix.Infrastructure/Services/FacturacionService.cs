using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Calcula y congela la facturación mensual por consumo.
    ///
    /// Todas las consultas filtran por tenant explícitamente y además pasan por
    /// los global query filters del DbContext, de modo que ningún tenant puede
    /// ver ni cerrar el período de otro.
    /// </summary>
    public class FacturacionService : IFacturacionService
    {
        private readonly DataMedixDbContext _db;
        private readonly IAuditoriaRepository _auditoria;

        public FacturacionService(DataMedixDbContext db, IAuditoriaRepository auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        public async Task<FacturacionPeriodoDto> ObtenerPeriodoAsync(Guid tenantId, int anio, int mes)
        {
            var cerrado = await _db.FacturacionPeriodos
                .Include(f => f.Detalles)
                .FirstOrDefaultAsync(f => f.TenantId == tenantId &&
                                          f.PeriodoAnio == anio && f.PeriodoMes == mes);

            // Período ya cerrado: se sirve tal cual quedó registrado, sin recalcular
            if (cerrado is { Estado: EstadoFacturacion.Cerrado })
                return MapCerrado(cerrado);

            return await CalcularEnVivoAsync(tenantId, anio, mes, cerrado);
        }

        public async Task<FacturacionPeriodoDto> CerrarPeriodoAsync(
            Guid tenantId, int anio, int mes, Guid usuarioId)
        {
            var existente = await _db.FacturacionPeriodos
                .Include(f => f.Detalles)
                .FirstOrDefaultAsync(f => f.TenantId == tenantId &&
                                          f.PeriodoAnio == anio && f.PeriodoMes == mes);

            // Idempotente: cerrar dos veces no altera la factura ya emitida
            if (existente is { Estado: EstadoFacturacion.Cerrado })
                return MapCerrado(existente);

            var calculo = await CalcularEnVivoAsync(tenantId, anio, mes, existente);

            // Detalle anterior fuera primero y en su propia sentencia: borrar e
            // insertar la misma (periodo, paciente) en un solo SaveChanges puede
            // violar el índice único según el orden que elija EF.
            if (existente is not null)
            {
                await _db.FacturacionPeriodoDetalles
                    .Where(d => d.FacturacionPeriodoId == existente.Id)
                    .ExecuteDeleteAsync();
                existente.Detalles.Clear();
            }

            var periodo = existente ?? new FacturacionPeriodo
            {
                TenantId    = tenantId,
                PeriodoAnio = anio,
                PeriodoMes  = mes
            };

            periodo.PlanNombre          = calculo.PlanNombre;
            periodo.TarifaBase          = calculo.TarifaBase;
            periodo.TarifaPaciente      = calculo.TarifaPaciente;
            periodo.PacientesFacturados = calculo.PacientesFacturados;
            periodo.Total               = calculo.Total;
            periodo.Estado              = EstadoFacturacion.Cerrado;
            periodo.CerradoAt           = DateTime.UtcNow;
            periodo.CerradoPor          = usuarioId;
            periodo.UpdatedAt           = DateTime.UtcNow;

            if (existente is null)
                _db.FacturacionPeriodos.Add(periodo);

            foreach (var p in calculo.Pacientes)
            {
                _db.FacturacionPeriodoDetalles.Add(new FacturacionPeriodoDetalle
                {
                    FacturacionPeriodoId = periodo.Id,
                    TenantId             = tenantId,
                    PacienteId           = p.PacienteId,
                    Identificacion       = p.Identificacion,
                    NombreCompleto       = p.NombreCompleto,
                    TuvoLaboratorio      = p.TuvoLaboratorio,
                    TuvoSnapshot         = p.TuvoSnapshot,
                    TuvoPrescripcion     = p.TuvoPrescripcion,
                    TuvoCronograma       = p.TuvoCronograma,
                    EstadoPaciente       = p.EstadoPaciente,
                    TarifaAplicada       = calculo.TarifaPaciente
                });
            }

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "CLOSE",
                Entidad     = "FacturacionPeriodo",
                EntidadId   = periodo.Id,
                Descripcion = $"Cierre de facturación {anio}-{mes:D2}: " +
                              $"{calculo.PacientesFacturados} pacientes, total {calculo.Total:N2}",
                CreatedAt   = DateTime.UtcNow
            });

            calculo.Id        = periodo.Id;
            calculo.Estado    = EstadoFacturacion.Cerrado;
            calculo.CerradoAt = periodo.CerradoAt;
            return calculo;
        }

        public async Task ReabrirPeriodoAsync(Guid tenantId, int anio, int mes, Guid usuarioId)
        {
            var periodo = await _db.FacturacionPeriodos
                .FirstOrDefaultAsync(f => f.TenantId == tenantId &&
                                          f.PeriodoAnio == anio && f.PeriodoMes == mes);

            if (periodo is null || periodo.Estado != EstadoFacturacion.Cerrado) return;

            periodo.Estado    = EstadoFacturacion.Abierto;
            periodo.CerradoAt = null;
            periodo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "REOPEN",
                Entidad     = "FacturacionPeriodo",
                EntidadId   = periodo.Id,
                Descripcion = $"Período de facturación {anio}-{mes:D2} reabierto para recálculo",
                CreatedAt   = DateTime.UtcNow
            });
        }

        public async Task<List<FacturacionResumenDto>> ListarPeriodosAsync(Guid tenantId)
            => await _db.FacturacionPeriodos
                .Where(f => f.TenantId == tenantId)
                .OrderByDescending(f => f.PeriodoAnio).ThenByDescending(f => f.PeriodoMes)
                .Select(f => new FacturacionResumenDto
                {
                    Id                  = f.Id,
                    Anio                = f.PeriodoAnio,
                    Mes                 = f.PeriodoMes,
                    Estado              = f.Estado,
                    PacientesFacturados = f.PacientesFacturados,
                    Total               = f.Total,
                    CerradoAt           = f.CerradoAt
                })
                .ToListAsync();

        // ──────────────────────────────────────────────────────────────────────
        // Cálculo en vivo: qué pacientes tuvieron actividad en el período
        // ──────────────────────────────────────────────────────────────────────
        private async Task<FacturacionPeriodoDto> CalcularEnVivoAsync(
            Guid tenantId, int anio, int mes, FacturacionPeriodo? existente)
        {
            var period = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);

            var conLaboratorio = await _db.ResultadosLaboratorio
                .Where(r => r.TenantId == tenantId && r.PeriodoAnio == anio && r.PeriodoMes == mes)
                .Select(r => r.PacienteId).Distinct().ToListAsync();

            var conSnapshot = await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == anio && s.PeriodoMes == mes)
                .Select(s => s.PacienteId).Distinct().ToListAsync();

            var conPrescripcion = await _db.PrescripcionesSugeridas
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .Select(p => p.PacienteId).Distinct().ToListAsync();

            var conPrescripcionFinal = await _db.PrescripcionesFinales
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .Select(p => p.PacienteId).Distinct().ToListAsync();

            var conCronograma = await _db.CronogramasMedicamento
                .Where(c => c.TenantId == tenantId && c.PeriodoAnio == anio && c.PeriodoMes == mes)
                .Select(c => c.PacienteId).Distinct().ToListAsync();

            var lab   = conLaboratorio.ToHashSet();
            var snap  = conSnapshot.ToHashSet();
            var presc = conPrescripcion.Concat(conPrescripcionFinal).ToHashSet();
            var crono = conCronograma.ToHashSet();

            var todos = new HashSet<Guid>(lab);
            todos.UnionWith(snap);
            todos.UnionWith(presc);
            todos.UnionWith(crono);

            // Datos del padrón para los pacientes con actividad. Puede faltar
            // alguno si ya fue eliminado físicamente: se marca como ELIMINADO.
            var padron = await _db.Pacientes
                .Where(p => p.TenantId == tenantId && todos.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Identificacion,
                    p.PrimerNombre,
                    p.SegundoNombre,
                    p.PrimerApellido,
                    p.SegundoApellido,
                    p.Activo
                })
                .ToDictionaryAsync(p => p.Id);

            var tenant = await _db.Tenants
                .Where(t => t.Id == tenantId)
                .Select(t => new { t.PlanNombre, t.TarifaBase, t.TarifaPaciente })
                .FirstOrDefaultAsync();

            var dto = new FacturacionPeriodoDto
            {
                Id             = existente?.Id,
                Anio           = anio,
                Mes            = mes,
                PlanNombre     = tenant?.PlanNombre,
                TarifaBase     = tenant?.TarifaBase ?? 0,
                TarifaPaciente = tenant?.TarifaPaciente ?? 0,
                Estado         = EstadoFacturacion.Abierto
            };

            foreach (var pacienteId in todos)
            {
                padron.TryGetValue(pacienteId, out var p);

                var nombre = p is null
                    ? null
                    : $"{p.PrimerNombre} {p.SegundoNombre} {p.PrimerApellido} {p.SegundoApellido}"
                        .Trim().Replace("  ", " ");

                dto.Pacientes.Add(new FacturacionPacienteDto
                {
                    PacienteId       = pacienteId,
                    Identificacion   = p?.Identificacion,
                    NombreCompleto   = nombre,
                    TuvoLaboratorio  = lab.Contains(pacienteId),
                    TuvoSnapshot     = snap.Contains(pacienteId),
                    TuvoPrescripcion = presc.Contains(pacienteId),
                    TuvoCronograma   = crono.Contains(pacienteId),
                    EstadoPaciente   = p is null      ? EstadoPacienteFacturado.Eliminado
                                     : p.Activo      ? EstadoPacienteFacturado.Activo
                                                     : EstadoPacienteFacturado.Baja,
                    TarifaAplicada   = dto.TarifaPaciente
                });
            }

            dto.Pacientes = dto.Pacientes
                .OrderBy(p => p.NombreCompleto ?? "￿")
                .ToList();

            return dto;
        }

        private static FacturacionPeriodoDto MapCerrado(FacturacionPeriodo f) => new()
        {
            Id             = f.Id,
            Anio           = f.PeriodoAnio,
            Mes            = f.PeriodoMes,
            PlanNombre     = f.PlanNombre,
            TarifaBase     = f.TarifaBase,
            TarifaPaciente = f.TarifaPaciente,
            Estado         = f.Estado,
            CerradoAt      = f.CerradoAt,
            Pacientes      = f.Detalles
                .OrderBy(d => d.NombreCompleto ?? "￿")
                .Select(d => new FacturacionPacienteDto
                {
                    PacienteId       = d.PacienteId,
                    Identificacion   = d.Identificacion,
                    NombreCompleto   = d.NombreCompleto,
                    TuvoLaboratorio  = d.TuvoLaboratorio,
                    TuvoSnapshot     = d.TuvoSnapshot,
                    TuvoPrescripcion = d.TuvoPrescripcion,
                    TuvoCronograma   = d.TuvoCronograma,
                    EstadoPaciente   = d.EstadoPaciente,
                    TarifaAplicada   = d.TarifaAplicada
                })
                .ToList()
        };
    }
}
