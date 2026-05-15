import type { PolicyLineOfBusiness } from './quote.types'
import type { PolicyFormType } from './policyForm.types'

export type PolicyStatus = 'Active' | 'Renewed' | 'NonRenewed' | 'Expired' | 'Cancelled'
export type PolicyTransactionStatus = 'Pending' | 'Issued'
export type TransactionType = 'NewBusiness' | 'Endorsement' | 'Renewal' | 'Cancellation' | 'Reinstatement' | 'Audit'

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
  endorsementDescription: string | null
  priorPolicyId: string | null
  cancellationReason: string | null
  cancellationMethod: string | null
  cancellationComplianceChecklist: CancellationComplianceChecklistItem[]
  cancellationLegalRequirementSnapshotJson: string | null
  premiumChange: number
  newTotalPremium: number
  processedByName: string
  processedAt: string
  notes: string | null
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
