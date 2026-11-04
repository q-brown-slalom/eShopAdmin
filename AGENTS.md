# eShopAdmin — Agent Guide

This repo follows the organization-wide agent guide in the `eShopOrgMeta` repo:
[eShopOrgMeta/AGENTS.md](../eShopOrgMeta/AGENTS.md). Read it first. It sets the load order
(registry, architecture, standards, ADRs, lessons learned) and the spec-driven workflow.
Specs and plans for this repo live in `eShopOrgMeta/specs/` and `eShopOrgMeta/plans/`, never here.

## Repo-local overrides

None. Org standards apply as written.

## Quick facts

- .NET 10, Aspire 13.6, Blazor Web App (static server-side rendering only), MSTest.Sdk + NSubstitute,
  Playwright (TypeScript).
- Build and test: `dotnet build eShopAdmin.slnx` (warnings are errors), `dotnet test --solution eShopAdmin.slnx`.
- No new third-party package without a tech radar change: every package version comes from the eShop repo.
