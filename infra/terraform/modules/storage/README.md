# storage

Storage account backing the Function App host plus the Flex Consumption deployment
container. Shared-key auth is disabled — Function Apps connect via managed identity
(Storage Blob Data Owner), matching the passwordless posture.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `name` | string | — | Account name (3-24, globally unique). |
| `resource_group_name` | string | — | RG. |
| `location` | string | — | Region. |
| `deployment_container_name` | string | `deployments` | Flex deployment container. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `id` | Account resource ID. |
| `name` | Account name. |
| `primary_blob_endpoint` | Blob endpoint. |
| `deployment_container_name` | Container name. |
| `deployment_container_endpoint` | Full container blob endpoint (Flex `storage_container_endpoint`). |
