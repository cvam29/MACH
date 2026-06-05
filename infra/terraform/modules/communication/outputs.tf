output "communication_service_id" {
  description = "Azure Communication Services resource ID."
  value       = azurerm_communication_service.this.id
}

output "email_service_id" {
  description = "Email Communication Service resource ID."
  value       = azurerm_email_communication_service.this.id
}

output "managed_domain_id" {
  description = "Azure-managed email domain resource ID."
  value       = azurerm_email_communication_service_domain.managed.id
}

output "managed_domain_from_sender" {
  description = "The Azure-managed sender domain (MailFrom) usable as the from-domain."
  value       = azurerm_email_communication_service_domain.managed.from_sender_domain
}

output "primary_connection_string" {
  description = "ACS primary connection string (sensitive — surface into Key Vault, not app settings)."
  value       = azurerm_communication_service.this.primary_connection_string
  sensitive   = true
}
