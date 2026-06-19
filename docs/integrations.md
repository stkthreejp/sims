# Integrations

## Microsoft Azure AD (Authentication)

- **Type:** OAuth 2.0 / OpenID Connect via MSAL
- **Tenant:** `49037468-6b8e-4c49-a58e-2588bd7b2706`
- **Client ID:** `a3c3a9ab-09a7-4b9c-8b1e-0d97fe97ff6f`
- **Flow:** Frontend uses `@azure/msal-browser` to obtain a Microsoft token, which is sent to `POST /api/auth/microsoft-login`. The backend validates it and issues a SIMS JWT.
- **Config keys:** `MicrosoftAuth:TenantId`, `MicrosoftAuth:ClientId`

## Microsoft Graph API (Email)

- **Purpose:** Ingests emails from a shared mailbox into the SIMS inbox
- **Mailbox:** `submissionstest@longleaf-ins.com`
- **Worker:** `EmailIngestionWorker` polls on a schedule and calls `EmailIngestionService`
- **Auth:** `ClientSecretCredential` using the Azure AD app registration
- **Config keys:** `GraphApi:ClientSecret`, `GraphApi:MailboxAddress`

## Xero (Accounting Sync)

- **Purpose:** Syncs journal entries from SIMS accounting to Xero as Manual Journals. This is the sole accounting integration — QBO was removed pre-launch.
- **Connection type:** Xero **Custom connection** (one-to-one, single organisation), using the OAuth2 **client-credentials** grant. There is no interactive authorization-code flow and **no refresh token** — the backend mints a short-lived (~30 min) access token directly from the client id/secret and re-mints on expiry.
- **Auth:** `XeroTokenService` (token cached in `XeroOAuthToken` / `xero_oauth_tokens`). Every API call sends `Authorization: Bearer` plus the `xero-tenant-id` header.
- **Secrets:** stored in Key Vault under **flat** names (`XeroClientID`, `XeroClientSecret`, `XeroTenantId`) because `:` is not allowed in Key Vault secret names. The DI binding applies these flat keys over the `Xero:*` section (the section is the dev/appsettings fallback). Config lookups are case-insensitive.
- **Account mapping:** Manual Journals reference accounts by **account code**. Configure `GlAccountMap` rows with `ExternalSystem = "Xero"` whose `ExternalId` holds the Xero account code.
- **Sync flow:**
  1. Accounting events generate `JournalEntryRollup` records (`DriverType = "Xero"`)
  2. `XeroJournalDriver` converts each transaction group into a Manual Journal (one signed `LineAmount` per line: positive = debit, negative = credit; `Status = POSTED`)
  3. Failed syncs queue in `PendingJournalSync` (`pending_journal_syncs`) and are retried by `JournalSyncRetryWorker` (queue + worker are driver-agnostic and re-run via `RollupService.ResyncAsync`, which respects each rollup's stored `DriverType`)
- **Config keys:** `Xero:ClientId`, `Xero:ClientSecret`, `Xero:TenantId`, `Xero:Scopes` (+ flat Key Vault names above)
- **Known capability gap:** Xero webhooks do **not** support Manual Journal events (only Contacts / Invoices / Subscriptions), so there is no "Divergent" detection that flags a rollup when the ledger is edited on the accounting side. This existed for QBO and was intentionally not reimplemented.
- **Migration to apply:** the schema change (drop `qbo_oauth_tokens`, rename `pending_qbo_syncs` → `pending_journal_syncs`, add `xero_oauth_tokens`) is generated with `dotnet ef migrations add SwitchAccountingToXero` then `database update`.

## Azure Blob Storage (Documents)

- **Purpose:** Stores all file attachments and generated documents
- **Account:** `smmimsdocuments`
- **Container:** `documents`
- **Max file size:** 50MB
- **Service:** `AzureBlobStorageService` in Infrastructure
- **Config keys:** `Storage:AzureBlobConnectionString`, `Storage:AzureBlobContainerName`

## Google Gemini API (AI Extraction)

- **Purpose:** Extract structured data from uploaded documents and emails (submissions, certificates, etc.)
- **Service:** `GeminiExtractionService` in Infrastructure
- **Config key:** `GeminiApi:ApiKey`
- **Status:** API key not currently configured (feature is wired but inactive)

## Syncfusion (Document Generation)

- **Purpose:** Generate PDFs and documents from templates
- **Services:** `DocumentGenerationService`, `WireSheetPdfService`
- **License:** Community license (stored in Key Vault / user secrets)
- **Config key:** `Syncfusion:LicenseKey`

## CSV Journal Export

- **Purpose:** Alternative to the live Xero sync — export journal entries as CSV
- **Service:** `CsvJournalDriver` implements `IJournalDriver` interface
- **Use case:** Offline/manual import, or for period-end batch export

## Journal Driver Architecture

The accounting sync uses a plugin-style `IJournalDriver` interface, allowing multiple export targets:
- `XeroJournalDriver` — Live sync to Xero (Manual Journals) — **active default**
- `CsvJournalDriver` — CSV file export

New drivers can be added by implementing `IJournalDriver` without changing core accounting logic.
