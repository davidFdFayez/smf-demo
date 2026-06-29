# SMF — Demo deployment guide

One `docker compose up -d --build` brings up the entire stack:

| Container  | What it runs                        | Internal port |
| ---------- | ----------------------------------- | ------------- |
| `smf-db`   | SQL Server 2022 (Developer edition) | 1433          |
| `smf-api`  | .NET 8 Minimal API + SignalR hubs   | 8080          |
| `smf-web`  | Public web app (Vite SPA + Nginx)   | 80            |
| `smf-admin`| Admin panel (Vite SPA + Nginx)      | 80            |

Each Nginx front-end serves the static SPA **and** reverse-proxies
`/api`, `/hubs` and `/swagger` to the API container on the shared
Docker network, so you only ever hand out **two URLs** to your manager
(public web + admin) — SignalR WebSockets work out of the box, no CORS
dance required.

---

## Prerequisites

- **Docker Desktop 4.x** (Windows/macOS) or **Docker Engine 24+** (Linux)
- **Docker Compose v2** (bundled with Docker Desktop; on Linux: `apt install docker-compose-plugin`)
- ~4 GB free RAM (SQL Server alone wants ~2 GB)
- ~6 GB free disk

> **Windows note** — make sure Docker Desktop is set to **Linux containers**
> (the default). WSL 2 backend recommended.

---

## 1. First-time setup

```powershell
# From the repo root
Copy-Item .env.example .env
```

```bash
# Linux / macOS
cp .env.example .env
```

Open `.env` and at minimum change:

```dotenv
DB_SA_PASSWORD=<a strong SQL Server password>
JWT_SIGNING_KEY=<32+ character random string>
```

> Generate a JWT key quickly:
> ```powershell
> [Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))
> ```
> ```bash
> openssl rand -base64 48
> ```

---

## 2. Launch the stack

```bash
docker compose up -d --build
```

First build is slow (~3-5 min: downloads .NET SDK, Node, SQL Server images).
Subsequent `up` commands are instant thanks to layer caching.

Check health:

```bash
docker compose ps
docker compose logs -f api
```

You should see:

```
SMF.Api: Now listening on: http://[::]:8080
Using relational database 'SMF' on 'db,1433' (provider: Microsoft.EntityFrameworkCore.SqlServer).
Dev seed: created … (if DevSeeder ran)
```

---

## 3. Open the demo

After the API is healthy:

- **Public web**  →  <http://localhost:8080>
- **Admin panel** →  <http://localhost:8081>
- **Swagger**     →  <http://localhost:5080/swagger>

(Ports are overridable in `.env` via `WEB_PORT` / `ADMIN_PORT` / `API_PORT`.)

### Demo flow cheat sheet

1. Open **public web → /watch** — pick a match code, see the live scoreboard + AI insights.
2. Open **admin → /login**, then `Scoring` / `Head Referee` / `Timekeeper` to drive the bout.
3. Watch the public page update in real time over SignalR.
4. In admin → `Brackets`, try **Generate from registrations** to spin up a tournament.
5. Open **public → /tournaments** to show the live bracket view with connectors.

---

## 4. Sharing the demo with a remote manager

You don't have to put this on a cloud to share it. Pick whichever is easiest:

### Option A — Cloudflare Tunnel (free, no account setup headaches)

```bash
# Install once (Windows)
winget install --id Cloudflare.cloudflared

# Linux
curl -L --output cloudflared https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64
chmod +x cloudflared && sudo mv cloudflared /usr/local/bin/

# Public HTTPS URL that forwards to your local compose:
cloudflared tunnel --url http://localhost:8080     # public web
cloudflared tunnel --url http://localhost:8081     # admin panel
```

Each command prints a `https://*.trycloudflare.com` URL you can text to your
manager. No account, no config — disposable demo tunnels.

### Option B — ngrok

```bash
ngrok http 8080
ngrok http 8081
```

### Option C — Deploy to a VPS (DigitalOcean / Hetzner / Azure VM / AWS EC2)

