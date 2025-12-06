# Project Rename Instructions

The client folder has been renamed successfully:
✅ `lemonadeapp.client` → `lemonadeapp.dashboard.client`

## Manual Steps Required

The `LemonadeApp.Server` directory is locked and needs to be renamed manually:

### Step 1: Close All IDEs
1. Close Visual Studio
2. Close VS Code
3. Stop any running dotnet processes

### Step 2: Rename the Server Directory
```bash
cd c:\Users\matt\source\repos\LemonadeApp
mv LemonadeApp.Server LemonadeApp.Dashboard.Server
```

Or use Windows Explorer to rename:
- `LemonadeApp.Server` → `LemonadeApp.Dashboard.Server`

### Step 3: Update Solution File
```bash
cd c:\Users\matt\source\repos\LemonadeApp
dotnet sln remove LemonadeApp.Server/LemonadeApp.Server.csproj
dotnet sln add LemonadeApp.Dashboard.Server/LemonadeApp.Server.csproj
```

### Step 4: Rename the .csproj File
Inside `LemonadeApp.Dashboard.Server/`:
- Rename: `LemonadeApp.Server.csproj` → `LemonadeApp.Dashboard.Server.csproj`

### Step 5: Update .csproj Contents
Edit `LemonadeApp.Dashboard.Server/LemonadeApp.Dashboard.Server.csproj`:

Find:
```xml
<SpaRoot>..\lemonadeapp.dashboard.client\</SpaRoot>
```

Replace with:
```xml
<SpaRoot>..\lemonadeapp.dashboard.client\</SpaRoot>
```

### Step 6: Update Launch Settings (if exists)
In `LemonadeApp.Dashboard.Server/Properties/launchSettings.json`, no changes needed (URLs remain the same).

### Step 7: Update VS Code Launch Config (if exists)
In `.vscode/launch.json`:

Find:
```json
"program": "${workspaceFolder}/LemonadeApp.Server/bin/Debug/net8.0/LemonadeApp.Server.dll",
"cwd": "${workspaceFolder}/LemonadeApp.Server",
```

Replace with:
```json
"program": "${workspaceFolder}/LemonadeApp.Dashboard.Server/bin/Debug/net8.0/LemonadeApp.Dashboard.Server.dll",
"cwd": "${workspaceFolder}/LemonadeApp.Dashboard.Server",
```

### Step 8: Clean and Rebuild
```bash
dotnet clean
dotnet build
```

## Final Structure

After renaming, your project structure will be:

```
LemonadeApp/
├── LemonadeApp.Domain/
├── LemonadeApp.Application/
├── LemonadeApp.Infrastructure/
├── LemonadeApp.Dashboard.Server/      ← Renamed from LemonadeApp.Server
├── lemonadeapp.dashboard.client/       ← Renamed from lemonadeapp.client
├── LemonadeApp.Rewards.Server/
└── lemonadeapp.rewards.client/
```

This creates a clear separation:
- **Dashboard**: Merchant management app (accounts, customers, transactions)
- **Rewards**: Customer-facing rewards lookup app
