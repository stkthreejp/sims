/** Validates a standard email address */
export function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())
}

/** Validates a US phone number (accepts 10 digits in any common format) */
export function isValidPhone(value: string): boolean {
  const digits = value.replace(/\D/g, '')
  return digits.length === 10
}

/** Validates a US ZIP code (5-digit or ZIP+4) */
export function isValidZip(value: string): boolean {
  return /^\d{5}(-\d{4})?$/.test(value.trim())
}

/**
 * Formats a raw digit string into (XXX) XXX-XXXX as the user types.
 * Strips non-digits, then applies mask progressively.
 */
export function formatPhoneInput(raw: string): string {
  const digits = raw.replace(/\D/g, '').slice(0, 10)
  if (digits.length <= 3) return digits.length ? `(${digits}` : ''
  if (digits.length <= 6) return `(${digits.slice(0, 3)}) ${digits.slice(3)}`
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`
}

/** react-hook-form compatible validators */
export const rhfValidators = {
  email: (v: string) =>
    !v || isValidEmail(v) || 'Enter a valid email address',

  phone: (v: string) =>
    !v || isValidPhone(v) || 'Enter a valid 10-digit phone number',

  zip: (v: string) =>
    !v || isValidZip(v) || 'Enter a valid ZIP code (e.g. 78701 or 78701-1234)',
}
