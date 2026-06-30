# syntax=docker/dockerfile:1.7
# SnapDeploy / cloud — build context MUST be repo root containing: web/ src/ tests/ SMF.sln
# SnapDeploy settings: Root Directory = .  |  Dockerfile = Dockerfile  |  Port = 8080

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
RUN mkdir -p /app/App_Data/uploads && chown -R www-data:www-data /var/www/web

# Embedded nginx + start script (no deploy/ folder required on GitHub)
RUN mkdir -p /etc/nginx/templates /etc/nginx/sites-available /etc/nginx/sites-enabled
COPY <<'NGINX' /etc/nginx/templates/default.conf.template
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}
server {
    listen      ${PORT};
    server_name _;
    root  /var/www/web;
    index index.html;
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files $uri =404;
    }
    location /api/ {
        proxy_pass         http://127.0.0.1:${API_PORT};
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
    }
    location /swagger/ {
        proxy_pass         http://127.0.0.1:${API_PORT};
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
    location /hubs/ {
        proxy_pass         http://127.0.0.1:${API_PORT};
        proxy_http_version 1.1;
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        $connection_upgrade;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_buffering    off;
        proxy_read_timeout 3600s;
    }
    location = /index.html {
        add_header Cache-Control "no-cache, no-store, must-revalidate";
        try_files $uri =404;
    }
    location / {
        add_header Cache-Control "no-cache, no-store, must-revalidate";
        try_files $uri $uri/ /index.html;
    }
}
NGINX

COPY <<'START' /start.sh
#!/bin/bash
set -euo pipefail
export PORT="${PORT:-8080}"
export API_PORT="${API_PORT:-5000}"
export ASPNETCORE_URLS="http://127.0.0.1:${API_PORT}"
if [ -n "${PUBLIC_ORIGIN:-}" ]; then
  export Compliance__PublicBaseUrl="${PUBLIC_ORIGIN}"
fi
envsubst '${PORT} ${API_PORT}' < /etc/nginx/templates/default.conf.template > /etc/nginx/sites-available/default
ln -sf /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default
dotnet /app/SMF.Api.dll &
sleep 15
nginx -g 'daemon off;'
START

RUN chmod +x /start.sh

ENV ASPNETCORE_ENVIRONMENT=CloudDemo \
    ConnectionStrings__DefaultConnection="" \
    Jwt__Issuer=smf-api \
    Jwt__Audience=smf-clients \
    Jwt__SigningKey=cloud-demo-secret-key-minimum-32-characters-long \
    API_PORT=5000

EXPOSE 8080
CMD ["/start.sh"]
