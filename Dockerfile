# syntax=docker/dockerfile:1.7
# Cloud deploy (SnapDeploy / Render) — website + API in one container.
# Build context MUST be repository root (.)
# Do NOT use Windows paths like E:\... in SnapDeploy settings.

FROM node:20-alpine AS web-build
WORKDIR /app
COPY web/package.json web/package-lock.json* ./
RUN npm ci || npm install
COPY web/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
WORKDIR /src
COPY SMF.sln ./
COPY src/SMF.Domain/SMF.Domain.csproj src/SMF.Domain/
COPY src/SMF.Application/SMF.Application.csproj src/SMF.Application/
COPY src/SMF.Infrastructure/SMF.Infrastructure.csproj src/SMF.Infrastructure/
COPY src/SMF.Api/SMF.Api.csproj src/SMF.Api/
COPY tests/ tests/
RUN dotnet restore src/SMF.Api/SMF.Api.csproj
COPY src/ src/
RUN dotnet publish src/SMF.Api/SMF.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends nginx gettext \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=api-build /app/publish .
COPY --from=web-build /app/dist /var/www/web

RUN mkdir -p /app/App_Data/uploads \
 && chown -R www-data:www-data /var/www/web

COPY deploy/snapdeploy/nginx-all-in-one.template /etc/nginx/templates/default.conf.template
COPY deploy/snapdeploy/start.sh /start.sh
RUN chmod +x /start.sh

ENV ASPNETCORE_ENVIRONMENT=CloudDemo \
    ConnectionStrings__DefaultConnection="" \
    Jwt__Issuer=smf-api \
    Jwt__Audience=smf-clients \
    Jwt__SigningKey=cloud-demo-secret-key-minimum-32-characters-long \
    API_PORT=5000

EXPOSE 8080
CMD ["/start.sh"]
