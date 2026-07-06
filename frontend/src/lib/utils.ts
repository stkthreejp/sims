import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

interface CurrencyOptions {
  /** Compact large amounts as $1.2K / $3.4M (default false — full amount). */
  compact?: boolean
  /** Show cents (default true). Pass false for whole-dollar display. */
  cents?: boolean
}

/**
 * Single currency formatter (audit X11). Default renders full USD with cents,
 * matching the historical behavior. Pass { compact } for $1.2K/$3.4M and
 * { cents: false } for whole dollars — use these instead of hand-rolled local
 * fmtMoney/money helpers.
 */
export function formatCurrency(amount: number, opts: CurrencyOptions = {}): string {
  const { compact = false, cents = true } = opts
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    notation: compact ? 'compact' : 'standard',
    minimumFractionDigits: cents ? 2 : 0,
    maximumFractionDigits: cents ? 2 : (compact ? 1 : 0),
  }).format(amount)
}

/**
 * Parse an API date value without the UTC-midnight shift.
 * Backend DateOnly fields serialize as 'yyyy-MM-dd'; `new Date('yyyy-MM-dd')`
 * is UTC midnight, which renders one day early in US timezones.
 */
export function parseDateOnly(date: string): Date {
  return /^\d{4}-\d{2}-\d{2}$/.test(date) ? new Date(date + 'T00:00:00') : new Date(date)
}

export function formatDate(date: string | null | undefined): string {
  if (!date) return '—'
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric' }).format(parseDateOnly(date))
}

/** Today's date as 'yyyy-MM-dd' in the user's local timezone (never UTC). */
export function todayLocal(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function formatDateTime(date: string | null | undefined): string {
  if (!date) return '—'
  return new Intl.DateTimeFormat('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit',
  }).format(new Date(date))
}

/**
 * Formats a fractional rate (0–1, e.g. 0.075 → "7.50%") as a percent.
 * NOTE: the input is a 0–1 fraction, NOT a 0–100 percentage — passing 7.5 yields
 * "750%". Local fmtPct helpers that expect 0–100 are the off-by-100 trap in X11.
 */
export function formatPercent(rate: number): string {
  return new Intl.NumberFormat('en-US', { style: 'percent', minimumFractionDigits: 2 }).format(rate)
}
