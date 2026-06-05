# 0001 — Adopt a MACH / composable-commerce architecture

- **Status:** Accepted
- **Date:** 2026-06-05
- **Tags:** architecture, foundation

## Context and Problem Statement

This is a portfolio piece whose headline is **architecture and platform
engineering**. We must choose an overall shape for a commerce demo that
demonstrates modern, hireable skills rather than reinventing a monolithic
storefront. How should commerce capabilities be structured and sourced?

## Decision Drivers

- Demonstrate **best-of-breed SaaS composition** and clean module boundaries.
- Each capability independently deployable / replaceable ("composable").
- Cloud-native, event-driven, observable — first-class IaC and CI/CD.
- Avoid building (and maintaining) commerce/search/CMS/payments from scratch.

## Considered Options

1. **Monolithic platform** (e.g. a single open-source commerce app, customized).
2. **Headless-but-single-vendor suite** (one vendor for commerce + CMS + search).
3. **MACH / composable** — Microservices, API-first, Cloud-native, Headless,
   with a best-of-breed vendor per capability behind a hexagonal core.

## Decision Outcome

**Chosen option: "MACH / composable."** A hexagonal core (`Mach.Domain` +
`Mach.Application` **ports**) is vendor-agnostic; each vendor is sealed inside a
single **translator** project implementing a port; deployable **Functions hosts**
("doors") compose them. commercetools (commerce + identity), Contentstack
(content), Algolia (search), and Adyen (payments) are wired as real sandboxes.

## Consequences

- **Good:** Swapping a vendor changes exactly **one** translator folder — the
  visible proof of composability, enforced by an ArchUnitNET dependency test
  (Doors → Brain + Translators; Translators → Brain ports only; Brain → nothing).
- **Good:** Each capability scales/deploys independently; async flows are
  event-driven over Service Bus.
- **Good:** Maximises portfolio signal across the exact "MACH" vocabulary
  employers screen for.
- **Bad / trade-off:** More moving parts and more vendor accounts than a
  monolith; mitigated by offline stubs/fallbacks and a single `run.ps1`.
- **Neutral:** Establishes the ports/contracts every later ADR builds on.

## More Information

- [Architecture plan](../architecture-plan.md)
- [README — MACH mapping](../../README.md)
