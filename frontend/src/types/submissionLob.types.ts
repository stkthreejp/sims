export type VehicleClass = 'Unknown' | 'Truck' | 'Tractor' | 'Trailer'
export type OperatingRadius = 'Local' | 'Intermediate'

export const VEHICLE_CLASS_LABELS: Record<VehicleClass, string> = {
  Unknown: 'Unknown',
  Truck: 'Truck',
  Tractor: 'Tractor',
  Trailer: 'Trailer',
}

export const OPERATING_RADIUS_LABELS: Record<OperatingRadius, string> = {
  Local: 'Local',
  Intermediate: 'Intermediate',
}

export interface SubmissionDriver {
  id: string
  submissionId: string
  driverNumber: number
  name: string
  dateOfBirth: string | null
  licenseNumber: string | null
  licenseState: string | null
  dateHired: string | null
  createdAt: string
}

export interface SubmissionDriverCreate {
  driverNumber: number
  name: string
  dateOfBirth?: string
  licenseNumber?: string
  licenseState?: string
  dateHired?: string
}

// APD rating lookup constants
export const APD_VEHICLE_CLASS_OPTIONS = [
  { value: 1, label: 'Light/Medium Truck' },
  { value: 2, label: 'Heavy/Extra Heavy Truck' },
  { value: 3, label: 'Truck-Tractor' },
  { value: 4, label: 'Trailer' },
] as const

export const APD_ROAD_TYPE_OPTIONS = [
  { value: 1, label: 'On-Road Only' },
  { value: 2, label: 'On/Off-Road' },
  { value: 3, label: 'On-Road w/ Mining' },
  { value: 4, label: 'On/Off-Road w/ Mining' },
  { value: 5, label: 'Off-Road Only' },
] as const

export const APD_OPERATION_CODE_OPTIONS = [
  { value: 91, label: '91 – For-Hire' },
  { value: 92, label: '92 – Private' },
  { value: 99, label: '99 – All Other' },
] as const

export const APD_DRIVER_AGE_CODE_OPTIONS = [
  { value: 0, label: 'N/A' },
  { value: 1, label: 'Age 20' },
  { value: 2, label: 'Age 21' },
  { value: 3, label: 'Age 22' },
  { value: 4, label: 'Age 23' },
  { value: 5, label: 'Age 24' },
  { value: 6, label: 'Age 25–29' },
  { value: 7, label: 'Age 30–64' },
  { value: 8, label: 'Age 65+' },
] as const

export const APD_DRIVER_POINTS_CODE_OPTIONS = [
  { value: 0, label: '0 Points' },
  { value: 1, label: '1 Point' },
  { value: 2, label: '2 Points' },
  { value: 3, label: '3 Points' },
  { value: 4, label: '4 Points' },
  { value: 5, label: '5+ Points' },
] as const

export const APD_DRIVER_EXP_MOD_OPTIONS = [
  { value: 1.0, label: '1.00 – Standard' },
  { value: 1.15, label: '1.15 – Surcharge' },
  { value: 1.25, label: '1.25 – High Surcharge' },
] as const

export const APD_COMP_DEDUCTIBLE_OPTIONS = [
  { value: 1000, label: '$1,000' },
  { value: 2500, label: '$2,500' },
  { value: 5000, label: '$5,000' },
  { value: 10000, label: '$10,000' },
  { value: 25000, label: '$25,000' },
] as const

export const APD_COLL_DEDUCTIBLE_OPTIONS = [
  { value: 1000, label: '$1,000' },
  { value: 2500, label: '$2,500' },
  { value: 5000, label: '$5,000' },
  { value: 10000, label: '$10,000' },
  { value: 25000, label: '$25,000' },
] as const

export const APD_SUPPORTED_STATES = [
  'AL','AR','AZ','CO','FL','GA','IA','ID','IL','IN','KS','KY','LA',
  'MD','MI','MN','MO','MS','MT','NC','ND','NE','NM','NV','OH','OK',
  'OR','PA','SC','SD','TN','TX','UT','VA','WA','WI','WV','WY',
] as const

