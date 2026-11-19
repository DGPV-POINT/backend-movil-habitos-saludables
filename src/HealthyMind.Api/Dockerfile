FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
# .editorconfig: excluye del análisis las migraciones generadas por EF Core (sin él, CA1861 rompe el build).
COPY .editorconfig Directory.Build.props HealthyMind.slnx ./
COPY src/ src/
RUN dotnet publish src/HealthyMind.Api/HealthyMind.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "HealthyMind.Api.dll"]
