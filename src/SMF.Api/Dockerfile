# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Multi-stage Dockerfile for the SMF .NET 8 API.
#
# Build from the repository root so that sibling projects (SMF.Application,
# SMF.Infrastructure, SMF.Domain) resolve correctly:
#
#     docker build -f src/SMF.Api/Dockerfile -t smf-api:latest .
# ---------------------------------------------------------------------------

# ---- 1. Build & publish ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + csproj files first so `dotnet restore` gets a warm cache
# even when only source files change on subsequent builds.
COPY SMF.sln ./
COPY src/SMF.Domain/SMF.Domain.csproj                   src/SMF.Domain/
COPY src/SMF.Application/SMF.Application.csproj         src/SMF.Application/
COPY src/SMF.Infrastructure/SMF.Infrastructure.csproj   src/SMF.Infrastructure/
COPY src/SMF.Api/SMF.Api.csproj                         src/SMF.Api/
COPY tests/                                             tests/

RUN dotnet restore src/SMF.Api/SMF.Api.csproj

# Now bring in the full source tree.
COPY src/ src/

RUN dotnet publish src/SMF.Api/SMF.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---- 2. Runtime image -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for safety.
RUN groupadd --system --gid 1001 smf \
 && useradd  --system --uid 1001 --gid smf smf

COPY --from=build /app/publish .

# Create the upload directory used by IFileStorage (governance documents,
# future uploads) and hand ownership to the non-root runtime user. Mounted
# volumes can re-overlay /app/App_Data and inherit this ownership when the
# host bind-mount or named volume is initially empty.
RUN mkdir -p /app/App_Data/uploads \
 && chown -R smf:smf /app/App_Data

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --chmod=755 src/SMF.Api/docker-entrypoint.sh /app/docker-entrypoint.sh

EXPOSE 8080

USER smf

ENTRYPOINT ["/app/docker-entrypoint.sh"]
