output "id" {
  description = "API Management service resource ID."
  value       = azurerm_api_management.this.id
}

output "name" {
  description = "API Management service name."
  value       = azurerm_api_management.this.name
}

output "gateway_url" {
  description = "APIM gateway base URL."
  value       = azurerm_api_management.this.gateway_url
}

output "principal_id" {
  description = "APIM system-assigned identity principal ID."
  value       = azurerm_api_management.this.identity[0].principal_id
}

output "bff_api_id" {
  description = "Sample BFF API resource ID."
  value       = azurerm_api_management_api.bff.id
}
