# Multi-stage build for AzerothCore Manager
# Stage 1: Build frontend
FROM node:22-alpine AS frontend-build

WORKDIR /app/frontend

# Copy package files
COPY frontend/package*.json ./

# Install dependencies
RUN npm ci

# Copy frontend source
COPY frontend/ ./

# Build frontend for production
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /app/backend

# Copy project files and restore dependencies
COPY backend/*.sln ./
COPY backend/AzerothCoreManager.Api/*.csproj ./AzerothCoreManager.Api/
COPY backend/AzerothCoreManager.Core/*.csproj ./AzerothCoreManager.Core/
COPY backend/AzerothCoreManager.Infrastructure/*.csproj ./AzerothCoreManager.Infrastructure/

RUN dotnet restore

# Copy all source code
COPY backend/ ./

# Build and publish the application
RUN dotnet publish AzerothCoreManager.Api/AzerothCoreManager.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Install git, curl, docker with compose v2 plugin
RUN apt-get update && \
    apt-get install -y git curl docker.io docker-compose-v2 && \
    rm -rf /var/lib/apt/lists/*

# Create data directory with appropriate permissions
# Use the existing non-root user from the base image
RUN mkdir -p /app/data && \
    chown -R app:app /app

# Copy backend from build stage
COPY --from=backend-build --chown=app:app /app/publish ./

# Copy frontend static files to wwwroot
COPY --from=frontend-build --chown=app:app /app/frontend/dist ./wwwroot

# Switch to non-root user
USER app

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "AzerothCoreManager.Api.dll"]
