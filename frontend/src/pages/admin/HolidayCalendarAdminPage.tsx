import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { adminHolidayCalendarApi } from '@/api/admin.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

export function HolidayCalendarAdminPage() {
  const qc = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ date: '', name: '' })

  const { data: holidays = [], isLoading } = useQuery({
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
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Add failed'),
  })

  const { mutate: remove } = useMutation({
    mutationFn: (id: string) => adminHolidayCalendarApi.delete(id),
    onSuccess: () => { toast.success('Removed'); qc.invalidateQueries({ queryKey: ['admin', 'holiday-calendar'] }) },
    onError: () => toast.error('Delete failed'),
  })

  if (isLoading) return <LoadingSpinner />

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
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg text-sm hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" /> Add Holiday
          </button>
        }
      />

      {showForm && (
        <div className="bg-white border rounded-lg p-5 space-y-4 max-w-md">
          <h3 className="font-semibold text-slate-700">Add Holiday</h3>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs text-slate-500 block mb-1">Date *</label>
              <input
                type="date"
                value={form.date}
                onChange={(e) => setForm({ ...form, date: e.target.value })}
                className="w-full border rounded-lg px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-slate-500 block mb-1">Name *</label>
              <input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Labor Day"
                className="w-full border rounded-lg px-3 py-2 text-sm"
              />
            </div>
          </div>
          <div className="flex gap-2">
            <button
              disabled={creating || !form.date || !form.name}
              onClick={() => create()}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg text-sm disabled:opacity-50"
            >
              Add
            </button>
            <button onClick={() => setShowForm(false)} className="px-4 py-2 border rounded-lg text-sm">
              Cancel
            </button>
          </div>
        </div>
      )}

      {holidays.length === 0 ? (
        <div className="bg-white border rounded-lg p-10 text-center text-sm text-slate-400">
          No holidays configured. Holidays are excluded from business-day due-date calculations.
        </div>
      ) : (
        <div className="space-y-4">
          {Object.keys(grouped).sort((a, b) => b.localeCompare(a)).map((year) => (
            <div key={year} className="bg-white border rounded-lg overflow-hidden">
              <div className="px-4 py-2 bg-slate-50 border-b text-xs font-semibold text-slate-500 uppercase tracking-wide">
                {year}
              </div>
              <table className="w-full text-sm">
                <tbody className="divide-y">
                  {grouped[year].sort((a, b) => a.date.localeCompare(b.date)).map((h) => (
                    <tr key={h.id} className="hover:bg-slate-50">
                      <td className="px-4 py-3 text-slate-600 w-36">
                        {new Date(h.date + 'T00:00:00').toLocaleDateString(undefined, { month: 'long', day: 'numeric' })}
                      </td>
                      <td className="px-4 py-3 font-medium text-slate-800">{h.name}</td>
                      <td className="px-4 py-3 text-right">
                        <button
                          onClick={() => { if (confirm(`Remove "${h.name}"?`)) remove(h.id) }}
                          className="text-slate-300 hover:text-red-500 p-1"
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
