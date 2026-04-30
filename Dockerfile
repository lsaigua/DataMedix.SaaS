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

# Publicar en modo Release — SIN --no-restore para que los targets de Blazor
# static web assets se ejecuten completos y generen _framework/blazor.web.js
RUN dotnet publish DataMedix.Portal/DataMedix.Portal.csproj \
    -c Release \
    -o /app/publish

# Verificar que blazor.web.js quedó en el output
RUN echo "=== _framework ===" && ls /app/publish/wwwroot/_framework/ 2>/dev/null || echo "(vacío)"

# ─── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# libkrb5-3 silencia el warning "Cannot load libgssapi_krb5.so.2" en logs
RUN apt-get update && apt-get install -y --no-install-recommends libkrb5-3 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

# Railway inyecta $PORT en tiempo de ejecución
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet DataMedix.Portal.dll"]
