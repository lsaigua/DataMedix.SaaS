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

# Diagnóstico: buscar blazor.web.js en todas las ubicaciones del SDK
RUN echo "=== wwwroot ===" && find /app/publish/wwwroot -type f 2>/dev/null | sort | head -30 && \
    echo "=== blazor.web.js en publish ===" && find /app/publish -name "blazor.web.js" 2>/dev/null || echo "no encontrado en publish" && \
    echo "=== blazor.web.js en SDK packs ===" && find /usr/share/dotnet/packs -name "blazor.web.js" 2>/dev/null | head -5 || echo "no en packs" && \
    echo "=== blazor.web.js en SDK ===" && find /usr/share/dotnet/sdk -name "blazor.web.js" 2>/dev/null | head -5 || echo "no en sdk" && \
    echo "=== blazor.web.js en shared framework ===" && find /usr/share/dotnet/shared -name "blazor.web.js" 2>/dev/null | head -5 || echo "no en shared"

# Intentar copiar blazor.web.js al wwwroot físico (para UseStaticFiles)
RUN BLAZOR=$(find /usr/share/dotnet -name "blazor.web.js" 2>/dev/null | head -1); \
    [ -z "$BLAZOR" ] && BLAZOR=$(find /root/.nuget -name "blazor.web.js" 2>/dev/null | head -1); \
    mkdir -p /app/publish/wwwroot/_framework; \
    if [ -n "$BLAZOR" ]; then \
        cp "$BLAZOR" /app/publish/wwwroot/_framework/blazor.web.js; \
        echo "[OK] blazor.web.js copiado desde $BLAZOR"; \
    else \
        echo "[INFO] blazor.web.js no encontrado en disco — se servirá desde recurso embebido en runtime"; \
    fi && \
    ls -la /app/publish/wwwroot/_framework/ 2>/dev/null

# ─── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

# Railway inyecta $PORT en tiempo de ejecución — el CMD lo usa para ASPNETCORE_URLS
# Si no está definido, cae a 8080 (compatibilidad con otros hosts)
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet DataMedix.Portal.dll"]
