output "id" {
  description = "Storage account resource ID."
  value       = azurerm_storage_account.this.id
}

output "name" {
  description = "Storage account name."
  value       = azurerm_storage_account.this.name
}

output "primary_blob_endpoint" {
  description = "Primary blob service endpoint."
  value       = azurerm_storage_account.this.primary_blob_endpoint
}

output "deployment_container_name" {
  description = "Name of the deployment container."
  value       = azurerm_storage_container.deployments.name
}

output "deployment_container_endpoint" {
  description = "Full blob endpoint of the deployment container (for Flex storage_container_endpoint)."
  value       = "${azurerm_storage_account.this.primary_blob_endpoint}${azurerm_storage_container.deployments.name}"
}
