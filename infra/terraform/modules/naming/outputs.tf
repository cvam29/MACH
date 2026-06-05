output "tags" {
  description = "Base tag map merged with any extra tags. Attach to every resource."
  value       = merge(local.base_tags, var.extra_tags)
}

output "location" {
  description = "Pass-through of the long-form Azure region."
  value       = var.location
}

# Dashed names (RG, Key Vault parent naming, Function Apps, APIM, SQL, Service Bus...).
output "names" {
  description = "Map of derived, dashed resource names keyed by resource kind."
  value = {
    resource_group       = "rg-${local.base}"
    log_analytics        = "log-${local.base}"
    app_insights         = "appi-${local.base}"
    key_vault            = substr("kv-${local.base}", 0, 24)
    sql_server           = "sql-${local.base}"
    sql_database         = "sqldb-${local.base}"
    servicebus           = "sb-${local.base}"
    communication        = "acs-${local.base}"
    email_service        = "acse-${local.base}"
    maps                 = "maps-${local.base}"
    redis                = "redis-${local.base}"
    apim                 = "apim-${local.base}"
    service_plan         = "asp-${local.base}"
    function_app_prefix  = "func-${local.base}" # callers append "-<host>"
    user_assigned_prefix = "id-${local.base}"
  }
}

# No-dash / globally-unique-sensitive names. Callers usually append a random suffix
# for storage accounts to satisfy the global-uniqueness + length (<=24) constraint.
output "names_nodash" {
  description = "Map of derived names without dashes (storage account base, etc.)."
  value = {
    storage_base = substr("st${local.base_nd}", 0, 18) # leave room for a 6-char suffix
  }
}
