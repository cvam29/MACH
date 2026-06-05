# Composition root for the dev environment. Wires every module into the target topology.
# This is "IaC as documentation": `terraform plan` is the deliverable; it is not applied.

locals {
  # Vendor-key Key Vault placeholder names (EMPTY secrets; values set out-of-band).
  vendor_secret_names = [
    "commercetools-client-secret",
    "contentstack-management-token",
    "adyen-api-key",
    "adyen-hmac-key",
    "algolia-admin-key",
  ]

  # Function App host catalogue.
  #   role: "sender" / "receiver" / "both" / "none" -> Service Bus RBAC.
  #   The "consolidated" key groups hosts into 2 apps (api + workers) when toggled.
  hosts = {
    auth          = { sb = "none", consolidated = "api" }
    bff           = { sb = "sender", consolidated = "api" }
    webhooks      = { sb = "sender", consolidated = "api" }
    projection    = { sb = "receiver", consolidated = "workers" }
    indexer       = { sb = "receiver", consolidated = "workers" }
    notifications = { sb = "receiver", consolidated = "workers" }
    outbox        = { sb = "sender", consolidated = "workers" }
  }

  # Effective app set depending on the consolidation toggle.
  function_apps = var.consolidate_function_apps ? {
    api     = { sb = "sender" } # api group sends events
    workers = { sb = "both" }   # workers consume + republish
  } : local.hosts
}

module "naming" {
  source = "../../modules/naming"

  prefix         = var.prefix
  env            = var.env
  location       = var.location
  location_short = var.location_short
}

resource "azurerm_resource_group" "this" {
  name     = module.naming.names.resource_group
  location = module.naming.location
  tags     = module.naming.tags
}

# Random suffix to make the storage account name globally unique.
resource "random_string" "storage_suffix" {
  length  = 6
  upper   = false
  special = false
}

module "monitoring" {
  source = "../../modules/monitoring"

  name_log_analytics  = module.naming.names.log_analytics
  name_app_insights   = module.naming.names.app_insights
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  tags                = module.naming.tags
}

module "keyvault" {
  source = "../../modules/keyvault"

  name                     = module.naming.names.key_vault
  resource_group_name      = azurerm_resource_group.this.name
  location                 = azurerm_resource_group.this.location
  tenant_id                = var.tenant_id
  secret_placeholder_names = local.vendor_secret_names
  admin_object_ids         = var.kv_admin_object_ids

  # Function App identities granted Secrets User are wired below via per-app role assignments
  # inside the functions module; cross-referencing here would create a cycle.
  secrets_user_principal_ids = []

  tags = module.naming.tags
}

module "storage" {
  source = "../../modules/storage"

  name                = "${module.naming.names_nodash.storage_base}${random_string.storage_suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  tags                = module.naming.tags
}

module "sql" {
  source = "../../modules/sql"

  server_name         = module.naming.names.sql_server
  database_name       = module.naming.names.sql_database
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  aad_admin_login     = var.sql_aad_admin_login
  aad_admin_object_id = var.sql_aad_admin_object_id
  tenant_id           = var.tenant_id
  tags                = module.naming.tags
}

module "servicebus" {
  source = "../../modules/servicebus"

  namespace_name      = module.naming.names.servicebus
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  tags                = module.naming.tags
}

module "communication" {
  source = "../../modules/communication"

  communication_name  = module.naming.names.communication
  email_service_name  = module.naming.names.email_service
  resource_group_name = azurerm_resource_group.this.name
  data_location       = "Europe"
  tags                = module.naming.tags
}

module "maps" {
  source = "../../modules/maps"

  name                = module.naming.names.maps
  resource_group_name = azurerm_resource_group.this.name
  location            = "global"
  key_vault_id        = module.keyvault.id
  tags                = module.naming.tags

  # Maps writes its key into Key Vault; the admin role assignment must exist first.
  depends_on = [module.keyvault]
}

module "redis" {
  source = "../../modules/redis"

  name                = module.naming.names.redis
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  sku_name            = "Standard"
  key_vault_id        = module.keyvault.id
  tags                = module.naming.tags

  # Redis writes its connection string into Key Vault; the admin role assignment must exist first.
  depends_on = [module.keyvault]
}

# --- Function Apps (per-host or consolidated) ---
module "functions" {
  source   = "../../modules/functions"
  for_each = local.function_apps

  name                = "${module.naming.names.function_app_prefix}-${each.key}"
  service_plan_name   = "${module.naming.names.service_plan}-${each.key}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  deployment_storage_account_id         = module.storage.id
  deployment_storage_container_endpoint = module.storage.deployment_container_endpoint

  app_insights_connection_string = module.monitoring.app_insights_connection_string

  # Non-secret settings + Key Vault references for vendor secrets (no plaintext).
  app_settings = {
    "MACH__Environment"          = var.env
    "ServiceBus__FullyQualified" = module.servicebus.namespace_endpoint
    "Sql__ConnectionString"      = module.sql.ado_net_connection_string
    "Maps__KeyVaultSecretUri"    = module.maps.key_secret_uri
    # Redis connection string resolved at runtime from Key Vault via the app's managed identity:
    "Cache__ConnectionString" = "@Microsoft.KeyVault(SecretUri=${module.redis.connection_string_secret_uri})"
    # Vendor secrets resolved at runtime from Key Vault via the app's managed identity:
    "Commercetools__ClientSecret"   = "@Microsoft.KeyVault(SecretUri=${module.keyvault.secret_uris["commercetools-client-secret"]})"
    "Contentstack__ManagementToken" = "@Microsoft.KeyVault(SecretUri=${module.keyvault.secret_uris["contentstack-management-token"]})"
    "Adyen__ApiKey"                 = "@Microsoft.KeyVault(SecretUri=${module.keyvault.secret_uris["adyen-api-key"]})"
    "Adyen__HmacKey"                = "@Microsoft.KeyVault(SecretUri=${module.keyvault.secret_uris["adyen-hmac-key"]})"
    "Algolia__AdminKey"             = "@Microsoft.KeyVault(SecretUri=${module.keyvault.secret_uris["algolia-admin-key"]})"
  }

  # RBAC for the system-assigned identity.
  key_vault_id                = module.keyvault.id
  servicebus_namespace_id     = module.servicebus.namespace_id
  grant_servicebus_sender     = contains(["sender", "both"], each.value.sb)
  grant_servicebus_receiver   = contains(["receiver", "both"], each.value.sb)
  storage_blob_owner_scope_id = module.storage.id

  tags = module.naming.tags
}

module "apim" {
  source = "../../modules/apim"

  name                = module.naming.names.apim
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  publisher_email     = "demo@example.com"
  sku_name            = "Developer_1"

  # BFF backend: the bff host (per-host) or the api group (consolidated).
  bff_backend_url = "https://${module.functions[var.consolidate_function_apps ? "api" : "bff"].default_hostname}/api"

  key_vault_id = module.keyvault.id
  key_vault_named_values = {
    "adyen-hmac-key" = module.keyvault.secret_uris["adyen-hmac-key"]
  }

  tags = module.naming.tags
}
