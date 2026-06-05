variable "name" {
  description = "API Management service name (globally unique)."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "publisher_name" {
  description = "Publisher/organization name shown in the developer portal."
  type        = string
  default     = "MACH Commerce Demo"
}

variable "publisher_email" {
  description = "Publisher contact email."
  type        = string
  default     = "demo@example.com"
}

variable "sku_name" {
  description = "APIM SKU (e.g. Developer_1 or Consumption_0). Consumption requires capacity 0."
  type        = string
  default     = "Developer_1"
}

variable "bff_backend_url" {
  description = "Backend base URL of the BFF Function App (e.g. https://func-...azurewebsites.net/api)."
  type        = string
}

variable "key_vault_named_values" {
  description = <<-EOT
    Map of APIM named-value name -> Key Vault secret URI. APIM resolves these via its own
    managed identity (Key Vault Secrets User) so no secret text lives in APIM config/state.
  EOT
  type        = map(string)
  default     = {}
}

variable "rate_limit_calls" {
  description = "Rate-limit: allowed calls per renewal period."
  type        = number
  default     = 300
}

variable "rate_limit_period_seconds" {
  description = "Rate-limit renewal period in seconds."
  type        = number
  default     = 60
}

variable "key_vault_id" {
  description = "Key Vault ID to grant APIM's identity 'Key Vault Secrets User' on. Null to skip."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
