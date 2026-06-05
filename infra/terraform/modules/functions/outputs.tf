output "id" {
  description = "Function App resource ID."
  value       = azurerm_function_app_flex_consumption.this.id
}

output "name" {
  description = "Function App name."
  value       = azurerm_function_app_flex_consumption.this.name
}

output "default_hostname" {
  description = "Default hostname of the Function App."
  value       = azurerm_function_app_flex_consumption.this.default_hostname
}

output "principal_id" {
  description = "System-assigned managed identity principal ID."
  value       = azurerm_function_app_flex_consumption.this.identity[0].principal_id
}

output "service_plan_id" {
  description = "Flex Consumption service plan ID."
  value       = azurerm_service_plan.this.id
}
