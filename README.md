# AzerothCore Manager

A modern web-based management application for private World of Warcraft servers based on [AzerothCore](https://www.azerothcore.org/).

## Overview

AzerothCore Manager is a comprehensive web application designed to simplify the deployment, configuration, and administration of AzerothCore servers running in Docker containers. Built with React 19 and ASP.NET Core 10, it provides an intuitive guided setup experience and powerful management tools for running private WoW 3.3.5a (Wrath of the Lich King) servers.

### What Can It Do?

**✅ Fully Functional Features:**

- 🧙‍♂️ **6-Step Guided Setup Wizard** - Intuitive interface for deploying new AzerothCore servers with real-time validation
- 🐳 **Docker Stack Management** - Creates and manages isolated AzerothCore Docker Compose deployments
- 🔨 **Automated Builds** - Clones, compiles, and containerizes AzerothCore from source with real-time progress tracking via SignalR
- 📦 **Module System** - Browse, select, and install community modules during setup
- ⚙️ **Module Configuration** - Apply per-module settings (AH Bot, AutoBalance, Playerbots, Transmog) via environment variables
- 🎛️ **Container Lifecycle Control** - Start, stop, and restart server stacks with one click
- 📊 **Real-Time Status Monitoring** - Live container status, health checks, and uptime tracking
- 🔄 **Automatic Update Detection** - Hourly checks for AzerothCore and module updates via Git
- ⬆️ **One-Click Updates** - Update stacks to the latest version and automatically rebuild
- 🔧 **Configuration Management** - Edit stack configurations (database, ports, server settings)
- 💾 **Draft Persistence** - Wizard automatically saves progress to localStorage
- 🏗️ **Multi-Stack Support** - Manage unlimited AzerothCore environments simultaneously
- 🔍 **Build Validation** - Pre-flight checks for port conflicts, name uniqueness, and resource requirements
- 📜 **Build Log Streaming** - Real-time build logs with SignalR streaming in dedicated progress page
- 📋 **Container Log Viewing** - Real-time log streaming with filtering, search, and auto-scrolling
- 👥 **Account Management** - Create accounts, set GM levels, ban/unban users, reset passwords, and delete accounts via SOAP
- 🧑‍🤝‍🧑 **Character Management** - View all characters, kick players, ban/unban, mute/unmute, revive, set level, rename, customize, send messages/items/money, add items, and view inventory
- 🤖 **AH Bot Setup** - One-click creation of the Auction House Bot account with Alliance and Horde characters

**📋 Planned:**
- Advanced configuration editor (300+ worldserver.conf settings)
- Server-wide announcements
- Performance metrics and analytics

## About AzerothCore

[AzerothCore](https://github.com/azerothcore/azerothcore-wotlk) is a production-ready, community-driven MMORPG server emulator that provides:

- **Stability**: Rigorous CI/CD processes ensure all changes are tested before merging
- **Blizzlike Content**: High-quality, authentic game mechanics faithful to the original WoW experience
- **Modular Architecture**: Extensible design allowing custom features and content through modules
- **Active Community**: Collaborative development with contributors worldwide
- **Open Source**: Released under GNU GPLv2 license

AzerothCore is built on the solid foundation of MaNGOS, TrinityCore, and SunwellCore, refined through years of development and real-world production use.

## Architecture

AzerothCore Manager runs as a containerized web application and communicates with the Docker daemon via socket sharing to create and manage AzerothCore server stacks.

### Technology Stack

**Frontend:**
- React 19 with TypeScript
- Vite (build tool)
- TailwindCSS v4 (styling)
- React Router v7 (routing)
- React Query (server state management)
- SignalR client (real-time WebSocket updates)
- React Hook Form + Zod (form validation)
- Axios (HTTP client)

**Backend:**
- ASP.NET Core 10 Web API
- Entity Framework Core with SQLite (manager metadata)
- Docker.DotNet (Docker API client)
- SignalR (real-time communication)
- Serilog (structured logging)

**Infrastructure:**
- Docker & Docker Compose
- MySQL 8.4 (for AzerothCore databases)
- Docker socket sharing (`/var/run/docker.sock`)

### How It Works

1. **Manager Container** runs the web application (React frontend + .NET backend)
2. **Docker Socket Access** enables the manager to control the host Docker daemon
3. **Wizard Flow** collects configuration (6 steps: server type, database, ports, modules, advanced settings, review)
4. **Build Orchestration** clones AzerothCore, installs modules, generates docker-compose, and builds images
5. **Real-Time Updates** stream build progress and container status via SignalR WebSockets
6. **Stack Management** provides lifecycle control (start/stop/restart) and configuration editing
7. **Update Tracking** checks Git repositories hourly for new commits and notifies users

### Docker-in-Docker Architecture

The manager uses **Docker-in-Docker volume mounting** to enable nested containers:
- Manager runs at `/app/data` (container path)
- Host data at `/home/user/Projects/AzerothCoreManager/data` (host path)
- Volume mounts in generated docker-compose files use **host paths** for compatibility
- Path translation via `Docker__HostDataPath` configuration setting

## Project Status

**Current Version**: v0.2.0

The core functionality is **working and tested**:
- ✅ Stack creation wizard with validation
- ✅ Full build pipeline (clone, compile, containerize)
- ✅ Stack lifecycle management (start, stop, restart)
- ✅ Real-time container monitoring
- ✅ Update detection and one-click updates
- ✅ Module installation and configuration system
- ✅ Configuration editing
- ✅ Build log streaming UI with real-time SignalR updates
- ✅ Container log viewing with real-time updates
- ✅ Account management (SOAP-based)
- ✅ Character management (kick, ban, mute, revive, set level, items, money, inventory)
- ✅ AH Bot account and character setup

**Known Limitations:**
- Server-wide announcements not yet implemented
- No database backup/restore yet (planned)
- No performance metrics dashboard yet (planned)

## Requirements

- **Docker** 20.10+ with Docker Compose v2
- **Host OS**: Linux (Fedora 44 tested), macOS, or Windows with WSL2
- **Memory**: Minimum 8GB RAM (16GB+ recommended for building and running servers)
- **Storage**: 20GB+ free disk space per AzerothCore instance (build artifacts are large)
- **Network**: Internet connection for cloning repositories and downloading dependencies

## Quick Start

### Using Docker Compose (Recommended)

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/AzerothCoreManager.git
   cd AzerothCoreManager
   ```

2. **Configure environment:**
   ```bash
   cp .env.example .env
   # Edit .env and set HOST_DATA_PATH to your absolute host path
   # Example: HOST_DATA_PATH=/home/youruser/Projects/AzerothCoreManager/data
   ```

3. **Start the manager:**
   ```bash
   docker compose up -d --build
   ```

4. **Access the web interface:**
   Open http://localhost:8080 in your browser

5. **Create your first stack:**
   - Click "Create New Stack"
   - Follow the 6-step wizard
   - Wait for build to complete (~15-30 minutes)
   - Start your server!

### Important: Docker-in-Docker Configuration

The manager requires **host filesystem paths** for Docker-in-Docker volume mounting. You MUST:

1. Set `HOST_DATA_PATH` in `.env` to your **absolute host path**:
   ```
   HOST_DATA_PATH=/home/youruser/Projects/AzerothCoreManager/data
   ```

2. This path must match where your data directory is mounted on the host

See [DOCKER.md](./DOCKER.md) for detailed setup instructions and troubleshooting.

## Development Setup

### Prerequisites

- .NET SDK 10.0.100+ (specified in `global.json`)
- Node.js 18+ and npm
- Docker & Docker Compose
- Git

### Backend Development

```bash
cd backend

# Restore dependencies
dotnet restore

# Run API with hot reload
dotnet watch --project AzerothCoreManager.Api

# Run tests (when implemented)
dotnet test

# Create EF Core migration
dotnet ef migrations add MigrationName \
  --project AzerothCoreManager.Infrastructure \
  --startup-project AzerothCoreManager.Api
```

Backend runs on:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger: http://localhost:5000/swagger

### Frontend Development

```bash
cd frontend

# Install dependencies
npm install

# Run dev server with hot reload
npm run dev

# Build for production
npm run build

# Lint
npm run lint
```

Frontend dev server runs on http://localhost:5173 with API proxy to backend.

### Project Structure

```
├── backend/
│   ├── AzerothCoreManager.Api/              # Web API + Controllers + SignalR Hubs
│   ├── AzerothCoreManager.Core/             # Domain contracts, DTOs, interfaces
│   └── AzerothCoreManager.Infrastructure/   # Services, EF Core, Docker integration
├── frontend/
│   ├── src/
│   │   ├── components/       # Reusable UI components
│   │   ├── pages/            # Page components (Wizard, StackList, StackDetails)
│   │   ├── services/         # API client, SignalR
│   │   ├── hooks/            # Custom React hooks
│   │   ├── types/            # TypeScript definitions
│   │   └── schemas/          # Zod validation schemas
│   └── public/
├── data/                     # Stack data (gitignored)
│   └── stacks/
│       └── {stackId}/
│           └── azerothcore-wotlk/
├── .github/
│   ├── agents/               # Custom Copilot agent specifications
│   └── copilot-instructions.md
└── docker-compose.yml        # Manager container definition
```

### Clean Architecture

The backend follows clean architecture principles:

- **Api Layer**: HTTP endpoints, SignalR hubs, dependency injection
- **Core Layer**: Domain models, DTOs, service interfaces (no dependencies)
- **Infrastructure Layer**: Service implementations, EF Core context, Docker.DotNet integration

Dependencies flow: `Api → Infrastructure → Core` and `Api → Core`

## Usage

### Creating Your First Server

1. **Navigate to the wizard:**
   - Access http://localhost:8080
   - Click "Create New Stack" from the dashboard

2. **Step 1 - Server Configuration:**
   - Enter a unique stack name (e.g., "my-wow-server")
   - Choose server type:
     - **Standard**: Classic AzerothCore (recommended)
     - **Playerbots**: Includes AI-controlled NPCs (experimental)

3. **Step 2 - Database Configuration:**
   - Set MySQL root password (for the stack's database container)
   - Choose database port (default: 3306)
   - Port conflict validation ensures uniqueness

4. **Step 3 - Server Ports:**
   - Auth Server Port (default: 3724) - Client authentication
   - World Server Port (default: 8085) - Game world server
   - SOAP Port (default: 7878) - Admin commands interface

5. **Step 4 - Modules (Optional):**
   - Browse available community modules
   - Select modules to install (e.g., mod-transmog, mod-eluna)
   - Modules are fetched from azerothcore.org catalogue

6. **Step 5 - Advanced Settings:**
   - Max Players (default: 100)
   - Realm Name (default: "AzerothCore")
   - Custom environment variables (optional)

7. **Step 6 - Review & Deploy:**
   - Review all settings
   - Click "Create Stack" to start the build
   - Build progress shows in real-time (~15-30 minutes)
   - Redirects to build page to monitor progress

### Managing Stacks

**Stack List Page:**
- View all your AzerothCore stacks
- See status: Running (green), Stopped (gray), Building (blue), Failed (red)
- Quick actions: Start, Stop, View Details
- Update indicators show when new versions are available

**Stack Details Page:**
- **Lifecycle Controls**: Start, Stop, Restart buttons
- **Container Status**: Real-time status of authserver, worldserver, database
- **Health Checks**: Visual indicators (✓ healthy, ✗ unhealthy, ○ unknown)
- **Uptime Tracking**: Shows how long containers have been running
- **Configuration View**: Database ports, server ports, max players, realm name
- **Update Notifications**: Shows available updates with commit SHAs
- **Actions**:
  - **Check for Updates**: Manually trigger update check (also runs hourly)
  - **Update Stack**: One-click update to latest version (stops → rebuilds → ready to start)
  - **Edit Configuration**: Modify ports, passwords, settings
  - **Rebuild**: Force full rebuild from scratch
  - **Delete**: Remove stack and all data

### Updating Stacks

When AzerothCore or installed modules have new commits:

1. **Notification appears** in stack details: "Updates Available"
2. Shows outdated components: "AzerothCore Server: abc1234 → def5678"
3. Click **"Update Stack"** button
4. Stack automatically stops (if running) and rebuilds with latest code
5. Update clears immediately after build completes (no stale notifications)
6. Start stack to use updated version

### Managing Accounts

The **Accounts** tab in Stack Details provides comprehensive account management:

**Viewing Accounts:**
- View all accounts from the `acore_auth` database
- See account ID, username, GM level, character count, online status, and last login
- Real-time online status indicators
- **BANNED** badge for banned accounts

**Creating Accounts:**
- Click "Create Account" to open the creation dialog
- Enter username and password (auto-generated or custom)
- Account created via SOAP command to worldserver
- Success confirmation displayed

**Setting GM Levels:**
- Select account from the list to open details panel
- Choose GM level from dropdown (0-3):
  - 0 - Player (default)
  - 1 - Moderator
  - 2 - Game Master
  - 3 - Administrator
- Click "Set Level" to apply changes
- Success confirmation displayed

**Banning Accounts:**
- Select account and click "Ban Account"
- Enter ban duration (e.g., "2h", "7d", "permanent")
- Provide ban reason for record keeping
- Banned accounts show red "BANNED" badge
- Ban details displayed in account panel (reason, expiry, banned by)

**Unbanning Accounts:**
- Select banned account (shows "Unban Account" button)
- Confirm unban action
- Ban immediately removed

**Resetting Passwords:**
- Select account and click "Reset Password"
- Enter new password (auto-generated or custom)
- Password changed via SOAP command

**Deleting Accounts:**
- Select account and click "Delete Account"
- Confirm deletion in dialog
- Account permanently removed from database

All account operations require the worldserver to be running and use SOAP authentication with the admin account.

### Monitoring

- **Container Status**: Live updates every 5 seconds when stack is running
- **Health Indicators**: Database, authserver, worldserver health checks
- **Uptime Display**: Shows hours and minutes since containers started
- **Build Progress**: Real-time phase updates during builds (clone, modules, compile, containerize)

### Configuration Tips

- **Port Conflicts**: Manager validates all ports are unique across stacks
- **Database Passwords**: Use strong passwords for production servers
- **Module Selection**: Test modules individually before combining multiple
- **Resource Planning**: Each stack needs ~2-4GB RAM when running
- **Build Times**: First build takes 15-30 minutes (compiles C++ from source)
- **Disk Space**: Each stack uses ~5-10GB (build artifacts, databases, logs)

## Troubleshooting

### "Mounts denied" or "not shared from the host" errors

This is a Docker-in-Docker volume mounting issue. Fix:

1. Ensure `.env` file has `HOST_DATA_PATH` set to your **absolute host path**:
   ```
   HOST_DATA_PATH=/home/youruser/Projects/AzerothCoreManager/data
   ```

2. Restart manager container:
   ```bash
   docker compose down
   docker compose up -d
   ```

### Port already in use

If the manager won't start:

```bash
# Check what's using port 8080
lsof -ti:8080 | xargs kill -9

# Or use a different port in docker-compose.yml
ports:
  - "8081:80"  # Use 8081 instead
```

### Stack build fails

Common causes:
- **Out of disk space**: Each build needs ~5-10GB
- **Out of memory**: Need at least 4GB RAM available during compilation
- **Network issues**: Check internet connection for git clones
- **Missing dependencies**: Ensure Docker has access to package repos

Check build logs (coming soon in UI) or inspect container:
```bash
docker logs azerothcore-manager
```

### Stack won't start

1. Check container logs:
   ```bash
   docker logs acore-{stackId}-worldserver
   docker logs acore-{stackId}-authserver
   docker logs acore-{stackId}-database
   ```

2. Common issues:
   - Database not ready yet (wait for db-import to complete)
   - Port conflicts (change ports in Edit Configuration)
   - Missing client data (db-import failed)

### Update notification won't clear

This was a bug fixed in v0.1.0. If you see updates available for identical SHAs after upgrading to v0.2.0:

1. Rebuild manager container:
   ```bash
   docker compose up -d --build
   ```

2. Update flags now clear immediately after rebuilds

## Resources

### AzerothCore
- [AzerothCore Website](https://www.azerothcore.org/)
- [AzerothCore GitHub](https://github.com/azerothcore/azerothcore-wotlk)
- [AzerothCore Wiki](https://www.azerothcore.org/wiki)
- [AzerothCore Discord](https://discord.gg/gkt4y2x)
- [Module Catalogue](https://www.azerothcore.org/catalogue.html)

### Technologies
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [React Documentation](https://react.dev)
- [Docker.DotNet GitHub](https://github.com/dotnet/Docker.DotNet)
- [SignalR Documentation](https://docs.microsoft.com/aspnet/core/signalr)

## Contributing

Contributions are welcome! This project is actively developed and accepting PRs.

### Development Guidelines

- Follow .NET and React best practices
- Use the existing code patterns (Clean Architecture, React Query, etc.)
- Write descriptive commit messages (Conventional Commits format)
- Update documentation when changing APIs or features
- Test your changes thoroughly
- See `.github/copilot-instructions.md` for detailed coding standards

### Areas for Contribution

**High Priority:**
- Server-wide announcements
- Advanced worldserver.conf editor
- Database backup/restore functionality

**Medium Priority:**
- Performance metrics dashboard
- Automated testing (unit, integration, E2E)
- Docker image optimization (reduce size)
- Multi-architecture support (ARM64)

**Nice to Have:**
- MaNGOS server support
- Module rating and reviews
- One-click server cloning
- Scheduled backups
- Email notifications for server issues

### Reporting Issues

When reporting bugs, include:
- Your OS and Docker version
- Manager version (check docker image tag)
- Steps to reproduce
- Relevant logs from `docker logs azerothcore-manager`
- Screenshots if applicable

## Roadmap

**~~v0.2.0 - Player Management~~** ✅ Delivered
- Character management (kick, ban, mute, revive, set level, items, money, inventory)
- Account management (create, GM level, ban, password reset, delete)
- AH Bot account setup

**v0.3.0 - Server Announcements & Advanced Configuration** (Next Release)
- Server-wide announcements

**v0.3.0 - Advanced Configuration**
- worldserver.conf editor (300+ settings)
- Configuration templates
- Preset management

**v0.4.0 - Backup & Recovery**
- Database backup automation
- One-click restore
- Scheduled backups

**v1.0.0 - Production Ready**
- Complete documentation
- Comprehensive testing
- Performance optimizations
- Security hardening

## License

This project is licensed under the GNU GPLv2 license - see the LICENSE file for details.

## Disclaimer

This project is not affiliated with or endorsed by Blizzard Entertainment or World of Warcraft. AzerothCore Manager is intended for educational purposes and private server testing only. The authors do not support or sponsor illegal public servers.

## Acknowledgments

- **AzerothCore Team** - For creating and maintaining an amazing open-source WoW server emulator
- **AzerothCore Community** - For modules, support, and contributions
- **MaNGOS & TrinityCore** - For the foundational code AzerothCore builds upon
- **Docker Community** - For excellent containerization tools
- **WoW Private Server Community** - For keeping the passion alive
