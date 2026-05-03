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

## QuickBooks Online (Accounting Sync)

- **Purpose:** Syncs journal entries from SIMS accounting to QBO
- **Environment:** Sandbox (switch to production when live)
- **Auth:** OAuth 2.0 with stored refresh token (`QboOAuthToken` entity)
- **Sync flow:**
  1. Accounting events generate `JournalEntryRollup` records
  2. `QboJournalDriver` converts rollups to QBO journal entries
  3. Failed syncs queue in `PendingQboSync` and are retried by `QboSyncRetryWorker`
- **Webhook:** `POST /api/webhooks/qbo` receives real-time change notifications
- **Config keys:** `Qbo:ClientId`, `Qbo:ClientSecret`, `Qbo:RefreshToken`, `Qbo:RealmId`, `Qbo:Environment`

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

- **Purpose:** Alternative to QBO — export journal entries as CSV
- **Service:** `CsvJournalDriver` implements `IJournalDriver` interface
- **Use case:** Clients not on QBO, or for period-end batch export

## Journal Driver Architecture

The accounting sync uses a plugin-style `IJournalDriver` interface, allowing multiple export targets:
- `QboJournalDriver` — Live sync to QuickBooks Online
- `CsvJournalDriver` — CSV file export

New drivers can be added by implementing `IJournalDriver` without changing core accounting logic.
