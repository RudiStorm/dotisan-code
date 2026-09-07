FROM node:22-alpine AS web-build
WORKDIR /src
COPY src/GeneratedAppintegrationsCheck.Web/package*.json ./
RUN npm install
COPY src/GeneratedAppintegrationsCheck.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY src/GeneratedAppintegrationsCheck.Api/GeneratedAppintegrationsCheck.Api.csproj src/GeneratedAppintegrationsCheck.Api/
RUN dotnet restore src/GeneratedAppintegrationsCheck.Api/GeneratedAppintegrationsCheck.Api.csproj
COPY . .
COPY --from=web-build /src/dist src/GeneratedAppintegrationsCheck.Api/wwwroot
RUN dotnet publish src/GeneratedAppintegrationsCheck.Api/GeneratedAppintegrationsCheck.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=api-build /app/publish .
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s CMD wget --spider --no-verbose http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "GeneratedAppintegrationsCheck.Api.dll"]