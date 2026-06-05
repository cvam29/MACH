output "server_id" {
  description = "SQL logical server resource ID."
  value       = azurerm_mssql_server.this.id
}

output "server_fqdn" {
  description = "Fully qualified domain name of the SQL server."
  value       = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "database_id" {
  description = "Database resource ID."
  value       = azurerm_mssql_database.this.id
}

output "database_name" {
  description = "Database name."
  value       = azurerm_mssql_database.this.name
}

output "ado_net_connection_string" {
  description = <<-EOT
    AAD-passwordless ADO.NET connection string template. No credentials embedded — the
    Function App's managed identity supplies the token via 'Authentication=Active Directory Default'.
  EOT
  value       = "Server=tcp:${azurerm_mssql_server.this.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.this.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;"
}
