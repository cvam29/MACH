# Storage account that backs the Function App host (AzureWebJobsStorage) and holds the
# Flex Consumption deployment container. Identity-based access is preferred: shared key
# auth is disabled so Function Apps connect via managed identity (Storage Blob Data Owner).

resource "azurerm_storage_account" "this" {
  name                            = var.name
  resource_group_name             = var.resource_group_name
  location                        = var.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  account_kind                    = "StorageV2"
  min_tls_version                 = "TLS1_2"
  shared_access_key_enabled       = false # passwordless: Function Apps use managed identity
  allow_nested_items_to_be_public = false
  tags                            = var.tags
}

# Deployment container for Flex Consumption one-deploy packages.
resource "azurerm_storage_container" "deployments" {
  name                  = var.deployment_container_name
  storage_account_id    = azurerm_storage_account.this.id
  container_access_type = "private"
}
