output "id" {
  description = "Key Vault resource ID."
  value       = azurerm_key_vault.this.id
}

output "name" {
  description = "Key Vault name."
  value       = azurerm_key_vault.this.name
}

output "vault_uri" {
  description = "Key Vault URI (https://<name>.vault.azure.net/)."
  value       = azurerm_key_vault.this.vault_uri
}

output "secret_uris" {
  description = "Map of placeholder secret name -> versionless secret URI for @Microsoft.KeyVault references."
  value = {
    for name, secret in azurerm_key_vault_secret.placeholders :
    name => "${azurerm_key_vault.this.vault_uri}secrets/${name}"
  }
}
