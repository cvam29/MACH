# keyvault

RBAC-authorized Key Vault plus **empty** vendor-secret placeholders and managed-identity
role assignments. Terraform never stores secret values.

## Secret posture
- `enable_rbac_authorization = true` — no access policies.
- `azurerm_key_vault_secret` placeholders are created with `value = ""` and
  `ignore_changes = [value]`. Real values are written out-of-band by a privileged identity
  (portal / CI), so plaintext never enters tfvars or state.
- Function Apps read secrets via `@Microsoft.KeyVault(SecretUri=...)` using their
  system-assigned identity granted **Key Vault Secrets User**.

## Inputs
| Name | Type | Default | Description |
|---|---|---|---|
| `name` | string | — | Vault name (<=24). |
| `resource_group_name` | string | — | RG. |
| `location` | string | — | Region. |
| `tenant_id` | string | — | AAD tenant. |
| `sku_name` | string | `standard` | SKU. |
| `secret_placeholder_names` | list(string) | `[]` | Empty secrets to create. |
| `admin_object_ids` | list(string) | `[]` | Key Vault Administrator grantees. |
| `secrets_user_principal_ids` | list(string) | `[]` | Key Vault Secrets User grantees. |
| `tags` | map(string) | `{}` | Tags. |

## Outputs
| Name | Description |
|---|---|
| `id` | Vault resource ID. |
| `name` | Vault name. |
| `vault_uri` | Vault URI. |
| `secret_uris` | Map secret-name → versionless URI for KV references. |
