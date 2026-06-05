# sql

Azure SQL logical server + database with **AAD-only** authentication (no SQL login/password)
and an "allow Azure services" firewall rule. Function App managed identities authenticate
via AAD, so no credentials live in state or app settings.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `server_name` | string | — | Logical server name (global). |
| `database_name` | string | — | Database name. |
| `resource_group_name` | string | — | RG. |
| `location` | string | — | Region. |
| `aad_admin_login` | string | — | AAD admin display name. |
| `aad_admin_object_id` | string | — | AAD admin object ID. |
| `tenant_id` | string | — | AAD tenant. |
| `sku_name` | string | `GP_S_Gen5_1` | Database SKU. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `server_id` | Server resource ID. |
| `server_fqdn` | Server FQDN. |
| `database_id` | Database resource ID. |
| `database_name` | Database name. |
| `ado_net_connection_string` | Passwordless ADO.NET connection string. |
