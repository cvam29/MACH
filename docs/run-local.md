# Running MACH locally

The whole demo runs on your machine with **no Azure cost**. SQL is the only durable store; every
cloud dependency is either an emulator (Storage, Service Bus) or has an offline provider
(Maps = haversine stub, Cache = in-memory, Email = `.eml` dev sink). Real vendor sandbox keys
(commercetools, Adyen, Algolia, Contentstack) light up the corresponding features when supplied,
but the storefront browses, carts, and quotes delivery without them.

## TL;DR

```powershell
./run.ps1
```

That single command starts the Docker dependencies, applies the database migrations, launches all
seven Function hosts (each in its own window) and the Next.js storefront, then prints the URLs.
Tear it all down with `./run.ps1 -Stop`.

## Prerequisites

| Tool | Why | Install |
|---|---|---|
| .NET 10 SDK | builds/runs the hosts | already required by the repo |
| Azure Functions Core Tools v4 | `func start` per host | `npm i -g azure-functions-core-tools@4` |
| Docker Desktop | Azurite + SQL Server + Service Bus emulator | <https://docs.docker.com/desktop/> |
| Node 24+ / npm 11+ | storefront + seed scripts | <https://nodejs.org> |
| `dotnet-ef` | applies EF Core migrations | `dotnet tool install -g dotnet-ef` |

> **License note.** `run.ps1` sets `ACCEPT_EULA=Y`, acknowledging the Microsoft Software License
> Terms for the [Service Bus emulator](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt)
> and [SQL Server Linux](https://learn.microsoft.com/sql/linux/sql-server-linux-docker-container-deployment).
> Only the Service Bus emulator path uses these; `-Offline` does not.

## What `run.ps1` does

1. **Docker dependencies** (`docker-compose.yml`)
   - `mach-azurite` — Azure Storage emulator → `AzureWebJobsStorage=UseDevelopmentStorage=true`.
   - `mach-mssql` — SQL Server 2022, ports `1433`. Backs the Service Bus emulator **and** hosts the
     app database `MachDb` (one container, two jobs).
   - `mach-servicebus` — Service Bus emulator on `amqp://localhost:5672`, health on `:5300`. Topics
     and subscriptions come from [`local/servicebus/config.json`](../local/servicebus/config.json).
2. **Migrations** — `dotnet ef database update --project src/Mach.Persistence` against `MachDb`.
3. **Host settings** — merges the local-infra connection strings (Azurite, the emulator, the SQL
   container) into each host's gitignored `local.settings.json`, **preserving any vendor secrets you
   already added**.
4. **Hosts** — `func start --port <port>` for each, in its own titled window.
5. **Storefront** — `npm install` (first run) then `npm run dev`.

## Ports

| Service | URL | Kind |
|---|---|---|
| Storefront (Next.js) | <http://localhost:3000> | UI |
| Auth API | <http://localhost:7070/api/auth> | HTTP |
| BFF API | <http://localhost:7071/api> | HTTP |
| Webhooks | `http://localhost:7072/api/hooks/{adyen\|commercetools\|contentstack}` | HTTP |
| Projection worker | port 7073 | Service Bus trigger (`payments` → `projection`) |
| Indexer worker | port 7074 | Service Bus (`catalog` → `indexer`, `content` → `indexer-content`) + nightly Timer |
| Notifications worker | port 7075 | Service Bus trigger (`notifications` → `notifications`) |
| Outbox worker | port 7076 | Timer (every 10s) → publishes queued events |
| SQL Server | `localhost,1433` (`sa` / `Mach_local_Dev123!`) | container |
| Service Bus emulator | `amqp://localhost:5672`, health `:5300` | container |

## Provider switches

Defaults run fully offline. Set these in a host's `local.settings.json` `Values` (or as environment
variables, using `__` for the `:`) to switch to real services:

| Key | Default (offline) | Real |
|---|---|---|
| `Messaging:Provider` | `InMemory` (`-Offline`) / `ServiceBus` (emulator) | `ServiceBus` + `Messaging:ConnectionString` |
| `Cache:Provider` | `InMemory` | `Redis` + `Cache:ConnectionString` |
| `Maps:Provider` | `Stub` (haversine over seeded store coords) | `Azure` + `Maps:SubscriptionKey` |
| `Email:Provider` | `DevSink` → writes `.eml` to `./mail` | `Acs` + `Email:AcsConnectionString` |

> **In-memory bus is per-process.** Cross-host async fan-out (BFF/Webhooks → Projection / Indexer /
> Notifications) only works over the Service Bus emulator. That is why `-Offline` starts just the
> synchronous hosts (Auth + BFF) plus the storefront.

## Vendor keys (optional)

Follow [vendor-setup.md](./vendor-setup.md) to create the four sandbox accounts, then add the keys to
the relevant host's `local.settings.json` and the storefront's `.env.local` (see
`apps/storefront/.env.example`). Without them the offline providers keep the demo fully browsable.

## Seeding data

With commercetools / Algolia / Contentstack keys in `seed/.env` (copy `seed/.env.example`):

```powershell
cd seed
npm install
npm run seed:commercetools   # products, categories, shipping methods
npm run seed:sql             # Stores + Suppliers (drives distance quoting + recipients)
npm run seed:algolia         # search index + facets
npm run seed:contentstack    # content types + per-audience email templates
# or: npm run seed:all
```

`seed:sql` targets the same `MachDb`; set `SQL_CONNECTION_STRING` if you are not on the container default.

### Firing the payment chain offline

Without real Adyen, replay a signed notification so Webhooks → Projection → Notifications fires:

```powershell
cd seed
$env:ADYEN_HMAC_KEY = "<hexKey>"   # must match Adyen:HmacKey on the Webhooks host
npm run replay:adyen -- --order <orderId> --post   # omit --post for a dry run
```

Generate a dev key with `node -e "console.log(require('crypto').randomBytes(32).toString('hex').toUpperCase())"`
and set the same value as `Adyen:HmacKey` in `src/Mach.Webhooks.Functions/local.settings.json`.

## Common tasks

```powershell
./run.ps1 -Offline           # sync browse path only, no Docker
./run.ps1 -SkipInfra         # dependencies already running
./run.ps1 -SkipMigrations    # DB already migrated
./run.ps1 -Stop              # docker compose down + stop hosts
docker compose logs servicebus   # emulator logs if a worker can't connect
```

## Troubleshooting

- **A worker logs "ServiceBusConnection not found" / can't connect** — the emulator wasn't healthy
  before the host started. Re-run `./run.ps1 -SkipMigrations`, or check `docker compose logs servicebus`.
- **`dotnet ef` not found** — `dotnet tool install -g dotnet-ef`.
- **SQL login failures** — the container password must match `Mach_local_Dev123!`; if you changed
  `MSSQL_SA_PASSWORD`, update it in both `docker-compose.yml` and `run.ps1`.
- **No `.eml` files after checkout** — confirm `Email:Provider=DevSink` and look in `./mail`.
