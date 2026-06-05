# Key Vault using RBAC authorization (enable_rbac_authorization = true) — no access policies.
# Vendor secret values are NEVER set by Terraform: we create empty placeholder secret
# objects so @Microsoft.KeyVault(...) references resolve, then a privileged identity sets
# the real values out-of-band. This keeps all plaintext secrets out of Terraform state.

resource "azurerm_key_vault" "this" {
  name                       = var.name
  resource_group_name        = var.resource_group_name
  location                   = var.location
  tenant_id                  = var.tenant_id
  sku_name                   = var.sku_name
  rbac_authorization_enabled = true
  purge_protection_enabled   = true
  soft_delete_retention_days = 7
  tags                       = var.tags
}

# --- RBAC role assignments (preferred over access policies) ---

resource "azurerm_role_assignment" "admins" {
  for_each             = toset(var.admin_object_ids)
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = each.value
}

resource "azurerm_role_assignment" "secrets_users" {
  for_each             = toset(var.secrets_user_principal_ids)
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value
}

# --- EMPTY secret placeholders (no value attribute) ---
# NOTE: `value` is intentionally an empty string. Once a real secret is written by a
# privileged identity, ignore_changes prevents Terraform from reverting it back to empty.
resource "azurerm_key_vault_secret" "placeholders" {
  for_each     = toset(var.secret_placeholder_names)
  name         = each.value
  value        = "" # placeholder only — real value set out-of-band
  key_vault_id = azurerm_key_vault.this.id
  content_type = "placeholder/managed-out-of-band"
  tags         = var.tags

  lifecycle {
    ignore_changes = [value, content_type]
  }

  # The deploying identity needs the Administrator role assignment to write secrets.
  depends_on = [azurerm_role_assignment.admins]
}
