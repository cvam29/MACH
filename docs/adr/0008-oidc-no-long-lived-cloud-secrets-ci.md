# 0008 — OIDC federation in CI/CD: no long-lived cloud secrets

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** ci-cd, security, devops

## Context and Problem Statement

The deploy pipeline needs to authenticate to Azure to publish Functions and the
static web app. Storing a long-lived service-principal secret (or cloud
credentials) in GitHub is a standing liability. How should CI/CD authenticate to
the cloud?

## Decision Drivers

- **No long-lived cloud secrets** anywhere in the repo or CI configuration.
- Auditable, scoped, short-lived credentials.
- Demonstrate the current best-practice deploy posture.
- Keep deploy **gated** (this demo never actually applies to a subscription).

## Considered Options

1. **Stored SP secret / publish profile** in GitHub Secrets.
2. **OIDC federated credentials** — GitHub Actions exchanges its OIDC token for a
   short-lived Azure access token via a federated identity (no stored secret).

## Decision Outcome

**Chosen option: "OIDC federated credentials."** `deploy.yml` is
`workflow_dispatch`, bound to a protected GitHub **Environment** (`azure-demo`)
with a required reviewer, and logs in to Azure via **OIDC federation** —
**no stored cloud secrets**. Jobs `deploy-functions` (`dotnet publish` +
functions-action) and `deploy-swa` exist as production-grade documentation and
never fire in the demo. `ci.yml` (lint, build/test, terraform plan, security
scans) needs no cloud credentials at all.

## Consequences

- **Good:** Zero long-lived cloud secrets; credentials are short-lived,
  subject-scoped, and auditable.
- **Good:** Required-reviewer environment gate prevents accidental deploys.
- **Good:** `id-token: write` + a federated identity is the recommended pattern,
  a clear hiring signal.
- **Bad / trade-off:** Requires a one-time federated-credential setup on the
  Azure identity; documented but not executed here.
- **Neutral:** Vendor secrets (commercetools, Adyen, …) are handled separately as
  Terraform `sensitive` vars / Key Vault placeholders, never in CI.

## More Information

- [Architecture plan — CI/CD](../architecture-plan.md)
- Related: [ADR 0009](0009-terraform-as-documentation.md) (no plaintext in state).
