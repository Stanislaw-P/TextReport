FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY global.json ./
COPY src/TextReport/TextReport.csproj src/TextReport/
RUN dotnet restore src/TextReport/TextReport.csproj
COPY src/TextReport/ src/TextReport/
RUN dotnet publish src/TextReport/TextReport.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TextReport.dll"]
