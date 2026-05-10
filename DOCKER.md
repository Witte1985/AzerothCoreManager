# AzerothCore Manager - Docker Setup

This project can be run in Docker as a single container that includes both the frontend and backend.

## Quick Start

### 1. Configure Environment

```bash
# Quickest way — generates the correct absolute path automatically:
echo "HOST_DATA_PATH=$(pwd)/data" > .env

# Or copy the example and edit manually:
cp .env.example .env
# Linux:  HOST_DATA_PATH=/home/username/AzerothCoreManager/data
# macOS:  HOST_DATA_PATH=/Users/username/AzerothCoreManager/data
```

**Important:** The `HOST_DATA_PATH` must be the **absolute path** on your host system to the `data` directory. This is required for volume mounting in nested Docker containers created by AzerothCore Manager.

### 2. Build and run with Docker Compose

```bash
# Build and start the container
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the container
docker-compose down
```

The application will be available at http://localhost:8080

### 2. Build and run with Docker directly

```bash
# Build the image
docker build -t azerothcore-manager:latest .

# Create data directory
mkdir -p ./data

# Get absolute path to data directory
DATA_PATH=$(pwd)/data

# Run the container
docker run -d \
  --name azerothcore-manager \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v $DATA_PATH:/app/data \
  -e Docker__HostDataPath=$DATA_PATH \
  azerothcore-manager:latest
```

## Architecture

The Docker image uses a multi-stage build:

1. **Stage 1 (frontend-build)**: Builds the React frontend using Node.js
2. **Stage 2 (backend-build)**: Builds the .NET backend
3. **Stage 3 (runtime)**: Combines both into a minimal ASP.NET runtime image

The ASP.NET backend serves the frontend static files and provides the API.

## Volumes

- `/var/run/docker.sock` - Required to manage Docker containers for AzerothCore stacks
- `/app/data` - Persistent storage for:
  - SQLite database (`azerothcore-manager.db`)
  - Stack build directories (`stacks/`)

**Important:** When running in Docker, you must configure `Docker__HostDataPath` to point to the absolute host path that corresponds to `/app/data`. This is required for volume mounting in nested Docker containers.

## Environment Variables

Configure the application via environment variables:

```yaml
environment:
  # ASP.NET Core
  - ASPNETCORE_ENVIRONMENT=Production
  
  # Paths
  - Docker__BuildsPath=/app/data/stacks
  - Docker__HostDataPath=/absolute/path/on/host/to/data  # Required!
  - ConnectionStrings__DefaultConnection=Data Source=/app/data/azerothcore-manager.db
  
  # Docker Compose
  - Docker__ComposeCommand=plugin  # or "standalone" or "auto"
  
  # CORS (if needed)
  - Cors__AllowedOrigins__0=https://your-domain.com
```

### Required Configuration for Docker-in-Docker

When the manager runs in a container and creates AzerothCore stacks, it uses the host's Docker daemon via the mounted socket. Volume paths in the generated docker-compose files must be **host paths**, not container paths.

Configure the translation:
- `Docker__BuildsPath`: Path inside the manager container (e.g., `/app/data/stacks`)
- `Docker__HostDataPath`: Absolute path on the host that maps to `/app/data` (e.g., `/home/user/project/data`)

Example:
```bash
# In docker-compose.yml or docker run command:
-e Docker__HostDataPath=/home/witte/Projects/AzerothCoreManager/data
```

## Network

All managed AzerothCore stacks are created on the `azerothcore-network` Docker network, allowing them to communicate with each other.

## Security Notes

### Docker Socket Access

The container requires access to the Docker socket (`/var/run/docker.sock`) to create and manage AzerothCore stack containers. This gives the container significant privileges on the host system.

**Recommendations:**
- Only run this on trusted networks
- Consider using Docker socket proxy for additional security
- Ensure proper firewall rules are in place

### File Permissions

The application runs as user `appuser` (UID 1000). Ensure the mounted data directory has appropriate permissions:

```bash
chown -R 1000:1000 ./data
```

## Updating

```bash
# Pull latest code
git pull

# Rebuild and restart
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

## Troubleshooting

### macOS: Volume mounting fails after first setup

On macOS the `.env` file (project root) is required before running `docker compose up`. If it was missing, the manager container started without `HOST_DATA_PATH`, causing it to write wrong container-internal paths (`/app/data/stacks/...`) into the generated `docker-compose.override.yml` and `.env` files inside each stack.

**Fix:**
```bash
# 1. Create the .env file (run from project root)
echo "HOST_DATA_PATH=$(pwd)/data" > .env

# 2. Restart the manager so it picks up the new config
docker compose down && docker compose up -d
```

The next time you start a stack through the UI, `EnsureRuntimeConfigurationAsync` will automatically regenerate the stack's configuration files with the correct macOS host paths.



Ensure Docker socket is mounted correctly:
```bash
ls -la /var/run/docker.sock
# Should show: srw-rw---- 1 root docker
```

Add your user to the docker group if needed:
```bash
sudo usermod -aG docker $USER
```

### Volume mounting errors in AzerothCore stacks

**Error:** `mounts denied: The path /app/data/stacks/... is not shared from the host`

**Solution:** Ensure `Docker__HostDataPath` is configured correctly in the manager container:

1. Check the `.env` file has the correct `HOST_DATA_PATH`
2. The path must be **absolute** on the host system
3. It must correspond to the directory you mounted to `/app/data`

Example:
```bash
# If you mount with: -v $(pwd)/data:/app/data
# Then set: Docker__HostDataPath=$(pwd)/data
```

### Permission denied on data directory

```bash
sudo chown -R 1000:1000 ./data
```

### Port already in use

Change the port mapping in `docker-compose.yml`:
```yaml
ports:
  - "8081:8080"  # Use port 8081 on host instead
```

## Development vs Production

For **development**, run frontend and backend separately:
```bash
# Terminal 1: Backend
cd backend && dotnet watch --project AzerothCoreManager.Api

# Terminal 2: Frontend
cd frontend && npm run dev
```

For **production**, use Docker as described above.
