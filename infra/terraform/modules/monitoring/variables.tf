variable "name_log_analytics" {
  description = "Name of the Log Analytics workspace."
  type        = string
}

variable "name_app_insights" {
  description = "Name of the Application Insights component."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group to deploy into."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "retention_in_days" {
  description = "Log Analytics retention in days."
  type        = number
  default     = 30
}

variable "tags" {
  description = "Tags to apply."
  type        = map(string)
  default     = {}
}
