# monitoring

Log Analytics workspace + workspace-based Application Insights. All telemetry from the
Function Apps (OpenTelemetry → App Insights) lands here.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `name_log_analytics` | string | — | Workspace name. |
| `name_app_insights` | string | — | App Insights name. |
| `resource_group_name` | string | — | Target RG. |
| `location` | string | — | Region. |
| `retention_in_days` | number | `30` | Log retention. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `log_analytics_workspace_id` | Workspace resource ID. |
| `app_insights_id` | App Insights resource ID. |
| `app_insights_connection_string` | Connection string (sensitive). |
| `app_insights_instrumentation_key` | Instrumentation key (sensitive). |
