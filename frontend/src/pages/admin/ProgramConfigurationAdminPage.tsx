import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { Check, Pencil, Plus, Save, X } from 'lucide-react'
import { toast } from 'sonner'
import { carriersApi } from '@/api/carriers.api'
import { programConfigurationsApi } from '@/api/programConfigurations.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import { ACTIVE_LOBS, LOB_LABELS, type PolicyLineOfBusiness } from '@/types/quote.types'
import type { ProgramConfiguration, ProgramConfigurationUpsert } from '@/types/programConfiguration.types'

const STATES = [
  'ALL', 'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY', 'DC',
]

const emptyProgram: ProgramConfigurationUpsert = {
  name: '',
  code: '',
  carrierId: null,
  lineOfBusiness: 'InlandMarine',
  stateCode: 'ALL',
  isActive: true,
  notes: '',
}

const inputCls = 'w-full rounded border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400'

export function ProgramConfigurationAdminPage() {
  const qc = useQueryClient()
  const [form, setForm] = useState<ProgramConfigurationUpsert>(emptyProgram)
  const [editingId, setEditingId] = useState<string | null>(null)

  const { data: programs = [], isLoading } = useQuery({
    queryKey: ['admin', 'program-configurations'],
    queryFn: () => programConfigurationsApi.getAll(true),
  })

  const { data: carriers = [] } = useQuery({
    queryKey: ['carriers', 'active'],
    queryFn: () => carriersApi.getAll(true),
  })

  const saveProgram = useMutation({
    mutationFn: () => {
      const payload = cleanProgram(form)
      return editingId
        ? programConfigurationsApi.update(editingId, payload)
        : programConfigurationsApi.create(payload)
    },
    onSuccess: () => {
      toast.success('Program configuration saved')
      setForm(emptyProgram)
      setEditingId(null)
      qc.invalidateQueries({ queryKey: ['admin', 'program-configurations'] })
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Program configuration could not be saved')),
  })

  function editProgram(program: ProgramConfiguration) {
    setEditingId(program.id)
    setForm({
      name: program.name,
      code: program.code,
      carrierId: program.carrierId,
      lineOfBusiness: program.lineOfBusiness,
      stateCode: program.stateCode,
      isActive: program.isActive,
      notes: program.notes ?? '',
    })
  }

  function resetForm() {
    setForm(emptyProgram)
    setEditingId(null)
  }

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="space-y-5 p-6">
      <PageHeader
        title="Program Configuration"
        subtitle="Reusable program scopes for company, line, state, guidelines, and AI rule setup"
      />

      <div className="grid gap-5 xl:grid-cols-[380px_1fr]">
        <section className="rounded-lg border bg-white">
          <div className="flex items-center justify-between gap-3 border-b px-5 py-4">
            <h2 className="text-sm font-semibold text-slate-800">{editingId ? 'Edit program' : 'New program'}</h2>
            {editingId && (
              <button type="button" onClick={resetForm} className="sims-icon-btn" title="Cancel edit">
                <X className="h-4 w-4" />
              </button>
            )}
          </div>
          <div className="space-y-3 p-5">
            <label className="block">
              <span className="sims-field-label">Program name</span>
              <input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} className={inputCls} />
            </label>
            <label className="block">
              <span className="sims-field-label">Program code</span>
              <input value={form.code} onChange={(e) => setForm((f) => ({ ...f, code: e.target.value.toUpperCase() }))} className={`${inputCls} font-mono`} />
            </label>
            <label className="block">
              <span className="sims-field-label">Company</span>
              <select value={form.carrierId ?? ''} onChange={(e) => setForm((f) => ({ ...f, carrierId: e.target.value || null }))} className={inputCls}>
                <option value="">All companies</option>
                {carriers.map((carrier) => <option key={carrier.id} value={carrier.id}>{carrier.name}</option>)}
              </select>
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="sims-field-label">Line</span>
                <select value={form.lineOfBusiness} onChange={(e) => setForm((f) => ({ ...f, lineOfBusiness: e.target.value as PolicyLineOfBusiness }))} className={inputCls}>
                  {ACTIVE_LOBS.map((lob) => <option key={lob} value={lob}>{LOB_LABELS[lob]}</option>)}
                </select>
              </label>
              <label className="block">
                <span className="sims-field-label">State</span>
                <select value={form.stateCode} onChange={(e) => setForm((f) => ({ ...f, stateCode: e.target.value }))} className={inputCls}>
                  {STATES.map((state) => <option key={state} value={state}>{state}</option>)}
                </select>
              </label>
            </div>
            <label className="flex items-center gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700">
              <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
              Active program
            </label>
            <label className="block">
              <span className="sims-field-label">Notes</span>
              <textarea value={form.notes ?? ''} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} className={inputCls} rows={3} />
            </label>
            <button
              type="button"
              onClick={() => saveProgram.mutate()}
              disabled={saveProgram.isPending || !form.name.trim() || !form.code.trim()}
              className="inline-flex w-full items-center justify-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {editingId ? <Save className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
              {editingId ? 'Save Program' : 'Add Program'}
            </button>
          </div>
        </section>

        <section className="rounded-lg border bg-white">
          <div className="border-b px-5 py-4">
            <h2 className="text-sm font-semibold text-slate-800">Programs <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{programs.length}</span></h2>
          </div>
          <div className="overflow-auto">
            <table className="sd-table">
              <thead>
                <tr>
                  <th>Program</th>
                  <th>Code</th>
                  <th>Company</th>
                  <th>Line</th>
                  <th>State</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {programs.map((program) => (
                  <tr key={program.id}>
                    <td className="primary-cell">{program.name}</td>
                    <td className="id">{program.code}</td>
                    <td>{program.carrierName ?? 'All companies'}</td>
                    <td>{LOB_LABELS[program.lineOfBusiness]}</td>
                    <td>{program.stateCode === 'ALL' ? 'All states' : program.stateCode}</td>
                    <td>
                      <span className={`sd-pill ${program.isActive ? 'bound' : 'expired'}`}>
                        {program.isActive && <Check className="h-3 w-3" />}
                        {program.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td>
                      <div className="flex justify-end">
                        <button type="button" onClick={() => editProgram(program)} className="sims-icon-btn" title="Edit program">
                          <Pencil className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {programs.length === 0 && (
                  <tr>
                    <td colSpan={7} className="py-8 text-center" style={{ color: 'var(--ink-4)' }}>No programs configured yet.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  )
}

function cleanProgram(program: ProgramConfigurationUpsert): ProgramConfigurationUpsert {
  return {
    ...program,
    stateCode: program.stateCode || 'ALL',
    carrierId: program.carrierId || null,
    notes: program.notes?.trim() ? program.notes : null,
  }
}

function getApiErrorMessage(e: unknown, fallback: string) {
  if (axios.isAxiosError(e)) {
    const data = e.response?.data
    if (typeof data === 'string') return data
    return data?.errorMessage ?? data?.message ?? data?.title ?? fallback
  }
  return fallback
}
