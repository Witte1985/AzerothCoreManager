# AzerothCore Manager - Backend

ASP.NET Core Web API for managing AzerothCore Docker stacks.

## Project Structure

```
backend/
├── AzerothCoreManager.Api/          # Web API project
│   ├── Controllers/                 # API controllers
│   ├── Hubs/                        # SignalR hubs
│   ├── Program.cs                   # Application entry point
│   └── appsettings.json            # Configuration
├── AzerothCoreManager.Core/         # Contracts and business abstractions
│   ├── Services/
│   │   └── Interfaces/             # Service contracts
│   ├── Models/                      # Domain models
│   └── Contracts/                   # DTOs for API contracts
└── AzerothCoreManager.Infrastructure/ # Data access and external services
    ├── Configuration/               # Bound options objects
    ├── Data/                        # Entity Framework Core
    │   └── AzerothCoreDbContext.cs # Database context
    ├── Services/                    # Docker and Git adapters
    └── DependencyInjection.cs       # AddInfrastructure registration

```

## Prerequisites

- .NET 10 SDK (preferred) or .NET 11 preview
- Docker (for running AzerothCore stacks)

## Setup Instructions

### 1. Install .NET SDK

The backend projects target `net10.0`, and this repository pins the SDK to .NET 10 via `global.json`. Copilot's cloud
agent also preinstalls .NET 10 and .NET 11 preview through `.github/workflows/copilot-setup-steps.yml`.

**Fedora/RHEL:**
```bash
sudo dnf install dotnet-sdk-10.0
```

**Ubuntu/Debian:**
```bash
sudo apt install dotnet-sdk-10.0
```

**macOS:**
```bash
brew install dotnet@10
```

**Windows:**
Download from https://dotnet.microsoft.com/download/dotnet/10.0

### 2. Restore Dependencies

```bash
cd backend
dotnet restore
```

### 3. Build the Project

```bash
dotnet build
```

### 4. Run the API

```bash
dotnet run --project AzerothCoreManager.Api
```

The API will start on:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

### 5. Access Swagger UI

Navigate to: http://localhost:5000/swagger

## Configuration

Edit `AzerothCoreManager.Api/appsettings.json` to configure:

- **Database**: SQLite connection string (default: `azerothcore-manager.db`)
- **Docker**: Socket path and builds directory
- **CORS**: Allowed frontend origins

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=azerothcore-manager.db"
  },
  "Docker": {
    "SocketPath": "unix:///var/run/docker.sock",
    "BuildsPath": "/builds"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173", "http://localhost:8080"]
  }
}
```

## NuGet Packages

### API Project
- **Serilog.AspNetCore** - Structured logging
- **Serilog.Sinks.Console** - Console output for logs
- **Swashbuckle.AspNetCore** - Swagger/OpenAPI documentation

### Infrastructure Project
- **Docker.DotNet** - Docker SDK for .NET
- **Microsoft.EntityFrameworkCore** - ORM framework
- **Microsoft.EntityFrameworkCore.Sqlite** - SQLite provider
- **Microsoft.EntityFrameworkCore.Design** - Design-time tools
- **Microsoft.Extensions.Options.ConfigurationExtensions** - Configuration binding for infrastructure options

## Development

### Run in Watch Mode

```bash
dotnet watch --project AzerothCoreManager.Api
```

### Run Tests

```bash
dotnet test
```

### Create Database Migration

```bash
dotnet ef migrations add <MigrationName> --project AzerothCoreManager.Infrastructure --startup-project AzerothCoreManager.Api
```

### Update Database

```bash
dotnet ef database update --project AzerothCoreManager.Infrastructure --startup-project AzerothCoreManager.Api
```

## API Endpoints

### Health Check
- `GET /api/health` - Returns API status plus database, Docker, and Git dependency status

### Stack Management
- `POST /api/stacks` - Create new stack
- `GET /api/stacks` - List all stacks
- `GET /api/stacks/{id}` - Get stack details
- `DELETE /api/stacks/{id}` - Delete stack
- `POST /api/stacks/{id}/start` - Start stack
- `POST /api/stacks/{id}/stop` - Stop stack
- `POST /api/stacks/{id}/restart` - Restart stack

### Build Management (TODO)
- `POST /api/stacks/{id}/build` - Start build
- `GET /api/stacks/{id}/build/status` - Get build status
- `POST /api/stacks/{id}/build/cancel` - Cancel build
- `DELETE /api/stacks/{id}/build/files` - Clean up build files

### Configuration
- `GET /api/modules` - List available modules
- `POST /api/stacks/validate` - Validate configuration

## SignalR Hubs

### BuildProgressHub
Real-time build progress subscriptions. The current backend exposes the hub route and build status scaffold; full streamed orchestration events still need to be implemented.

**Client Methods:**
- `BuildPhaseChanged(stackId, phase)` - Build phase changed
- `BuildProgressUpdated(stackId, percent, step)` - Progress updated
- `BuildLogReceived(stackId, logLine)` - New log line
- `BuildCompleted(stackId, success)` - Build completed
- `BuildFailed(stackId, error)` - Build failed

**Server Methods:**
- `SubscribeToBuild(stackId)` - Subscribe to build updates
- `UnsubscribeFromBuild(stackId)` - Unsubscribe from updates

## Architecture

### Project Dependencies
```
Api → Infrastructure → Core
Api → Core
```

### Service Layer
- **IDockerService** - Docker daemon connectivity and container discovery
- **IGitService** - Git executable availability
- **IModuleCatalogService** - Available module catalog for setup flows
- **IStackConfigurationValidator** - Stack configuration validation and conflict detection
- **IStackService** - Stack persistence and DTO mapping
- **IBuildService** - Initial build tracking scaffold, to be expanded into full clone → modules → docker build orchestration

### Data Layer
- **AzerothCoreDbContext** - EF Core database context
- SQLite database for stack configuration and metadata, provisioned on API startup

## Security Notes

⚠️ **IMPORTANT**: This application is designed for local use only.

- Docker socket access grants root-level privileges
- Do not expose the API to the internet
- Run on a dedicated machine or accept security risks
- No authentication in Phase 1 (add if hosting remotely)

## Troubleshooting

### Port Already in Use
```bash
# Kill process using port 5000
lsof -ti:5000 | xargs kill -9
```

### Docker Socket Permission Denied
```bash
# Add user to docker group
sudo usermod -aG docker $USER
# Log out and back in for changes to take effect
```

### Database Locked
```bash
# Remove existing database file
rm backend/AzerothCoreManager.Api/azerothcore-manager.db*
# Database will be recreated on next run
```

## License

[Your License Here]

## Support

For issues and questions, please create a GitHub issue.
