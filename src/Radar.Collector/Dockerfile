FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Radar.Data/Radar.Data.csproj src/Radar.Data/
COPY src/Radar.Collector/Radar.Collector.csproj src/Radar.Collector/
RUN dotnet restore src/Radar.Collector/Radar.Collector.csproj


COPY src/Radar.Data/ src/Radar.Data/
COPY src/Radar.Collector/ src/Radar.Collector/
RUN dotnet publish src/Radar.Collector/Radar.Collector.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .


USER $APP_UID

ENTRYPOINT ["dotnet", "Radar.Collector.dll"]
