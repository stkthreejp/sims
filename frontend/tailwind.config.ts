import type { Config } from 'tailwindcss'

const config: Config = {
  darkMode: ['class'],
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // shadcn/ui compatibility
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive))',
          foreground: 'hsl(var(--destructive-foreground))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
        // SIMS design tokens
        sims: {
          bg:           'var(--bg)',
          surface:      'var(--surface)',
          'surface-2':  'var(--surface-2)',
          ink:          'var(--ink)',
          'ink-2':      'var(--ink-2)',
          'ink-3':      'var(--ink-3)',
          'ink-4':      'var(--ink-4)',
          line:         'var(--line)',
          'line-2':     'var(--line-2)',
          accent:       'var(--accent)',
          'accent-soft':'var(--accent-soft)',
          'accent-ink': 'var(--accent-ink)',
          'accent-deep':'var(--accent-deep)',
          'accent-light':'var(--accent-light)',
          hover:        'var(--hover)',
        },
      },
      borderRadius: {
        lg: 'var(--radius)',
        md: 'calc(var(--radius) - 2px)',
        sm: 'calc(var(--radius) - 4px)',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      fontSize: {
        'sims-xs':   'var(--fs-xs)',
        'sims-sm':   'var(--fs-sm)',
        'sims-base': 'var(--fs-base)',
        'sims-body': 'var(--fs-body)',
        'sims-app':  'var(--fs-app)',
        'sims-md':   'var(--fs-md)',
        'sims-lg':   'var(--fs-lg)',
        'sims-xl':   'var(--fs-xl)',
      },
      width: {
        sidebar: 'var(--sidebar-w)',
      },
      height: {
        topbar: 'var(--topbar-h)',
      },
    },
  },
  plugins: [require('tailwindcss-animate')],
}

export default config
