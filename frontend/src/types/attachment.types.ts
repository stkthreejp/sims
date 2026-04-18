export type DocumentEntityType = 'Submission' | 'Policy' | 'Carrier' | 'Agent'

export type DocumentType =
  // Submission
  | 'Application'
  | 'SupplementalApplication'
  | 'SignedApplication'
  | 'StatementOfValues'
  | 'LossRuns'
  | 'PriorPolicy'
  | 'FinancialStatements'
  | 'CreditReport'
  | 'CabReport'
  | 'Mvr'
  | 'ProposalQuoteLetter'
  | 'Declination'
  | 'UnderwritingMemo'
  // Policy
  | 'DeclarationsPage'
  | 'PolicyForm'
  | 'Endorsement'
  | 'Binder'
  | 'CertificateOfInsurance'
  | 'Invoice'
  | 'PremiumFinanceAgreement'
  | 'Audit'
  | 'InspectionSurvey'
  | 'CancellationNonRenewal'
  // Carrier
  | 'AppointmentLetter'
  | 'AgencyAgreement'
  | 'UnderwritingGuidelines'
  | 'RateFiling'
  | 'ComplianceMarketConduct'
  // Agent
  | 'License'
  | 'EosCertificate'
  | 'W9'
  // Shared
  | 'Correspondence'
  | 'Other'

export interface Attachment {
  id: string
  entityType: DocumentEntityType
  documentType: DocumentType
  fileName: string
  contentType: string
  fileSizeBytes: number
  description: string | null
  uploadedById: string
  uploadedByName: string
  createdAt: string
}

// ── Document type metadata ────────────────────────────────────────────────────

export const DOCUMENT_TYPE_LABELS: Record<DocumentType, string> = {
  // Submission
  Application: 'Application',
  SupplementalApplication: 'Supplemental Application',
  SignedApplication: 'Signed Application',
  StatementOfValues: 'Statement of Values',
  LossRuns: 'Loss Runs',
  PriorPolicy: 'Prior Policy',
  FinancialStatements: 'Financial Statements',
  CreditReport: 'Credit Report',
  CabReport: 'CAB Report',
  Mvr: 'MVR',
  ProposalQuoteLetter: 'Proposal / Quote Letter',
  Declination: 'Declination',
  UnderwritingMemo: 'Underwriting Memo',
  // Policy
  DeclarationsPage: 'Declarations Page',
  PolicyForm: 'Policy Form',
  Endorsement: 'Endorsement',
  Binder: 'Binder',
  CertificateOfInsurance: 'Certificate of Insurance',
  Invoice: 'Invoice',
  PremiumFinanceAgreement: 'Premium Finance Agreement',
  Audit: 'Audit',
  InspectionSurvey: 'Inspection / Survey',
  CancellationNonRenewal: 'Cancellation / Non-Renewal',
  // Carrier
  AppointmentLetter: 'Appointment Letter',
  AgencyAgreement: 'Agency Agreement',
  UnderwritingGuidelines: 'Underwriting Guidelines',
  RateFiling: 'Rate Filing / Rate Card',
  ComplianceMarketConduct: 'Compliance / Market Conduct',
  // Agent
  License: 'License',
  EosCertificate: 'E&O Certificate',
  W9: 'W-9',
  // Shared
  Correspondence: 'Correspondence',
  Other: 'Other',
}

export const DOCUMENT_TYPES_BY_ENTITY: Record<DocumentEntityType, DocumentType[]> = {
  Submission: [
    'Application', 'SupplementalApplication', 'SignedApplication', 'StatementOfValues',
    'LossRuns', 'PriorPolicy', 'FinancialStatements', 'CreditReport', 'CabReport', 'Mvr',
    'ProposalQuoteLetter', 'Declination', 'UnderwritingMemo', 'Correspondence', 'Other',
  ],
  Policy: [
    'DeclarationsPage', 'PolicyForm', 'Endorsement', 'Binder', 'CertificateOfInsurance',
    'Invoice', 'PremiumFinanceAgreement', 'Audit', 'InspectionSurvey',
    'CancellationNonRenewal', 'Correspondence', 'Other',
  ],
  Carrier: [
    'AppointmentLetter', 'AgencyAgreement', 'UnderwritingGuidelines',
    'RateFiling', 'ComplianceMarketConduct', 'Correspondence', 'Other',
  ],
  Agent: [
    'License', 'EosCertificate', 'W9', 'AgencyAgreement', 'Correspondence', 'Other',
  ],
}
