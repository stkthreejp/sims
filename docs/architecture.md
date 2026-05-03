# Architecture

## Overview

SIMS uses a clean four-layer architecture:

```
SIMS.API           → REST API, controllers, middleware
SIMS.Application   → Business logic, services, DTOs
SIMS.Infrastructure → EF Core, migrations, external services, background workers
SIMS.Domain        → Entities, value objects, enums
```

Dependencies flow inward only: API → Application → Infrastructure → Domain.

## Project Structure

```
SIMS/
├── backend/src/
│   ├── SIMS.API/               # ASP.NET Core Web API (port 5000)
│   │   ├── Controllers/        # 43 controllers organized by domain
│   │   │   ├── Billing/        # 11 billing/accounting controllers
│   │   │   └── Admin/          # 6 admin controllers
│   │   ├── Middleware/         # ExceptionHandlingMiddleware
│   │   └── Program.cs          # Startup, DI registration
│   ├── SIMS.Application/       # Business logic
│   │   ├── Services/           # 36 service implementations
│   │   ├── Interfaces/Services/ # Service interfaces (42 total)
│   │   ├── DTOs/               # Data transfer objects by domain
│   │   └── Common/             # Result<T>, PagedResult<T>, QueryParameters
│   ├── SIMS.Infrastructure/    # Data access and external integrations
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── Configurations/ # 62 EF entity configurations
│   │   ├── Migrations/         # 56 EF Core migrations
│   │   ├── Services/           # External service implementations
│   │   ├── Workers/            # 4 background hosted services
│   │   └── Extensions/         # DI setup, Key Vault config
│   └── SIMS.Domain/            # Pure domain layer
│       ├── Entities/           # 73 domain entities
│       └── Enums/              # 18 enumerations
├── frontend/src/
│   ├── pages/                  # 36 page components
│   ├── api/                    # 26 API modules + Axios client
│   ├── components/             # Shared UI components
│   ├── hooks/                  # Custom React hooks
│   ├── store/                  # Zustand state management
│   └── types/                  # TypeScript type definitions
├── docker-compose.yml          # PostgreSQL, API, Frontend services
└── docs/                       # This documentation
```

## Key Patterns

### Soft Delete
All entities inherit from `BaseEntity` which includes `IsDeleted` and `DeletedAt`. EF Core global query filters automatically exclude deleted records from all queries.

### Result Pattern
Services return `Result<T>` — a discriminated union of success/failure. Controllers unwrap results into appropriate HTTP responses.

### Pagination
List endpoints use `QueryParameters` (page, pageSize, sort, filter) and return `PagedResult<T>`.

### Background Workers
Four `IHostedService` implementations run on background threads:
- `EmailIngestionWorker` — Polls Microsoft Graph API for new emails
- `TaskNotificationWorker` — Sends task due/overdue notifications
- `TaskEscalationWorker` — Escalates overdue tasks per escalation rules
- `QboSyncRetryWorker` — Retries failed QuickBooks Online sync operations

## Authentication Flow

1. User logs in via Microsoft Azure AD (MSAL in frontend)
2. `AuthController` validates the Microsoft token and issues a short-lived JWT (15 min) + refresh token (7 days)
3. Frontend stores tokens and attaches JWT as `Authorization: Bearer` header on all requests
4. `ExceptionHandlingMiddleware` handles auth failures globally
