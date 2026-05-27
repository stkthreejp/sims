# SIMS AI Review Runbook

## Goal

Run a repeatable, evidence-backed review of SIMS using narrow read-only reviewer agents.

## Agent Set

- `sims_review_lead`: synthesizes and prioritizes findings.
- `sims_backend_api_reviewer`: controllers, services, DTOs, validation, and API behavior.
- `sims_data_ef_reviewer`: EF Core, migrations, PostgreSQL, soft delete, indexes, and transactions.
- `sims_security_auth_reviewer`: auth, authorization, access boundaries, uploads, secrets, and external inputs.
- `sims_frontend_ui_reviewer`: React workflows, forms, API clients, routing, permissions, and usability.
- `sims_insurance_workflow_reviewer`: MGA domain workflows, bordereaux, rating, surplus lines, accounting, and compliance.
- `sims_qa_test_coverage_reviewer`: targeted test gaps and brittle verification.
- `sims_integrations_ops_reviewer`: background workers, external services, storage, retries, logging, and deploy risk.

## Ground Rules

- Review agents are read-only during audit passes.
- Findings must cite files and symbols or lines.
- Findings must explain user/business impact, not only code smell.
- Security findings must include a plausible attack or misuse path.
- Domain findings must distinguish confirmed defects from open business-rule questions.
- The lead reviewer owns final severity and deduplication.

## Suggested First Audit Prompt

```text
Run a read-only SIMS code review against the current workspace.

Use these project agents:
- sims_backend_api_reviewer
- sims_data_ef_reviewer
- sims_security_auth_reviewer
- sims_frontend_ui_reviewer
- sims_insurance_workflow_reviewer
- sims_qa_test_coverage_reviewer
- sims_integrations_ops_reviewer

Each specialist should return only evidence-backed findings with file paths, symbols or lines, impact, suggested fix, and verification.

Then have sims_review_lead synthesize the results into docs/ai-review/sims-code-review-backlog.md with:
- P0/P1 correctness and data integrity risks
- Security and authorization risks
- Insurance workflow and financial/compliance risks
- UI workflow risks
- Targeted test gaps
- Open business-rule questions
- Recommended fix order

Do not edit application code during this audit.
```

## Manual Verification Commands

```powershell
Set-Location C:\Users\JeremiahPODonovan\SIMS\backend
dotnet build
dotnet test
```

```powershell
Set-Location C:\Users\JeremiahPODonovan\SIMS\frontend
npx tsc --noEmit
npm run build
```
