variable "communication_name" {
  description = "Azure Communication Services resource name."
  type        = string
}

variable "email_service_name" {
  description = "Email Communication Service resource name."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group."
  type        = string
}

variable "data_location" {
  description = "Where ACS stores data at rest (e.g. 'Europe', 'United States')."
  type        = string
  default     = "United States"
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
