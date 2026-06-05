# Observability

How MACH is instrumented, and the queries/workbook to see it.

## How telemetry flows

`Mach.ServiceDefaults` wires **OpenTelemetry** (traces, metrics, logs) for every Function host and
exports to **Application Insights** via `Azure.Monitor.OpenTelemetry.Exporter` when
`APPLICATIONINSIGHTS_CONNECTION_STRING` is set. With no connection string it falls back to the
**console exporter**, so traces are visible locally with no Azure resource.

- HTTP server spans → `requests`
- Outbound HTTP (vendor calls) and Service Bus → `dependencies`
- Logs → `traces`
- Exceptions → `exceptions`

The **W3C `traceparent`** propagates storefront → BFF → vendor dependency spans → Service Bus and
back; `x-correlation-id` rides on HTTP headers and Service Bus application properties (see the
[checkout sequence](../diagrams/checkout-sequence.md)). One shopper action becomes one distributed
trace — the App Insights **Application Map** fanning out to all four vendors is the Cloud-native
punchline (see [ADR 0010](../adr/0010-opentelemetry-end-to-end-tracing.md)).

### Role names

Every host defaults to `cloud_RoleName == "mach"` because `OTEL_SERVICE_NAME` is unset. To split the
Application Map and queries per service, set it per host — e.g. `OTEL_SERVICE_NAME=mach-bff`,
`mach-projection`, `mach-webhooks`.

> **Scope note.** This build instruments HttpClient + manual Service Bus `Activity` correlation.
> There are **no custom metrics yet** (no `Meter`/`Counter`), so the queries derive webhook latency
> and async-hop visibility from `requests`/`dependencies` rather than `customMetrics`. Adding
> business metrics (reindex lag, cart conversion) is a natural next step.

## Files

| File | What it is |
|---|---|
| [`queries.kql`](./queries.kql) | Seven ready-to-run KQL queries (end-to-end trace, vendor latency, failure rate, webhook latency, async hops, exceptions, request volume). |
| [`workbook.json`](./workbook.json) | An Azure Monitor **Workbook** (gallery template) with a Time-range parameter and tiles for the same signals. |

## Using the queries

In the Azure Portal: **Application Insights → Logs**, paste a query from `queries.kql`, run. For
query #1 (end-to-end trace), copy an `operation_Id` from any request (or the `traceparent`/
`x-correlation-id` returned to the storefront) into the `opId` let-statement.

## Importing the workbook

**Application Insights → Workbooks → New → Advanced Editor (`</>`)** → paste `workbook.json` →
**Apply** → **Save**. The Time-range pill at the top drives every tile (bound via
`timeContextFromParameter`).

To deploy it as code instead, embed the JSON in a `microsoft.insights/workbooks` resource — a
natural extension of the `monitoring` Terraform module.