export interface SubmissionVehicle {
  id: string
  submissionId: string
  unitNumber: number
  year: number | null
  make: string | null
  model: string | null
  vin: string | null
  gvw: number | null
  vehicleClass: VehicleClass
  garagingZip: string | null
  radius: OperatingRadius | null
  createdAt: string
  // APD rating inputs
  apdVehicleClass: number | null
  apdRoadType: number | null
  apdAnnualMiles: number | null
  apdOperationCode: number | null
  apdState: string | null
  apdStatedValue: number | null
  apdCompDeductible: number | null
  apdCollDeductible: number | null
  apdDriverAgeCode: number | null
  apdDriverPointsCode: number | null
  apdDriverExpMod: number | null
}

export interface SubmissionVehicleCreate {
  unitNumber: number
  year?: number
  make?: string
  model?: string
  vin?: string
  gvw?: number
  vehicleClass: VehicleClass
  garagingZip?: string
  radius?: OperatingRadius
  // APD rating inputs
  apdVehicleClass?: number
  apdRoadType?: number
  apdAnnualMiles?: number
  apdOperationCode?: number
  apdState?: string
  apdStatedValue?: number
  apdCompDeductible?: number
  apdCollDeductible?: number
  apdDriverAgeCode?: number
  apdDriverPointsCode?: number
  apdDriverExpMod?: number
}

export interface SubmissionLocation {
  id: string
  submissionId: string
  locationNumber: number
  address: string
  city: string | null
  state: string | null
  county: string | null
  zipCode: string | null
  country: string | null
  isPrimary: boolean
  createdAt: string
}

export interface SubmissionLocationCreate {
  locationNumber: number
  address: string
  city?: string
  state?: string
  county?: string
  zipCode?: string
  country?: string
  isPrimary: boolean
}

export interface SubmissionPriorCarrier {
  id: string
  submissionId: string
  lineOfBusiness: string | null
  carrierName: string
  policyNumber: string | null
  expirationDate: string | null
  premium: number | null
  createdAt: string
}

export interface SubmissionPriorCarrierCreate {
  lineOfBusiness?: string
  carrierName: string
  policyNumber?: string
  expirationDate?: string
  premium?: number
}

export type AdditionalInterestAppliesToType = 'Blanket' | 'ScheduledItems'
export type AdditionalInterestCoverageType = 'AdditionalInsured' | 'LossPayee' | 'WaiverOfSubrogation' | 'PrimaryNonContributory'
export type AdditionalInterestChargeMethod = 'NoCharge' | 'Included' | 'PerInterest' | 'BlanketFlat'

export const ADDITIONAL_INTEREST_APPLIES_TO_LABELS: Record<AdditionalInterestAppliesToType, string> = {
  Blanket: 'Blanket',
  ScheduledItems: 'Scheduled items',
}

export const ADDITIONAL_INTEREST_COVERAGE_LABELS: Record<AdditionalInterestCoverageType, string> = {
  AdditionalInsured: 'Additional Insured',
  LossPayee: 'Loss Payee',
  WaiverOfSubrogation: 'Waiver of Subrogation',
  PrimaryNonContributory: 'Primary & Non-Contributory',
}

export const ADDITIONAL_INTEREST_CHARGE_METHOD_LABELS: Record<AdditionalInterestChargeMethod, string> = {
  NoCharge: 'No charge',
  Included: 'Included',
  PerInterest: 'Per interest',
  BlanketFlat: 'Blanket flat',
}

export interface SubmissionAdditionalInterest {
  id: string
  submissionId: string
  lineOfBusiness: string
  name: string
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  zipCode: string | null
  email: string | null
  phone: string | null
  appliesToType: AdditionalInterestAppliesToType
  scheduledItemNumbers: string | null
  additionalInsured: boolean
  lossPayee: boolean
  waiverOfSubrogation: boolean
  primaryNonContributory: boolean
  notes: string | null
  createdAt: string
}

export interface SubmissionAdditionalInterestCreate {
  lineOfBusiness: string
  name: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  state?: string
  zipCode?: string
  email?: string
  phone?: string
  appliesToType: AdditionalInterestAppliesToType
  scheduledItemNumbers?: string
  additionalInsured: boolean
  lossPayee: boolean
  waiverOfSubrogation: boolean
  primaryNonContributory: boolean
  notes?: string
}

