using DataMedix.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace DataMedix.Infrastructure.Services
{
    /// <summary>
    /// Registra eventos de facturación directamente en la tabla usage_events
    /// de la base central usando Npgsql raw (no EF Core) para evitar interferir
    /// con transacciones en curso del DbContext principal.
    /// </summary>
    public sealed class UsageMeter : IUsageMeter
    {
        private readonly string _connectionString;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<UsageMeter> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public UsageMeter(IConfiguration config, ITenantContext tenantContext, ILogger<UsageMeter> logger)
        {
            _connectionString = config.GetConnectionString("DatabasePostgres")
                ?? throw new InvalidOperationException("ConnectionStrings:DatabasePostgres no configurado.");
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public Task RecordAsync(string eventType, object? metadata = null)
            => RecordManyAsync(eventType, metadata is null ? [] : [metadata]);

        public async Task RecordManyAsync(string eventType, IEnumerable<object> metadatas)
        {
            if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
                return;

            // Una lista vacía sigue representando un evento sin metadata
            var lista = metadatas as IList<object> ?? metadatas.ToList();
            var jsons = lista.Count == 0
                ? new List<string?> { null }
                : lista.Select(m => (string?)JsonSerializer.Serialize(m, _jsonOpts)).ToList();

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Un batch en una sola conexión: registrar por paciente con una
                // conexión cada vez agotaría el pool en importaciones grandes.
                await using var batch = new NpgsqlBatch(conn);
                foreach (var metadataJson in jsons)
                {
                    var cmd = new NpgsqlBatchCommand(@"
                        INSERT INTO usage_events (id, tenant_id, event_type, occurred_at, metadata)
                        VALUES ($1, $2, $3, $4, $5::jsonb)");
                    cmd.Parameters.AddWithValue(Guid.NewGuid());
                    cmd.Parameters.AddWithValue(_tenantContext.TenantId);
                    cmd.Parameters.AddWithValue(eventType);
                    cmd.Parameters.AddWithValue(DateTime.UtcNow);
                    cmd.Parameters.AddWithValue(metadataJson is null ? DBNull.Value : metadataJson);
                    batch.BatchCommands.Add(cmd);
                }

                await batch.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // No propagar el error al caller — la lógica de negocio no debe fallar
                // por un problema de facturación. Solo loguear.
                _logger.LogError(ex,
                    "Error al registrar {Cantidad} usage_event {EventType} para tenant {TenantId}",
                    jsons.Count, eventType, _tenantContext.TenantId);
            }
        }
    }
}
