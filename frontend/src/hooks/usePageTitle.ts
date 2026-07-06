import { useEffect } from 'react'

const BASE_TITLE = 'SIMS — Insurance Management'

/**
 * Sets the browser tab title to "<title> · SIMS" so multi-tab MGA work is
 * distinguishable (audit O23). Restores the base title on unmount. Called from
 * PageHeader, so any page using PageHeader gets a meaningful tab title for free.
 */
export function usePageTitle(title: string | undefined) {
  useEffect(() => {
    document.title = title ? `${title} · SIMS` : BASE_TITLE
    return () => { document.title = BASE_TITLE }
  }, [title])
}
