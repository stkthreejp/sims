# Backend

## Controllers (43 total)

### Authentication & Users
| Controller | Route | Description |
|---|---|---|
| `AuthController` | `/api/auth` | Login, logout, token refresh, password change |
| `UsersController` | `/api/users` | User CRUD, roles, permissions, delegations |

### Core Entities
| Controller | Route | Description |
|---|---|---|
| `AgentsController` | `/api/agents` | Agent management |
| `CarriersController` | `/api/carriers` | Carrier/insurance company management |
| `InsuredsController` | `/api/insureds` | Insured party management |
| `PoliciesController` | `/api/policies` | Policy operations |
| `QuotesController` | `/api/quotes` | Quote management |
| `SubmissionsController` | `/api/submissions` | Submission CRUD and lifecycle |

### Submission Components
| Controller | Route | Description |
|---|---|---|
| `SubmissionDriversController` | `/api/submissions/{id}/drivers` | Driver records |
| `SubmissionLocationsController` | `/api/submissions/{id}/locations` | Coverage locations |
| `SubmissionVehiclesController` | `/api/submissions/{id}/vehicles` | Vehicle information |
| `SubmissionGLController` | `/api/submissions/{id}/gl` | General Liability details |
| `SubmissionIMController` | `/api/submissions/{id}/im` | Inland Marine details |
| `SubmissionPriorCarriersController` | `/api/submissions/{id}/priorcarriers` | Prior coverage |
| `SubmissionSupplementalController` | `/api/submissions/{id}/supplemental` | Supplemental info |

### Commissions
| Controller | Route | Description |
|---|---|---|
| `AgentCommissionsController` | `/api/agentcommissions` | Agent commission tracking |
| `CarrierCommissionsController` | `/api/carriercommissions` | Carrier commission tracking |

### Billing & Accounting
| Controller | Route | Description |
|---|---|---|
| `InvoicesController` | `/api/billing/invoices` | Invoice management |
| `ReceiptsController` | `/api/billing/receipts` | Receipt tracking |
| `CashApplicationController` | `/api/billing/cashapplication` | Payment application |
| `CashDistributionController` | `/api/billing/cashdistribution` | Payment distribution |
| `DisbursementsController` | `/api/billing/disbursements` | Vendor disbursements |
| `PayeeStatementsController` | `/api/billing/payeestatements` | Payee reconciliation |
| `ActivityController` | `/api/billing/activity` | Transaction activity |
| `BalanceController` | `/api/billing/balance` | Balance inquiries |
| `RollupController` | `/api/billing/rollup` | Financial rollups |
| `PeriodCloseController` | `/api/billing/periodclose` | Month-end close |
| `VoidController` | `/api/billing/void` | Transaction voiding |

### QBO Integration
| Controller | Route | Description |
|---|---|---|
| `QboController` | `/api/qbo` | QBO auth, sync status |
| `QboWebhookController` | `/api/webhooks/qbo` | QBO webhook receiver |

### Documents & Communication
| Controller | Route | Description |
|---|---|---|
| `AttachmentsController` | `/api/attachments` | File upload/download |
| `DocumentTemplatesController` | `/api/documenttemplates` | Template management |
| `DocumentGenerationController` | `/api/documents` | Generate from template |
| `InboundEmailsController` | `/api/inboundemails` | Email ingestion |
| `NotesController` | `/api/notes` | Notes and comments |

### Tasks
| Controller | Route | Description |
|---|---|---|
| `TasksController` | `/api/tasks` | Task CRUD, assignment, completion |

### Admin
| Controller | Route | Description |
|---|---|---|
| `FeesController` | `/api/admin/fees` | Fee structure administration |
| `EscalationRulesController` | `/api/admin/escalationrules` | Escalation rule setup |
| `HolidayCalendarController` | `/api/admin/holidaycalendar` | Holiday definitions |
| `SystemEventsController` | `/api/admin/systemevents` | System event log |
| `TaskTypesAdminController` | `/api/admin/tasktypes` | Task type definitions |
| `WorkflowTemplatesController` | `/api/admin/workflowtemplates` | Workflow templates |

---

## Services (36 total)

### Sales & Submissions
| Service | Description |
|---|---|
| `SubmissionService` | Submission lifecycle, status transitions |
| `QuoteService` | Quote generation and conversion to policy |
| `PolicyService` | Policy management, endorsements, renewals, cancellations |
| `InsuredService` | Insured party management |

### Parties
| Service | Description |
|---|---|
| `AgentService` | Agent administration |
| `CarrierService` | Carrier/company management |
| `AgentCommissionService` | Commission calculations |
| `CarrierCommissionService` | Carrier commission tracking |

### User & Auth
| Service | Description |
|---|---|
| `AuthService` | JWT generation, refresh, Microsoft token validation |
| `UserService` | User CRUD, delegation, permission assignment |

