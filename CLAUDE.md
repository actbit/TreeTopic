# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

### Development Environment (Recommended)
Use .NET Aspire to orchestrate all services:
```bash
# Install dependencies
dotnet restore

# Start all services (PostgreSQL, Keycloak, App)
dotnet run --project TreeTopic.AppHost -- --parameter keycloak-admin-password=admin123

# Access services:
# - TreeTopic App: https://localhost:5001
# - Aspire Dashboard: http://localhost:19629 (from console output)
# - Keycloak Admin: http://localhost:8080 / admin / admin123
```

### Frontend Only
```bash
cd TreeTopic/TreeTopic.Client
npm install
npm run dev          # Development server with hot reload
npm run build        # Production build
npm run check        # TypeScript checking
```

### Backend Only
```bash
cd TreeTopic
dotnet run          # Runs with ASP.NET Core
```

### Database Operations
```bash
# Run migrations
dotnet ef database update --context TenantCatalogDbContext

# Create new migration
dotnet ef migrations add MigrationName --context TenantCatalogDbContext

# Reset database (development only)
dotnet ef database drop --context TenantCatalogDbContext --force
```

## Architecture Overview

### Technology Stack
- **Backend**: ASP.NET Core 10.0 with C#
- **Frontend**: SvelteKit 5.45.6 with TypeScript
- **Database**: PostgreSQL with Entity Framework Core
- **Authentication**: OpenID Connect with Keycloak
- **Real-time**: SignalR for messaging
- **Deployment**: Docker with Linux containers

### Multi-Tenant Architecture
The application is designed for multi-tenancy with:
- Tenant isolation using URL routing (`/{tenant}/...`)
- Separate databases for tenant and shared data
- Finbuckle.MultiTenant for tenant management

Key tenant-related files:
- `TreeTopic/Models/Tenant.cs` - Tenant entity
- `TreeTopic/Services/TenantService.cs` - Tenant management logic
- `TreeTopic.AppHost/AppHost.cs` - Service orchestration

### Frontend Structure
- **Components** (`src/lib/components/`): Reusable UI organized by feature
  - `topics/` - Hierarchical topic management
  - `messages/` - Real-time messaging components
  - `brainstorming/` - Interactive brainstorming boards
  - `documents/` - PDF.js integration for document viewing
  - `permissions/` - Access control components

- **Pages** (`src/lib/pages/`): Page-specific components
- **Routes** (`src/routes/`): SvelteKit routing configuration

### Backend Structure
- **Controllers**: API endpoints in `TreeTopic/Controllers/`
- **Models**: Data entities in `TreeTopic/Models/`
- **Services**: Business logic in `TreeTopic/Services/`
- **Program.cs**: Application startup and configuration

### Key Features
1. **Topic Management**: Hierarchical topic organization with nested structure
2. **Real-time Messaging**: SignalR-based instant messaging
3. **Brainstorming**: Interactive boards with voting and idea management
4. **Document Processing**: PDF viewing with PDF.js
5. **File Management**: Upload, preview, and version control
6. **Push Notifications**: Web Push API integration
7. **Permissions**: Role-based access control

### Development Workflow
1. Use Aspire for development to automatically manage dependencies
2. Frontend changes trigger hot reload during development
3. Backend changes require app restart (auto-reload enabled)
4. Database migrations are managed through EF Core CLI

### Authentication Flow
1. User authenticates via Keycloak (OpenID Connect)
2. JWT tokens are used for API authentication
3. Tenant context is established from URL or subdomain
4. Claims-based authorization for permissions

### Configuration
- Development settings in `appsettings.Development.json`
- Production settings via environment variables
- Aspire configuration in `TreeTopic.AppHost/AppHost.cs`

### Important Notes
- The application uses .NET 10.0 (latest version)
- Frontend is built with Vite and outputs to `wwwroot`
- PostgreSQL requires Docker for development (managed by Aspire)
- Keycloak is auto-provisioned in development mode
- Tenant registration requires manual initialization via API calls