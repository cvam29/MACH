# communication

Azure Communication Services + Email Communication Service with an **Azure-managed** email
domain, associated back to the Communication Service. Powers the multi-party transactional
email fan-out (customer / store / supplier / reception).

These resources are global (`location = "global"`); only `data_location` is configurable.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `communication_name` | string | — | ACS resource name. |
| `email_service_name` | string | — | Email service name. |
| `resource_group_name` | string | — | RG. |
| `data_location` | string | `United States` | Data-at-rest region. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `communication_service_id` | ACS resource ID. |
| `email_service_id` | Email service resource ID. |
| `managed_domain_id` | Managed domain resource ID. |
| `managed_domain_from_sender` | Azure-managed MailFrom domain. |
| `primary_connection_string` | ACS connection string (sensitive). |
