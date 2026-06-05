# Flex Consumption Function App (Linux, dotnet-isolated) modeled via the first-class
# `azurerm_function_app_flex_consumption` resource (azurerm 4.x). This is preferred over the
# older azurerm_service_plan(FC1)+azurerm_linux_function_app+functionAppConfig site-config
# combo because the dedicated resource maps 1:1 to the ARM functionAppConfig shape
# (runtime, instance memory, scale-and-concurrency, MI-auth deployment storage).
#
# Design: ONE Function App per Flex plan (Flex Consumption requires a dedicated plan per app).
# The composition root instantiates this module once per host (auth/bff/webhooks/projection/
# indexer/notifications/outbox) OR twice (api + workers) — see the env-level consolidation var.

resource "azurerm_service_plan" "this" {
  name                = var.service_plan_name
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "FC1" # FC1 = Flex Consumption
  tags                = var.tags
}

resource "azurerm_function_app_flex_consumption" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.this.id

  # Deployment package storage via managed identity (no keys).
  storage_container_type      = "blobContainer"
  storage_container_endpoint  = var.deployment_storage_container_endpoint
  storage_authentication_type = "SystemAssignedIdentity"

  # functionAppConfig: runtime + scale-and-concurrency.
  runtime_name           = var.runtime_name
  runtime_version        = var.runtime_version
  instance_memory_in_mb  = var.instance_memory_in_mb
  maximum_instance_count = var.maximum_instance_count

  https_only = true

  # Secret values arrive only as @Microsoft.KeyVault(...) references (merged by the caller);
  # App Insights connection string is injected when provided.
  app_settings = merge(
    var.app_settings,
    var.app_insights_connection_string == null ? {} : {
      "APPLICATIONINSIGHTS_CONNECTION_STRING" = var.app_insights_connection_string
    }
  )

  site_config {
    application_insights_connection_string = var.app_insights_connection_string
  }

  # System-assigned managed identity → passwordless access to KV / Service Bus / Storage / SQL.
  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

# --- RBAC role assignments for the system-assigned identity ---

resource "azurerm_role_assignment" "kv_secrets_user" {
  count                = var.key_vault_id == null ? 0 : 1
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_function_app_flex_consumption.this.identity[0].principal_id
}

resource "azurerm_role_assignment" "sb_sender" {
  count                = var.grant_servicebus_sender && var.servicebus_namespace_id != null ? 1 : 0
  scope                = var.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_function_app_flex_consumption.this.identity[0].principal_id
}

resource "azurerm_role_assignment" "sb_receiver" {
  count                = var.grant_servicebus_receiver && var.servicebus_namespace_id != null ? 1 : 0
  scope                = var.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_function_app_flex_consumption.this.identity[0].principal_id
}

resource "azurerm_role_assignment" "storage_blob_owner" {
  count                = var.storage_blob_owner_scope_id == null ? 0 : 1
  scope                = var.storage_blob_owner_scope_id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_function_app_flex_consumption.this.identity[0].principal_id
}
