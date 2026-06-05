variable "name" {
  description = "Storage account name (3-24 lowercase alphanumeric, globally unique)."
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

variable "deployment_container_name" {
  description = "Blob container used by Flex Consumption for deployment packages."
  type        = string
  default     = "deployments"
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
