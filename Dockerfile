# ─── Stage 1: build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar archivos de proyecto primero (cachea restore si no cambian deps)
COPY DataMedix.Domain/DataMedix.Domain.csproj           DataMedix.Domain/
COPY DataMedix.Core/DataMedix.Core.csproj               DataMedix.Core/
COPY Application/DataMedix.Application.csproj           Application/
COPY DataMedix.Infrastructure/DataMedix.Infrastructure.csproj  DataMedix.Infrastructure/
COPY DataMedix.Portal/DataMedix.Portal.csproj           DataMedix.Portal/

RUN dotnet restore DataMedix.Portal/DataMedix.Portal.csproj

# Copiar todo el código
COPY . .

# Publicar en modo Release
RUN dotnet publish DataMedix.Portal/DataMedix.Portal.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Diagnóstico: verificar que los static web assets de Blazor quedaron en el publish
RUN echo "=== wwwroot contents ===" && \
    ls /app/publish/wwwroot/ 2>/dev/null || echo "[WARN] wwwroot missing" && \
    echo "=== _framework ===" && \
    ls /app/publish/wwwroot/_framework/ 2>/dev/null || echo "[WARN] _framework missing" && \
    echo "=== Static web assets manifest ===" && \
    ls /app/publish/*.staticwebassets.* 2>/dev/null || echo "[WARN] no staticwebassets manifest"

# ─── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

# Railway inyecta $PORT en tiempo de ejecución — el CMD lo usa para ASPNETCORE_URLS
# Si no está definido, cae a 8080 (compatibilidad con otros hosts)
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet DataMedix.Portal.dll"]
