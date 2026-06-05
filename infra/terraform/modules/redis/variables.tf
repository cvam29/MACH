variable "name" {
  description = "Azure Cache for Redis name."
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

# --- SKU / capacity ---
# Redis pricing tier is (family, sku_name, capacity):
#   Basic/Standard  -> family = "C", capacity 0-6 (C0..C6)
#   Premium         -> family = "P" (out of scope here; this demo uses Basic/Standard)
variable "sku_name" {
  description = "Redis SKU tier. Basic (single node, no SLA) or Standard (replicated, 99.9% SLA)."
  type        = string
  default     = "Standard"

  validation {
    condition     = contains(["Basic", "Standard"], var.sku_name)
    error_message = "sku_name must be Basic or Standard for this module."
  }
}

variable "family" {
  description = "SKU family. C = Basic/Standard (the only families this module supports)."
  type        = string
  default     = "C"

  validation {
    condition     = var.family == "C"
    error_message = "family must be 'C' for the Basic/Standard tiers."
  }
}

variable "capacity" {
  description = "Cache size within the family (C0..C6). C1 = 1GB."
  type        = number
  default     = 1
}

variable "minimum_tls_version" {
  description = "Minimum TLS version clients must use. Kept at 1.2 (TLS-only posture)."
  type        = string
  default     = "1.2"

  validation {
    condition     = var.minimum_tls_version == "1.2"
    error_message = "minimum_tls_version must be 1.2 for this demo's security posture."
  }
}

# --- Key Vault surfacing (same pattern as the maps module) ---
variable "key_vault_id" {
  description = "Key Vault ID into which the Redis connection string is surfaced."
  type        = string
}

variable "key_vault_secret_name" {
  description = "Name of the Key Vault secret that will hold the Redis connection string."
  type        = string
  default     = "redis-connection-string"
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
