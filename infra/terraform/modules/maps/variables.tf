variable "name" {
  description = "Azure Maps account name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group."
  type        = string
}

variable "location" {
  description = "Azure region. Azure Maps is global; this is the metadata location ('global' or a region)."
  type        = string
  default     = "global"
}

variable "sku_name" {
  description = "Azure Maps SKU (G2 is the current Gen2 tier; S0/S1 are deprecated for new accounts)."
  type        = string
  default     = "G2"
}

variable "key_vault_id" {
  description = "Key Vault ID into which the Maps primary key is surfaced."
  type        = string
}

variable "key_vault_secret_name" {
  description = "Name of the Key Vault secret that will hold the Maps key."
  type        = string
  default     = "maps-primary-key"
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
