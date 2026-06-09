using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Domain.Entities;
using DataMedix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataMedix.Infrastructure.Services
{
    public class DepuracionService : IDepuracionService
    {
        private readonly DataMedixDbContext _db;
        private readonly IAuditoriaRepository _auditoria;

        public DepuracionService(DataMedixDbContext db, IAuditoriaRepository auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        public async Task<DepuracionConteoDto> ContarDatosPeriodoAsync(Guid tenantId, int año, int mes)
        {
            var period = new DateTime(año, mes, 1, 0, 0, 0, DateTimeKind.Utc);

            // Ids de lotes del período (para filtrar FK)
            var loteIds = await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.PeriodoAnio == año && l.PeriodoMes == mes)
                .Select(l => l.Id)
                .ToListAsync();

            // Ids de snapshots del período (para filtrar SnapshotDetalle)
            var snapIds = await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .Select(s => s.Id)
                .ToListAsync();

            return new DepuracionConteoDto
            {
                Año = año,
                Mes = mes,
                ResultadosLaboratorio = await _db.ResultadosLaboratorio
                    .CountAsync(r => r.TenantId == tenantId && r.PeriodoAnio == año && r.PeriodoMes == mes),
                Snapshots = snapIds.Count,
                SnapshotDetalles = snapIds.Count == 0 ? 0 :
                    await _db.SnapshotsMensualesDetalle
                        .CountAsync(d => snapIds.Contains(d.SnapshotId)),
                PrescripcionesSugeridas = await _db.PrescripcionesSugeridas
                    .CountAsync(p => p.TenantId == tenantId && p.PeriodDate == period),
                PrescripcionesFinales = await _db.PrescripcionesFinales
                    .CountAsync(p => p.TenantId == tenantId && p.PeriodDate == period),
                LotesImportacion = loteIds.Count,
                DetallesImportacion = loteIds.Count == 0 ? 0 :
                    await _db.ImportacionDetalles
                        .CountAsync(d => loteIds.Contains(d.LoteId)),
                ErroresImportacion = loteIds.Count == 0 ? 0 :
                    await _db.ImportacionErrores
                        .CountAsync(e => loteIds.Contains(e.LoteId)),
            };
        }

        public async Task<int> EliminarPeriodoAsync(Guid tenantId, int año, int mes, Guid usuarioId)
        {
            var period = new DateTime(año, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            int total = 0;

            // ── 1. PrescripcionFinal ──────────────────────────────────────────
            total += await _db.PrescripcionesFinales
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .ExecuteDeleteAsync();

            // ── 2. PrescripcionSugerida ───────────────────────────────────────
            total += await _db.PrescripcionesSugeridas
                .Where(p => p.TenantId == tenantId && p.PeriodDate == period)
                .ExecuteDeleteAsync();

            // ── 3. SnapshotMensualDetalle (FK → SnapshotMensual) ──────────────
            var snapIds = await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .Select(s => s.Id)
                .ToListAsync();

            if (snapIds.Count > 0)
            {
                total += await _db.SnapshotsMensualesDetalle
                    .Where(d => snapIds.Contains(d.SnapshotId))
                    .ExecuteDeleteAsync();
            }

            // ── 4. SnapshotMensual ────────────────────────────────────────────
            total += await _db.SnapshotsMensuales
                .Where(s => s.TenantId == tenantId && s.PeriodoAnio == año && s.PeriodoMes == mes)
                .ExecuteDeleteAsync();

            // ── 5. ResultadoLaboratorio ───────────────────────────────────────
            total += await _db.ResultadosLaboratorio
                .Where(r => r.TenantId == tenantId && r.PeriodoAnio == año && r.PeriodoMes == mes)
                .ExecuteDeleteAsync();

            // ── 6. ImportacionError + ImportacionDetalle + LoteImportacion ────
            var loteIds = await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.PeriodoAnio == año && l.PeriodoMes == mes)
                .Select(l => l.Id)
                .ToListAsync();

            if (loteIds.Count > 0)
            {
                total += await _db.ImportacionErrores
                    .Where(e => loteIds.Contains(e.LoteId))
                    .ExecuteDeleteAsync();

                total += await _db.ImportacionDetalles
                    .Where(d => loteIds.Contains(d.LoteId))
                    .ExecuteDeleteAsync();
            }

            total += await _db.LotesImportacion
                .Where(l => l.TenantId == tenantId && l.PeriodoAnio == año && l.PeriodoMes == mes)
                .ExecuteDeleteAsync();

            // ── Auditoría ─────────────────────────────────────────────────────
            await _auditoria.RegistrarAsync(new AuditoriaLog
            {
                TenantId    = tenantId,
                UsuarioId   = usuarioId,
                Accion      = "DELETE",
                Entidad     = "Periodo",
                Descripcion = $"Depuración: {total} registros eliminados del período {año}-{mes:D2}",
                CreatedAt   = DateTime.UtcNow
            });

            return total;
        }
    }
}
