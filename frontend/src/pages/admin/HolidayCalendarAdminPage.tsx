import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { adminHolidayCalendarApi } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'

export function HolidayCalendarAdminPage() {
  const qc = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ date: '', name: '' })

  const { data: holidays = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['admin', 'holiday-calendar'],
    queryFn: adminHolidayCalendarApi.getAll,
  })

  const { mutate: create, isPending: creating } = useMutation({
    mutationFn: () => adminHolidayCalendarApi.create(form),
    onSuccess: () => {
      toast.success('Holiday added')
      qc.invalidateQueries({ queryKey: ['admin', 'holiday-calendar'] })
      setShowForm(false)
      setForm({ date: '', name: '' })
    },
    onError: (e) => toast.error(getApiErrorMessage(e, 'Add failed')),
  })

  const { mutate: remove } = useMutation({
    mutationFn: (id: string) => adminHolidayCalendarApi.delete(id),
    onSuccess: () => { toast.success('Removed'); qc.invalidateQueries({ queryKey: ['admin', 'holiday-calendar'] }) },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Delete failed')),
  })

  if (isLoading) return <LoadingSpinner />
  if (isError) return (
    <div className="p-6 space-y-5">
      <PageHeader title="Holiday Calendar" />
      <ErrorState error={error} onRetry={refetch} />
    </div>
  )

  const grouped = holidays.reduce<Record<string, typeof holidays>>((acc, h) => {
    const year = h.date.slice(0, 4)
    ;(acc[year] ??= []).push(h)
    return acc
  }, {})

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="Holiday Calendar"
        subtitle={`${holidays.length} holidays configured`}
        action={
          <button
            onClick={() => setShowForm(true)}
            className="sd-btn primary"
          >
            <Plus className="h-4 w-4" /> Add Holiday
          </button>
        }
      />

      {showForm && (
        <div className="admin-panel max-w-md p-5 space-y-4">
          <h3 className="admin-panel-title">Add Holiday</h3>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="sims-field-label">Date *</label>
              <input
                type="date"
                value={form.date}
                onChange={(e) => setForm({ ...form, date: e.target.value })}
                className="sims-input"
              />
            </div>
            <div>
              <label className="sims-field-label">Name *</label>
              <input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Labor Day"
                className="sims-input"
              />
            </div>
          </div>
          <div className="flex gap-2">
            <button
              disabled={creating || !form.date || !form.name}
              onClick={() => create()}
              className="sd-btn primary"
            >
              Add
            </button>
            <button onClick={() => setShowForm(false)} className="sd-btn outline">
              Cancel
            </button>
          </div>
        </div>
      )}

      {holidays.length === 0 ? (
        <div className="admin-empty">
          No holidays configured. Holidays are excluded from business-day due-date calculations.
        </div>
      ) : (
        <div className="space-y-4">
          {Object.keys(grouped).sort((a, b) => b.localeCompare(a)).map((year) => (
            <div key={year} className="admin-panel">
              <div className="px-4 py-2 text-xs font-semibold uppercase tracking-wide" style={{ borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
                {year}
              </div>
              <table className="sd-table">
                <tbody className="divide-y">
                  {grouped[year].sort((a, b) => a.date.localeCompare(b.date)).map((h) => (
                    <tr key={h.id}>
                      <td className="w-36">
                        {new Date(h.date + 'T00:00:00').toLocaleDateString(undefined, { month: 'long', day: 'numeric' })}
                      </td>
                      <td className="primary-cell">{h.name}</td>
                      <td className="text-right">
                        <button
                          onClick={() => { if (confirm(`Remove "${h.name}"?`)) remove(h.id) }}
                          className="admin-icon-action danger ml-auto"
                          aria-label="Remove holiday"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
