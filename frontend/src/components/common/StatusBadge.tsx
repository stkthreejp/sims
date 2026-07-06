// Known pill variants defined in index.css (.sd-pill.<variant>). A status outside
// this set still renders a visible neutral pill via the .sd-pill default, but we
// warn in dev so an unmapped/raw enum value gets caught instead of shipping (audit G7).
const KNOWN_PILL_VARIANTS = new Set([
  'draft', 'new', 'inprogress', 'submitted', 'quoted', 'bound', 'declined',
  'cancelled', 'expired', 'withdrawn', 'active', 'renewed', 'nonrenewed',
  'posted', 'voided', 'good', 'expiring',
])

interface StatusBadgeProps {
  status: string
  label?: string
}

export function StatusBadge({ status, label }: StatusBadgeProps) {
  const variant = status.toLowerCase()
  if (import.meta.env.DEV && status && !KNOWN_PILL_VARIANTS.has(variant)) {
    console.warn(`[StatusBadge] no pill variant for status "${status}" — rendering neutral fallback`)
  }
  return (
    <span className={`sd-pill ${variant}`}>
      {label ?? status}
    </span>
  )
}
