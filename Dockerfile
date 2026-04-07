# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# Copy everything first for development watch support
COPY . .
RUN dotnet restore src/HomeStoq.App/HomeStoq.App.csproj

# Build and publish for production
RUN dotnet publish src/HomeStoq.App/HomeStoq.App.csproj -c Release -o out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Create data directory for SQLite
RUN mkdir /app/data && chown -R 1000:1000 /app/data

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "HomeStoq.App.dll"]
