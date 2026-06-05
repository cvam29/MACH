# Azure SQL logical server + database, AAD-only authentication (passwordless), and a
# firewall rule allowing Azure services. SQL auth is disabled so Function App managed
# identities authenticate via AAD — no SQL admin password anywhere in state.

resource "azurerm_mssql_server" "this" {
  name                          = var.server_name
  resource_group_name           = var.resource_group_name
  location                      = var.location
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  # AAD-only: no SQL administrator_login/password set at all.
  azuread_administrator {
    login_username              = var.aad_admin_login
    object_id                   = var.aad_admin_object_id
    tenant_id                   = var.tenant_id
    azuread_authentication_only = true
  }

  tags = var.tags
}

resource "azurerm_mssql_database" "this" {
  name        = var.database_name
  server_id   = azurerm_mssql_server.this.id
  sku_name    = var.sku_name
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb = 2
  tags        = var.tags
}

# "Allow Azure services" rule: 0.0.0.0 start+end is the documented sentinel for that.
resource "azurerm_mssql_firewall_rule" "allow_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}
