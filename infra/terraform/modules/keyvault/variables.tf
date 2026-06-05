variable "name" {
  description = "Key Vault name (<= 24 chars, globally unique)."
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

variable "tenant_id" {
  description = "AAD tenant ID for the vault."
  type        = string
}

variable "sku_name" {
  description = "Key Vault SKU."
  type        = string
  default     = "standard"
}

variable "secret_placeholder_names" {
  description = <<-EOT
    Names of EMPTY secret placeholders to create (one per vendor key). Terraform creates
    the secret object so Key Vault references resolve, but writes NO value here — the value
    is set out-of-band (portal / CI with a privileged identity). Keeps secrets out of state.
  EOT
  type        = list(string)
  default     = []
}

variable "admin_object_ids" {
  description = "Object IDs granted 'Key Vault Administrator' (e.g. the deploying user/CI SP)."
  type        = list(string)
  default     = []
}

variable "secrets_user_principal_ids" {
  description = "Principal IDs (managed identities) granted 'Key Vault Secrets User' (read)."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
