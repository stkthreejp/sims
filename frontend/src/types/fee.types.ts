export type FeeCategory = 'Tax' | 'StampingFee' | 'PolicyFee' | 'BrokerFee' | 'Inspection' | 'Other'
export type CalcType = 'Flat' | 'Percent' | 'Stratified'
export type PayableRouting = 'NotPayable' | 'Company' | 'Entity'
export type RoundingMode = 'NearestCent' | 'RoundUp' | 'RoundDown' | 'NearestDollar' | 'RoundUpDollar' | 'RoundDownDollar'

export interface FeeDefinition {
  id: number
  code: string
  displayName: string
  feeCategory: FeeCategory
  isTaxable: boolean
  calculationOrder: number
  ledgerAccountId: number
}

export interface LedgerAccountOption {
  id: number
  internalCode: string
  externalLabel: string
  accountType: string
}

export interface PayeeOption {
  id: number
  name: string
  payeeType: string
}

export interface FeePremiumBracket {
  id?: number
  tierFrom: number
  tierTo: number | null
  percentRate: number
}

export interface FeeRuleVersion {
  id: number
  feeDefinitionId: number
  feeCode: string
  feeDisplayName: string
  carrierId: string | null
  companyId: number | null
  producerId: number | null
  lineOfBusiness: string | null
  stateCode: string | null
  city: string | null
  licenseType: string | null
  effectiveDate: string  // 'YYYY-MM-DD'
  disabledDate: string | null
  calcType: CalcType
  flatAmount: number | null
  percentRate: number | null
  percentOfNet: boolean
  minimumAmount: number | null
  maxPercent: number | null
  maxAmount: number | null
  commissionable: boolean
  installmentBehavior: string
  splitByParticipation: boolean
  fullyEarned: boolean
  fullyEarnedDays: number | null
  excludeTerrorism: boolean
  multiplyByLocations: boolean
  multiplyByVehicles: boolean
  sendToAccounting: boolean
  applyOnlyOnce: boolean
  mandatoryCharge: boolean
  applyAutomatically: boolean
  applyWhenPackagePolicyOnly: boolean
  doNotApplyWhenPackagePolicyOnly: boolean
  applyToChildLines: boolean
  onlyAppliesToIssuanceState: boolean
  appliesToFlatCancellations: boolean
  premiumMinThreshold: number | null
  premiumMaxThreshold: number | null
  premiumThresholdBasis: string | null
  stateCountMin: number | null
  stateCountMax: number | null
  roundingMode: RoundingMode
  excludeWhenNotFiling: boolean
  excludeOnEndorsements: boolean
  excludeOnRenewal: boolean
  excludeOnOriginalBinder: boolean
  excludeOnMultiCarrierPolicy: boolean
  payHomeState: boolean
  excludedPolicyTransactionTypes: string | null
  payableRouting: PayableRouting
  payablePayeeId: number | null
  masterPayeeWhenHomeState: boolean
  notes: string | null
  premiumBrackets: FeePremiumBracket[]
  nonTaxableStates: string[]
}

export interface FeeAuditLogEntry {
  id: number
  editedBy: string
  editedAt: string
  changeType: string
  fieldChanges: string | null
  notes: string | null
}