export interface SubmissionAdditionalInterestBlanket {
  id: string
  submissionId: string
  lineOfBusiness: string
  additionalInsured: boolean
  waiverOfSubrogation: boolean
  primaryNonContributory: boolean
  createdAt: string
}

export interface SubmissionAdditionalInterestBlanketUpsert {
  additionalInsured: boolean
  waiverOfSubrogation: boolean
  primaryNonContributory: boolean
}

export interface CarrierAdditionalInterestRate {
  id: string
  carrierId: string | null
  lineOfBusiness: string | null
  coverageType: AdditionalInterestCoverageType
  chargeMethod: AdditionalInterestChargeMethod
  perInterestAmount: number | null
  blanketAmount: number | null
  minimumCharge: number | null
  maximumCharge: number | null
  state: string | null
  effectiveDate: string | null
  expirationDate: string | null
  isActive: boolean
  createdAt: string
}

export interface CarrierAdditionalInterestRateCreate {
  carrierId?: string
  lineOfBusiness?: string
  coverageType: AdditionalInterestCoverageType
  chargeMethod: AdditionalInterestChargeMethod
  perInterestAmount?: number
  blanketAmount?: number
  minimumCharge?: number
  maximumCharge?: number
  state?: string
  effectiveDate?: string
  expirationDate?: string
  isActive: boolean
}

// GL endorsement/surcharge options
export const GL_OCC_LIMIT_OPTIONS = [
  { value: 300_000,   label: '$300,000' },
  { value: 500_000,   label: '$500,000' },
  { value: 1_000_000, label: '$1,000,000' },
] as const

export const GL_PCO_LIMIT_OPTIONS = [
  { value: 1_000_000, label: '$1,000,000' },
  { value: 2_000_000, label: '$2,000,000' },
] as const

export const GL_MED_LIMIT_OPTIONS = [
  { value: 5_000,  label: '$5,000' },
  { value: 10_000, label: '$10,000' },
  { value: 15_000, label: '$15,000' },
  { value: 25_000, label: '$25,000' },
] as const

// Logging & Lumbering endorsement (class 97111 only) — greater of a flat minimum
// or a % of the 97111 premium, keyed by the selected limit.
export const GL_LL_LIMIT_OPTIONS = [
  { value: 100_000,   label: '$100,000' },
  { value: 250_000,   label: '$250,000' },
  { value: 500_000,   label: '$500,000' },
  { value: 1_000_000, label: '$1,000,000' },
] as const

export const GL_CLASS_CODE_OPTIONS = [
  { value: '97111', label: '97111 – Logging and Lumbering' },
  { value: '99793', label: '99793 – Truckers – Common/Contract' },
  { value: '43822', label: '43822 – Forestry Services' },
  { value: '49451', label: '49451 – Vacant Land – Other' },
  { value: '61226', label: '61226 – Buildings/Premises – Office NOC' },
  { value: '61224', label: '61224 – Buildings/Premises – Office (Emp)' },
  { value: '91581', label: '91581 – Sub-contracted Work' },
  { value: '91590', label: '91590 – Contractors Permanent Yard' },
  { value: '94007', label: '94007 – Excavation' },
  { value: '95410', label: '95410 – Grading of Land' },
  { value: '58873', label: '58873 – Saw Mills or Planing Mills' },
  { value: '59738', label: '59738 – Tie, Post or Pole Yard' },
  { value: '45819', label: '45819 – Lumberyards' },
  { value: '61212', label: '61212 – Buildings/Premises – Bank/Office (LRO)' },
] as const

export interface SubmissionGLCoverages {
  id: string
  submissionId: string
  generalAggregate: number | null
  productsCompletedOps: number | null
  eachOccurrence: number | null
  personalAndAdvInjury: number | null
  damageToRentedPremises: number | null
  medicalExpense: number | null
  totalSubcontractorCost: number | null
  classifications: SubmissionGLClassification[]
  updatedAt: string
  // GL rating inputs — endorsements & surcharges
  aiIndividualCount: number
  aiBlanket: boolean
  wosIndividualCount: number
  wosBlanket: boolean
  primaryNonContributory: boolean
  includeTria: boolean
  loggingLumberingLimit: number | null
}

