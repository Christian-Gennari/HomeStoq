# Build Stage
# Using Alpine for minimal size and attack surface.
# This is a simple ASP.NET API with no special native dependencies,
# so Alpine's musl libc works fine and keeps the image ~100MB.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# Copy everything first for development watch support
COPY . .
RUN dotnet restore src/HomeStoq.App/HomeStoq.App.csproj

# Build and publish for production
RUN dotnet publish src/HomeStoq.App/HomeStoq.App.csproj -c Release -o out

# Runtime Stage
# Alpine ASP.NET runtime for minimal footprint (~100MB).
# The API only needs to serve HTTP requests and access SQLite.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Install wget for healthcheck and create dynamic healthcheck script
# Script reads HostUrl from config.ini to determine the port dynamically
RUN apk add --no-cache wget && \
    echo '#!/bin/sh\nPORT=$(grep -oE "HostUrl.*:([0-9]+)" /app/config.ini 2>/dev/null | grep -oE "[0-9]+" || echo 5050)\nwget --no-verbose --tries=1 --spider "http://localhost:${PORT}/api/inventory"' > /app/healthcheck.sh && \
    chmod +x /app/healthcheck.sh

# Create data directory for SQLite
RUN mkdir /app/data && chown -R 1000:1000 /app/data

EXPOSE 5050

ENTRYPOINT ["dotnet", "HomeStoq.App.dll"]
