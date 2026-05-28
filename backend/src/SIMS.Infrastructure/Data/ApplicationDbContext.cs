using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Entities.Bordereaux;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Domain.Entities.Rating;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SIMS.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<User, Role, Guid,
    IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{
    private static readonly HashSet<string> LedgerVoidMetadataProperties = new(StringComparer.Ordinal)
    {
        nameof(LedgerTransaction.PostingStatus),
        nameof(LedgerTransaction.VoidedByTransactionId),
        nameof(LedgerTransaction.VoidedAt),
        nameof(LedgerTransaction.VoidedBy),
        nameof(LedgerTransaction.VoidReason)
    };

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentLocation> AgentLocations => Set<AgentLocation>();
    public DbSet<AgentContact> AgentContacts => Set<AgentContact>();
    public DbSet<Intermediary> Intermediaries => Set<Intermediary>();
    public DbSet<IntermediaryProgramCarrierLobSetup> IntermediaryProgramCarrierLobSetups => Set<IntermediaryProgramCarrierLobSetup>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<CarrierContact> CarrierContacts => Set<CarrierContact>();
    public DbSet<CarrierLineOfBusiness> CarrierLinesOfBusiness => Set<CarrierLineOfBusiness>();
    public DbSet<CarrierCommission> CarrierCommissions => Set<CarrierCommission>();
    public DbSet<AgentCommission> AgentCommissions => Set<AgentCommission>();
    public DbSet<Insured> Insureds => Set<Insured>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<UnderwritingClearanceResult> UnderwritingClearanceResults => Set<UnderwritingClearanceResult>();
    public DbSet<UnderwritingAppetiteResult> UnderwritingAppetiteResults => Set<UnderwritingAppetiteResult>();
    public DbSet<UnderwritingReferral> UnderwritingReferrals => Set<UnderwritingReferral>();
    public DbSet<ProgramConfiguration> ProgramConfigurations => Set<ProgramConfiguration>();
    public DbSet<ProgramCarrier> ProgramCarriers => Set<ProgramCarrier>();
    public DbSet<ProgramCarrierLineOfBusiness> ProgramCarrierLinesOfBusiness => Set<ProgramCarrierLineOfBusiness>();
    public DbSet<ProgramCarrierLobState> ProgramCarrierLobStates => Set<ProgramCarrierLobState>();
    public DbSet<SurplusLinesStateSetup> SurplusLinesStateSetups => Set<SurplusLinesStateSetup>();
    public DbSet<BordereauxProfile> BordereauxProfiles => Set<BordereauxProfile>();
    public DbSet<BordereauxRun> BordereauxRuns => Set<BordereauxRun>();
    public DbSet<UnderwritingGuidelineDocument> UnderwritingGuidelineDocuments => Set<UnderwritingGuidelineDocument>();
    public DbSet<UnderwritingGuidelineControl> UnderwritingGuidelineControls => Set<UnderwritingGuidelineControl>();
    public DbSet<UnderwritingGuidelineAuditLog> UnderwritingGuidelineAuditLogs => Set<UnderwritingGuidelineAuditLog>();
    public DbSet<UnderwritingControlEnforcementResult> UnderwritingControlEnforcementResults => Set<UnderwritingControlEnforcementResult>();
    public DbSet<AuthorityApprovalRequest> AuthorityApprovalRequests => Set<AuthorityApprovalRequest>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<PolicyNumberSequence> PolicyNumberSequences => Set<PolicyNumberSequence>();
    public DbSet<PolicyNumberAssignment> PolicyNumberAssignments => Set<PolicyNumberAssignment>();
    public DbSet<PolicyNumberSequenceUsage> PolicyNumberSequenceUsages => Set<PolicyNumberSequenceUsage>();
    public DbSet<PolicyTransaction> PolicyTransactions => Set<PolicyTransaction>();
    public DbSet<PolicyCancellationDetail> PolicyCancellationDetails => Set<PolicyCancellationDetail>();
    public DbSet<PolicyNonRenewalDetail> PolicyNonRenewalDetails => Set<PolicyNonRenewalDetail>();
    public DbSet<PolicyReinstatementDetail> PolicyReinstatementDetails => Set<PolicyReinstatementDetail>();
    public DbSet<PolicyRewriteDetail> PolicyRewriteDetails => Set<PolicyRewriteDetail>();
    public DbSet<PolicyTransactionStatusHistory> PolicyTransactionStatusHistory => Set<PolicyTransactionStatusHistory>();
    public DbSet<PolicyTransactionComplianceChecklist> PolicyTransactionComplianceChecklists => Set<PolicyTransactionComplianceChecklist>();
    public DbSet<PolicyTransactionComplianceChecklistItem> PolicyTransactionComplianceChecklistItems => Set<PolicyTransactionComplianceChecklistItem>();
    public DbSet<PolicyTransactionApproval> PolicyTransactionApprovals => Set<PolicyTransactionApproval>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<QuoteUWWriteup> QuoteUWWriteups => Set<QuoteUWWriteup>();
    public DbSet<QuoteUWWriteupCondition> QuoteUWWriteupConditions => Set<QuoteUWWriteupCondition>();
    public DbSet<QuoteChecklistItem> QuoteChecklistItems => Set<QuoteChecklistItem>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<PolicyFormTemplate> PolicyFormTemplates => Set<PolicyFormTemplate>();
    public DbSet<PolicyFormFieldMapping> PolicyFormFieldMappings => Set<PolicyFormFieldMapping>();
    public DbSet<PolicyPackageConfiguration> PolicyPackageConfigurations => Set<PolicyPackageConfiguration>();
    public DbSet<PolicyPackageForm> PolicyPackageForms => Set<PolicyPackageForm>();
    public DbSet<QuotePolicyFormSelection> QuotePolicyFormSelections => Set<QuotePolicyFormSelection>();
    public DbSet<ProposalDocumentConfiguration> ProposalDocumentConfigurations => Set<ProposalDocumentConfiguration>();
    public DbSet<OutboundCommunication> OutboundCommunications => Set<OutboundCommunication>();
    public DbSet<OutboundCommunicationAttachment> OutboundCommunicationAttachments => Set<OutboundCommunicationAttachment>();
    public DbSet<InboundEmail> InboundEmails => Set<InboundEmail>();
    public DbSet<EmailAttachment> EmailAttachments => Set<EmailAttachment>();
    public DbSet<SubmissionLocation> SubmissionLocations => Set<SubmissionLocation>();
    public DbSet<SubmissionDriver> SubmissionDrivers => Set<SubmissionDriver>();
    public DbSet<SubmissionVehicle> SubmissionVehicles => Set<SubmissionVehicle>();
    public DbSet<SubmissionPriorCarrier> SubmissionPriorCarriers => Set<SubmissionPriorCarrier>();
    public DbSet<SubmissionLossYear> SubmissionLossYears => Set<SubmissionLossYear>();
    public DbSet<SubmissionLossClaim> SubmissionLossClaims => Set<SubmissionLossClaim>();
    public DbSet<SubmissionSupplemental> SubmissionSupplementals => Set<SubmissionSupplemental>();
    public DbSet<SubmissionGLCoverages> SubmissionGLCoverages => Set<SubmissionGLCoverages>();
    public DbSet<SubmissionGLClassification> SubmissionGLClassifications => Set<SubmissionGLClassification>();
    public DbSet<SubmissionIMCoverages> SubmissionIMCoverages => Set<SubmissionIMCoverages>();
    public DbSet<SubmissionEquipment> SubmissionEquipment => Set<SubmissionEquipment>();
    public DbSet<SubmissionAdditionalInterest> SubmissionAdditionalInterests => Set<SubmissionAdditionalInterest>();
    public DbSet<SubmissionAdditionalInterestBlanket> SubmissionAdditionalInterestBlankets => Set<SubmissionAdditionalInterestBlanket>();
    public DbSet<CarrierAdditionalInterestRate> CarrierAdditionalInterestRates => Set<CarrierAdditionalInterestRate>();
    public DbSet<TaskType> TaskTypes => Set<TaskType>();
    public DbSet<TaskInstance> TaskInstances => Set<TaskInstance>();
    public DbSet<SystemEvent> SystemEvents => Set<SystemEvent>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<HolidayCalendar> HolidayCalendar => Set<HolidayCalendar>();
    public DbSet<UserDelegation> UserDelegations => Set<UserDelegation>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<TaskAuditEntry> TaskAuditEntries => Set<TaskAuditEntry>();
    public DbSet<LegalRequirementSection> LegalRequirementSections => Set<LegalRequirementSection>();
    public DbSet<LegalTrackedSource> LegalTrackedSources => Set<LegalTrackedSource>();
    public DbSet<LegalSourceScanRun> LegalSourceScanRuns => Set<LegalSourceScanRun>();
    public DbSet<LegalSourceScanResult> LegalSourceScanResults => Set<LegalSourceScanResult>();
    public DbSet<LegalRequirementChangeLog> LegalRequirementChangeLogs => Set<LegalRequirementChangeLog>();
    public DbSet<LegiScanTrackedBill> LegiScanTrackedBills => Set<LegiScanTrackedBill>();
    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();
    public DbSet<ComplianceDocumentVersion> ComplianceDocumentVersions => Set<ComplianceDocumentVersion>();
    public DbSet<ComplianceDocumentReview> ComplianceDocumentReviews => Set<ComplianceDocumentReview>();
    public DbSet<ComplianceEvidence> ComplianceEvidence => Set<ComplianceEvidence>();
    public DbSet<ComplianceEvidenceAttachment> ComplianceEvidenceAttachments => Set<ComplianceEvidenceAttachment>();
    public DbSet<ComplianceAttestationCampaign> ComplianceAttestationCampaigns => Set<ComplianceAttestationCampaign>();
    public DbSet<ComplianceAttestationRecipient> ComplianceAttestationRecipients => Set<ComplianceAttestationRecipient>();
    public DbSet<ComplianceAuditLog> ComplianceAuditLogs => Set<ComplianceAuditLog>();
    public DbSet<AiModelRegistry> AiModelRegistry => Set<AiModelRegistry>();
    public DbSet<AiUseCaseModelSetting> AiUseCaseModelSettings => Set<AiUseCaseModelSetting>();
    public DbSet<AiModelSettingAuditLog> AiModelSettingAuditLogs => Set<AiModelSettingAuditLog>();

    // Rating
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<Territory> Territories => Set<Territory>();
    public DbSet<RatingPlan> RatingPlans => Set<RatingPlan>();
    public DbSet<RatingPlanVersion> RatingPlanVersions => Set<RatingPlanVersion>();
    public DbSet<CarrierRatingAssignment> CarrierRatingAssignments => Set<CarrierRatingAssignment>();
    public DbSet<FactorTable> FactorTables => Set<FactorTable>();
    public DbSet<FactorRow> FactorRows => Set<FactorRow>();
    public DbSet<EligibilityRule> EligibilityRules => Set<EligibilityRule>();
    public DbSet<QuoteRatingSnapshot> QuoteRatingSnapshots => Set<QuoteRatingSnapshot>();
    public DbSet<QuoteRatingLine> QuoteRatingLines => Set<QuoteRatingLine>();
    public DbSet<RatingPlanVersionImpactPreview> RatingPlanVersionImpactPreviews => Set<RatingPlanVersionImpactPreview>();
    public DbSet<ShadowRatingResult> ShadowRatingResults => Set<ShadowRatingResult>();
    public DbSet<RatingSettings> RatingSettings => Set<RatingSettings>();

    // FMCSA / Auto underwriting
    public DbSet<FmcsaCarrierSnapshot> FmcsaCarrierSnapshots => Set<FmcsaCarrierSnapshot>();
    public DbSet<FmcsaInspection> FmcsaInspections => Set<FmcsaInspection>();
    public DbSet<FmcsaViolation> FmcsaViolations => Set<FmcsaViolation>();
    public DbSet<FmcsaCrash> FmcsaCrashes => Set<FmcsaCrash>();
    public DbSet<FmcsaScoringRun> FmcsaScoringRuns => Set<FmcsaScoringRun>();
    public DbSet<FmcsaBasicScore> FmcsaBasicScores => Set<FmcsaBasicScore>();

    // Accounting
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<GlAccountMap> GlAccountMaps => Set<GlAccountMap>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<JournalEntryRollup> JournalEntryRollups => Set<JournalEntryRollup>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<PeriodCloseChecklistItem> PeriodCloseChecklist => Set<PeriodCloseChecklistItem>();
    public DbSet<FeeDefinition> FeeDefinitions => Set<FeeDefinition>();
    public DbSet<FeeRuleVersion> FeeRuleVersions => Set<FeeRuleVersion>();
    public DbSet<FeeStateTaxability> FeeStateTaxabilities => Set<FeeStateTaxability>();
    public DbSet<FeePremiumBracket> FeePremiumBrackets => Set<FeePremiumBracket>();
    public DbSet<FeeAuditLog> FeeAuditLogs => Set<FeeAuditLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<CashApplication> CashApplications => Set<CashApplication>();
    public DbSet<CashMovementInstruction> CashMovementInstructions => Set<CashMovementInstruction>();
    public DbSet<CashDistributionBatch> CashDistributionBatches => Set<CashDistributionBatch>();
    public DbSet<Payable> Payables => Set<Payable>();
    public DbSet<Disbursement> Disbursements => Set<Disbursement>();
    public DbSet<DisbursementLine> DisbursementLines => Set<DisbursementLine>();
    public DbSet<PayeeStatement> PayeeStatements => Set<PayeeStatement>();
    public DbSet<PayeeStatementLine> PayeeStatementLines => Set<PayeeStatementLine>();
    public DbSet<QboOAuthToken> QboOAuthTokens => Set<QboOAuthToken>();
    public DbSet<PendingQboSync> PendingQboSyncs => Set<PendingQboSync>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables
        builder.Entity<User>().ToTable("users");
        builder.Entity<Role>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Apply all IEntityTypeConfiguration classes in this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global soft-delete query filters
        builder.Entity<Agent>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AgentLocation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AgentContact>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Intermediary>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<IntermediaryProgramCarrierLobSetup>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Carrier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CarrierContact>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CarrierLineOfBusiness>().HasQueryFilter(e => !e.Carrier.IsDeleted);
        builder.Entity<Insured>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Submission>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingClearanceResult>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingAppetiteResult>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingReferral>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProgramConfiguration>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProgramCarrier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProgramCarrierLineOfBusiness>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProgramCarrierLobState>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SurplusLinesStateSetup>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BordereauxProfile>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BordereauxRun>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingGuidelineDocument>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingGuidelineControl>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingGuidelineAuditLog>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UnderwritingControlEnforcementResult>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AuthorityApprovalRequest>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Quote>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Policy>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyVersion>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyNumberSequence>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyNumberAssignment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyNumberSequenceUsage>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyCancellationDetail>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyNonRenewalDetail>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyReinstatementDetail>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyRewriteDetail>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyTransactionStatusHistory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyTransactionComplianceChecklist>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyTransactionComplianceChecklistItem>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyTransactionApproval>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Note>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Attachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DocumentTemplate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyFormTemplate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyFormFieldMapping>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyPackageConfiguration>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyPackageForm>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<QuotePolicyFormSelection>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<OutboundCommunication>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<OutboundCommunicationAttachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<InboundEmail>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<EmailAttachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionLocation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionDriver>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionVehicle>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionPriorCarrier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionLossYear>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionLossClaim>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionSupplemental>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionGLCoverages>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionGLClassification>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionIMCoverages>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionEquipment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionAdditionalInterest>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionAdditionalInterestBlanket>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CarrierAdditionalInterestRate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<TaskType>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<TaskInstance>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SystemEvent>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<WorkflowTemplate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<WorkflowStep>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<HolidayCalendar>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserDelegation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<EscalationRule>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<QuoteChecklistItem>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LegalRequirementSection>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LegalTrackedSource>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LegalSourceScanRun>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LegalSourceScanResult>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LegalRequirementChangeLog>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LegiScanTrackedBill>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceDocument>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceDocumentVersion>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceDocumentReview>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceEvidence>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceEvidenceAttachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceAttestationCampaign>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceAttestationRecipient>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ComplianceAuditLog>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AiModelRegistry>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AiUseCaseModelSetting>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AiModelSettingAuditLog>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaCarrierSnapshot>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaInspection>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaViolation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaCrash>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaScoringRun>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaBasicScore>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<LedgerTransaction>())
        {
            if (entry.State == EntityState.Deleted ||
                (entry.State == EntityState.Modified && !IsAllowedLedgerVoidMetadataUpdate(entry)))
                throw new InvalidOperationException(
                    "LedgerTransaction rows are immutable. Use a reversing entry.");
        }

        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
        }
    }

    private static bool IsAllowedLedgerVoidMetadataUpdate(EntityEntry<LedgerTransaction> entry)
    {
        var modifiedProperties = entry.Properties
            .Where(property => property.IsModified)
            .ToList();

        if (modifiedProperties.Count == 0 ||
            modifiedProperties.Any(property => !LedgerVoidMetadataProperties.Contains(property.Metadata.Name)))
            return false;

        var status = entry.Property(transaction => transaction.PostingStatus);
        if (!status.IsModified || status.OriginalValue != "Posted" || status.CurrentValue != "Voided")
            return false;

        var voidedByTransactionId = entry.Property(transaction => transaction.VoidedByTransactionId);
        if (!voidedByTransactionId.IsModified ||
            voidedByTransactionId.OriginalValue is not null ||
            voidedByTransactionId.CurrentValue is null)
            return false;

        var voidedAt = entry.Property(transaction => transaction.VoidedAt);
        if (!voidedAt.IsModified || voidedAt.OriginalValue is not null || voidedAt.CurrentValue is null)
            return false;

        var voidedBy = entry.Property(transaction => transaction.VoidedBy);
        if (!voidedBy.IsModified || voidedBy.OriginalValue is not null || voidedBy.CurrentValue is null)
            return false;

        var voidReason = entry.Property(transaction => transaction.VoidReason);
        return !voidReason.IsModified || !string.IsNullOrWhiteSpace(voidReason.CurrentValue);
    }
}
