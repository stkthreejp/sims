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
