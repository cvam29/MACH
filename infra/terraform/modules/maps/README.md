# maps

Azure Maps account for geocoding + route/distance (distance-based delivery quoting). The
Azure-generated primary key is surfaced into Key Vault; apps read it via a KV reference and
managed identity, so the value never lands in app settings or tfvars.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `name` | string | — | Maps account name. |
| `resource_group_name` | string | — | RG. |
| `location` | string | `global` | Metadata location (Maps is global). |
| `sku_name` | string | `G2` | Maps SKU. |
| `key_vault_id` | string | — | Vault to store the key in. |
| `key_vault_secret_name` | string | `maps-primary-key` | Secret name. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `id` | Maps account resource ID. |
| `name` | Account name. |
| `client_id` | Maps client ID for AAD auth. |
| `key_secret_uri` | Versionless KV secret URI for the Maps key. |
