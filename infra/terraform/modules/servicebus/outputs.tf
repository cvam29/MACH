output "namespace_id" {
  description = "Service Bus namespace resource ID."
  value       = azurerm_servicebus_namespace.this.id
}

output "namespace_name" {
  description = "Service Bus namespace name."
  value       = azurerm_servicebus_namespace.this.name
}

output "namespace_endpoint" {
  description = "Fully qualified namespace endpoint (for the AMQP/MI connection)."
  value       = "${azurerm_servicebus_namespace.this.name}.servicebus.windows.net"
}

output "topic_ids" {
  description = "Map of topic name -> resource ID."
  value       = { for k, v in azurerm_servicebus_topic.this : k => v.id }
}

output "subscription_ids" {
  description = "Map of 'topic/subscription' -> resource ID."
  value       = { for k, v in azurerm_servicebus_subscription.this : k => v.id }
}
