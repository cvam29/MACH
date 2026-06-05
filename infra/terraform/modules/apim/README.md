# apim

API Management gateway fronting the BFF. Ships a sample API (`/api`) with one operation and
an API-level policy: **rate-limit**, **CORS** (credentials allowed for the storefront origin),
and **session presence validation** (state-changing requests must carry the `mach_session`
httpOnly cookie or an `Authorization` header). Named values are sourced from Key Vault via
APIM's system-assigned identity, so no secret text lives in APIM config or state.

## Inputs (selected)
| Name | Type | Default | Description |
|---|---|---|---|
| `name` | string | — | APIM service name (global). |
| `resource_group_name` / `location` | string | — | RG / region. |
| `publisher_name` / `publisher_email` | string | demo values | Portal publisher info. |
| `sku_name` | string | `Developer_1` | SKU (or `Consumption_0`). |
| `bff_backend_url` | string | — | BFF Function App base URL. |
| `key_vault_named_values` | map(string) | `{}` | named-value → KV secret URI. |
| `rate_limit_calls` / `rate_limit_period_seconds` | number | `300` / `60` | Rate-limit. |
| `key_vault_id` | string | `null` | Grant APIM Secrets User. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `id` / `name` / `gateway_url` | APIM identifiers. |
| `principal_id` | APIM identity principal. |
| `bff_api_id` | Sample API ID. |
