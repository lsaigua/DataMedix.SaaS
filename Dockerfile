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

# ─── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Puerto que expone Railway/Render (8080 es el estándar)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DataMedix.Portal.dll"]
