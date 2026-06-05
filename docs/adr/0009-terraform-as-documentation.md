# 0009 — Terraform as target-topology documentation (plan, not apply)

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** iac, devops, cost

## Context and Problem Statement

The portfolio's headline is architecture + IaC, but the demo must **run locally
with no Azure cost**. How do we present production-grade infrastructure-as-code
without standing up (and paying for) a live subscription?

## Decision Drivers

- Demonstrate real, reviewable IaC (Flex Consumption, Key Vault, Service Bus,
  SQL, APIM, ACS, Maps, monitoring) with module structure and RBAC.
- **No cloud spend** — nothing applied to a live subscription.
- Reviewers can validate offline.
- **No plaintext secrets** in state or repo.

## Considered Options

1. **Apply to a real subscription** (and tear down) — incurs cost / risk.
2. **No IaC** — describe infrastructure in prose only.
3. **Terraform written as target-topology docs** — `fmt`/`validate`/`plan` green
   offline; `apply` is intentionally never run.

## Decision Outcome

**Chosen option: "Terraform as target-topology documentation."** `infra/terraform`
ships full `modules/` (naming, monitoring, keyvault, storage, sql, servicebus,
communication, maps, functions [Flex `functionAppConfig`], apim, network) and
`environments/dev`. The deliverable is a **green `terraform plan`** run offline
(`-backend=false`); the azurerm remote backend is present but commented so
reviewers can `init`/`plan` locally. Vendor keys are `sensitive` variables from
`TF_VAR_*`; Terraform creates **empty Key Vault secret placeholders** referenced
by app settings — **no plaintext in state**. RBAC over keys (managed identities
granted Key Vault Secrets User, Service Bus Data roles, Storage Blob Data Owner,
SQL AAD) models a passwordless posture.

## Consequences

- **Good:** Production-quality, reviewable IaC with zero cost or risk.
- **Good:** No secrets in state; passwordless RBAC modeled end-to-end.
- **Good:** `terraform-validate` runs in CI (`fmt -check`, `init -backend=false`,
  `validate`, `plan` → PR comment).
- **Bad / trade-off:** `plan` can't catch every apply-time error (quota, drift);
  acceptable since this is documentation, and noted as a known limitation.
- **Neutral:** Pairs with [ADR 0008](0008-oidc-no-long-lived-cloud-secrets-ci.md)
  — the (gated) deploy pipeline would consume these same definitions.

## More Information

- [Architecture plan — Terraform (IaC)](../architecture-plan.md)
