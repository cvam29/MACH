variable "namespace_name" {
  description = "Service Bus namespace name (globally unique)."
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

variable "sku" {
  description = "Namespace SKU (Basic has no topics; use Standard or Premium)."
  type        = string
  default     = "Standard"
}

variable "topics" {
  description = <<-EOT
    Map of topic name -> list of subscription names. Each subscription is created with
    dead-lettering enabled and a max delivery count, matching the resilience design.
  EOT
  type        = map(list(string))
  default = {
    payments      = ["projection"]
    catalog       = ["indexer"]
    content       = ["indexer"]
    notifications = ["notifications"]
  }
}

variable "max_delivery_count" {
  description = "Max delivery attempts before a message is dead-lettered."
  type        = number
  default     = 10
}

variable "data_sender_principal_ids" {
  description = "Principal IDs granted 'Azure Service Bus Data Sender'."
  type        = list(string)
  default     = []
}

variable "data_receiver_principal_ids" {
  description = "Principal IDs granted 'Azure Service Bus Data Receiver'."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
