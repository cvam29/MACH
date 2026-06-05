# MACH — Terraform IaC (as documentation)

This tree models the **target Azure topology** for the MACH composable-commerce demo. It is
**IaC as documentation: plan, don't apply.** `terraform fmt`, `init -backend=false`, and
`validate` (and a `plan` against a real subscription) are the deliverables — the demo runs
locally and incurs **no Azure cost**. Nothing here is applied to a live subscription.

## What it models
- **naming** — name + tag generation (no resources).
- **monitoring** — Log Analytics + Application Insights.
- **keyvault** — RBAC Key Vault + EMPTY vendor-secret placeholders + role assignments.
- **storage** — Function host storage + Flex deployment container (keyless).
- **sql** — Azure SQL server + database, AAD-only admin, firewall rule.
- **servicebus** — namespace + topics (payments/catalog/content/notifications) + subscriptions + DLQ.
- **communication** — Azure Communication Services + Email + Azure-managed domain.
- **maps** — Azure Maps account; key surfaced into Key Vault.
- **functions** — Flex Consumption Function App (Linux, dotnet-isolated) + MI + RBAC.
- **apim** — API Management + sample API + policy (rate-limit/CORS/session) + KV named values.

Composition root: `environments/dev/`.

## Secret posture (no plaintext anywhere)
- Vendor keys are `variable { sensitive = true }` sourced from `TF_VAR_*` — never in tfvars/state.
- Key Vault gets **empty** secret placeholders; real values are written **out-of-band**.
- Function Apps reference secrets via `@Microsoft.KeyVault(SecretUri=...)` using their
  **system-assigned managed identity** (Key Vault Secrets User).
- **RBAC over keys** throughout: Service Bus Data Sender/Receiver, Storage Blob Data Owner,
  Key Vault Secrets User, SQL AAD-only.

## Function App topology
`environments/dev` exposes `consolidate_function_apps`:
- `false` (default) — one Function App per host (auth/bff/webhooks/projection/indexer/
  notifications/outbox); cleanest isolation + independent scaling.
- `true` — two consolidated apps (api + workers); fewer Flex plans / lower cost.

Flex Consumption requires one app per plan, so the `functions` module is instantiated per app.

## How to run (offline / reviewer)
From `infra/terraform/environments/dev`:

```bash
terraform init -backend=false      # init without remote state / Azure
terraform validate                 # schema + reference validation
```

Formatting (from `infra/terraform`):

```bash
terraform fmt -check -recursive    # verify formatting
```

## How to plan against a real subscription
Provide identity + subscription context and run a plan (still never applied):

```bash
export ARM_SUBSCRIPTION_ID=...        # or TF_VAR_subscription_id
export TF_VAR_tenant_id=...
export TF_VAR_sql_aad_admin_object_id=...
export TF_VAR_kv_admin_object_ids='["<your-object-id>"]'
# Vendor keys are NOT needed for plan (placeholders are empty); set them only when
# populating Key Vault out-of-band.
terraform init                       # uses the azurerm remote backend in backend.tf
terraform plan
```

For fully offline init, either keep the `azurerm` backend and pass `-backend=false`, or
uncomment the commented `local` backend in `environments/dev/backend.tf`.

## Provider versions
`azurerm ~> 4.0`, `azuread ~> 3.0`, `random ~> 3.6`. Terraform `>= 1.9` (tested with 1.15.x).
