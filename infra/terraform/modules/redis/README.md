# redis

Azure Cache for Redis (distributed cache / session + idempotency store for the BFF and
workers). TLS-only: the non-SSL `6379` port is disabled and the minimum TLS version is `1.2`.

The Azure-generated connection string is surfaced into Key Vault (the same pattern the
`maps` module uses for its platform key); apps read it via a `@Microsoft.KeyVault(...)`
reference and managed identity, so the value never lands in app settings or tfvars. The
module outputs the host, SSL port, resource ID, and the KV secret URI — never the access key
or the raw connection string.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `name` | string | — | Redis cache name. |
| `resource_group_name` | string | — | RG. |
| `location` | string | — | Azure region. |
| `sku_name` | string | `Standard` | `Basic` or `Standard`. |
| `family` | string | `C` | SKU family (C = Basic/Standard). |
| `capacity` | number | `1` | Cache size within the family (C0..C6). |
| `minimum_tls_version` | string | `1.2` | TLS floor (pinned to 1.2). |
| `key_vault_id` | string | — | Vault to store the connection string in. |
| `key_vault_secret_name` | string | `redis-connection-string` | Secret name. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `id` | Redis cache resource ID. |
| `name` | Cache name. |
| `hostname` | Redis hostname. |
| `ssl_port` | TLS port (non-SSL port disabled). |
| `connection_string_secret_uri` | Versionless KV secret URI for the connection string. |
