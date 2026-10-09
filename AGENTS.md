# AGENTS.md

This repo is the SignatureAPI ASP.NET Core starter: a template app that sends a PDF for e-signature with SignatureAPI, tracks it with webhooks, and links to the signed PDF. Keep it a minimal, idiomatic template.

## Run and test

```bash
npx --yes signatureapi init          # writes SIGNATUREAPI_KEY to .env
dotnet run --project src/SignatureApiStarter   # http://localhost:3000
npx --yes signatureapi listen --forward-to http://127.0.0.1:3000/webhooks/signatureapi
dotnet test                          # unit tests, SignatureAPI mocked
dotnet format --verify-no-changes    # lint
```

`e2e/` is a real test-mode run (Playwright). It needs a key and `signatureapi listen`; see `e2e/README.md`. Only `e2e/app.config.ts` is specific to this repo.

Schema changes go through EF Core migrations: `dotnet tool restore`, then `dotnet ef migrations add <Name> --project src/SignatureApiStarter`. Migrations apply on startup.

## Where things live

- `src/SignatureApiStarter/SignatureApi/SignatureApiClient.cs`: all SignatureAPI HTTP calls.
- `src/SignatureApiStarter/SignatureApi/WebhookEndpoint.cs`: webhook verification and event handling.
- `src/SignatureApiStarter/Pages/Index.cshtml(.cs)`: the page and the form post (`POST /agreements`).
- `src/SignatureApiStarter/Program.cs`: configuration, routes, and the signed PDF redirect.
- `src/SignatureApiStarter/Data/`: the `Agreement` model and `AppDbContext`.
- `tests/SignatureApiStarter.Tests/`: xUnit tests with `WebApplicationFactory` and a fake `HttpMessageHandler`.

## SignatureAPI rules

- The OpenAPI spec at https://spec.signatureapi.com/openapi.yaml is the source of truth for endpoints, fields and event types. Check it, not memory.
- Verify webhooks with the Standard Webhooks library over the **raw body bytes** and the `webhook-id`, `webhook-timestamp` and `webhook-signature` headers. Never hand-roll the HMAC. A failed check returns 401 and does nothing else.
- Events carry ids only (`data.envelope_id`), not a nested envelope. Find the agreement by envelope id. Answer 2xx quickly, ignore unknown types and envelopes, and never move a final status back.
- Deliverable URLs expire after an hour. Fetch them on demand.
- Use test keys (`key_test_...`) while developing. Never commit `.env`.

## SignatureAPI skills

The SignatureAPI skills are in `.agents/skills/` and in the Claude Code plugin enabled by `.claude/settings.json`, both pinned to one commit of github.com/signatureapi/skills. Use them for integration work, docs lookups and diagnosing envelopes. Bump the pin with `scripts/update-agent-skills.sh <commit-sha>`.
