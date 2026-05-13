// Sample optional-endorsements data
window.PROPOSAL_ENDORSEMENTS = [
  {
    name: 'Debris Removal',
    limits: [
      { label: 'Any one loss',   value: '$2,500' },
      { label: 'Aggregate',      value: '$10,000' },
    ],
    included: true,
    premium: '$250.00',
    premiumNum: 250,
  },
  {
    name: 'Rental Reimbursement',
    limits: [
      { label: 'Per day',        value: '$2,500' },
      { label: 'Aggregate',      value: '$10,000' },
    ],
    included: true,
    premium: '$500.00',
    premiumNum: 500,
  },
  {
    name: 'Towing, Storage & Recovery',
    limits: [
      { label: 'Any one loss',   value: '$5,000' },
    ],
    included: true,
    premium: '$175.00',
    premiumNum: 175,
  },
  {
    name: 'Newly Acquired Equipment',
    note: 'Coverage for newly purchased units, reported within 30 days.',
    limits: [
      { label: 'Maximum limit',  value: '$25,000' },
    ],
    included: false,
    premium: null,
    premiumNum: 0,
  },
];
