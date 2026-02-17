# Holiday Monitor – SaaS Subscription Monitor

A dashboard that monitors public holiday APIs and sends notifications when countries are off-work. Built as event-driven microservices with .NET 8 and React.

## Architecture

| Service | Role | API / Stack |
|--------|------|-------------|
| **HolidayScraper.Api** | Polls public holidays | [Nager.Date API](https://date.nager.at) (free, no auth) |
| **UserPref.Api** | User subscriptions | MongoDB |
| **Notifier.Api** | Sends alerts | NServiceBus + SignalR |
| **Dashboard.UI** | React dashboard | Consumes all services |

### Message flow

1. **HolidayScraper** polls Nager.Date every hour; when today is a holiday for a monitored country, it publishes a `PublicHolidayDetected` event (NServiceBus).
2. **UserPref** subscribes to `PublicHolidayDetected`, looks up users interested in that country in MongoDB, and sends a `NotifyUsersCommand` to the Notifier.
3. **Notifier** handles the command and pushes a WebSocket message to the dashboard via SignalR.

Transport: **RabbitMQ**. All three .NET services use **OpenTelemetry** tracing (OTLP).

## Prerequisites

- .NET 8 SDK
- Node.js 18+ (for Dashboard.UI)
- Docker (for RabbitMQ and MongoDB)

## Quick start

### 1. Start infrastructure

```bash
docker-compose up -d
```

This starts:

- **RabbitMQ** on `localhost:5672` (management UI: http://localhost:15672, guest/guest)
- **MongoDB** on `localhost:27017`

### 2. Run the .NET services

From the repo root:

```bash
dotnet run --project src/HolidayScraper.Api
dotnet run --project src/UserPref.Api
dotnet run --project src/Notifier.Api
```

- UserPref API: http://localhost:5001 (Swagger: http://localhost:5001/swagger)
- Notifier (SignalR): http://localhost:5002

### 3. Run the dashboard

```bash
cd src/Dashboard.UI
npm install
npm run dev
```

Open http://localhost:5173. The Vite dev server proxies:

- `/api` → UserPref.Api (5001)
- `/hubs` → Notifier.Api (5002)

### 4. Use the dashboard

1. Set a **User ID** (e.g. `demo-user`). This identifies you for subscriptions and SignalR groups.
2. **Subscribe** to countries (e.g. US, GB, DE). Subscriptions are stored in MongoDB via UserPref.Api.
3. **Live alerts**: When the scraper detects a public holiday for a country you subscribed to, a notification is pushed over SignalR and shown in the “Live holiday alerts” list.

To see alerts without waiting for the next hourly run, temporarily set `PollingIntervalHours` to a small value (e.g. `0.016` ≈ 1 minute) in `HolidayScraper.Api/appsettings.Development.json`, or trigger a run by restarting the scraper on a day that has holidays for the configured countries.

## OpenTelemetry

Each .NET service configures OTLP tracing to `OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://localhost:4317`). To collect traces:

1. Run an OTLP-capable collector (e.g. [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)) or a backend like Jaeger.
2. Optionally uncomment the `otel-collector` service in `docker-compose.yml` and use the provided `otel-collector-config.yaml` (export to logging by default).

## Configuration

- **HolidayScraper.Api**: `ConnectionStrings:RabbitMQ`, `PollingIntervalHours`, `CountriesToMonitor`.
- **UserPref.Api**: `ConnectionStrings:RabbitMQ`, `ConnectionStrings:MongoDB`.
- **Notifier.Api**: `ConnectionStrings:RabbitMQ`, `Cors:Origins`.
- **Dashboard.UI**: `VITE_API_BASE` and `VITE_HUB_BASE` (default: use Vite proxy).

## Solution layout

```
HolidayMonitor/
├── HolidayMonitor.sln
├── docker-compose.yml
├── otel-collector-config.yaml
├── README.md
└── src/
    ├── HolidayMonitor.Contracts/   # NServiceBus events/commands
    ├── HolidayScraper.Api/          # Worker: poll Nager.Date, publish events
    ├── UserPref.Api/                # Web API + handler: MongoDB, notify command
    ├── Notifier.Api/                # SignalR hub + handler: push to UI
    └── Dashboard.UI/                # React + Tailwind + SignalR client
```

## License

MIT.
