import { Component, type ReactNode } from 'react'

interface Props {
  children: ReactNode
  fallback?: ReactNode
  /** When this changes (e.g. the route path), a caught error is cleared so a
   *  crash on one page doesn't stick across navigation (audit O13). */
  resetKey?: string | number
}

interface State {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, error: null }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error }
  }

  componentDidUpdate(prev: Props) {
    if (this.state.hasError && prev.resetKey !== this.props.resetKey) {
      this.setState({ hasError: false, error: null })
    }
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback ?? (
        <div className="flex h-full items-center justify-center p-8">
          <div
            style={{
              width: 'min(100%, 460px)',
              padding: '16px 18px',
              border: '1px solid var(--danger-border)',
              borderLeft: '4px solid var(--error-border)',
              borderRadius: 'var(--r-lg)',
              background: 'var(--surface)',
              boxShadow: 'var(--shadow-sm)',
            }}
          >
            <p style={{ margin: 0, fontSize: 'var(--fs-md)', fontWeight: 600, color: 'var(--bad-fg)' }}>
              Something went wrong
            </p>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-body)', color: 'var(--ink-3)' }}>
              This page could not finish loading. Try again, or reload the app if it persists.
            </p>
            {import.meta.env.DEV && this.state.error?.message && (
              <p style={{ margin: '8px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--ink-4)' }}>
                {this.state.error.message}
              </p>
            )}
            <div style={{ display: 'flex', gap: 8, marginTop: 14 }}>
              <button
                className="sd-btn outline sm"
                onClick={() => this.setState({ hasError: false, error: null })}
              >
                Try again
              </button>
              <button className="sd-btn sm" onClick={() => window.location.reload()}>
                Reload page
              </button>
            </div>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}
