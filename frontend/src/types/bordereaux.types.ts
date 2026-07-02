// Default transaction types for new BDX profiles — the backend rejects an empty
// list (it would filter every premium preview to zero rows).
export const DEFAULT_BDX_TXN_TYPES = '["NewBusiness","Endorsement","Renewal","Cancellation","Reinstatement","Rewrite","NonRenewal"]'

export interface BordereauxProfile {
  id: string
  name: string
  programConfigurationId: string
  programName: string
  carrierId: string
  carrierName: string
  lineOfBusiness: string | null
  stateCode: string | null
  programCarrierId: string | null
  programCarrierLineOfBusinessId: string | null
  programCarrierLobStateId: string | null
  reportType: string
  frequency: string
  outputFormat: string
  dateBasis: string
  requiresAccountCurrent: boolean
  isActive: boolean
  requiredTabsJson: string
  requiredColumnsJson: string
  mappingRulesJson: string
  staticValuesJson: string
  validationRulesJson: string
  includedTransactionTypesJson: string
  notes: string | null
  setupStatus: BordereauxProfileSetupStatus
}

export interface BordereauxProfileSetupStatus {
  isReadyForExport: boolean
  missingItems: number
  requiredTabs: BordereauxProfileSetupItem[]
  requiredColumns: BordereauxProfileSetupItem[]
  staticValues: BordereauxProfileSetupItem[]
  mappingRules: BordereauxProfileSetupItem[]
}

export interface BordereauxProfileSetupItem {
  key: string
  label: string
  status: 'Configured' | 'Default' | 'Missing' | string
  value: string | null
  defaultValue: string | null
}

export interface UpsertBordereauxProfileRequest {
  name: string
  programConfigurationId: string
  carrierId: string
  lineOfBusiness: string | null
  stateCode: string | null
  reportType: string
  frequency: string
  outputFormat: string
  dateBasis: string
  requiresAccountCurrent: boolean
  isActive: boolean
  requiredTabsJson: string
  requiredColumnsJson: string
  mappingRulesJson: string
  staticValuesJson: string
  validationRulesJson: string
  includedTransactionTypesJson: string
  notes: string | null
}

export interface BordereauxPremiumPreviewRow {
  policyId: string
  policyTransactionId: string
  invoiceId: number
  policyNumber: string
  transactionNumber: string
  transactionType: string
  reportingDate: string
  transactionEffectiveDate: string
  billedDate: string
  expirationDate: string | null
  insuredName: string
  insuredState: string
  programConfigurationId: string | null
  programName: string | null
  carrierId: string
  carrierName: string
  lineOfBusiness: string
  grossPremium: number
  grossCommission: number
  fees: number
  totalAmount: number
  netDueCarrier: number
  invoiceNumber: string
  insuredAddress: string
  insuredPostcode: string
  insuredCounty: string
  policyIssuanceDate: string | null
  industrialSector: string
  newRenewalIndicator: string
}

export interface BordereauxPremiumPreview {
  profileId: string
  periodStart: string
  periodEnd: string
  rows: BordereauxPremiumPreviewRow[]
  grossPremiumTotal: number
  grossCommissionTotal: number
  feesTotal: number
  netDueCarrierTotal: number
}

export interface BordereauxRun {
  id: string
  bordereauxProfileId: string
  profileName: string
  runNumber: number
  periodStart: string
  periodEnd: string
  status: string
  reconciliationStatus: string
  generatedById: string | null
  generatedAt: string | null
  londonBordereauxBlobPath: string | null
  londonBordereauxFileName: string | null
  londonBordereauxContentType: string | null
  accountCurrentBlobPath: string | null
  accountCurrentFileName: string | null
  accountCurrentContentType: string | null
  bordereauxRowCount: number
  accountCurrentRowCount: number
  detailRowCountsJson: string
  validationSummaryJson: string
  reconciliationSummaryJson: string
  profileSnapshotJson: string
  sourceRowsSnapshotJson: string
}

export interface ReconcileBordereauxRunRequest {
  accountCurrentRowCount: number
  accountCurrentGrossPremiumTotal: number
  accountCurrentGrossCommissionTotal: number
  accountCurrentFeesTotal: number
  accountCurrentNetDueCarrierTotal: number
}
