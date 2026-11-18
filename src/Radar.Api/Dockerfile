FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Radar.Data/Radar.Data.csproj src/Radar.Data/
COPY src/Radar.Api/Radar.Api.csproj src/Radar.Api/
RUN dotnet restore src/Radar.Api/Radar.Api.csproj


COPY src/Radar.Data/ src/Radar.Data/
COPY src/Radar.Api/ src/Radar.Api/
RUN dotnet publish src/Radar.Api/Radar.Api.csproj -c Release -o /app --no-restore
#what dotnet p
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "Radar.Api.dll"]
