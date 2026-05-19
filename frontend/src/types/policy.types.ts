import type { PolicyLineOfBusiness, RatingResult } from './quote.types'
import type { PolicyFormType } from './policyForm.types'
import type { Attachment } from './attachment.types'
import type { InvoiceSummary } from './invoice.types'
import type { OutboundCommunicationListItem } from './outboundCommunication.types'
import type { TaskInstanceListItem } from './task.types'

export type PolicyStatus = 'Active' | 'Renewed' | 'NonRenewed' | 'Expired' | 'Cancelled'
export type PolicyTransactionStatus =
  | 'Submitted'
  | 'Issued'
  | 'InReview'
  | 'Referred'
  | 'Approved'
  | 'Quoted'
  | 'Accepted'
  | 'Bound'
  | 'NoticePending'
  | 'NoticeSent'
  | 'PendingEffectiveDate'
  | 'Completed'
  | 'Declined'
  | 'Withdrawn'
  | 'Voided'
export type TransactionType = 'NewBusiness' | 'Endorsement' | 'Renewal' | 'Cancellation' | 'Reinstatement' | 'Audit' | 'NonRenewal' | 'Rewrite'

export const POLICY_STATUS_LABELS: Record<PolicyStatus, string> = {
  Active: 'Active',
  Renewed: 'Renewed',
  NonRenewed: 'Non-Renewed',
  Expired: 'Expired',
  Cancelled: 'Cancelled',
}

export const POLICY_STATUS_COLORS: Record<PolicyStatus, string> = {
  Active: 'bg-green-100 text-green-700',
  Renewed: 'bg-blue-100 text-blue-700',
  NonRenewed: 'bg-orange-100 text-orange-700',
  Expired: 'bg-slate-100 text-slate-600',
  Cancelled: 'bg-red-100 text-red-700',
}

export const POLICY_TRANSACTION_STATUS_LABELS: Record<PolicyTransactionStatus, string> = {
  Submitted: 'Submitted',
  Issued: 'Issued',
  InReview: 'In Review',
  Referred: 'Referred',
  Approved: 'Approved',
  Quoted: 'Quoted',
  Accepted: 'Accepted',
  Bound: 'Bound',
  NoticePending: 'Notice Pending',
  NoticeSent: 'Notice Sent',
  PendingEffectiveDate: 'Pending Effective Date',
  Completed: 'Completed',
  Declined: 'Declined',
  Withdrawn: 'Withdrawn',
  Voided: 'Voided',
}

export const POLICY_TRANSACTION_STATUS_PILL: Record<PolicyTransactionStatus, string> = {
  Submitted: 'inprogress',
  Issued: 'bound',
  InReview: 'inprogress',
  Referred: 'warning',
  Approved: 'quoted',
  Quoted: 'quoted',
  Accepted: 'quoted',
  Bound: 'bound',
  NoticePending: 'warning',
  NoticeSent: 'warning',
  PendingEffectiveDate: 'inprogress',
  Completed: 'bound',
  Declined: 'danger',
  Withdrawn: 'draft',
  Voided: 'danger',
}

export const POLICY_TRANSACTION_STATUS_META: Record<PolicyTransactionStatus, { owner: string; meaning: string; isTerminal: boolean }> = {
  Submitted: { owner: 'Underwriting', meaning: 'Entered and awaiting review or processing.', isTerminal: false },
  Issued: { owner: 'Operations', meaning: 'Issued and ready for financial processing.', isTerminal: false },
  InReview: { owner: 'Underwriting', meaning: 'Actively being reviewed.', isTerminal: false },
  Referred: { owner: 'Senior Underwriting', meaning: 'Outside straight-through authority and awaiting referral approval.', isTerminal: false },
  Approved: { owner: 'Underwriting Authority', meaning: 'Approved to proceed.', isTerminal: false },
  Quoted: { owner: 'Underwriting', meaning: 'Financial impact calculated and presented.', isTerminal: false },
  Accepted: { owner: 'Insured or Producer', meaning: 'Terms accepted but not fully bound or issued.', isTerminal: false },
  Bound: { owner: 'Underwriting', meaning: 'Coverage bound and ready for issuance/accounting.', isTerminal: false },
  NoticePending: { owner: 'Compliance', meaning: 'Required legal notice identified but not sent.', isTerminal: false },
  NoticeSent: { owner: 'Compliance', meaning: 'Required notice sent; awaiting effective date or final action.', isTerminal: false },
  PendingEffectiveDate: { owner: 'Operations', meaning: 'Waiting for effective date.', isTerminal: false },
  Completed: { owner: 'Operations', meaning: 'Fully complete with no further action expected.', isTerminal: true },
  Declined: { owner: 'Underwriting', meaning: 'Declined and cannot proceed.', isTerminal: true },
  Withdrawn: { owner: 'Producer or Insured', meaning: 'Withdrawn before completion.', isTerminal: true },
  Voided: { owner: 'Operations', meaning: 'Voided and not active business.', isTerminal: true },
}

