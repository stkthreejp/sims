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
  zipCode: string | null
  createdAt: string
}

export interface SubmissionLocationCreate {
  locationNumber: number
  address: string
  zipCode?: string
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
}

export interface SubmissionGLCoveragesUpsert {
  generalAggregate?: number
  productsCompletedOps?: number
  eachOccurrence?: number
  personalAndAdvInjury?: number
  damageToRentedPremises?: number
  medicalExpense?: number
  totalSubcontractorCost?: number
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
