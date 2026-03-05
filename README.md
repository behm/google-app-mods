# Google App Mods

A .NET 10 [Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) application that automates personal Google account housekeeping — archiving Gmail, cleaning up YouTube playlists, and more — all orchestrated from a single solution with centralized OAuth2 authentication.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    GoogleAppMods.AppHost                        │
│              (Aspire orchestrator — wires everything)           │
└────┬──────────────┬──────────────┬──────────────┬───────────────┘
     │              │              │              │
     ▼              ▼              ▼              ▼
┌─────────┐  ┌───────────┐  ┌───────────┐  ┌────────────────────┐
│  Redis  │  │  Server   │  │  Gmail    │  │  YouTube           │
│  (cache)│  │  (API +   │  │  Sweeper  │  │  WatchThese        │
│         │  │   Auth)   │  │  (Worker) │  │  Cleanup (Worker)  │
└─────────┘  └─────┬─────┘  └─────┬─────┘  └─────────┬──────────┘
                   │              │                  │
                   │         ┌────▼──────────────────▼───┐
                   │         │   GoogleAppMods.Google    │
                   └────────►│   (shared library)        │
                             │   • GoogleTokenProvider   │
                             │   • GoogleScopes          │
                             │   • GoogleProjectOptions  │
                             └──────────┬────────────────┘
                                        │
                                        ▼
                              Shared token store (disk)
```

[Google Authorization Documentation](docs/auth.md)

## Projects

### GoogleAppMods.AppHost

The Aspire host that orchestrates all services. It reads configuration from `appsettings.json` (and user secrets) and distributes it to each project as environment variables using `ResourceBuilderExtensions`.

### GoogleAppMods.Server

ASP.NET Core API backend. Serves the Vite frontend as static files and exposes:

| Endpoint | Method | Description |
|---|---|---|
| `/api/auth/google/status` | GET | Check if a valid OAuth token exists |
| `/api/auth/google/authorize` | GET | Redirect to Google consent screen (requests all scopes) |
| `/api/auth/google/callback` | GET | OAuth2 callback — exchanges the code for a token and stores it |
| `/api/auth/google/revoke` | POST | Delete the stored token |

This is the **only** place where OAuth2 consent happens, making the solution safe to run in Docker or any headless environment.

### GoogleAppMods.Google (shared library)

Contains everything the worker services need to authenticate with Google APIs without ever opening a browser:

- **`GoogleProjectOptions`** — Shared configuration (Client ID, Client Secret, Token Store Path).
- **`GoogleScopes`** — Single registry of all OAuth2 scopes. Add new Google product scopes here and re-authorize once via the web UI.
- **`GoogleTokenProvider`** — Reads the stored OAuth2 token from disk, automatically refreshes it if stale, and throws a clear error if no token exists yet.

### GoogleAppMods.GmailSweeper (Worker Service)

A background service that archives Gmail messages on a cron schedule. It runs one or more configurable Gmail queries and removes the `INBOX` label from all matching messages.

**Key classes:**
- `Worker` — `BackgroundService` that sleeps until the next cron occurrence, then triggers a sweep.
- `GmailArchiveService` — Executes each query, pages through all results, and archives in batches via Gmail's `batchModify` API.
- `GmailSweeperOptions` — Configuration: cron schedule, list of queries, batch size.

### GoogleAppMods.YouTubeWatchTheseCleanup (Worker Service)

Placeholder worker service for upcoming YouTube playlist automation. Already references the shared `GoogleAppMods.Google` library and receives `GoogleProject` configuration from the AppHost.

### frontend (Vite)

A Vite-based web frontend served by `GoogleAppMods.Server` as static files.

## Configuration

### Google Cloud Console setup

1. Create a project in the [Google Cloud Console](https://console.cloud.google.com/).
2. Enable the **Gmail API** and **YouTube Data API v3**.
3. Create **OAuth 2.0 Client ID** credentials (Web application type).
4. Add `https://localhost:{port}/api/auth/google/callback` as an authorized redirect URI.

### User Secrets

Store your Client ID and Client Secret in the AppHost project's user secrets:

```bash
cd GoogleAppMods.AppHost
dotnet user-secrets set "GoogleProject:ClientId" "your-client-id"
dotnet user-secrets set "GoogleProject:ClientSecret" "your-client-secret"
```

### appsettings.json

The AppHost `appsettings.json` contains the non-secret configuration:

```json
{
  "GoogleProject": {
    "ClientId": "configure-in-user-secrets",
    "ClientSecret": "configure-in-user-secrets",
    "TokenStorePath": "G App Mods/tokens"
  },
  "GmailSweeper": {
    "Schedule": "0 12 * * *",
    "Queries": [
      "in:inbox category:promotions -is:pinned -is:starred older_than:14d"
    ],
    "BatchSize": 100
  }
}
```

| Setting | Description |
|---|---|
| `GoogleProject:TokenStorePath` | Relative path under `AppData` where OAuth tokens are stored. Shared by all services. |
| `GmailSweeper:Schedule` | Cron expression (UTC). `0 12 * * *` = daily at noon. |
| `GmailSweeper:Queries` | Array of [Gmail search queries](https://support.google.com/mail/answer/7190). |
| `GmailSweeper:BatchSize` | Number of messages to archive per Gmail API batch request (max 1000). |

### Cron schedule examples

| Expression | Meaning |
|---|---|
| `0 6 * * *` | Daily at 6:00 AM UTC |
| `0 6 * * 1` | Every Monday at 6:00 AM UTC |
| `0 */6 * * *` | Every 6 hours |
| `0 8 1 * *` | 1st of each month at 8:00 AM UTC |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the Vite frontend)
- [Docker](https://www.docker.com/) (for Redis, or a local Redis instance)

### Run

```bash
# Start the Aspire AppHost (launches all services + Redis + frontend)
cd GoogleAppMods.AppHost
dotnet run
```

Open the Aspire dashboard (printed in the console output), navigate to the Server's URL, then visit `/api/auth/google/authorize` to complete the one-time Google OAuth2 consent. After that, all worker services can access Google APIs automatically.

## Deployment

For production deployment using Docker containers on Synology NAS or other Docker hosts, see the [Deployment Guide](docs/deployment.md).

The application can be deployed as Docker containers with automated builds via GitHub Actions:
- **GitHub Container Registry**: Automatic image builds and publishing
- **Docker Compose**: Simple deployment configuration for Synology Container Manager
- **Multi-platform**: Supports both AMD64 and ARM64 architectures

See [docs/deployment.md](docs/deployment.md) for complete deployment instructions.

## Adding a New Google Service

1. **Add the scope** to `GoogleAppMods.Google\GoogleScopes.cs`:
   ```csharp
   public static readonly string[] All =
   [
       GmailService.Scope.GmailModify,
       YouTubeService.Scope.Youtube,
       CalendarService.Scope.Calendar, // new
   ];
   ```
2. **Re-authorize** via `/api/auth/google/authorize` to grant the new scope.
3. **Create a worker** (or add to an existing one) that injects `GoogleTokenProvider` to get credentials.
4. **Wire it up** in the AppHost with `.WithGoogleProjectConfig(googleProjectSection)`.

## Tech Stack

- [.NET 10](https://dotnet.microsoft.com/) / C# 14
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) — orchestration, service discovery, health checks, OpenTelemetry
- [Google APIs Client Library for .NET](https://github.com/googleapis/google-api-dotnet-client) — Gmail, YouTube
- [Cronos](https://github.com/HangfireIO/Cronos) — cron expression parsing
- [Redis](https://redis.io/) — output caching
- [Vite](https://vite.dev/) — frontend tooling
