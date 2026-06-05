output "resource_group_name" {
  description = "Resource group name."
  value       = azurerm_resource_group.this.name
}

output "key_vault_uri" {
  description = "Key Vault URI."
  value       = module.keyvault.vault_uri
}

output "app_insights_id" {
  description = "Application Insights resource ID."
  value       = module.monitoring.app_insights_id
}

output "sql_server_fqdn" {
  description = "Azure SQL server FQDN."
  value       = module.sql.server_fqdn
}

output "servicebus_endpoint" {
  description = "Service Bus fully qualified namespace."
  value       = module.servicebus.namespace_endpoint
}

output "maps_client_id" {
  description = "Azure Maps client ID."
  value       = module.maps.client_id
}

output "apim_gateway_url" {
  description = "APIM gateway base URL."
  value       = module.apim.gateway_url
}

output "redis_hostname" {
  description = "Azure Cache for Redis hostname."
  value       = module.redis.hostname
}

output "redis_connection_string_secret_uri" {
  description = "Key Vault secret URI holding the Redis connection string."
  value       = module.redis.connection_string_secret_uri
}

output "function_app_hostnames" {
  description = "Map of function app key -> default hostname."
  value       = { for k, m in module.functions : k => m.default_hostname }
}

output "function_app_principal_ids" {
  description = "Map of function app key -> system-assigned identity principal ID."
  value       = { for k, m in module.functions : k => m.principal_id }
}
