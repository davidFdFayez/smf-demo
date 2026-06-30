#!/usr/bin/env bash
# Always-on SMF on a Linux VPS (Oracle Cloud Free recommended).
# Run: sudo bash deploy/bootstrap-vps.sh /opt/smf

set -euo pipefail

APP_DIR="${1:-/opt/smf}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "==> Installing Docker..."
if ! command -v docker >/dev/null 2>&1; then
  apt-get update -qq
  apt-get install -y ca-certificates curl gnupg rsync unzip
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
fi

echo "==> Syncing app to $APP_DIR"
mkdir -p "$APP_DIR"
rsync -a --delete \
  --exclude node_modules --exclude bin --exclude obj --exclude .git \
  "$REPO_ROOT/" "$APP_DIR/"
cd "$APP_DIR"

PUBLIC_IP="$(curl -fsSL https://api.ipify.org 2>/dev/null || hostname -I | awk '{print $1}')"
SSLIP_HOST="$(echo "$PUBLIC_IP" | tr '.' '-').sslip.io"
WEB_ORIGIN="https://${SSLIP_HOST}"
ADMIN_ORIGIN="https://admin.${SSLIP_HOST}"

if [ ! -f .env.cloud ]; then
  cp .env.cloud.example .env.cloud
fi

JWT_KEY="$(openssl rand -base64 48 | tr -d '\n')"
DB_PASS="$(openssl rand -base64 24 | tr -d '\n/+=' | head -c 24)!Aa1"

grep -q '^DB_SA_PASSWORD=' .env.cloud && sed -i "s|^DB_SA_PASSWORD=.*|DB_SA_PASSWORD=$DB_PASS|" .env.cloud || echo "DB_SA_PASSWORD=$DB_PASS" >> .env.cloud
grep -q '^JWT_SIGNING_KEY=' .env.cloud && sed -i "s|^JWT_SIGNING_KEY=.*|JWT_SIGNING_KEY=$JWT_KEY|" .env.cloud || echo "JWT_SIGNING_KEY=$JWT_KEY" >> .env.cloud
grep -q '^SSLIP_HOST=' .env.cloud && sed -i "s|^SSLIP_HOST=.*|SSLIP_HOST=$SSLIP_HOST|" .env.cloud || echo "SSLIP_HOST=$SSLIP_HOST" >> .env.cloud
grep -q '^PUBLIC_WEB_ORIGIN=' .env.cloud && sed -i "s|^PUBLIC_WEB_ORIGIN=.*|PUBLIC_WEB_ORIGIN=$WEB_ORIGIN|" .env.cloud || echo "PUBLIC_WEB_ORIGIN=$WEB_ORIGIN" >> .env.cloud
grep -q '^PUBLIC_ADMIN_ORIGIN=' .env.cloud && sed -i "s|^PUBLIC_ADMIN_ORIGIN=.*|PUBLIC_ADMIN_ORIGIN=$ADMIN_ORIGIN|" .env.cloud || echo "PUBLIC_ADMIN_ORIGIN=$ADMIN_ORIGIN" >> .env.cloud

if [ -f .env ] && grep -q '^CLOUDFLARE_TUNNEL_TOKEN=' .env 2>/dev/null; then
  TOKEN="$(grep '^CLOUDFLARE_TUNNEL_TOKEN=' .env | cut -d= -f2-)"
  grep -q '^CLOUDFLARE_TUNNEL_TOKEN=' .env.cloud && sed -i "s|^CLOUDFLARE_TUNNEL_TOKEN=.*|CLOUDFLARE_TUNNEL_TOKEN=$TOKEN|" .env.cloud || echo "CLOUDFLARE_TUNNEL_TOKEN=$TOKEN" >> .env.cloud
fi

echo "==> Building stack (first run ~10 min)..."
docker compose -f docker-compose.cloud.yml --env-file .env.cloud up -d --build

echo ""
echo "=============================================="
echo " SMF is LIVE 24/7 (PC can be OFF)"
echo "=============================================="
echo "  Website:  $WEB_ORIGIN/"
echo "  Watch:    $WEB_ORIGIN/watch?match=match-002"
echo "  Admin:    $ADMIN_ORIGIN/"
echo "  Server IP: $PUBLIC_IP"
echo "=============================================="
