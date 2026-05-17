# SIMS Compliance Documentation Module — Design Specification

**Version:** 1.0  
**Date:** May 15, 2026  
**Prepared for:** Specialty Market Managers, LLC  
**Integrates with:** SIMS (Specialty Insurance Management System)

---

## 1. Overview

The Compliance Documentation module adds a purpose-built policy and procedure management system directly inside SIMS. It houses internal compliance documents (underwriting guidelines, data security policies, HR procedures, etc.), enforces a structured review/approval workflow, maintains an immutable audit trail, and provides auditor-ready exports — all under the same Azure AD login and UI as the rest of SIMS.

### Goals

- Single place to store, version, and manage all internal compliance documents
- Clear ownership: every document has an assigned owner responsible for keeping it current
- Structured lifecycle: Draft → In Review → Approved → Active → Archived
- Immutable audit trail: every view, edit, approval, and attestation is logged and cannot be altered
- E-attestation: staff can acknowledge ("I have read this policy") with a timestamp and version number
- Review reminders: automated notifications (email/in-app) when documents are approaching their review date
- Auditor export: one-click PDF or Excel report of a document's full history, version changes, and attestation records

---

## 2. Integration Strategy

The compliance module follows all existing SIMS conventions exactly. Nothing in the existing codebase needs to change — this is an additive module.

### Where Things Live

| Layer | Location |
|---|---|
| Domain entities | `SIMS.Domain/Entities/Compliance/` |
| DTOs | `SIMS.Application/DTOs/Compliance/` |
| Service interface + impl | `SIMS.Application/Services/ComplianceService.cs` |
| EF configurations | `SIMS.Infrastructure/Data/Configurations/Compliance/` |
| DbContext additions | Add DbSets to `ApplicationDbContext.cs` |
| Migration | New timestamped migration: `AddComplianceModule` |
| API controller | `SIMS.API/Controllers/ComplianceController.cs` |
| Frontend API client | `frontend/src/api/compliance.api.ts` |
| Frontend types | `frontend/src/types/compliance.ts` |
| Frontend pages | `frontend/src/pages/compliance/` |

### Reused Infrastructure (No Changes Needed)

