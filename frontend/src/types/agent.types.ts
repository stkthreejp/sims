export interface AgentContact {
  id: string
  firstName: string
  lastName: string | null
  title: string | null
  email: string | null
  phone: string | null
  isPrimary: boolean
}

export interface AgentLocation {
  id: string
  name: string | null
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  state: string | null
  zipCode: string | null
  phone: string | null
  isPrimary: boolean
  contacts: AgentContact[]
}

export interface Agent {
  id: string
  name: string
  agencyName: string | null
  licenseNumber: string | null
  email: string | null
  phone: string | null
  isActive: boolean
  createdAt: string
  locations: AgentLocation[]
}

export interface AgentListItem {
  id: string
  name: string
  agencyName: string | null
  licenseNumber: string | null
  email: string | null
  isActive: boolean
  primaryCity: string | null
  primaryState: string | null
  locationCount: number
  contactCount: number
}

export interface AgentCreate {
  name: string
  agencyName?: string
  licenseNumber?: string
  email?: string
  phone?: string
}

export interface AgentUpdate extends AgentCreate {
  isActive: boolean
}

export interface AgentContactInput {
  firstName: string
  lastName?: string
  title?: string
  email?: string
  phone?: string
  isPrimary: boolean
}

export interface AgentLocationInput {
  name?: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  state?: string
  zipCode?: string
  phone?: string
  isPrimary: boolean
  contacts: AgentContactInput[]
}

// ─── Compliance Docs ──────────────────────────────────────────────────────────

export type AgentComplianceDocType = 'EOCertificate' | 'StateLicense' | 'BrokerAgreement'
export type AgentComplianceStatus = 'Current' | 'ExpiringSoon' | 'Expired' | 'Missing'

export interface AgentComplianceDoc {
  id: string
  docType: AgentComplianceDocType
  expirationDate: string | null
  licenseState: string | null
  executedDate: string | null
  notes: string | null
  status: AgentComplianceStatus
}

export interface AgentComplianceDocUpsert {
  expirationDate?: string | null
  licenseState?: string | null
  executedDate?: string | null
  notes?: string | null
}

export interface AgentComplianceStatusResult {
  isQuoteReady: boolean
  missingOrExpired: string[]
  docs: AgentComplianceDoc[]
}

// ─── Contact Log ─────────────────────────────────────────────────────────────

export type AgentContactLogType = 'Visit' | 'Call' | 'Email' | 'Other'

export interface AgentContactLog {
  id: string
  logDate: string
  logType: AgentContactLogType
  contactName: string | null
  notes: string
  createdByName: string
  createdAt: string
}

export interface AgentContactLogCreate {
  logDate: string
  logType: AgentContactLogType
  contactName?: string | null
  notes: string
}

// ─── KPIs ────────────────────────────────────────────────────────────────────

export interface AgentKpi {
  boundPremiumLast12Months: number
  boundPremiumPrior12Months: number
  quotesIssuedLast12Months: number
  quotesBoundLast12Months: number
  hitRatio: number | null
}

// ─── Summary Stats ────────────────────────────────────────────────────────────

export interface AgentSummaryStats {
  totalAgents: number
  missingComplianceDocs: number
  eoExpiringSoon: number
  licensesExpiringSoon: number
}
