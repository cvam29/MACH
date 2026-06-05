# --- Non-secret composition inputs (values live in terraform.tfvars) ---

variable "subscription_id" {
  description = "Azure subscription ID (supply via TF_VAR_subscription_id / ARM_SUBSCRIPTION_ID)."
  type        = string
  default     = "00000000-0000-0000-0000-000000000000" # placeholder for plan-only/offline init
}

variable "prefix" {
  description = "Workload prefix."
  type        = string
  default     = "mach"
}

variable "env" {
  description = "Environment short name."
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region (long form)."
  type        = string
  default     = "westeurope"
}

variable "location_short" {
  description = "Short region code used in names."
  type        = string
  default     = "weu"
}

variable "tenant_id" {
  description = "AAD tenant ID (for Key Vault + SQL AAD admin)."
  type        = string
  default     = "00000000-0000-0000-0000-000000000000"
}

variable "sql_aad_admin_login" {
  description = "Display name of the SQL AAD admin principal."
  type        = string
  default     = "MACH SQL Admins"
}

variable "sql_aad_admin_object_id" {
  description = "Object ID of the SQL AAD admin principal."
  type        = string
  default     = "00000000-0000-0000-0000-000000000000"
}

variable "kv_admin_object_ids" {
  description = "Object IDs granted Key Vault Administrator (deploying user / CI SP)."
  type        = list(string)
  default     = []
}

variable "consolidate_function_apps" {
  description = <<-EOT
    Function App topology toggle. false = one Function App per host (auth/bff/webhooks/
    projection/indexer/notifications/outbox). true = two consolidated apps (api + workers).
    Trade-off: per-host gives the cleanest isolation/scaling; 2-app reduces plan count/cost.
  EOT
  type        = bool
  default     = false
}

# --- Sensitive vendor keys (NEVER in tfvars; sourced from TF_VAR_* env) ---
# These are declared so the plan is aware of them, but their VALUES are only ever written
# into EMPTY Key Vault placeholders out-of-band. They are not assigned to any resource here;
# they exist to document the secret surface and keep CI honest. Default "" lets offline
# init/validate/plan run without the env vars set.

variable "commercetools_client_secret" {
  description = "commercetools API client secret (sensitive, via TF_VAR_)."
  type        = string
  default     = ""
  sensitive   = true
}

variable "contentstack_management_token" {
  description = "Contentstack management token (sensitive, via TF_VAR_)."
  type        = string
  default     = ""
  sensitive   = true
}

variable "adyen_api_key" {
  description = "Adyen API key (sensitive, via TF_VAR_)."
  type        = string
  default     = ""
  sensitive   = true
}

variable "adyen_hmac_key" {
  description = "Adyen webhook HMAC key (sensitive, via TF_VAR_)."
  type        = string
  default     = ""
  sensitive   = true
}

variable "algolia_admin_key" {
  description = "Algolia admin (indexing) API key (sensitive, via TF_VAR_)."
  type        = string
  default     = ""
  sensitive   = true
}
