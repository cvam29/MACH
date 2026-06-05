# servicebus

Service Bus namespace with the four MACH topics (`payments`, `catalog`, `content`,
`notifications`) and their subscriptions. Every subscription enables dead-lettering and a
max delivery count for poison-message handling. RBAC (Data Sender/Receiver) replaces SAS keys.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `namespace_name` | string | — | Namespace name (global). |
| `resource_group_name` | string | — | RG. |
| `location` | string | — | Region. |
| `sku` | string | `Standard` | Namespace SKU (topics need Standard+). |
| `topics` | map(list(string)) | payments/catalog/content/notifications | Topic → subscription names. |
| `max_delivery_count` | number | `10` | Deliveries before DLQ. |
| `data_sender_principal_ids` | list(string) | `[]` | Data Sender grantees. |
| `data_receiver_principal_ids` | list(string) | `[]` | Data Receiver grantees. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `namespace_id` | Namespace resource ID. |
| `namespace_name` | Namespace name. |
| `namespace_endpoint` | FQDN endpoint for MI auth. |
| `topic_ids` | Map topic → ID. |
| `subscription_ids` | Map `topic/sub` → ID. |