1. Provision a VM with ≥ 4 GB RAM (Ubuntu 22.04 works perfectly).
2. Install Docker:

   ```bash
   curl -fsSL https://get.docker.com | sh
   sudo usermod -aG docker $USER && newgrp docker
   ```

3. Copy the repo up:

   ```bash
   rsync -az --exclude node_modules --exclude bin --exclude obj \
         ./ user@your-vm:/opt/smf/
   ```

4. SSH in, then:

   ```bash
   cd /opt/smf
   cp .env.example .env       # edit secrets
   docker compose up -d --build
   ```

5. Open the VM's firewall for ports `8080` and `8081` (or front everything
   with Caddy/Traefik for HTTPS — see Option D).

### Option D — HTTPS with a real domain (optional polish)

Add Caddy as an extra service that terminates TLS and forwards to the two
Nginx containers. Drop this at the bottom of `docker-compose.yml` and add a
`Caddyfile`:

```yaml
  caddy:
    image: caddy:2-alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    depends_on: [web, admin]
```

```caddyfile
# Caddyfile
smf.example.com       { reverse_proxy web:80 }
admin.smf.example.com { reverse_proxy admin:80 }
```

Caddy auto-issues Let's Encrypt certs — zero-config HTTPS.

---

## 5. Day-2 commands

```bash
docker compose logs -f api           # tail API logs
docker compose logs -f web admin     # tail Nginx access logs
docker compose restart api           # bounce the API (e.g. after code change)
docker compose down                  # stop everything (volumes kept)
docker compose down -v               # nuke the SQL Server volume too (fresh data)
docker compose build --no-cache api  # force rebuild after big changes
```

### Rebuilding after code changes

```bash
# Change backend code → rebuild API image only
docker compose up -d --build api

# Change web code → rebuild web image only
docker compose up -d --build web

# Change admin code → rebuild admin image only
docker compose up -d --build admin
```

---

## 6. Troubleshooting

**SQL Server container keeps restarting.**
Cause: password doesn't meet complexity rules or host doesn't have enough RAM.
Check `docker compose logs db`. Set `DB_SA_PASSWORD` to 8+ chars mixing upper,
lower, digit, symbol. Give Docker Desktop ≥ 4 GB.

**API can't connect to DB.**
Wait — the healthcheck delays `smf-api` until SQL Server accepts connections,
but the first launch can take ~40 s on slower machines. `docker compose logs api`
tells you exactly what's happening.

**Swagger 404 / CORS errors when hitting API directly on `:5080`.**
That's because your browser origin (e.g. `http://localhost:8080`) is listed in
`Cors__AllowedOrigins__*` env vars. If you change `WEB_PORT` / `ADMIN_PORT`,
restart the API container so it picks up the new origins.

**Port 8080 already in use.**
Open `.env` and change `WEB_PORT` (and / or `ADMIN_PORT`, `API_PORT`). Then:

```bash
docker compose up -d
```

**"mcr.microsoft.com/mssql/server: manifest unknown" on Apple Silicon.**
SQL Server 2022 Linux images are x64-only but run fine under Rosetta. Enable
"Use Rosetta for x86/amd64 emulation on Apple Silicon" in Docker Desktop →
Settings → General.

---

## 7. Production-grade checklist (if this demo turns real)

- [ ] Run API as `ASPNETCORE_ENVIRONMENT=Production` (remove DevSeeder run)
- [ ] Replace `JWT_SIGNING_KEY` with a value from a secret manager (Azure Key Vault, AWS SSM, ...)
- [ ] Put Caddy / Traefik / Nginx in front with real TLS certs
- [ ] Move SQL Server to a managed service (Azure SQL, RDS) or pin a backup cadence
- [ ] Configure log aggregation (e.g. Seq, Loki, or Azure Log Analytics)
- [ ] Lock down `Cors__AllowedOrigins__*` to the production domains only
- [ ] Build images in CI and push to a registry (`ghcr.io` / ACR / ECR) instead of building on the host