export interface PolicyListItem {
  id: string
  policyNumber: string
  submissionId: string
  insuredName: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  effectiveDate: string
  expirationDate: string
  totalPremium: number
  status: PolicyStatus
  boundDate: string
  createdAt: string
}

export interface PolicyTransaction {
  id: string
  policyId: string
  transactionType: TransactionType
  status: PolicyTransactionStatus
  transactionNumber: string
  effectiveDate: string
  expirationDate: string | null
  sourceQuoteId: string | null
  renewalQuoteId: string | null
  priorPolicyVersionId: string | null
  resultingPolicyVersionId: string | null
  priorVersion: PolicyVersionSummary | null
  resultingVersion: PolicyVersionSummary | null
  requestedById: string | null
  requestedAt: string | null
  reviewedById: string | null
  reviewedAt: string | null
  approvedById: string | null
  approvedAt: string | null
  issuedById: string | null
  issuedAt: string | null
  completedById: string | null
  completedAt: string | null
  reasonCode: string | null
  reasonText: string | null
  endorsementDescription: string | null
  priorPolicyId: string | null
  cancellationReason: string | null
  cancellationMethod: string | null
  cancellationDetail: PolicyCancellationDetail | null
  nonRenewalDetail: PolicyNonRenewalDetail | null
  reinstatementDetail: PolicyReinstatementDetail | null
  cancellationComplianceChecklist: CancellationComplianceChecklistItem[]
  cancellationLegalRequirementSnapshotJson: string | null
  premiumBefore: number | null
  premiumChange: number
  newTotalPremium: number
  premiumAfter: number | null
  taxesAndFeesDelta: number | null
  commissionDelta: number | null
  billingModeSnapshot: string | null
  externalReference: string | null
  voidsPolicyTransactionId: string | null
  reversesPolicyTransactionId: string | null
  processedByName: string
  processedAt: string
  notes: string | null
}

export interface PolicyVersionSummary {
  id: string
  versionNumber: number
  effectiveDate: string
  expirationDate: string
  status: PolicyStatus
  premiumAmount: number
  taxesAndFees: number
  totalPremium: number
  ratingSnapshotId: string | null
  createdAt: string
}

export interface PolicyCancellationDetail {
  reasonCode: string
  reasonLabel: string
  reasonCategory: string
  reasonLanguageTemplate: string
  reasonInputsJson: string
  resolvedReasonLanguage: string
  noticeMailingDate: string
  noticeRequirementDays: number
  mailingDays: number
  cancellationEffectiveDate: string
  method: string
  noticeTemplateId: string | null
  noticeTemplateName: string | null
}

export interface PolicyNonRenewalDetail {
  reason: string
  noticeMailingDate: string
  noticeRequirementDays: number
  mailingDays: number
  nonRenewalEffectiveDate: string
  method: string
  noticeTemplateId: string | null
  noticeTemplateName: string | null
}

export interface PolicyReinstatementDetail {
  reinstatementEffectiveDate: string
  reason: string
  notes: string | null
}

export interface PolicyTransactionArtifacts {
  transaction: PolicyTransaction
  documents: Attachment[]
  ratingSnapshots: RatingResult[]
  invoices: InvoiceSummary[]
  communications: OutboundCommunicationListItem[]
  complianceChecklists: PolicyTransactionComplianceChecklist[]
  approvals: PolicyTransactionApproval[]
  tasks: TaskInstanceListItem[]
}

export interface PolicyTransactionApproval {
  id: string
  policyTransactionId: string
  approvalType: string
  requestedById: string
  requestedByName: string
  requestedAt: string
  decisionById: string | null
  decisionByName: string | null
  decisionAt: string | null
  decision: string | null
  notes: string | null
}

export interface PolicyTransactionComplianceChecklist {
  id: string
  policyTransactionId: string
  purpose: string
  items: PolicyTransactionComplianceChecklistItem[]
}

