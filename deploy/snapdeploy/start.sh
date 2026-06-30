#!/bin/bash
set -euo pipefail

export PORT="${PORT:-8080}"
export API_PORT="${API_PORT:-5000}"
export ASPNETCORE_URLS="http://127.0.0.1:${API_PORT}"

# Public URL your manager opens (set in SnapDeploy env vars).
if [ -n "${PUBLIC_ORIGIN:-}" ]; then
  export Compliance__PublicBaseUrl="${PUBLIC_ORIGIN}"
fi

envsubst '${PORT} ${API_PORT}' < /etc/nginx/templates/default.conf.template > /etc/nginx/sites-available/default
ln -sf /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default

dotnet /app/SMF.Api.dll &
sleep 15

nginx -g 'daemon off;'