export interface SubmissionGLCoveragesUpsert {
  generalAggregate?: number
  productsCompletedOps?: number
  eachOccurrence?: number
  personalAndAdvInjury?: number
  damageToRentedPremises?: number
  medicalExpense?: number
  totalSubcontractorCost?: number
  // GL rating inputs — endorsements & surcharges
  aiIndividualCount: number
  aiBlanket: boolean
  wosIndividualCount: number
  wosBlanket: boolean
  primaryNonContributory: boolean
  includeTria: boolean
  loggingLumberingLimit?: number
}

export interface SubmissionGLClassification {
  id: string
  submissionId: string
  locationNumber: number
  classCode: string | null
  description: string | null
  premiumBasis: string | null
  exposure: number | null
  createdAt: string
}

export interface SubmissionGLClassificationCreate {
  locationNumber: number
  classCode?: string
  description?: string
  premiumBasis?: string
  exposure?: number
}

export interface SubmissionIMCoverages {
  id: string
  submissionId: string
  scheduledEquipmentTotalLimit: number | null
  unscheduledEquipmentLimit: number | null
  maximumValueAnyOneItem: number | null
  deductible: number | null
  coinsurancePercentage: number | null
  equipmentSchedule: SubmissionEquipment[]
  updatedAt: string
}

export interface SubmissionIMCoveragesUpsert {
  scheduledEquipmentTotalLimit?: number
  unscheduledEquipmentLimit?: number
  maximumValueAnyOneItem?: number
  deductible?: number
  coinsurancePercentage?: number
}

// 4 fixed deductible tiers + null sentinel for "10% ACV". Stored on
// SubmissionEquipment.deductible (number for the dollar tiers, null for 10% ACV).
export const IM_DEDUCTIBLE_TIERS = [
  { value: 2500, label: '$2,500' },
  { value: 5000, label: '$5,000' },
  { value: 10000, label: '$10,000' },
  { value: 25000, label: '$25,000' },
  { value: null, label: '10% ACV' },
] as const

export type SettlementBasis = 'ACV' | 'RCV'
export const SETTLEMENT_BASIS_LABELS: Record<SettlementBasis, string> = {
  ACV: 'Actual Cash Value (ACV)',
  RCV: 'Replacement Cost (RCV)',
}

export interface SubmissionEquipment {
  id: string
  submissionId: string
  itemNumber: number
  year: number | null
  make: string | null
  model: string | null
  description: string | null
  serialNumber: string | null
  value: number | null
  // IM rating inputs
  equipmentTypeId: string | null
  territoryCode: string | null
  deductible: number | null         // dollar tier; null means "10% ACV"
  settlementBasis: SettlementBasis | null
  createdAt: string
}

export interface SubmissionEquipmentCreate {
  itemNumber: number
  year?: number
  make?: string
  model?: string
  description?: string
  serialNumber?: string
  value?: number
  equipmentTypeId?: string | null
  territoryCode?: string | null
  deductible?: number | null
  settlementBasis?: SettlementBasis | null
}

// IM-specific reference data
export interface IMEquipmentType {
  id: string
  typeNumber: number
  name: string
}

export interface IMTerritory {
  id: string
  territoryNumber: number
  code: string         // string form of territoryNumber, used as the FK on equipment
  states: string       // CSV like "AL,AR,FL,GA,LA,MS,OK,SC,TX"
  modifier: number
}

export interface SubmissionSupplemental {
  id: string
  submissionId: string
  commoditiesHauled: string[]
  terminalLocations: string[]
  safetyProgramInPlace: boolean
  filingsRequired: string[]
  ownerOperator: boolean
  updatedAt: string
}

export interface SubmissionSupplementalUpsert {
  commoditiesHauled: string[]
  terminalLocations: string[]
  safetyProgramInPlace: boolean
  filingsRequired: string[]
  ownerOperator: boolean
}
