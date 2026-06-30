# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Multi-stage Dockerfile for the SMF public web app (Vite + React).
#
# Build:
#     docker build -t smf-web:latest ./web
# ---------------------------------------------------------------------------

# ---- 1. Build -------------------------------------------------------------
FROM node:20-alpine AS build
WORKDIR /app

# Dependencies first for better caching.
COPY package.json package-lock.json* ./
RUN npm ci || npm install

COPY . .

# Produce a production SPA bundle in /app/dist.
RUN npm run build

# ---- 2. Serve with Nginx --------------------------------------------------
FROM nginx:1.27-alpine AS runtime

# Our SPA + API proxy config.
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Static bundle.
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

# Default Nginx CMD is fine.
