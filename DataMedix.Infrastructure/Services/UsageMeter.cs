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

        public async Task RecordAsync(string eventType, object? metadata = null)
        {
            if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
                return;

            var metadataJson = metadata is null
                ? null
                : JsonSerializer.Serialize(metadata, _jsonOpts);

            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO usage_events (id, tenant_id, event_type, occurred_at, metadata)
                    VALUES ($1, $2, $3, $4, $5::jsonb)";
                cmd.Parameters.AddWithValue(Guid.NewGuid());
                cmd.Parameters.AddWithValue(_tenantContext.TenantId);
                cmd.Parameters.AddWithValue(eventType);
                cmd.Parameters.AddWithValue(DateTime.UtcNow);
                cmd.Parameters.AddWithValue(metadataJson is null ? DBNull.Value : (object)metadataJson);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // No propagar el error al caller — la lógica de negocio no debe fallar
                // por un problema de facturación. Solo loguear.
                _logger.LogError(ex,
                    "Error al registrar usage_event {EventType} para tenant {TenantId}",
                    eventType, _tenantContext.TenantId);
            }
        }
    }
}
