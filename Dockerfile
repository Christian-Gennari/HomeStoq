# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# Copy csproj and restore
COPY src/HomeStoq.Server/*.csproj ./src/HomeStoq.Server/
RUN dotnet restore src/HomeStoq.Server/HomeStoq.Server.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish src/HomeStoq.Server/HomeStoq.Server.csproj -c Release -o out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Create data directory for SQLite
RUN mkdir /app/data && chown -R 1000:1000 /app/data

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "HomeStoq.Server.dll"]
