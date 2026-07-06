import { useEffect } from 'react'

/**
 * Warns before losing unsaved edits on a full page unload — tab close, refresh, or
 * hard navigation — while `isDirty` (audit X10). Pass the same isDirty flag a page
 * already tracks for its Save button.
 *
 * NOTE: this intentionally does NOT block in-app (React Router) navigation. That
 * requires react-router's `useBlocker`, which only works under a data router
 * (createBrowserRouter); the app currently uses <BrowserRouter>. When the router is
 * migrated to a data router, add a useBlocker branch here — see the audit's deferred note.
 */
export function useUnsavedChangesGuard(isDirty: boolean) {
  useEffect(() => {
    if (!isDirty) return
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [isDirty])
}
