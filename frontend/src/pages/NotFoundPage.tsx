import { useNavigate } from 'react-router-dom'

export function NotFoundPage() {
  const navigate = useNavigate()
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 16 }}>
      <p style={{ fontSize: 48, fontWeight: 700, color: 'var(--ink-4)', margin: 0 }}>404</p>
      <p style={{ fontSize: 15, color: 'var(--ink-3)', margin: 0 }}>Page not found.</p>
      <button className="sd-btn sm outline" onClick={() => navigate('/dashboard')}>Go to Dashboard</button>
    </div>
  )
}
