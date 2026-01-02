# XcluadeAgent Dockerfile
# Developed by xman studio | https://xman4289.com

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/XcluadeAgent.Core/XcluadeAgent.Core.csproj", "src/XcluadeAgent.Core/"]
COPY ["src/XcluadeAgent.Shared/XcluadeAgent.Shared.csproj", "src/XcluadeAgent.Shared/"]
COPY ["src/XcluadeAgent.Infrastructure/XcluadeAgent.Infrastructure.csproj", "src/XcluadeAgent.Infrastructure/"]
COPY ["src/XcluadeAgent.Api/XcluadeAgent.Api.csproj", "src/XcluadeAgent.Api/"]

# Restore dependencies
RUN dotnet restore "src/XcluadeAgent.Api/XcluadeAgent.Api.csproj"

# Copy all source code
COPY . .

# Build
WORKDIR "/src/src/XcluadeAgent.Api"
RUN dotnet build "XcluadeAgent.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "XcluadeAgent.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install common tools
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    git \
    unzip \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r xcluade && useradd -r -g xcluade xcluade

# Create directories
RUN mkdir -p /app/data /app/config /app/data/logs /app/data/backups \
    && chown -R xcluade:xcluade /app

# Copy published files
COPY --from=publish /app/publish .
COPY config/config.example.yaml /app/config/config.example.yaml

# Switch to non-root user
USER xcluade

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "XcluadeAgent.Api.dll"]
