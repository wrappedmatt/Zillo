# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LemonadeApp is a loyalty management platform with card-present payments via Stripe Terminal. It consists of:
- **Dashboard App**: Admin portal for businesses to manage customers, transactions, and terminals
- **Rewards App**: Customer-facing portal for checking rewards balance
- **Terminal App**: Android app for Stripe S700 smart readers (point-of-sale)
- **Shared Core**: Domain, Application, and Infrastructure layers (Onion Architecture)
- **Database & Auth**: Supabase (PostgreSQL + Auth)
- **Payments**: Stripe Terminal for card-present transactions

### Domain Model

- **Accounts**: Business accounts linked to Supabase Auth
- **Customers**: Loyalty program members belonging to accounts
- **Transactions**: Point/cashback transactions (earn/redeem)
- **Terminals**: Stripe S700 terminals paired to accounts
- **Payments**: Stripe payment records
- **Cards**: Customer payment cards
- **UnclaimedTransactions**: Transactions awaiting customer linkage

## Development Commands

### Running the Applications

**Dashboard (admin portal)** - https://localhost:7024 (frontend on port 57195):
```bash
dotnet run --project LemonadeApp.Dashboard.Server
```

**Rewards (customer portal)** - https://localhost:7283 (frontend on port 57196):
```bash
dotnet run --project LemonadeApp.Rewards.Server
```

**Build entire solution**:
```bash
dotnet build
```

**Frontend lint** (run from client directory):
```bash
npm run lint
```

**Android Terminal App**:
```bash
cd LemonadeTerminalApp
./gradlew assembleDebug
```

## Architecture

### Project Structure

```
LemonadeApp/
├── LemonadeApp.Domain/              # Core entities and repository interfaces
├── LemonadeApp.Application/         # Service interfaces and DTOs
├── LemonadeApp.Infrastructure/      # Repository and service implementations (Supabase)
├── LemonadeApp.Dashboard.Server/    # Admin API + SPA hosting
├── lemonadeapp.dashboard.client/    # Admin React frontend (shadcn/ui, Tailwind CSS v4)
├── LemonadeApp.Rewards.Server/      # Customer rewards API + SPA hosting
├── lemonadeapp.rewards.client/      # Customer React frontend (minimal)
├── LemonadeTerminalApp/             # Android app for Stripe S700 (Kotlin)
└── database/                        # Supabase schema and migrations
```

### Onion Architecture Layers

**Domain** (`LemonadeApp.Domain`) - Pure C#, no dependencies:
- Entities: `Account`, `Customer`, `Transaction`, `Terminal`, `Payment`, `Card`, `UnclaimedTransaction`
- Repository interfaces in `Interfaces/`

**Application** (`LemonadeApp.Application`) - Depends on Domain:
- Service interfaces: `IAuthService`, `ICustomerService`, `ITransactionService`, `IPaymentService`, `ITerminalService`, `ICustomerPortalService`
- DTOs in `DTOs/`

**Infrastructure** (`LemonadeApp.Infrastructure`) - Depends on Domain + Application:
- Supabase repository implementations
- Service implementations

**Server** (Dashboard/Rewards) - Depends on all layers:
- API Controllers
- Middleware (terminal auth)
- DI configuration in `Program.cs`

### Key Integrations

**Supabase** - Database and Auth:
- Backend config: `appsettings.json` under `"Supabase"` section
- Frontend client: `src/lib/supabase.js`

**Stripe** - Payment processing:
- Backend config: `appsettings.json` under `"Stripe"` section
- Terminal SDK for S700 smart readers

### Terminal Authentication

Both servers have middleware for terminal API key authentication (`TerminalAuthMiddleware`). Applied to payment and terminal endpoints that require authenticated terminal access.

### Frontend Routes

**Dashboard Client** (`lemonadeapp.dashboard.client`):
- `/signin`, `/signup` - Authentication
- `/dashboard` - Main overview
- `/customers`, `/customers/:customerId` - Customer management
- `/transactions` - Transaction history
- `/terminals` - Terminal management
- `/settings`, `/reporting` - Account settings and reports
- `/signup/:slug` - Public customer signup
- `/portal/:token` - Customer portal access

**Rewards Client** (`lemonadeapp.rewards.client`):
- `/:slug` - Customer rewards lookup by account slug

## Adding New Features

### Backend (Following Onion Architecture)

1. **Domain Layer**: Add entity to `Entities/`, interface to `Interfaces/`
2. **Application Layer**: Add DTOs to `DTOs/`, service interface to `Services/`
3. **Infrastructure Layer**: Implement repository and service
4. **Server Layer**: Add controller, register in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IRepository, Repository>();
   builder.Services.AddScoped<IService, Service>();
   ```

### Frontend (shadcn/ui)

Add new shadcn/ui components:
```bash
cd lemonadeapp.dashboard.client
npx shadcn@latest add [component]
```

Use `@/` path alias and `cn()` utility for conditional classes.

## Android Terminal App

Located in `LemonadeTerminalApp/`. Uses Kotlin with Stripe Terminal SDK.

**Setup**: Configure `local.properties` with `BACKEND_URL` pointing to Dashboard server (use ngrok for physical devices).

**Build**:
```bash
./gradlew assembleDebug
```

See `LemonadeTerminalApp/README.md` and `BACKEND_INTEGRATION.md` for detailed setup.
