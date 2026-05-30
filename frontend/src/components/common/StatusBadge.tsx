interface StatusBadgeProps {
  status: string
  label?: string
}

export function StatusBadge({ status, label }: StatusBadgeProps) {
  return (
    <span className={`sd-pill ${status.toLowerCase()}`}>
      {label ?? status}
    </span>
  )
}
