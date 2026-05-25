export interface BordereauxProfile {
  id: string
  name: string
  programConfigurationId: string
  programName: string
  carrierId: string
  carrierName: string
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
