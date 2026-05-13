import { Component, type ReactNode } from 'react'

interface Props {
  children: ReactNode
  fallback?: ReactNode
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

  render() {
    if (this.state.hasError) {
      return this.props.fallback ?? (
        <div className="flex h-full items-center justify-center p-8">
          <div
            style={{
              width: 'min(100%, 460px)',
              padding: '16px 18px',
              border: '1px solid #f3c6be',
              borderLeft: '4px solid #b33a2a',
              borderRadius: 'var(--r-lg)',
              background: 'var(--surface)',
              boxShadow: 'var(--shadow-sm)',
            }}
          >
            <p style={{ margin: 0, fontSize: 'var(--fs-md)', fontWeight: 600, color: 'var(--bad-fg)' }}>
              Something went wrong
            </p>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-body)', color: 'var(--ink-3)' }}>
              This page could not finish loading. Try again to reload this section.
            </p>
            {import.meta.env.DEV && this.state.error?.message && (
              <p style={{ margin: '8px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--ink-4)' }}>
                {this.state.error.message}
              </p>
            )}
            <button
              className="sd-btn outline sm"
              style={{ marginTop: 14 }}
              onClick={() => this.setState({ hasError: false, error: null })}
            >
              Try again
            </button>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}
