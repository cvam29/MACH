# Azure Cache for Redis — distributed cache / session + idempotency store for the BFF and
# workers. TLS-only (the non-SSL 6379 port stays disabled) with a minimum TLS of 1.2.
#
# The Redis access key is an Azure-generated platform credential (not a user-supplied vendor
# secret), so — exactly like the `maps` module — the connection string is surfaced into Key
# Vault. Apps read it via a @Microsoft.KeyVault(...) reference + managed identity; the value
# lives only in Key Vault, never in app settings or tfvars. (The generated key transits
# Terraform state the same way the Maps primary key does; no plaintext is emitted to app
# config or version control.)

resource "azurerm_redis_cache" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location

  capacity = var.capacity
  family   = var.family
  sku_name = var.sku_name

  # TLS-only posture: non-SSL port disabled, TLS floor at 1.2.
  non_ssl_port_enabled = false
  minimum_tls_version  = var.minimum_tls_version

  tags = var.tags
}

# Surface the SSL connection string into Key Vault (same placeholder/secret pattern the
# `maps` module uses for its Azure-generated key). primary_connection_string already targets
# the TLS port and carries ssl=True.
resource "azurerm_key_vault_secret" "redis_connection_string" {
  name         = var.key_vault_secret_name
  value        = azurerm_redis_cache.this.primary_connection_string
  key_vault_id = var.key_vault_id
  content_type = "azure-cache-redis/connection-string"
  tags         = var.tags
}