export interface PolicyTransactionComplianceChecklistItem {
  id: string
  key: string
  label: string
  isCompleted: boolean
  legalRequirementSectionId: string | null
  completedById: string | null
  completedAt: string | null
  notes: string | null
  snapshotJson: string | null
}

export interface Policy {
  id: string
  policyNumber: string
  submissionId: string
  submissionNumber: string
  insuredId: string
  insuredName: string
  insuredState: string
  carrierId: string
  carrierName: string
  lineOfBusiness: PolicyLineOfBusiness
  effectiveDate: string
  expirationDate: string
  premiumAmount: number
  taxesAndFees: number
  totalPremium: number
  status: PolicyStatus
  boundDate: string
  issuedDate: string | null
  cancelledDate: string | null
  nonRenewedDate: string | null
  boundQuoteId: string
  carrierCommissionRate: number
  smmRetentionRate: number
  agentCommissionRate: number
  carrierCommissionAmount: number
  smmRetentionAmount: number
  agentCommissionAmount: number
  coverageDescription: string | null
  deductible: number | null
  limit: number | null
  uninsuredMotoristLimit: number | null
  medicalPaymentsLimit: number | null
  transactions: PolicyTransaction[]
  createdAt: string
}

export interface CreateEndorsement {
  effectiveDate: string
  premiumChange: number
  endorsementDescription?: string
  notes?: string
}

export interface IssueEndorsement {
  effectiveDate?: string
  premiumChange?: number
}

export interface PolicyIssuancePacket {
  policyId: string
  boundQuoteId: string
  isIssued: boolean
  issuedDate: string | null
  includedFormCount: number
  isReady: boolean
  readinessMessages: string[]
  forms: PolicyIssuanceForm[]
}

export interface PolicyIssuanceForm {
  id: string
  policyFormTemplateId: string
  formNumber: string
  formName: string
  editionDate: string | null
  sequenceOrder: number
  formType: PolicyFormType
  isIncluded: boolean
  isSystemGenerated: boolean
  fileName: string | null
  readinessStatus: 'Ready' | 'Warning' | 'Blocked'
  readinessMessage: string | null
}

export interface IssuePolicy {
  issuedDate: string
  notes?: string
}

export interface NonRenewPolicy {
  nonRenewedDate: string
  reason?: string
  noticeMailingDate: string
  noticeRequirementDays: number
  mailingDays: number
  method: string
  noticeTemplateId?: string
  complianceChecklist: CancellationComplianceChecklistItem[]
  legalRequirementSectionIds: string[]
}

export interface CancelPolicy {
  cancelledDate: string
  reason: string
  method: string
  premiumChange: number
  complianceChecklist: CancellationComplianceChecklistItem[]
  legalRequirementSectionIds: string[]
  notes?: string
}

export interface CancellationReason {
  code: string
  category: string
  label: string
  defaultNoticeRequirementDays: number
  noticeRequirementLabel: string
  languageTemplate: string
  requiredInputTokens: string[]
  requiresSpecialHandling: boolean
}

export interface IssueCancellationNotice {
  reasonCode: string
  reasonInputs: Record<string, string>
  noticeMailingDate: string
  noticeRequirementDays: number
  mailingDays: number
  method: string
  noticeTemplateId?: string
  notes?: string
}

export interface CompleteCancellation {
  completedDate: string
  notes?: string
}

export interface CompleteNonRenewal {
  completedDate: string
  notes?: string
}

export interface ReinstatePolicy {
  reinstatedDate: string
  reason: string
  notes?: string
}

export interface LegalComplianceGuidance {
  state: string
  lineOfBusiness: string
  action: string
  requirements: LegalComplianceRequirement[]
  noticeRequirements: LegalComplianceRequirement[]
  reasonRequirements: LegalComplianceRequirement[]
  proofOfNoticeRequirements: LegalComplianceRequirement[]
  lienholderRequirements: LegalComplianceRequirement[]
  stateAuthorityRequirements: LegalComplianceRequirement[]
  returnPremiumRequirements: LegalComplianceRequirement[]
}

export interface LegalComplianceRequirement {
  id: string
  category: string
  topic: string
  requirementText: string
  citations: string[]
  lastVerifiedAt: string
}

export interface LegalRequirementSnapshot extends LegalComplianceRequirement {
  state: string
}

export interface CancellationComplianceChecklistItem {
  key: string
  label: string
  isCompleted: boolean
  requirementSectionIds: string[]
}
