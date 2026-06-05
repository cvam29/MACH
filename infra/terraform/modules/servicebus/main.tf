# Service Bus namespace with topics (payments / catalog / content / notifications) and
# per-topic subscriptions. Each subscription enables dead-lettering + a max delivery count
# so poison messages land in the DLQ. RBAC roles (Data Sender/Receiver) replace SAS keys.

locals {
  # Flatten the topic -> [subscriptions] map into a keyed set for for_each.
  subscriptions = merge([
    for topic, subs in var.topics : {
      for sub in subs : "${topic}/${sub}" => {
        topic        = topic
        subscription = sub
      }
    }
  ]...)
}

resource "azurerm_servicebus_namespace" "this" {
  name                = var.namespace_name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  tags                = var.tags
}

resource "azurerm_servicebus_topic" "this" {
  for_each = var.topics

  name                  = each.key
  namespace_id          = azurerm_servicebus_namespace.this.id
  partitioning_enabled  = false
  support_ordering      = true
  default_message_ttl   = "P14D"
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_subscription" "this" {
  for_each = local.subscriptions

  name               = each.value.subscription
  topic_id           = azurerm_servicebus_topic.this[each.value.topic].id
  max_delivery_count = var.max_delivery_count

  # DLQ settings — the core of the at-least-once / poison-message handling design.
  dead_lettering_on_message_expiration      = true
  dead_lettering_on_filter_evaluation_error = true
  lock_duration                             = "PT1M"
  default_message_ttl                       = "P14D"
}

# --- RBAC (passwordless) instead of SAS connection strings ---

resource "azurerm_role_assignment" "senders" {
  for_each             = toset(var.data_sender_principal_ids)
  scope                = azurerm_servicebus_namespace.this.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = each.value
}

resource "azurerm_role_assignment" "receivers" {
  for_each             = toset(var.data_receiver_principal_ids)
  scope                = azurerm_servicebus_namespace.this.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = each.value
}
