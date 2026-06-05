variable "name" {
  description = "Function App name (e.g. func-mach-dev-weu-bff)."
  type        = string
}

variable "service_plan_name" {
  description = "Name of the dedicated Flex Consumption service plan for this app (one app per Flex plan)."
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

# --- Flex Consumption runtime config (functionAppConfig equivalents) ---

variable "runtime_name" {
  description = "Flex runtime name."
  type        = string
  default     = "dotnet-isolated"
}

variable "runtime_version" {
  description = "Flex runtime version (dotnet-isolated)."
  type        = string
  default     = "8.0"
}

variable "instance_memory_in_mb" {
  description = "Per-instance memory (MB) for the Flex plan (512/2048/4096)."
  type        = number
  default     = 2048
}

variable "maximum_instance_count" {
  description = "Maximum scale-out instance count for the Flex plan."
  type        = number
  default     = 40
}

# --- Deployment storage (managed-identity auth, no keys) ---

variable "deployment_storage_account_id" {
  description = "Storage account resource ID backing deployment + AzureWebJobsStorage."
  type        = string
}

variable "deployment_storage_container_endpoint" {
  description = "Blob endpoint of the deployment container (Flex storage_container_endpoint)."
  type        = string
}

# --- App settings + Key Vault references ---

variable "app_settings" {
  description = <<-EOT
    Non-secret app settings. Secret values MUST be passed as @Microsoft.KeyVault(SecretUri=...)
    references — never raw secret strings.
  EOT
  type        = map(string)
  default     = {}
}

variable "app_insights_connection_string" {
  description = "Application Insights connection string."
  type        = string
  default     = null
  sensitive   = true
}

# --- RBAC scopes for the system-assigned identity ---

variable "key_vault_id" {
  description = "Key Vault ID to grant 'Key Vault Secrets User' on. Null to skip."
  type        = string
  default     = null
}

variable "servicebus_namespace_id" {
  description = "Service Bus namespace ID for Data Sender/Receiver. Null to skip."
  type        = string
  default     = null
}

variable "grant_servicebus_sender" {
  description = "Grant 'Azure Service Bus Data Sender' to this app's identity."
  type        = bool
  default     = false
}

variable "grant_servicebus_receiver" {
  description = "Grant 'Azure Service Bus Data Receiver' to this app's identity."
  type        = bool
  default     = false
}

variable "storage_blob_owner_scope_id" {
  description = "Storage account ID to grant 'Storage Blob Data Owner' on. Null to skip."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags."
  type        = map(string)
  default     = {}
}
