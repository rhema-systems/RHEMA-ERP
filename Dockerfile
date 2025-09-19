# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ErpSystem.Web/ErpSystem.Web.csproj", "src/ErpSystem.Web/"]
COPY ["src/ErpSystem.Core/ErpSystem.Core.csproj", "src/ErpSystem.Core/"]
COPY ["src/ErpSystem.Data/ErpSystem.Data.csproj", "src/ErpSystem.Data/"]
COPY ["src/ErpSystem.Shared/ErpSystem.Shared.csproj", "src/ErpSystem.Shared/"]

RUN dotnet restore "src/ErpSystem.Web/ErpSystem.Web.csproj"

# Copy all source code
COPY . .
WORKDIR "/src/src/ErpSystem.Web"

# Build the application
RUN dotnet build "ErpSystem.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ErpSystem.Web.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install SQL Server tools for database operations (optional)
RUN apt-get update && apt-get install -y \
    curl \
    unzip \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Create app user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Expose port
EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "ErpSystem.Web.dll"]