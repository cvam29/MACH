output "id" {
  description = "Azure Cache for Redis resource ID."
  value       = azurerm_redis_cache.this.id
}

output "name" {
  description = "Redis cache name."
  value       = azurerm_redis_cache.this.name
}

output "hostname" {
  description = "Redis hostname (<name>.redis.cache.windows.net)."
  value       = azurerm_redis_cache.this.hostname
}

output "ssl_port" {
  description = "TLS port the cache listens on (non-SSL port is disabled)."
  value       = azurerm_redis_cache.this.ssl_port
}

output "connection_string_secret_uri" {
  description = "Versionless Key Vault secret URI holding the Redis connection string."
  value       = azurerm_key_vault_secret.redis_connection_string.versionless_id
}
