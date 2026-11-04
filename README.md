# eShopAdmin

Business rules admin web app for [eShop](../eShop). Business admins find, edit, schedule, publish and
audit runtime business rules here instead of calling BusinessRules.API directly.

- Blazor Web App, static server-side rendering only (no interactive render modes).
- Signs in with Identity.API (client `eshopadmin`, Authorization Code + PKCE). Access needs the
  `RulesEditor` or `RulesPublisher` role. Publishing needs `RulesPublisher`.
- Stores no data of its own. It has its own Aspire AppHost and reaches eShop's Identity.API,
  BusinessRules.API and Catalog.API as external services.

Specs, plans and standards live in the `eShopOrgMeta` repo. See [AGENTS.md](AGENTS.md).

## Prerequisites

- .NET SDK 10 (`global.json` pins `10.0.302`, rolling forward to the latest feature band)
- [Aspire CLI](https://aspire.dev) 13.6 and a container runtime (Docker or Podman) for eShop
- Node.js 22+ (only for the Playwright E2E tests)

## Run

1. **Start eShop first.** From the `eShop` repo, run `aspire run`. eShop's AppHost registers
   eShopAdmin as an Identity.API client (parameter `eshopadmin-url`, default `https://localhost:7250`).
2. From this repo, run `aspire run`.
3. Open `https://localhost:7250` and sign in as a user with a rules role, for example `alice`
   (publisher) or `bob` (editor), password `Pass123$` in local development.

The AppHost has its own dashboard at `https://localhost:19890`, so it runs next to eShop's.

### Where the eShop services are found

The AppHost declares the eShop services as parameters. Override them (for example in user secrets or
with `Parameters__identity-url`) when eShop runs somewhere else.

| Parameter | Default |
| --------- | ------- |
| `identity-url` | `https://localhost:5243` |
| `businessrules-url` | `http://localhost:5230` |
| `catalog-url` | `http://localhost:5222` |

Setting `ESHOP_USE_HTTP_ENDPOINTS=1` selects the `http` launch profile (`http://localhost:5250`),
as in eShop. Use it together with eShop's `ESHOP_USE_HTTP_ENDPOINTS=1` and set `identity-url` to
`http://localhost:5223` and `eshopadmin-url` in eShop to `http://localhost:5250`.

## Test

```powershell
dotnet test --solution eShopAdmin.slnx
```

End-to-end tests use Playwright and need eShop running (step 1 above):

```powershell
npm ci
npx playwright install chromium
$env:PASSWORD = 'Pass123$'
npm run test:e2e
```

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `ADMIN_URL` | `https://localhost:7250` | Where eShopAdmin is served |
| `CATALOG_URL` | `http://localhost:5222` | Where Catalog.API listens (the draft editor spec compares its type picker with it) |
| `PUBLISHER_USERNAME` | `alice` | User with the `RulesPublisher` role |
| `EDITOR_USERNAME` | `bob` | User with the `RulesEditor` role |
| `PASSWORD` | none (required) | Password of both users |
| `PUBLISHER_DISPLAY_NAME` | `Alice` | Name shown in the header for the publisher |

Playwright starts the AppHost itself unless it is already running.

## CI

Three workflows in `.github/workflows` run on pull requests. `pr-validation.yml` and `playwright.yml` also run on pushes
to `main`; `markdownlint.yml` runs on pull requests only.

- `pr-validation.yml` builds and tests `eShopAdmin.slnx` (skipped for Markdown-only changes).
- `markdownlint.yml` lints all Markdown with `.markdownlint.json`.
- `playwright.yml` runs the E2E tests against both AppHosts in one job:
  1. It fails at once with a clear message when the repository variable `ESHOP_REPOSITORY` isn't set, then checks out
     eShop (that repository, branch `main`) into `eshop/`.
  2. It starts eShop's AppHost in the background with `ESHOP_USE_HTTP_ENDPOINTS=1` and
     `Parameters__eshopadmin-url=http://localhost:5250`, then waits up to 5 minutes for Identity.API
     (`http://localhost:5223/.well-known/openid-configuration`) and BusinessRules.API
     (`http://localhost:5230/health`). The wait stops early if the eShop process dies.
  3. `npm run test:e2e` starts eShopAdmin's AppHost through Playwright's `webServer`, with
     `Parameters__identity-url=http://localhost:5223`, and signs in as `alice` and `bob`.
  4. The `playwright-report/` folder is uploaded as an artifact for 30 days.

The two AppHosts use different dashboard, OTLP and resource-service ports, so they run side by side.
