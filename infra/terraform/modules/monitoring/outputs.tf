output "log_analytics_workspace_id" {
  description = "Resource ID of the Log Analytics workspace."
  value       = azurerm_log_analytics_workspace.this.id
}

output "app_insights_id" {
  description = "Resource ID of the Application Insights component."
  value       = azurerm_application_insights.this.id
}

output "app_insights_connection_string" {
  description = "Application Insights connection string (used by Function Apps)."
  value       = azurerm_application_insights.this.connection_string
  sensitive   = true
}

output "app_insights_instrumentation_key" {
  description = "Application Insights instrumentation key (legacy callers)."
  value       = azurerm_application_insights.this.instrumentation_key
  sensitive   = true
}
