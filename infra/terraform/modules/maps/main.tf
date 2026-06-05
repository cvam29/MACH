# Azure Maps account (geocoding + route/distance for distance-based delivery quoting).
# The Maps primary key is an Azure-generated platform credential (not a user-supplied vendor
# secret), so it is surfaced into Key Vault here. Apps read it via a KV reference + managed
# identity; the key value still lives only in Key Vault, never in app settings or tfvars.

resource "azurerm_maps_account" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku_name            = var.sku_name
  tags                = var.tags
}

resource "azurerm_key_vault_secret" "maps_key" {
  name         = var.key_vault_secret_name
  value        = azurerm_maps_account.this.primary_access_key
  key_vault_id = var.key_vault_id
  content_type = "azure-maps/primary-key"
  tags         = var.tags
}