- **Azure Blob Storage** — document file uploads (already wired into the app)
- **ASP.NET Identity / JWT** — authentication and user context (`User.FindFirstValue(ClaimTypes.NameIdentifier)`)
- **TipTap** — rich text editor for policy body content (already installed)
- **Syncfusion / QuestPDF** — auditor PDF export generation (already installed)
- **Serilog** — structured logging (already wired)
- **Existing Task/Workflow engine** — review reminder tasks can be created as SIMS tasks assigned to document owners
- **`BaseEntity`** — all new entities inherit from it (GUID Id, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
- **`Result<T>` pattern** — all service methods return `Result<T>` or `Result`

---

## 3. Data Model

### 3.1 ComplianceDocument

The primary entity. Represents a single policy or procedure document.

```csharp
// SIMS.Domain/Entities/Compliance/ComplianceDocument.cs
public class ComplianceDocument : BaseEntity
{
    public string DocumentNumber { get; set; } = string.Empty;  // e.g. "POL-UW-001"
    public string Title { get; set; } = string.Empty;
    public ComplianceDocumentCategory Category { get; set; }
    public string? Description { get; set; }
    public string? ContentHtml { get; set; }         // TipTap rich text body
    public string? StoredFileUrl { get; set; }        // Azure Blob URL (optional PDF/DOCX upload)
    public string? StoredFileKey { get; set; }        // Blob key for deletion

    public ComplianceDocumentStatus Status { get; set; } = ComplianceDocumentStatus.Draft;
    public int CurrentVersionNumber { get; set; } = 1;

    // Ownership
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;   // Denormalized for display

    // Review schedule
    public int ReviewFrequencyDays { get; set; } = 365;      // Default: annual
    public DateTime? EffectiveDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public Guid? LastReviewedById { get; set; }
    public string? LastReviewedByName { get; set; }

    // Navigation
    public ICollection<ComplianceDocumentVersion> Versions { get; set; } = new List<ComplianceDocumentVersion>();
    public ICollection<ComplianceAuditEntry> AuditEntries { get; set; } = new List<ComplianceAuditEntry>();
    public ICollection<ComplianceAttestation> Attestations { get; set; } = new List<ComplianceAttestation>();
    public ICollection<ComplianceApprovalRequest> ApprovalRequests { get; set; } = new List<ComplianceApprovalRequest>();
    public ICollection<ComplianceDocumentTag> Tags { get; set; } = new List<ComplianceDocumentTag>();
}
```

### 3.2 ComplianceDocumentStatus (Enum)

```csharp
public enum ComplianceDocumentStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Active = 3,
    Archived = 4
}
```

**Lifecycle:**
```
Draft → InReview (owner submits for approval)
      → Approved (approver approves)
      → Active   (owner publishes)
      → Archived (superseded or retired)
      
Active documents can be re-drafted (creates new version, status back to Draft)
```

### 3.3 ComplianceDocumentCategory (Enum)

```csharp
public enum ComplianceDocumentCategory
{
    UnderwritingGuidelines = 0,
    ClaimsHandling = 1,
    DataSecurity = 2,
    HumanResources = 3,
    Financial = 4,
    RegulatoryCompliance = 5,
    Operations = 6,
    Privacy = 7,
    Other = 99
}
```

### 3.4 ComplianceDocumentVersion

Immutable snapshot of a document's content each time it is approved and published.

```csharp
// SIMS.Domain/Entities/Compliance/ComplianceDocumentVersion.cs
public class ComplianceDocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string? ContentHtml { get; set; }
    public string? StoredFileUrl { get; set; }
    public string? StoredFileKey { get; set; }
    public string ChangeDescription { get; set; } = string.Empty;  // "What changed in this version"

    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    // CreatedAt inherited from BaseEntity — this IS the version timestamp
}
```

### 3.5 ComplianceAuditEntry

**Immutable** audit log. Never updated or soft-deleted — only inserted. This is the source of truth for auditors.

```csharp
// SIMS.Domain/Entities/Compliance/ComplianceAuditEntry.cs
public class ComplianceAuditEntry  // Does NOT inherit BaseEntity — no soft delete, no UpdatedAt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;

    public ComplianceAuditAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }         // JSON: before/after values, status changes, etc.
    public string? IpAddress { get; set; }
    public int? VersionNumber { get; set; }       // Which version this action relates to (if applicable)
}
```

### 3.6 ComplianceAuditAction (Enum)

```csharp
public enum ComplianceAuditAction
{
    Created = 0,
    Viewed = 1,
    ContentEdited = 2,
    StatusChanged = 3,
    ApprovalRequested = 4,
    ApprovalGranted = 5,
    ApprovalRejected = 6,
    Published = 7,
    Archived = 8,
    Attested = 9,
    FileUploaded = 10,
    FileDownloaded = 11,
    OwnerChanged = 12,
    ReviewDateUpdated = 13,
    Exported = 14
}
```

### 3.7 ComplianceAttestation

Records that a specific user acknowledged a specific version of a document.

```csharp
// SIMS.Domain/Entities/Compliance/ComplianceAttestation.cs
public class ComplianceAttestation  // No soft-delete — attestations are permanent records
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;

    public int DocumentVersionNumber { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime AttestedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
```

### 3.8 ComplianceApprovalRequest

Tracks the approval chain for a document being moved from Draft/InReview to Approved.

```csharp
// SIMS.Domain/Entities/Compliance/ComplianceApprovalRequest.cs
public class ComplianceApprovalRequest : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;

    public int DocumentVersionNumber { get; set; }
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public Guid ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;

    public ComplianceApprovalStatus Status { get; set; } = ComplianceApprovalStatus.Pending;
    public string? ApproverComments { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public enum ComplianceApprovalStatus { Pending = 0, Approved = 1, Rejected = 2 }
```

### 3.9 ComplianceDocumentTag

Simple tag association for filtering/search.

```csharp
public class ComplianceDocumentTag : BaseEntity
{
    public Guid DocumentId { get; set; }
    public ComplianceDocument Document { get; set; } = null!;
    public string Tag { get; set; } = string.Empty;
}
```

---

## 4. EF Core Configuration

Each entity gets a configuration class in `SIMS.Infrastructure/Data/Configurations/Compliance/`:

```csharp
// ComplianceDocumentConfiguration.cs
public class ComplianceDocumentConfiguration : IEntityTypeConfiguration<ComplianceDocument>
{
    public void Configure(EntityTypeBuilder<ComplianceDocument> builder)
    {
        builder.ToTable("compliance_documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DocumentNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(d => d.DocumentNumber).IsUnique();
        builder.Property(d => d.Title).IsRequired().HasMaxLength(300);
        builder.Property(d => d.OwnerName).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ContentHtml).HasColumnType("text");

        builder.HasMany(d => d.Versions).WithOne(v => v.Document)
            .HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.AuditEntries).WithOne(a => a.Document)
            .HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Attestations).WithOne(a => a.Document)
            .HasForeignKey(a => a.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.ApprovalRequests).WithOne(r => r.Document)
            .HasForeignKey(r => r.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Tags).WithOne(t => t.Document)
            .HasForeignKey(t => t.DocumentId).OnDelete(DeleteBehavior.Cascade);

        // Global soft-delete filter (matches BaseEntity pattern)
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

// ComplianceAuditEntryConfiguration.cs — NOTE: no soft-delete filter
public class ComplianceAuditEntryConfiguration : IEntityTypeConfiguration<ComplianceAuditEntry>
{
    public void Configure(EntityTypeBuilder<ComplianceAuditEntry> builder)
    {
        builder.ToTable("compliance_audit_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ActorName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Details).HasColumnType("text");
        builder.HasIndex(e => e.DocumentId);
        builder.HasIndex(e => e.Timestamp);
        // No HasQueryFilter — audit entries are never filtered out
    }
}
```

Add DbSets to `ApplicationDbContext.cs`:
```csharp
public DbSet<ComplianceDocument> ComplianceDocuments { get; set; }
public DbSet<ComplianceDocumentVersion> ComplianceDocumentVersions { get; set; }
public DbSet<ComplianceAuditEntry> ComplianceAuditEntries { get; set; }
public DbSet<ComplianceAttestation> ComplianceAttestations { get; set; }
public DbSet<ComplianceApprovalRequest> ComplianceApprovalRequests { get; set; }
public DbSet<ComplianceDocumentTag> ComplianceDocumentTags { get; set; }
```

---

## 5. Migration

Create with: `dotnet ef migrations add AddComplianceModule`

Migration file name: `20260515000000_AddComplianceModule.cs`

This creates six new tables:
- `compliance_documents`
- `compliance_document_versions`
- `compliance_audit_entries`
- `compliance_attestations`
- `compliance_approval_requests`
- `compliance_document_tags`

---

## 6. DTOs

### Request DTOs

```csharp
// ComplianceDocumentCreateDto.cs
public class ComplianceDocumentCreateDto
{
    public string Title { get; set; } = string.Empty;
    public ComplianceDocumentCategory Category { get; set; }
    public string? Description { get; set; }
    public string? ContentHtml { get; set; }
    public Guid OwnerId { get; set; }
    public int ReviewFrequencyDays { get; set; } = 365;
    public DateTime? EffectiveDate { get; set; }
    public List<string> Tags { get; set; } = new();
}

// ComplianceDocumentUpdateDto.cs
public class ComplianceDocumentUpdateDto : ComplianceDocumentCreateDto
{
    public string ChangeDescription { get; set; } = string.Empty;  // Required for version history
}

// ComplianceSubmitForReviewDto.cs
public record ComplianceSubmitForReviewDto(Guid ApproverId, string? Notes);

// ComplianceApprovalDecisionDto.cs
public record ComplianceApprovalDecisionDto(bool Approved, string? Comments);
```

### Response DTOs

```csharp
// ComplianceDocumentListItemDto.cs — Lightweight for list view
public class ComplianceDocumentListItemDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ComplianceDocumentCategory Category { get; set; }
    public ComplianceDocumentStatus Status { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int CurrentVersionNumber { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public bool IsOverdue { get; set; }           // Computed: NextReviewDate < today
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ComplianceDocumentDto.cs — Full detail
public class ComplianceDocumentDto : ComplianceDocumentListItemDto
{
    public string? Description { get; set; }
    public string? ContentHtml { get; set; }
    public string? StoredFileUrl { get; set; }
    public Guid OwnerId { get; set; }
    public int ReviewFrequencyDays { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? LastReviewedByName { get; set; }
    public List<ComplianceDocumentVersionDto> Versions { get; set; } = new();
    public ComplianceApprovalRequestDto? PendingApproval { get; set; }
    public int AttestationCount { get; set; }
}

// ComplianceDocumentVersionDto.cs
public class ComplianceDocumentVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string ChangeDescription { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasFile { get; set; }
}

// ComplianceAuditEntryDto.cs
public class ComplianceAuditEntryDto
{
    public Guid Id { get; set; }
    public ComplianceAuditAction Action { get; set; }
    public string ActionDisplay { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
    public int? VersionNumber { get; set; }
}

// ComplianceAttestationDto.cs
public class ComplianceAttestationDto
{
    public Guid Id { get; set; }
    public int DocumentVersionNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime AttestedAt { get; set; }
    public string? Notes { get; set; }
}

// ComplianceDashboardDto.cs
public class ComplianceDashboardDto
{
    public int TotalDocuments { get; set; }
    public int ActiveDocuments { get; set; }
    public int DraftDocuments { get; set; }
    public int InReviewDocuments { get; set; }
    public int OverdueReviews { get; set; }
    public int DueIn30Days { get; set; }
    public int DueIn90Days { get; set; }
    public List<ComplianceDocumentListItemDto> UpcomingReviews { get; set; } = new();
    public List<ComplianceDocumentListItemDto> PendingMyApproval { get; set; } = new();
    public List<ComplianceDocumentListItemDto> MyDocuments { get; set; } = new();
    public List<ComplianceDocumentListItemDto> RecentlyUpdated { get; set; } = new();
}
```

---

## 7. API Endpoints

**Base route:** `api/v1/compliance`

### Documents

| Method | Route | Description |
|---|---|---|
| `GET` | `/documents` | List all documents (filterable by status, category, owner, overdue) |
| `GET` | `/documents/{id}` | Get document detail with versions and pending approval |
| `POST` | `/documents` | Create new document (starts as Draft, auto-assigns document number) |
| `PUT` | `/documents/{id}` | Update document (creates new version snapshot, logs edit) |
| `DELETE` | `/documents/{id}` | Soft-delete document |
| `POST` | `/documents/{id}/submit-for-review` | Move Draft → InReview, create ApprovalRequest |
| `POST` | `/documents/{id}/approve` | Approver grants/rejects approval, moves to Approved or back to Draft |
| `POST` | `/documents/{id}/publish` | Owner publishes Approved doc → Active |
| `POST` | `/documents/{id}/archive` | Archive Active document |
| `POST` | `/documents/{id}/attest` | Current user attests to having read the document |
| `GET` | `/documents/{id}/audit-log` | Get full audit log for one document |
| `GET` | `/documents/{id}/attestations` | Get all attestations for one document |
| `GET` | `/documents/{id}/versions/{versionNumber}` | Get a specific historical version |
| `POST` | `/documents/{id}/upload` | Upload file to Azure Blob, associate with document |
| `GET` | `/documents/{id}/download` | Generate signed Azure Blob download URL |

### Global / Admin

| Method | Route | Description |
|---|---|---|
| `GET` | `/dashboard` | Dashboard stats and upcoming review lists |
| `GET` | `/audit-log` | Global audit log across all documents (admin/auditor role) |
| `GET` | `/reports/export` | Export audit report — params: documentId (optional), dateFrom, dateTo, format (pdf/excel) |

### Sample Controller Structure

```csharp
[ApiController]
[Route("api/v1/compliance")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceService _complianceService;

    public ComplianceController(IComplianceService complianceService)
    {
        _complianceService = complianceService;
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] ComplianceDocumentQueryParams query)
    {
        var result = await _complianceService.GetDocumentsAsync(query);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    [HttpPost("documents/{id:guid}/attest")]
    public async Task<IActionResult> Attest(Guid id, [FromBody] ComplianceAttestDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userName = User.FindFirstValue(ClaimTypes.Name)!;
        var result = await _complianceService.AttestAsync(id, userId, userName, dto.Notes);
        return result.IsSuccess ? Ok() : BadRequest(new { result.ErrorCode, result.ErrorMessage });
    }

    // ... etc.
}
```

---

## 8. Service Layer

```csharp
// IComplianceService.cs interface (key methods)
public interface IComplianceService
{
    Task<Result<PagedResult<ComplianceDocumentListItemDto>>> GetDocumentsAsync(ComplianceDocumentQueryParams query);
    Task<Result<ComplianceDocumentDto>> GetByIdAsync(Guid id, Guid requestingUserId);
    Task<Result<ComplianceDocumentDto>> CreateAsync(ComplianceDocumentCreateDto dto, Guid creatorId, string creatorName);
    Task<Result<ComplianceDocumentDto>> UpdateAsync(Guid id, ComplianceDocumentUpdateDto dto, Guid editorId, string editorName);
    Task<Result> DeleteAsync(Guid id, Guid deletedById);
    Task<Result> SubmitForReviewAsync(Guid id, ComplianceSubmitForReviewDto dto, Guid requesterId, string requesterName);
    Task<Result> ProcessApprovalAsync(Guid id, ComplianceApprovalDecisionDto dto, Guid approverId, string approverName);
    Task<Result> PublishAsync(Guid id, Guid publisherId, string publisherName);
    Task<Result> ArchiveAsync(Guid id, Guid archiverId, string archiverName);
    Task<Result> AttestAsync(Guid id, Guid userId, string userName, string? notes);
    Task<Result<List<ComplianceAuditEntryDto>>> GetAuditLogAsync(Guid documentId);
    Task<Result<List<ComplianceAuditEntryDto>>> GetGlobalAuditLogAsync(ComplianceAuditLogQueryParams query);
    Task<Result<ComplianceDashboardDto>> GetDashboardAsync(Guid userId);
    Task<Result<byte[]>> ExportReportAsync(ComplianceExportParams exportParams);
}
```

**Key service-layer rules:**
- Every state-changing operation writes a `ComplianceAuditEntry` in the same transaction
- Document number is auto-generated on create: `POL-{CATEGORY_PREFIX}-{SEQUENCE:D3}` (e.g., `POL-UW-001`, `POL-DS-002`)
- When content is edited, a `ComplianceDocumentVersion` snapshot is always saved
- The `ComplianceAuditEntry` for `ContentEdited` should serialize a diff summary into `Details` (before/after title, version number change, etc.)
- Approval requests are only valid for the current version — if content is edited after submission, the approval request is invalidated and a new one required

---

## 9. Review Reminder System

Leverage the **existing SIMS task/workflow engine** rather than building a new scheduler.

**Strategy:** When a document is published to Active status, create a SIMS Task (using the existing task system) assigned to the document owner with a due date = `NextReviewDate`. Set a recurrence pattern matching `ReviewFrequencyDays`.

If an email/Teams notification layer already exists in SIMS, hook into it. Otherwise, a lightweight Azure Logic App on a daily schedule can query `/api/v1/compliance/dashboard` for overdue/upcoming reviews and send email notifications via Office 365.

**Reminder logic (to implement in service layer):**
```
On document published to Active:
  1. Calculate NextReviewDate = EffectiveDate + ReviewFrequencyDays (or today + ReviewFrequencyDays)
  2. Create SIMS task: "Review compliance document: {Title}" due NextReviewDate, assigned to OwnerId
  3. Log ComplianceAuditEntry: ReviewDateUpdated

When NextReviewDate is within 30 days:
  → Daily job or Logic App fires notification email to OwnerId

When NextReviewDate is past and document is still Active (overdue):
  → Document marked visually overdue in UI (IsOverdue flag)
  → Dashboard counter incremented
  → Daily notification continues until review is completed
```

---

## 10. Auditor Export

Uses the existing **Syncfusion / QuestPDF** infrastructure already in SIMS.

**PDF Export contains:**
1. Cover page: Document title, number, current version, export date, date range
2. Document metadata: owner, category, effective date, review frequency
3. Version history table: version #, date, author, change description
4. Approval history: who requested, who approved/rejected, date, comments
5. Attestation log: user, email, version attested, date
6. Full audit trail table: timestamp, actor, action, details
7. Footer on each page: "Confidential — Generated by SIMS — {timestamp}"

**Excel Export** (for auditors who want to do their own analysis):
- Sheet 1: Document summary
- Sheet 2: Version history
- Sheet 3: Approval history
- Sheet 4: Attestations
- Sheet 5: Full audit log (sortable/filterable)

---

## 11. Frontend Pages

All pages live under `frontend/src/pages/compliance/` and follow the same patterns as `SubmissionsPage.tsx` and `SubmissionDetailPage.tsx`.

### 11.1 Page Structure

```
frontend/src/pages/compliance/
├── ComplianceDashboardPage.tsx     — KPI cards, upcoming reviews, my docs
├── ComplianceDocumentsPage.tsx     — searchable/filterable document library
├── ComplianceDocumentDetailPage.tsx — tabbed detail: Content | Versions | Approvals | Attestations | Audit Log
├── ComplianceDocumentEditorPage.tsx — TipTap editor for creating/editing
├── ComplianceAuditLogPage.tsx       — global audit log (admin/auditor role)
└── components/
    ├── ComplianceStatusBadge.tsx
    ├── ComplianceCategoryBadge.tsx
    ├── ComplianceDocumentCard.tsx
    ├── ComplianceAttestButton.tsx
    ├── ComplianceApprovalPanel.tsx
    └── ComplianceExportButton.tsx
```

### 11.2 Frontend API Client

```typescript
// frontend/src/api/compliance.api.ts
export const complianceApi = {
  getDashboard: () =>
    apiClient.get<ComplianceDashboard>('/compliance/dashboard').then(r => r.data),

  getDocuments: (params: ComplianceDocumentQueryParams) =>
    apiClient.get<PagedResult<ComplianceDocumentListItem>>('/compliance/documents', { params }).then(r => r.data),

  getById: (id: string) =>
    apiClient.get<ComplianceDocument>(`/compliance/documents/${id}`).then(r => r.data),

  create: (data: ComplianceDocumentCreate) =>
    apiClient.post<ComplianceDocument>('/compliance/documents', data).then(r => r.data),

  update: (id: string, data: ComplianceDocumentUpdate) =>
    apiClient.put<ComplianceDocument>(`/compliance/documents/${id}`, data).then(r => r.data),

  submitForReview: (id: string, data: ComplianceSubmitForReview) =>
    apiClient.post(`/compliance/documents/${id}/submit-for-review`, data).then(r => r.data),

  processApproval: (id: string, data: ComplianceApprovalDecision) =>
    apiClient.post(`/compliance/documents/${id}/approve`, data).then(r => r.data),

  publish: (id: string) =>
    apiClient.post(`/compliance/documents/${id}/publish`).then(r => r.data),

  archive: (id: string) =>
    apiClient.post(`/compliance/documents/${id}/archive`).then(r => r.data),

  attest: (id: string, notes?: string) =>
    apiClient.post(`/compliance/documents/${id}/attest`, { notes }).then(r => r.data),

  getAuditLog: (id: string) =>
    apiClient.get<ComplianceAuditEntry[]>(`/compliance/documents/${id}/audit-log`).then(r => r.data),

  getAttestations: (id: string) =>
    apiClient.get<ComplianceAttestation[]>(`/compliance/documents/${id}/attestations`).then(r => r.data),

  getGlobalAuditLog: (params: AuditLogQueryParams) =>
    apiClient.get<PagedResult<ComplianceAuditEntry>>('/compliance/audit-log', { params }).then(r => r.data),

  exportReport: (params: ComplianceExportParams) =>
    apiClient.get('/compliance/reports/export', { params, responseType: 'blob' }).then(r => r.data),
}
```

### 11.3 TypeScript Types

```typescript
// frontend/src/types/compliance.ts
export type ComplianceDocumentStatus = 'Draft' | 'InReview' | 'Approved' | 'Active' | 'Archived'
export type ComplianceDocumentCategory =
  | 'UnderwritingGuidelines' | 'ClaimsHandling' | 'DataSecurity'
  | 'HumanResources' | 'Financial' | 'RegulatoryCompliance' | 'Operations' | 'Privacy' | 'Other'

export interface ComplianceDocumentListItem {
  id: string
  documentNumber: string
  title: string
  category: ComplianceDocumentCategory
  status: ComplianceDocumentStatus
  ownerName: string
  currentVersionNumber: number
  nextReviewDate: string | null
  lastReviewedDate: string | null
  isOverdue: boolean
  tags: string[]
  createdAt: string
  updatedAt: string
}

export interface ComplianceDocument extends ComplianceDocumentListItem {
  description: string | null
  contentHtml: string | null
  storedFileUrl: string | null
  ownerId: string
  reviewFrequencyDays: number
  effectiveDate: string | null
  lastReviewedByName: string | null
  versions: ComplianceDocumentVersion[]
  pendingApproval: ComplianceApprovalRequest | null
  attestationCount: number
}

// ... etc.
```

---

## 12. Roles & Permissions

Leverage the existing SIMS role/permission system.

| Role | What they can do |
|---|---|
| **Compliance Admin** | Full CRUD, approve documents, view global audit log, export reports |
| **Document Owner** | Create and edit documents they own, submit for review, publish |
| **Approver** | Approve/reject documents assigned to them for review |
| **Standard User** | View Active documents, attest to having read documents |
| **Auditor** (read-only) | View all documents + all audit logs + export reports, cannot change anything |

Add these as new permission constants in `SIMS.Application/Common/Permissions.cs` (following the existing permission constants pattern):
```csharp
public static class CompliancePermissions
{
    public const string ViewDocuments = "compliance.documents.view";
    public const string CreateDocuments = "compliance.documents.create";
    public const string EditDocuments = "compliance.documents.edit";
    public const string DeleteDocuments = "compliance.documents.delete";
    public const string ApproveDocuments = "compliance.documents.approve";
    public const string ViewAuditLog = "compliance.auditlog.view";
    public const string ExportReports = "compliance.reports.export";
}
```

---

## 13. Implementation Roadmap

### Phase 1 — Foundation (Week 1–2)
- [ ] Domain entities (6 classes)
- [ ] EF configurations + migration
- [ ] `IComplianceService` + `ComplianceService` (CRUD + audit logging)
- [ ] `ComplianceController` (CRUD endpoints + dashboard)
- [ ] Frontend: `compliance.api.ts`, `compliance.ts` types
- [ ] Frontend: `ComplianceDocumentsPage` (list view) + `ComplianceDocumentDetailPage` (read-only)

### Phase 2 — Workflow (Week 2–3)
- [ ] Submit for review, approve/reject, publish, archive endpoints
- [ ] `ComplianceDocumentEditorPage` with TipTap
- [ ] Version history tab in detail page
- [ ] Approval panel in detail page
- [ ] Review reminder task creation (hook into existing task engine)

### Phase 3 — Attestation & Audit (Week 3–4)
- [ ] Attest endpoint + `ComplianceAttestButton` component
- [ ] Attestations tab in detail page
- [ ] Audit log tab in detail page
- [ ] `ComplianceAuditLogPage` (global view, admin/auditor only)
- [ ] `ComplianceDashboardPage` with KPI cards

### Phase 4 — Auditor Export (Week 4–5)
- [ ] PDF export (Syncfusion/QuestPDF)
- [ ] Excel export (existing Excel library)
- [ ] `ComplianceExportButton` component
- [ ] Test with sample data and auditor review

### Phase 5 — Navigation & Polish
- [ ] Add "Compliance" to SIMS sidebar navigation
- [ ] Permission guards on all routes and API endpoints
- [ ] Review reminder email (Logic App or existing notification system)
- [ ] End-to-end testing

---

## 14. Notes for Implementation

- **Document numbering:** Use a database sequence or a max+1 query per category to generate `POL-UW-001` style numbers atomically
- **Audit immutability:** The `ComplianceAuditEntry` table should have no `UPDATE` or `DELETE` grants in the database role used by the app — insert-only at the DB level
- **File storage:** Follow the existing Azure Blob pattern for `StoredFileKey` — store the blob key (not full URL) and generate signed URLs on demand
- **TipTap integration:** The editor is already in the frontend — reuse the same `<RichTextEditor />` component used elsewhere in SIMS
- **Soft-delete vs. audit:** Deleting a document soft-deletes it but the audit entries remain permanently and are still accessible via the global audit log
- **Version snapshot timing:** Create a version snapshot at the moment a document is **published** (moved to Active), not on every save — this keeps the version history meaningful (published versions only)
