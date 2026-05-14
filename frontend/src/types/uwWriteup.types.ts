export interface IMWriteupPayload {
  newVentureDocsOk?: boolean | null
  reasonSubmitted?: string
  referralLossRatioOver55: boolean
  referralPieceOver500k: boolean
  referralTivOver2mil: boolean
  referralLossOver400k: boolean
  referralOtherText?: string
  lossMitigationActions?: string
  lossesOver25kDescription?: string
  eqValueChecked: boolean
  waterborneExposure: boolean
  lastInspectionDate?: string
  recommendationsOutstanding: boolean
  recommendationsDetail?: string
  websiteReviewed?: boolean | null
  websiteIssues?: string
  narrativeOperators?: string
  narrativeEquipment?: string
  narrativeFireSuppression?: string
  narrativeOtherConcerns?: string
  decisionRationale?: string

  programMarket?: string
  referralRateReduction?: boolean
  referralLossOver50k?: boolean
  referralFmcsaConditional?: boolean
  referralBasicOverThreshold?: boolean
  referralScheduleCreditOver20?: boolean
  referralPremiumOver100k?: boolean
  referralOwnerOperatorOver30?: boolean
  referralUnitOverCap?: boolean
  referralPowerUnitsOrPremium?: boolean
  referralTivLocationThreshold?: boolean
  referralTornadoHail?: boolean
  referralCoastalApd?: boolean
  referralCreditScoreLow?: boolean
  referralGlUwCreditOver20?: boolean
  referralGlRevenueBelowThreshold?: boolean
  referralSawmillOps?: boolean
  referralResidentialWork?: boolean
  referralBurningExposure?: boolean
  referralPayrollChangeOver25?: boolean
  referralSubcontractorControls?: boolean
  lossSynopsis?: string
  lossesOver50kDescription?: string
  cabAlertsNotes?: string
  fmcsaSafetyRating?: string
  issCabRating?: string
  maxConcentrationOneLocation?: string
  driverCount?: string
  driverAgeSpan?: string
  driverTurnoverPercent?: string
  ownerOperatorPercent?: string
  mvrInFile?: boolean | null
  driversWatchNotes?: string
  narrativeDrivers?: string
  narrativeCabFmcsa?: string
  narrativeAdditionalInterests?: string
  glClassExposureNotes?: string
  glRiskCharacteristics?: string
  glSubcontractorControls?: string
  glExposureChanges?: string
  glSubcontractorsNarrative?: string
  glEndorsementsNarrative?: string
  pricingRationale?: string
  specialTerms?: string
  lossControlAnalysis?: string
}

export interface EquipmentSummary {
  totalTiv: number
  largestUnitTiv: number
  countCutter: number
  countSkidder: number
  countLoader: number
  countDozer: number
  countOther: number
  totalCount: number
}

export interface PriorCarrierSummary {
  carrierName: string
  policyNumber?: string
  expirationDate?: string
  premiumAmount?: number
}

export interface WriteupCondition {
  id: string
  text: string
  required: boolean
  satisfied: boolean
  sortOrder: number
}

export interface UWWriteupDto {
  id: string
  quoteId: string
  status: 'Draft' | 'Submitted' | 'Approved' | 'Declined'
  decision?: 'Approve' | 'ApproveWithConditions' | 'ReferUp' | 'Decline'
  schemaVersion: number
  submittedAt?: string
  submittedByName?: string
  approvedAt?: string
  approvedByName?: string

  // Prefilled context
  uwName: string
  assistantUWName?: string
  agentName: string
  insuredName: string
  lob: string
  policyType: 'New' | 'Renewal'
  effectiveDate: string
  operationType?: string
  newVenture: boolean
  yearsInBusiness?: number
  creditScore?: number
  website?: string
  address: string
  priorCarriers: PriorCarrierSummary[]

  equipment: EquipmentSummary
  autoReferralPieceOver500k: boolean
  autoReferralTivOver2mil: boolean

  payload: IMWriteupPayload
  conditions: WriteupCondition[]
}

export interface SaveWriteupDto {
  payload: IMWriteupPayload
  conditions: Array<{
    id?: string
    text: string
    required: boolean
    satisfied: boolean
    sortOrder: number
  }>
}

export interface SubmitWriteupDto {
  decision: string
}

export const EMPTY_PAYLOAD: IMWriteupPayload = {
  referralLossRatioOver55: false,
  referralPieceOver500k: false,
  referralTivOver2mil: false,
  referralLossOver400k: false,
  eqValueChecked: false,
  waterborneExposure: false,
  recommendationsOutstanding: false,
}
