export type TemplateEntityType = 'General' | 'Policy' | 'Submission' | 'Carrier' | 'Agent'

export interface TemplateTag {
  name: string
  description: string
}

export interface TagGroup {
  label: string
  tags: TemplateTag[]
}

const SYSTEM_TAGS: TagGroup = {
  label: 'System',
  tags: [
    { name: 'TodayDate', description: "Today's date" },
    { name: 'CompanyName', description: 'SMM company name' },
    { name: 'PageNumber', description: 'Current page number' },
  ],
}

export const TEMPLATE_TAGS: Record<TemplateEntityType, TagGroup[]> = {
  General: [SYSTEM_TAGS],

  Policy: [
    {
      label: 'Policy',
      tags: [
        { name: 'PolicyNumber', description: 'Policy number' },
        { name: 'EffectiveDate', description: 'Policy effective date' },
        { name: 'ExpirationDate', description: 'Policy expiration date' },
        { name: 'BoundDate', description: 'Date policy was bound' },
        { name: 'LineOfBusiness', description: 'Line of business' },
        { name: 'TotalPremium', description: 'Total premium' },
        { name: 'TaxesAndFees', description: 'Taxes and fees' },
        { name: 'NetPremium', description: 'Net premium (excl. taxes)' },
        { name: 'CommissionRate', description: 'Agent commission %' },
        { name: 'CommissionAmount', description: 'Agent commission $' },
        { name: 'Deductible', description: 'Policy deductible' },
        { name: 'CoverageLimit', description: 'Coverage limit' },
        { name: 'CoverageDescription', description: 'Coverage description' },
      ],
    },
    {
      label: 'Insured',
      tags: [
        { name: 'InsuredName', description: 'Insured full name' },
        { name: 'InsuredDBA', description: 'Doing business as' },
        { name: 'InsuredType', description: 'Individual or Business' },
        { name: 'InsuredEmail', description: 'Insured email' },
        { name: 'InsuredPhone', description: 'Insured phone' },
        { name: 'InsuredAddressLine1', description: 'Address line 1' },
        { name: 'InsuredAddressLine2', description: 'Address line 2' },
        { name: 'InsuredCity', description: 'City' },
        { name: 'InsuredState', description: 'State' },
        { name: 'InsuredZip', description: 'Zip code' },
        { name: 'InsuredCounty', description: 'County' },
      ],
    },
    {
      label: 'Carrier',
      tags: [
        { name: 'CarrierName', description: 'Carrier name' },
        { name: 'CarrierNAIC', description: 'Carrier NAIC number' },
        { name: 'CarrierAMBest', description: 'AM Best rating' },
        { name: 'CarrierAddress', description: 'Carrier address' },
      ],
    },
    {
      label: 'Agent',
      tags: [
        { name: 'AgentName', description: 'Agent full name' },
        { name: 'AgentAgency', description: 'Agency name' },
        { name: 'AgentEmail', description: 'Agent email' },
        { name: 'AgentPhone', description: 'Agent phone' },
        { name: 'AgentLicense', description: 'Agent license number' },
        { name: 'AgentCity', description: 'Agent city' },
        { name: 'AgentState', description: 'Agent state' },
      ],
    },
    {
      label: 'Underwriter',
      tags: [
        { name: 'UnderwriterName', description: 'Underwriter name' },
        { name: 'UnderwriterEmail', description: 'Underwriter email' },
        { name: 'UnderwriterPhone', description: 'Underwriter phone' },
      ],
    },
    SYSTEM_TAGS,
  ],

  Submission: [
    {
      label: 'Submission',
      tags: [
        { name: 'SubmissionNumber', description: 'Submission number' },
        { name: 'SubmissionDate', description: 'Date submitted' },
        { name: 'RequestedEffDate', description: 'Requested effective date' },
        { name: 'SubmissionStatus', description: 'Current status' },
        { name: 'LinesRequested', description: 'Lines of business requested' },
      ],
    },
    {
      label: 'Insured',
      tags: [
        { name: 'InsuredName', description: 'Insured full name' },
        { name: 'InsuredType', description: 'Individual or Business' },
        { name: 'InsuredEmail', description: 'Insured email' },
        { name: 'InsuredPhone', description: 'Insured phone' },
        { name: 'InsuredAddressLine1', description: 'Address line 1' },
        { name: 'InsuredCity', description: 'City' },
        { name: 'InsuredState', description: 'State' },
        { name: 'InsuredZip', description: 'Zip code' },
      ],
    },
    {
      label: 'Agent',
      tags: [
        { name: 'AgentName', description: 'Agent full name' },
        { name: 'AgentAgency', description: 'Agency name' },
        { name: 'AgentEmail', description: 'Agent email' },
        { name: 'AgentPhone', description: 'Agent phone' },
      ],
    },
    {
      label: 'Underwriter',
      tags: [
        { name: 'UnderwriterName', description: 'Underwriter name' },
        { name: 'UnderwriterEmail', description: 'Underwriter email' },
      ],
    },
    SYSTEM_TAGS,
  ],

  Carrier: [
    {
      label: 'Carrier',
      tags: [
        { name: 'CarrierName', description: 'Carrier name' },
        { name: 'CarrierNAIC', description: 'NAIC number' },
        { name: 'CarrierAMBest', description: 'AM Best rating' },
        { name: 'CarrierAddressLine1', description: 'Address line 1' },
        { name: 'CarrierCity', description: 'City' },
        { name: 'CarrierState', description: 'State' },
        { name: 'CarrierZip', description: 'Zip code' },
        { name: 'CarrierPhone', description: 'Phone' },
        { name: 'CarrierEmail', description: 'Email' },
      ],
    },
    SYSTEM_TAGS,
  ],

  Agent: [
    {
      label: 'Agent',
      tags: [
        { name: 'AgentName', description: 'Agent full name' },
        { name: 'AgentAgency', description: 'Agency name' },
        { name: 'AgentLicense', description: 'License number' },
        { name: 'AgentEmail', description: 'Email' },
        { name: 'AgentPhone', description: 'Phone' },
        { name: 'AgentAddressLine1', description: 'Address line 1' },
        { name: 'AgentCity', description: 'City' },
        { name: 'AgentState', description: 'State' },
      ],
    },
    SYSTEM_TAGS,
  ],
}

export const ENTITY_TYPE_LABELS: Record<TemplateEntityType, string> = {
  General: 'General',
  Policy: 'Policy',
  Submission: 'Submission',
  Carrier: 'Carrier',
  Agent: 'Agent',
}
