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
  applyAutomatically: boolean
  premiumMinThreshold: number | null
  premiumMaxThreshold: number | null
  premiumThresholdBasis: string | null
  roundingMode: RoundingMode
  excludeWhenNotFiling: boolean
  excludeOnEndorsements: boolean
  payableRouting: PayableRouting
  payablePayeeId: number | null
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
