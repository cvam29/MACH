# functions

A single **Flex Consumption** Function App (Linux, dotnet-isolated) plus its dedicated FC1
service plan, a system-assigned managed identity, and parameterized RBAC role assignments.

## Resource choice
Uses the first-class `azurerm_function_app_flex_consumption` (azurerm 4.x), which maps 1:1
to the ARM `functionAppConfig` shape (runtime, `instance_memory_in_mb`,
`maximum_instance_count`, MI-auth deployment storage). This is cleaner than the legacy
`azurerm_service_plan(FC1)` + `azurerm_linux_function_app` + manual `site_config`/app-setting
emulation, so it is the chosen approach. Flex requires one app per plan, so this module is
instantiated **once per host** (or twice for the consolidated api/workers layout — controlled
by a variable at the environment level).

## Secrets / RBAC
- No secret values in `app_settings`; callers pass `@Microsoft.KeyVault(SecretUri=...)` refs.
- System-assigned identity gets, as toggled: **Key Vault Secrets User**, **Service Bus Data
  Sender/Receiver**, **Storage Blob Data Owner**.

## Inputs (selected)
| Name | Type | Default | Description |
|---|---|---|---|
| `name` / `service_plan_name` | string | — | App + dedicated plan names. |
| `resource_group_name` / `location` | string | — | RG / region. |
| `runtime_name` / `runtime_version` | string | `dotnet-isolated` / `8.0` | Flex runtime. |
| `instance_memory_in_mb` | number | `2048` | Per-instance memory. |
| `maximum_instance_count` | number | `40` | Max scale-out. |
| `deployment_storage_account_id` | string | — | Storage backing deploy. |
| `deployment_storage_container_endpoint` | string | — | Flex `storage_container_endpoint`. |
| `app_settings` | map(string) | `{}` | Non-secret settings + KV refs. |
| `app_insights_connection_string` | string | `null` | App Insights (sensitive). |
| `key_vault_id` | string | `null` | Grant Secrets User. |
| `servicebus_namespace_id` | string | `null` | Service Bus scope. |
| `grant_servicebus_sender` / `grant_servicebus_receiver` | bool | `false` | SB role toggles. |
| `storage_blob_owner_scope_id` | string | `null` | Grant Blob Data Owner. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `id` / `name` / `default_hostname` | App identifiers. |
| `principal_id` | System-assigned identity principal. |
| `service_plan_id` | FC1 plan ID. |
