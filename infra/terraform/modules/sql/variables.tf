variable "server_name" {
  description = "Azure SQL logical server name (globally unique)."
  type        = string
}

variable "database_name" {
  description = "Database name."
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

variable "aad_admin_login" {
  description = "Display name of the AAD admin (user/group) for the SQL server."
  type        = string
}

variable "aad_admin_object_id" {
  description = "Object ID of the AAD admin principal."
  type        = string
}

variable "tenant_id" {
  description = "AAD tenant ID."
  type        = string
}

variable "sku_name" {
  description = "Database SKU (e.g. GP_S_Gen5_1 serverless, S0, Basic)."
  type        = string
  default     = "GP_S_Gen5_1"
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
