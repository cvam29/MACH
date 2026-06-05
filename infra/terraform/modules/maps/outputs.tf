output "id" {
  description = "Azure Maps account resource ID."
  value       = azurerm_maps_account.this.id
}

output "name" {
  description = "Azure Maps account name."
  value       = azurerm_maps_account.this.name
}

output "client_id" {
  description = "Azure Maps unique client ID (used for AAD-based Maps auth)."
  value       = azurerm_maps_account.this.x_ms_client_id
}

output "key_secret_uri" {
  description = "Versionless Key Vault secret URI holding the Maps primary key."
  value       = azurerm_key_vault_secret.maps_key.versionless_id
}
