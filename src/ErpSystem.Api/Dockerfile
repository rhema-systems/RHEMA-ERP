# Use the official .NET 8 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ErpSystem.Api/ErpSystem.Api.csproj", "ErpSystem.Api/"]
COPY ["src/ErpSystem.Core/ErpSystem.Core.csproj", "ErpSystem.Core/"]
COPY ["src/ErpSystem.Data/ErpSystem.Data.csproj", "ErpSystem.Data/"]
COPY ["src/ErpSystem.Shared/ErpSystem.Shared.csproj", "ErpSystem.Shared/"]

# Restore NuGet packages
RUN dotnet restore "ErpSystem.Api/ErpSystem.Api.csproj"

# Copy all source code
COPY src/ .

# Build the application
WORKDIR "/src/ErpSystem.Api"
RUN dotnet build "ErpSystem.Api.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "ErpSystem.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy the published application
COPY --from=publish /app/publish .

# Create a non-root user
RUN groupadd -r erpuser && useradd --no-log-init -r -g erpuser erpuser
RUN mkdir -p /app/wwwroot/uploads /app/secure-file-storage && chown -R erpuser:erpuser /app
USER erpuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "ErpSystem.Api.dll"]
