variable "prefix" {
  description = "Short workload/product prefix (e.g. 'mach'). Lowercase alphanumeric, 2-10 chars."
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9]{1,9}$", var.prefix))
    error_message = "prefix must be 2-10 lowercase alphanumeric chars starting with a letter."
  }
}

variable "env" {
  description = "Environment short name (e.g. dev, test, prod)."
  type        = string

  validation {
    condition     = contains(["dev", "test", "stage", "prod"], var.env)
    error_message = "env must be one of: dev, test, stage, prod."
  }
}

variable "location" {
  description = "Azure region (long form, e.g. 'westeurope')."
  type        = string
}

variable "location_short" {
  description = "Short code for the region used inside resource names (e.g. 'weu')."
  type        = string
  default     = "weu"
}

variable "extra_tags" {
  description = "Additional tags merged on top of the generated base tags."
  type        = map(string)
  default     = {}
}
