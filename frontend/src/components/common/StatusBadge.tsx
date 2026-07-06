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
  /**
   * Explicit pill variant (one of the .sd-pill.<variant> classes) when the domain
   * status doesn't match a known variant name. Lets pages replace bespoke local
   * StatusBadge clones without losing their colors (audit X15). Defaults to
   * status.toLowerCase().
   */
  variant?: string
}

export function StatusBadge({ status, label, variant }: StatusBadgeProps) {
  const cls = (variant ?? status).toLowerCase()
  if (import.meta.env.DEV && cls && !KNOWN_PILL_VARIANTS.has(cls)) {
    console.warn(`[StatusBadge] no pill variant for "${variant ?? status}" — rendering neutral fallback`)
  }
  return (
    <span className={`sd-pill ${cls}`}>
      {label ?? status}
    </span>
  )
}