### Billing & Accounting
| Service | Description |
|---|---|
| `InvoicingService` | Invoice generation and tracking |
| `CashApplicationService` | Apply payments to invoices |
| `CashDistributionService` | Distribute funds across payees |
| `DisbursementService` | Vendor payment processing |
| `ReceiptsService` | Receipt tracking |
| `FeeCalculationService` | Compute fees based on premium and rules |
| `FeeAdminService` | Fee rule CRUD |
| `PayeeStatementService` | Payee reconciliation statements |
| `PeriodCloseService` | Month-end close workflow |
| `RollupService` | Financial aggregation for reporting |
| `LedgerService` | General ledger operations |
| `VoidService` | Transaction voiding logic |

### Workflow & Tasks
| Service | Description |
|---|---|
| `TaskInstanceService` | Task create, assign, complete, status tracking |
| `TaskTypeService` | Task type definitions |
| `WorkflowEngineService` | Workflow state transitions |
| `WorkflowTemplateService` | Workflow template management |
| `EscalationRuleService` | Escalation rule configuration |
| `SystemEventService` | System-wide event logging |

### Documents & Communication
| Service | Description |
|---|---|
| `DocumentTemplateService` | Template CRUD |
| `AttachmentService` | File upload/download via Azure Blob |
| `InboundEmailService` | Email processing and routing |
| `NoteService` | Note and comment management |
| `ActivityService` | Audit trail logging |

### Utilities
| Service | Description |
|---|---|
| `DueDateFormulaService` | Parse and evaluate due date formulas |
| `HolidayCalendarService` | Holiday lookups for due date calculation |

---

## Domain Entities (73 total)

### Core Insurance
- `Submission` — Insurance application
- `Quote` — Rated quote from a submission
- `Policy` — Issued insurance policy
- `PolicyTransaction` — Endorsement, cancellation, or renewal on a policy
- `Insured` — Party being insured
- `Agent` / `AgentContact` / `AgentLocation` — Insurance agent
- `Carrier` / `CarrierContact` / `CarrierLineOfBusiness` — Insurance company

### Submission Components
- `SubmissionDriver`, `SubmissionVehicle`, `SubmissionEquipment`
- `SubmissionLocation`, `SubmissionGLClassification`, `SubmissionGLCoverages`
- `SubmissionIMCoverages`, `SubmissionPriorCarrier`, `SubmissionSupplemental`

### Commissions
- `AgentCommission` — Agent commission rates by LOB
- `CarrierCommission` — Carrier commission rates by LOB

### Accounting & Billing
- `Invoice` / `InvoiceLine` — Bills issued
- `Receipt` — Payments received
- `CashApplication` — Payment applied to invoice
- `CashDistributionBatch` / `CashMovementInstruction` — Fund distribution
- `Disbursement` / `DisbursementLine` — Vendor payments
- `Payable` — Outstanding liability
- `Payee` / `PayeeStatement` / `PayeeStatementLine` — Payee reconciliation
- `AccountingPeriod` — Month/period definitions
- `LedgerAccount` / `LedgerTransaction` — General ledger
- `JournalEntryRollup` / `PendingQboSync` — QBO sync queue
- `GlAccountMap` — GL account mapping
- `FeeDefinition` / `FeeRuleVersion` / `FeePremiumBracket` / `FeeStateTaxability` / `FeeAuditLog` — Fee engine
- `PeriodCloseChecklistItem` — Month-end checklist
- `QboOAuthToken` — QBO OAuth credentials

### Users & Security
- `User` / `Role` / `Permission` / `RolePermission` — RBAC
- `UserDelegation` — User proxy/delegation
- `RefreshToken` — JWT refresh tokens

### Workflow & Tasks
- `TaskType` / `TaskInstance` / `TaskAuditEntry` — Task system
- `WorkflowTemplate` / `WorkflowStep` — Workflow definitions
- `EscalationRule` — Task escalation configuration
- `SystemEvent` — System-wide event log

### Documents & Communication
- `Attachment` — File attached to any entity
- `DocumentTemplate` — Reusable document template
- `InboundEmail` / `EmailAttachment` — Ingested emails
- `Note` — Comment on any entity
- `HolidayCalendar` — Holiday dates for due date calculation

---

## Enumerations (18 total)

`BusinessEntityType`, `DocumentType`, `EmailAttachmentDocumentType`, `InsuredType`, `OperatingRadius`, `PolicyLineOfBusiness`, `PolicyStatus`, `PolicyTransactionStatus`, `QuoteStatus`, `SubmissionStatus`, `TaskAuditAction`, `TaskEntityType`, `TaskInstanceStatus`, `TaskPriority`, `TemplateEntityType`, `TransactionType`, `UserStatus`, `VehicleClass`
