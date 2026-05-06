import { useState, useRef, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Bell, LogOut } from 'lucide-react'
import { useAuthStore } from '@/store/authStore'
import { authApi } from '@/api/auth.api'
import { queryClient } from '@/lib/queryClient'

export function Topbar() {
  const { user, clearAuth } = useAuthStore()
  const navigate = useNavigate()
  const [searchOpen, setSearchOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (searchOpen) inputRef.current?.focus()
  }, [searchOpen])

  const handleLogout = async () => {
    try {
      await authApi.logout()
    } finally {
      clearAuth()
      queryClient.clear()
      navigate('/login')
    }
  }

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const q = searchQuery.trim()
    if (!q) return
    navigate(`/insureds?search=${encodeURIComponent(q)}`)
    setSearchQuery('')
    setSearchOpen(false)
  }

  const initials = user?.fullName
    ? user.fullName.split(' ').map((w) => w[0]).join('').slice(0, 2).toUpperCase()
    : '?'

  return (
    <header
      className="flex items-center justify-between shrink-0"
      style={{
        height: 'var(--topbar-h)',
        background: 'var(--surface)',
        borderBottom: '1px solid var(--line)',
        padding: '0 var(--container-pad)',
      }}
    >
      {/* Left: search */}
      {searchOpen ? (
        <form onSubmit={handleSearchSubmit} className="flex items-center gap-2" style={{ width: 220 }}>
          <input
            ref={inputRef}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onBlur={() => { if (!searchQuery) setSearchOpen(false) }}
            onKeyDown={(e) => { if (e.key === 'Escape') { setSearchQuery(''); setSearchOpen(false) } }}
            placeholder="Search insureds…"
            style={{
              flex: 1,
              padding: '6px 10px',
              background: 'var(--surface-2)',
              borderRadius: 'var(--r-md)',
              color: 'var(--ink)',
              fontSize: 'var(--fs-base)',
              border: '1px solid var(--accent)',
              outline: 'none',
            }}
          />
        </form>
      ) : (
        <button
          onClick={() => setSearchOpen(true)}
          aria-label="Search"
          className="flex items-center gap-2"
          style={{
            padding: '6px 10px',
            background: 'var(--surface-2)',
            borderRadius: 'var(--r-md)',
            color: 'var(--ink-3)',
            fontSize: 'var(--fs-base)',
            width: 220,
            border: 'none',
            cursor: 'text',
            textAlign: 'left',
          }}
        >
          <Search style={{ width: 13, height: 13, flexShrink: 0 }} />
          <span>Search…</span>
        </button>
      )}

      {/* Right: bell + user */}
      <div className="flex items-center gap-3">
        <button
          aria-label="Notifications"
          style={{ color: 'var(--ink-3)', display: 'grid', placeItems: 'center' }}
        >
          <Bell style={{ width: 16, height: 16 }} />
        </button>

        <div
          className="flex items-center gap-2"
          style={{ fontSize: 'var(--fs-base)', color: 'var(--ink-2)' }}
        >
          {/* Avatar */}
          <span
            style={{
              width: 26,
              height: 26,
              borderRadius: '50%',
              background: 'var(--accent-soft)',
              color: 'var(--accent-ink)',
              fontSize: 10,
              fontWeight: 700,
              display: 'grid',
              placeItems: 'center',
            }}
          >
            {initials}
          </span>
          <span style={{ fontWeight: 500 }}>{user?.fullName}</span>
          {user?.roles[0] && (
            <span style={{ color: 'var(--ink-4)' }}>· {user.roles[0]}</span>
          )}
        </div>

        <button
          onClick={handleLogout}
          className="flex items-center gap-1.5 transition-colors"
          style={{ fontSize: 'var(--fs-base)', color: 'var(--ink-3)' }}
          onMouseEnter={(e) => (e.currentTarget.style.color = 'var(--ink)')}
          onMouseLeave={(e) => (e.currentTarget.style.color = 'var(--ink-3)')}
        >
          <LogOut style={{ width: 14, height: 14 }} />
          Logout
        </button>
      </div>
    </header>
  )
}
