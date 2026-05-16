import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Calculator, Check, CheckCircle2, ChevronDown, ChevronRight, Copy, Download,
  Edit2, ExternalLink, FileOutput, FileText, MoreHorizontal, Pin, Plus, RefreshCw,
  Save, Send, ShieldCheck, Trash2, TrendingDown, Upload, X,
} from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import { policyFormsApi } from '@/api/policyForms.api'
import { LOB_LABELS, type CommissionOverrideRequest, type PolicyFormType, type PolicyLineOfBusiness, type QuotePolicyFormSelection, type QuotePolicyFormSelectionUpsert, type QuoteStatus } from '@/types/quote.types'
import { QuoteAutoSafetyPanel } from '@/components/quotes/QuoteAutoSafetyPanel'
import { QuoteRatingPanel } from '@/components/quotes/QuoteRatingPanel'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { GenerateDocumentModal } from '@/components/documents/GenerateDocumentModal'
import { attachmentsApi } from '@/api/attachments.api'
import { documentGenerationApi } from '@/api/documentGeneration.api'
import { outboundCommunicationsApi } from '@/api/outboundCommunications.api'
import { uwWriteupApi } from '@/api/uwWriteup.api'
import type { IMWriteupPayload, WriteupCondition } from '@/types/uwWriteup.types'
import { EMPTY_PAYLOAD } from '@/types/uwWriteup.types'
import { formatCurrency, formatDate, formatPercent } from '@/lib/utils'
import { usePermissions } from '@/hooks/usePermissions'

// ── Constants ──────────────────────────────────────────────────────────────────

const AUTO_LOBS = new Set<PolicyLineOfBusiness>(['CommercialAuto', 'AutoLiability', 'AutoPhysicalDamage'])

const QUOTE_STAGES: { label: string; statuses: QuoteStatus[] }[] = [
  { label: 'Requested',          statuses: ['Draft'] },
  { label: 'Pending Information', statuses: ['Submitted'] },
  { label: 'Quoted',             statuses: ['Quoted'] },
  { label: 'Bound',              statuses: ['Bound'] },
  { label: 'Issued',             statuses: [] },  // reached when Bound + issuedDate set
]

const STATUS_PILL: Record<QuoteStatus, string> = {
  Draft:     'draft',
  Submitted: 'submitted',
  Quoted:    'quoted',
  Bound:     'bound',
  Declined:  'declined',
  Cancelled: 'cancelled',
  Expired:   'expired',
}

function fmt(n: number | null | undefined) {
  if (n == null) return '—'
  return '$' + n.toLocaleString('en-US', { maximumFractionDigits: 0 })
}

function fmtFull(n: number | null | undefined) {
  if (n == null) return '—'
  return '$' + n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function parseLineInputs(inputs: string): Record<string, unknown> | null {
  try {
    return JSON.parse(inputs)
  } catch {
    return null
  }
}

function stageIdx(status: QuoteStatus, issuedDate: string | null): number {
  if (status === 'Bound' && issuedDate) return QUOTE_STAGES.length - 1  // "Issued"
  return QUOTE_STAGES.findIndex((s) => s.statuses.includes(status))
}

// ── Sub-components ──────────────────────────────────────────────────────────────

function StageBar({ status, issuedDate }: { status: QuoteStatus; issuedDate: string | null }) {
  const isTerminal = status === 'Declined' || status === 'Cancelled' || status === 'Expired'
  const idx = stageIdx(status, issuedDate)

  if (isTerminal) {
    return (
      <div className="mb-5 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-medium text-red-700">
        <X className="h-4 w-4" />
        Quote {status} — no further action
      </div>
    )
  }

  return (
    <div className="mb-5 flex overflow-hidden rounded-lg border border-slate-200 bg-white">
      {QUOTE_STAGES.map((s, i) => {
        const done = i < idx
        const active = i === idx
        return (
          <div
            key={s.label}
            className={`flex flex-1 items-center justify-center gap-1.5 border-r border-slate-200 px-3 py-2.5 text-xs font-semibold last:border-r-0 ${
              done   ? 'bg-emerald-50 text-emerald-700'
              : active ? 'bg-sky-50 text-sky-800'
              : 'text-slate-400'
            }`}
          >
            {done && <Check className="h-3 w-3" />}
            <span className="mr-0.5 font-mono text-[10px] opacity-60">{String(i + 1).padStart(2, '0')}</span>
            {s.label}
          </div>
        )
      })}
    </div>
  )
}

function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`sd-card ${className}`}>
      {children}
    </div>
  )
}

function CardHead({
  title, count, right,
}: { title: React.ReactNode; count?: React.ReactNode; right?: React.ReactNode }) {
  return (
    <div className="sd-card-head">
      <h3>
        {title}
        {count != null && (
          <span className="cnt">{count}</span>
        )}
      </h3>
      {right && <div className="flex items-center gap-2">{right}</div>}
    </div>
  )
}

function KV({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <div className="sims-field-label">{label}</div>
      <div className="text-sm font-medium text-slate-800 break-words">{value ?? '—'}</div>
    </div>
  )
}

function Btn({ children, onClick, variant = 'ghost', disabled, className = '', type = 'button' }: {
  children: React.ReactNode; onClick?: () => void; variant?: 'ghost' | 'outline' | 'primary' | 'danger'
  disabled?: boolean; className?: string; type?: 'button' | 'submit'
}) {
  const cls: Record<string, string> = {
    ghost:   'sd-btn ghost sm',
    outline: 'sd-btn outline sm',
    primary: 'sd-btn primary sm',
    danger:  'sd-btn danger sm',
  }
  return (
    <button type={type} disabled={disabled} onClick={onClick} className={`${cls[variant]} ${className}`}>
      {children}
    </button>
  )
}

// ── Bind checklist ─────────────────────────────────────────────────────────────

function WriteupStatusPill({ status }: { status?: string }) {
  const cls = status === 'Approved'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : status === 'Submitted'
      ? 'border-sky-200 bg-sky-50 text-sky-700'
      : status === 'Declined'
        ? 'border-red-200 bg-red-50 text-red-700'
        : 'border-slate-200 bg-slate-50 text-slate-600'

  return (
    <span className={`rounded-md border px-2 py-1 text-[10.5px] font-semibold uppercase tracking-wide ${cls}`}>
      {status ?? 'Draft'}
    </span>
  )
}

function InlineWriteupSection({
  number,
  title,
  defaultOpen = false,
  children,
}: {
  number: string
  title: string
  defaultOpen?: boolean
  children: React.ReactNode
}) {
  const [open, setOpen] = useState(defaultOpen)

  return (
    <div className="border-t border-slate-100 first:border-t-0">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center gap-3 px-5 py-3 text-left hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-200"
      >
        <span className="font-mono text-[11px] font-semibold text-slate-400">{number}</span>
        <span className="text-sm font-semibold text-slate-800">{title}</span>
        {open ? (
          <ChevronDown className="ml-auto h-4 w-4 text-slate-400" />
        ) : (
          <ChevronRight className="ml-auto h-4 w-4 text-slate-400" />
        )}
      </button>
      {open && (
        <div className="space-y-4 bg-slate-50/40 px-5 pb-5 pt-1">
          {children}
        </div>
      )}
    </div>
  )
}

function InlineWriteupTextarea({
  label,
  value,
  onChange,
  readOnly,
  rows = 3,
}: {
  label: string
  value?: string
  onChange: (value: string) => void
  readOnly: boolean
  rows?: number
}) {
  return (
    <label className="block">
      <span className="sims-field-label">{label}</span>
      <textarea
        rows={rows}
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        readOnly={readOnly}
        className="sims-textarea mt-1"
        placeholder={readOnly ? '' : 'Enter notes...'}
      />
    </label>
  )
}

function InlineWriteupCheckbox({
  label,
  checked,
  onChange,
  readOnly,
  auto,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
  readOnly: boolean
  auto?: boolean
}) {
  return (
    <label className="flex items-center gap-2 text-sm text-slate-700">
      <input
        type="checkbox"
        checked={checked}
        disabled={readOnly}
        onChange={(e) => onChange(e.target.checked)}
        className="h-4 w-4 rounded border-slate-300"
      />
      <span className={checked ? 'font-semibold text-slate-800' : ''}>{label}</span>
      {auto && <span className="rounded bg-amber-50 px-1.5 py-0.5 text-[10.5px] font-semibold text-amber-700">auto</span>}
    </label>
  )
}

function MenuButton({
  label, children, variant = 'outline',
}: { label: React.ReactNode; children: React.ReactNode; variant?: 'outline' | 'ghost' | 'primary' }) {
  const [open, setOpen] = useState(false)
  return (
    <div className="relative">
      <Btn variant={variant} onClick={() => setOpen((v) => !v)}>
        {label}
      </Btn>
      {open && (
        <div className="absolute right-0 top-9 z-20 min-w-48 overflow-hidden py-1" style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-lg)', background: 'var(--surface)', boxShadow: 'var(--shadow-sm)' }}>
          {children}
        </div>
      )}
    </div>
  )
}

function MenuItem({ children, onClick, disabled }: { children: React.ReactNode; onClick?: () => void; disabled?: boolean }) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="flex w-full items-center gap-2 px-3 py-2 text-left disabled:cursor-not-allowed disabled:opacity-50"
      style={{ color: 'var(--ink-2)', fontSize: 'var(--fs-base)', fontWeight: 500 }}
    >
      {children}
    </button>
  )
}

function QuotePolicyFormsCard({ quoteId, canManage }: { quoteId: string; canManage: boolean }) {
  const qc = useQueryClient()
  const [templateId, setTemplateId] = useState('')

  const { data: forms = [], isLoading } = useQuery({
    queryKey: ['quote-policy-forms', quoteId],
    queryFn: () => quotesApi.getPolicyForms(quoteId),
  })

  const { data: templates = [] } = useQuery({
    queryKey: ['policy-form-templates', false],
    queryFn: () => policyFormsApi.getTemplates(false),
  })

  const saveMutation = useMutation({
    mutationFn: (nextForms: QuotePolicyFormSelectionUpsert[]) => quotesApi.savePolicyForms(quoteId, nextForms),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-policy-forms', quoteId] })
      toast.success('Policy form list saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to save policy forms'),
  })

  const resetMutation = useMutation({
    mutationFn: () => quotesApi.resetPolicyForms(quoteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-policy-forms', quoteId] })
      toast.success('Policy forms refreshed from package')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to refresh policy forms'),
  })

  const toUpsert = (row: QuotePolicyFormSelection, index: number): QuotePolicyFormSelectionUpsert => ({
    policyFormTemplateId: row.policyFormTemplateId,
    sequenceOrder: index + 1,
    formType: row.formType,
    isIncluded: row.isIncluded,
    isSystemGenerated: row.isSystemGenerated,
    triggerConditionJson: row.triggerConditionJson,
    notes: row.notes,
  })

  const saveRows = (nextRows: QuotePolicyFormSelection[]) => {
    saveMutation.mutate(nextRows.map(toUpsert))
  }

  const selectedIds = new Set(forms.map((f) => f.policyFormTemplateId))
  const availableTemplates = templates.filter((t) => !selectedIds.has(t.id))
  const includedCount = forms.filter((f) => f.isIncluded).length

  return (
    <Card>
      <CardHead
        title={<span className="flex items-center gap-2"><FileText className="h-4 w-4 text-slate-500" />Policy forms for proposal</span>}
        count={includedCount}
        right={canManage && (
          <Btn variant="outline" disabled={resetMutation.isPending} onClick={() => resetMutation.mutate()}>
            <RefreshCw className="h-3.5 w-3.5" /> Refresh from package
          </Btn>
        )}
      />
      <div className="px-5 py-4">
        {isLoading ? (
          <div className="flex h-20 items-center justify-center"><LoadingSpinner /></div>
        ) : forms.length === 0 ? (
          <div className="rounded-lg border border-dashed border-slate-200 bg-slate-50 px-4 py-6 text-sm text-slate-500">
            No forms were found for this carrier, line, and state package yet.
          </div>
        ) : (
          <div className="divide-y divide-slate-100 rounded-lg border border-slate-200">
            {forms.map((form, index) => (
              <div key={form.id || `${form.policyFormTemplateId}-${index}`} className="flex items-center gap-3 px-3 py-3">
                <span className="w-7 shrink-0 text-right font-mono text-[11px] font-semibold text-slate-400">{String(index + 1).padStart(2, '0')}</span>
                <input
                  type="checkbox"
                  checked={form.isIncluded}
                  disabled={!canManage || form.formType === 'Mandatory' || saveMutation.isPending}
                  onChange={(e) => saveRows(forms.map((row, rowIndex) => rowIndex === index ? { ...row, isIncluded: e.target.checked } : row))}
                  className="h-4 w-4 rounded border-slate-300"
                />
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-semibold text-slate-800">{form.formName}</div>
                  <div className="mt-0.5 flex flex-wrap items-center gap-2 text-[11px] text-slate-500">
                    <span className="font-mono">{form.formNumber}</span>
                    <span>{form.editionDate || '-'}</span>
                    <span className="rounded bg-slate-100 px-1.5 py-0.5 font-semibold text-slate-600">{form.formType}</span>
                    {!form.isIncluded && <span className="rounded bg-amber-50 px-1.5 py-0.5 font-semibold text-amber-700">Excluded</span>}
                  </div>
                </div>
                {canManage && form.formType === 'AdHoc' && (
                  <button
                    type="button"
                    disabled={saveMutation.isPending}
                    onClick={() => saveRows(forms.filter((_, rowIndex) => rowIndex !== index))}
                    className="sims-icon-btn hover:text-red-600 disabled:opacity-50"
                    title="Remove form"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                )}
              </div>
            ))}
          </div>
        )}

        {canManage && (
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <select
              value={templateId}
              onChange={(e) => setTemplateId(e.target.value)}
              className="sims-select"
              style={{ minWidth: 256, width: 'auto' }}
            >
              <option value="">Add ad-hoc form...</option>
              {availableTemplates.map((template) => (
                <option key={template.id} value={template.id}>{template.formNumber} - {template.name}</option>
              ))}
            </select>
            <Btn
              variant="outline"
              disabled={!templateId || saveMutation.isPending}
              onClick={() => {
                const template = templates.find((t) => t.id === templateId)
                if (!template) return
                saveRows([
                  ...forms,
                  {
                    id: '',
                    quoteId,
                    policyFormTemplateId: template.id,
                    formNumber: template.formNumber,
                    formName: template.name,
                    editionDate: template.editionDate,
                    sequenceOrder: forms.length + 1,
                    formType: 'AdHoc' as PolicyFormType,
                    isIncluded: true,
                    isSystemGenerated: false,
                    triggerConditionJson: null,
                    notes: null,
                  },
                ])
                setTemplateId('')
              }}
            >
              <Plus className="h-3.5 w-3.5" /> Add form
            </Btn>
          </div>
        )}
      </div>
    </Card>
  )
}

function ChecklistCard({ quoteId }: { quoteId: string }) {
  const qc = useQueryClient()
  const { data: items = [], isLoading } = useQuery({
    queryKey: ['quote-checklist', quoteId],
    queryFn: () => quotesApi.getChecklist(quoteId),
  })

  const toggleMutation = useMutation({
    mutationFn: ({ itemId, isCompleted }: { itemId: string; isCompleted: boolean }) =>
      quotesApi.toggleChecklistItem(quoteId, itemId, isCompleted),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['quote-checklist', quoteId] }),
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Update failed'),
  })

  const blockers = items.filter((i) => i.isBlocker)
  const advisory = items.filter((i) => !i.isBlocker)
  const openBlockers = blockers.filter((i) => !i.isCompleted).length

  return (
    <Card>
      <CardHead
        title={<span className="flex items-center gap-2">Bind checklist</span>}
        count={openBlockers > 0 ? <span className="rounded-full bg-amber-100 px-2 text-amber-700">{openBlockers} open</span> : undefined}
      />
      {isLoading ? (
        <div className="flex items-center justify-center py-8"><LoadingSpinner /></div>
      ) : items.length === 0 ? (
        <p className="px-5 py-4 text-sm text-slate-400">No checklist items.</p>
      ) : (
        <div className="divide-y divide-slate-100">
          {[...blockers, ...advisory].map((item) => (
            <div key={item.id} className="flex items-center gap-3 px-5 py-3">
              <button
                disabled={item.completionSource === 'System' || toggleMutation.isPending}
                onClick={() => toggleMutation.mutate({ itemId: item.id, isCompleted: !item.isCompleted })}
                className="flex h-5 w-5 flex-shrink-0 items-center justify-center rounded transition-colors disabled:cursor-default focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-200"
                style={{
                  border: `1px solid ${item.isCompleted ? '#1b8754' : 'var(--line)'}`,
                  background: item.isCompleted ? '#1b8754' : 'var(--surface)',
                  color: item.isCompleted ? '#fff' : 'var(--ink-3)',
                }}
              >
                {item.isCompleted && <Check className="h-3 w-3" />}
              </button>
              <div className="min-w-0 flex-1">
                <span className={`text-sm ${item.isCompleted ? 'text-slate-400 line-through' : 'text-slate-800'}`}>
                  {item.label}
                </span>
                {item.isBlocker && !item.isCompleted && (
                  <span className="ml-2 text-[10px] font-semibold uppercase tracking-wide text-red-500">blocker</span>
                )}
                {item.completionSource === 'System' && item.isCompleted && (
                  <span className="ml-2 text-[10px] font-semibold uppercase tracking-wide text-sky-500">auto</span>
                )}
              </div>
              {item.isCompleted && item.completedByName && (
                <span className="text-[10.5px] text-slate-400 flex-shrink-0">
                  {item.completionSource === 'System' ? 'System' : item.completedByName}
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}

// ── Bind modal ─────────────────────────────────────────────────────────────────

function BindModal({ quoteId, effectiveDate, expirationDate, onClose }: {
  quoteId: string; effectiveDate: string; expirationDate: string; onClose: () => void
}) {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const [form, setForm] = useState({
    boundDate: new Date().toISOString().slice(0, 10),
    effectiveDate: effectiveDate.slice(0, 10),
    expirationDate: expirationDate.slice(0, 10),
  })
  const bindMutation = useMutation({
    mutationFn: () => quotesApi.bind(quoteId, form),
    onSuccess: (bound) => {
      qc.invalidateQueries({ queryKey: ['quotes', quoteId] })
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission'] })
      toast.success('Quote bound successfully')
      if (bound.policyNumber) {
        toast.info(`Policy ${bound.policyNumber} created`)
      }
      onClose()
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Bind failed'),
  })

  return (
    <div className="sims-modal-backdrop">
      <div className="sims-modal max-w-sm">
        <div className="sims-modal-head">
          <h2 className="sims-modal-title">Bind quote</h2>
          <button type="button" onClick={onClose} className="sims-icon-btn" title="Close"><X className="h-4 w-4" /></button>
        </div>
        <div className="sims-modal-body space-y-4">
          {(['boundDate', 'effectiveDate', 'expirationDate'] as const).map((k) => (
            <label key={k} className="block">
              <span className="sims-field-label">
                {k === 'boundDate' ? 'Bound date' : k === 'effectiveDate' ? 'Effective date' : 'Expiration date'}
              </span>
              <input
                type="date"
                value={form[k]}
                onChange={(e) => setForm((f) => ({ ...f, [k]: e.target.value }))}
                className="sims-input"
              />
            </label>
          ))}
        </div>
        <div className="sims-modal-foot">
          <Btn variant="outline" onClick={onClose}>Cancel</Btn>
          <Btn
            variant="primary"
            disabled={bindMutation.isPending || !form.boundDate || !form.effectiveDate || !form.expirationDate}
            onClick={() => bindMutation.mutate()}
          >
            {bindMutation.isPending ? 'Binding…' : 'Confirm bind'}
          </Btn>
        </div>
      </div>
    </div>
  )
}

// ── Note card ──────────────────────────────────────────────────────────────────

function NotesCard({ quoteId }: { quoteId: string }) {
  const qc = useQueryClient()
  const [draft, setDraft] = useState('')
  const [adding, setAdding] = useState(false)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const { data: notes = [] } = useQuery({
    queryKey: ['quote-notes', quoteId],
    queryFn: () => quotesApi.getNotes(quoteId),
  })

  const createMutation = useMutation({
    mutationFn: () => quotesApi.createNote(quoteId, { body: draft.trim() }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-notes', quoteId] })
      setDraft('')
      setAdding(false)
      toast.success('Note added')
    },
    onError: () => toast.error('Failed to add note'),
  })

  const pinMutation = useMutation({
    mutationFn: (noteId: string) => quotesApi.togglePinNote(quoteId, noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['quote-notes', quoteId] }),
  })

  const deleteMutation = useMutation({
    mutationFn: (noteId: string) => quotesApi.deleteNote(quoteId, noteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-notes', quoteId] })
      toast.success('Note deleted')
    },
    onError: () => toast.error('Failed to delete note'),
  })

  const pinned = notes.filter((n) => n.isPinned)
  const unpinned = notes.filter((n) => !n.isPinned)
  const sorted = [...pinned, ...unpinned]

  return (
    <Card>
      <CardHead
        title="Notes"
        count={notes.length || undefined}
        right={
          !adding && (
            <Btn variant="outline" onClick={() => { setAdding(true); setTimeout(() => textareaRef.current?.focus(), 50) }}>
              <Plus className="h-3.5 w-3.5" /> Add note
            </Btn>
          )
        }
      />
      {adding && (
        <div className="border-b border-slate-200 p-4">
          <textarea
            ref={textareaRef}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="Add a note…"
            rows={3}
            className="sims-textarea"
          />
          <div className="mt-2 flex justify-end gap-2">
            <Btn variant="outline" onClick={() => { setAdding(false); setDraft('') }}>Cancel</Btn>
            <Btn
              variant="primary"
              disabled={!draft.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              Save
            </Btn>
          </div>
        </div>
      )}
      {sorted.length === 0 && !adding ? (
        <p className="px-5 py-6 text-sm text-slate-400">No notes yet.</p>
      ) : (
        <div>
          {sorted.map((note, i) => (
            <div key={note.id} className={`flex gap-3 px-5 py-4 ${i < sorted.length - 1 ? 'border-b border-slate-100' : ''} ${note.isPinned ? 'bg-amber-50/50' : ''}`}>
              {note.isPinned && <Pin className="mt-0.5 h-3.5 w-3.5 flex-shrink-0 text-amber-500" />}
              <div className="min-w-0 flex-1">
                <p className="text-sm text-slate-700 leading-relaxed">{note.body}</p>
                <div className="mt-1.5 flex items-center gap-2 text-xs text-slate-400">
                  <span className="font-medium text-slate-500">{note.createdByName}</span>
                  <span>·</span>
                  <span>{formatDate(note.createdAt)}</span>
                </div>
              </div>
              <div className="flex flex-shrink-0 items-start gap-1">
                <button
                  onClick={() => pinMutation.mutate(note.id)}
                  className={`sims-icon-btn ${note.isPinned ? 'text-amber-500 hover:text-amber-600' : ''}`}
                  title={note.isPinned ? 'Unpin' : 'Pin'}
                >
                  <Pin className="h-3.5 w-3.5" />
                </button>
                <button
                  onClick={() => { if (confirm('Delete this note?')) deleteMutation.mutate(note.id) }}
                  className="sims-icon-btn hover:text-red-500"
                  title="Delete"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  )
}

// ── Documents card ─────────────────────────────────────────────────────────────

function DocumentsCard({ quoteId }: { quoteId: string }) {
  const qc = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [showGenerateModal, setShowGenerateModal] = useState(false)

  const { data: attachments = [] } = useQuery({
    queryKey: ['quote-attachments', quoteId],
    queryFn: () => quotesApi.getAttachments(quoteId),
  })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => quotesApi.uploadAttachment(quoteId, file),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-attachments', quoteId] })
      toast.success('Document uploaded')
    },
    onError: () => toast.error('Upload failed'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => quotesApi.deleteAttachment(quoteId, id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-attachments', quoteId] })
      toast.success('Document deleted')
    },
    onError: () => toast.error('Failed to delete'),
  })

  const downloadMutation = useMutation({
    mutationFn: (id: string) => attachmentsApi.getDownloadUrl(id),
    onSuccess: (url) => {
      window.open(url, '_blank', 'noopener,noreferrer')
    },
    onError: () => toast.error('Download failed'),
  })

  function onFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) uploadMutation.mutate(file)
    e.target.value = ''
  }

  function fmtBytes(b: number) {
    if (b < 1024) return `${b} B`
    if (b < 1024 * 1024) return `${(b / 1024).toFixed(0)} KB`
    return `${(b / 1024 / 1024).toFixed(1)} MB`
  }

  return (
    <Card>
      <CardHead
        title="Documents"
        count={attachments.length || undefined}
        right={
          <>
            <input ref={fileInputRef} type="file" className="hidden" onChange={onFileChange} />
            <Btn variant="outline" onClick={() => setShowGenerateModal(true)}>
              <FileOutput className="h-3.5 w-3.5" />
              Generate
            </Btn>
            <Btn variant="outline" onClick={() => fileInputRef.current?.click()} disabled={uploadMutation.isPending}>
              <Upload className="h-3.5 w-3.5" />
              {uploadMutation.isPending ? 'Uploading…' : 'Upload'}
            </Btn>
          </>
        }
      />
      {showGenerateModal && (
        <GenerateDocumentModal
          entityType="Quote"
          entityId={quoteId}
          onClose={() => setShowGenerateModal(false)}
        />
      )}
      {attachments.length === 0 ? (
        <p className="px-5 py-6 text-sm text-slate-400">No documents yet.</p>
      ) : (
        <table className="sd-table w-full table-fixed">
          <colgroup>
            <col className="w-[54%]" />
            <col className="w-[10%]" />
            <col className="w-[16%]" />
            <col className="w-[12%]" />
            <col className="w-[8%]" />
          </colgroup>
          <thead>
            <tr>
              <th>File</th>
              <th>Size</th>
              <th>Uploaded by</th>
              <th>Date</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {attachments.map((a) => (
              <tr key={a.id}>
                <td className="min-w-0">
                  <div className="flex min-w-0 items-center gap-2">
                    <FileText className="h-3.5 w-3.5 flex-shrink-0 text-slate-400" />
                    <span className="min-w-0 cursor-pointer break-all font-medium text-sky-700 hover:text-sky-800" onClick={() => downloadMutation.mutate(a.id)}>
                      {a.fileName}
                    </span>
                  </div>
                  {a.description && <p className="mt-0.5 break-words pl-5.5 text-xs text-slate-400">{a.description}</p>}
                </td>
                <td className="id whitespace-normal">{fmtBytes(a.fileSizeBytes)}</td>
                <td className="break-words">{a.uploadedByName}</td>
                <td className="id">{formatDate(a.createdAt)}</td>
                <td>
                  <div className="flex items-center justify-end gap-1">
                    <button
                      onClick={() => downloadMutation.mutate(a.id)}
                      className="sims-icon-btn hover:text-sky-600"
                      title="Download"
                    >
                      <Download className="h-3.5 w-3.5" />
                    </button>
                    <button
                      onClick={() => { if (confirm('Delete this document?')) deleteMutation.mutate(a.id) }}
                      className="sims-icon-btn hover:text-red-500"
                      title="Delete"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  )
}

// ── Main page ──────────────────────────────────────────────────────────────────

// ── Activity card ─────────────────────────────────────────────────────────────

function ActivityCard({ quoteId }: { quoteId: string }) {
  const qc = useQueryClient()

  const { data: communications = [] } = useQuery({
    queryKey: ['quote-outbound-communications', quoteId],
    queryFn: () => outboundCommunicationsApi.getForEntity('Quote', quoteId),
  })

  const sendMutation = useMutation({
    mutationFn: (communicationId: string) => outboundCommunicationsApi.send(communicationId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quote-outbound-communications', quoteId] })
      toast.success('Email sent')
    },
    onError: (e: any) => {
      qc.invalidateQueries({ queryKey: ['quote-outbound-communications', quoteId] })
      toast.error(e?.response?.data?.errorMessage ?? 'Failed to send email')
    },
  })

  return (
    <Card>
      <CardHead title="Activity" count={communications.length || undefined} />
      {communications.length === 0 ? (
        <p className="px-5 py-6 text-sm text-slate-400">No quote communication activity yet.</p>
      ) : (
        <table className="sd-table w-full table-fixed">
          <colgroup>
            <col className="w-[18%]" />
            <col className="w-[42%]" />
            <col className="w-[25%]" />
            <col className="w-[9%]" />
            <col className="w-[6%]" />
          </colgroup>
          <thead>
            <tr>
              <th>Subject</th>
              <th>To</th>
              <th>From</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {communications.map((c) => (
              <tr key={c.id}>
                <td className="primary-cell break-words">{c.subject}</td>
                <td className="break-words">{c.toName ? `${c.toName} <${c.toAddress}>` : c.toAddress}</td>
                <td className="break-words">{c.fromAddress}</td>
                <td><span className="sd-lob whitespace-nowrap">{c.status}</span></td>
                <td>
                  <div className="flex justify-end gap-1">
                    {(c.status === 'Draft' || c.status === 'Failed') && (
                      <button
                        type="button"
                        className="sims-icon-btn hover:text-sky-600"
                        title="Send email"
                        disabled={sendMutation.isPending}
                        onClick={() => sendMutation.mutate(c.id)}
                      >
                        <Send className="h-3.5 w-3.5" />
                      </button>
                    )}
                    {c.status === 'Sent' && c.graphMessageWebLink && (
                      <button
                        type="button"
                        className="sims-icon-btn hover:text-sky-600"
                        title="Open in Outlook"
                        onClick={() => window.open(c.graphMessageWebLink!, '_blank', 'noopener,noreferrer')}
                      >
                        <ExternalLink className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  )
}

export function QuoteDetailPage() {
  const { quoteId } = useParams<{ quoteId: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { canCreatePolicies } = usePermissions()
  const [showBind, setShowBind] = useState(false)
  const [showRating, setShowRating] = useState(false)
  const [showReduce, setShowReduce] = useState(false)
  const [overrideMode, setOverrideMode] = useState<'dollar' | 'rate'>('dollar')
  const [overrideInput, setOverrideInput] = useState('')
  const ratingPanelRef = useRef<HTMLDivElement>(null)

  const { data: quote, isLoading, isError } = useQuery({
    queryKey: ['quotes', quoteId],
    queryFn: () => quotesApi.getById(quoteId!),
    enabled: !!quoteId,
  })

  const { data: ratingSnapshot } = useQuery({
    queryKey: ['rating-snapshot', quoteId],
    queryFn: () => quotesApi.getRatingSnapshot(quoteId!),
    enabled: !!quoteId,
  })

  const { data: invoicePreview } = useQuery({
    queryKey: ['quote-invoice-preview', quoteId],
    queryFn: () => quotesApi.getInvoicePreview(quoteId!),
    enabled: !!quoteId,
  })

  const { data: writeup, isLoading: writeupLoading, isError: writeupIsError } = useQuery({
    queryKey: ['uw-writeup', quoteId],
    queryFn: () => uwWriteupApi.get(quoteId!),
    enabled: !!quoteId,
  })

  const [writeupPayload, setWriteupPayload] = useState<IMWriteupPayload>(EMPTY_PAYLOAD)
  const [writeupConditions, setWriteupConditions] = useState<WriteupCondition[]>([])
  const [newWriteupCondition, setNewWriteupCondition] = useState('')

  useEffect(() => {
    if (!writeup) return
    setWriteupPayload({ ...EMPTY_PAYLOAD, ...(writeup.payload ?? {}) })
    setWriteupConditions(writeup.conditions ?? [])
  }, [writeup])

  const { data: siblingQuotes = [] } = useQuery({
    queryKey: ['quotes', 'by-submission', quote?.submissionId],
    queryFn: () => quotesApi.getBySubmission(quote!.submissionId),
    enabled: !!quote?.submissionId,
  })

  const { data: checklist = [] } = useQuery({
    queryKey: ['quote-checklist', quoteId],
    queryFn: () => quotesApi.getChecklist(quoteId!),
    enabled: !!quoteId,
  })

  const commissionOverrideMutation = useMutation({
    mutationFn: (data: CommissionOverrideRequest) => quotesApi.commissionOverride(quoteId!, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', quoteId] })
      qc.invalidateQueries({ queryKey: ['quotes', 'by-submission', quote?.submissionId] })
      qc.invalidateQueries({ queryKey: ['quote-invoice-preview', quoteId] })
      setShowReduce(false)
      setOverrideInput('')
      toast.success('Commission give-back applied')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to apply commission override'),
  })

  const previewInlandMarineProposalMutation = useMutation({
    mutationFn: () => documentGenerationApi.getInlandMarineProposalHtml(quoteId!),
    onSuccess: (html) => {
      const blob = new Blob([html], { type: 'text/html' })
      const url = URL.createObjectURL(blob)
      window.open(url, '_blank', 'noopener,noreferrer')
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to generate proposal preview'),
  })

  const saveInlandMarineProposalMutation = useMutation({
    mutationFn: () => documentGenerationApi.saveInlandMarineProposalPdf(quoteId!),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['quote-attachments', quoteId] })
      window.open(data.url, '_blank', 'noopener,noreferrer')
      toast.success('Proposal generated and filed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to generate proposal'),
  })

  const sendInlandMarineProposalDraftMutation = useMutation({
    mutationFn: () => documentGenerationApi.createInlandMarineProposalSendDraft(quoteId!),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['quote-attachments', quoteId] })
      qc.invalidateQueries({ queryKey: ['quote-outbound-communications', quoteId] })
      window.open(data.generatedDocument.url, '_blank', 'noopener,noreferrer')
      toast.success('Proposal PDF filed and email draft created')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to create proposal draft'),
  })

  const saveWriteupMutation = useMutation({
    mutationFn: () => uwWriteupApi.save(quoteId!, {
      payload: writeupPayload,
      conditions: writeupConditions.map((condition, index) => ({ ...condition, sortOrder: index })),
    }),
    onSuccess: (data) => {
      qc.setQueryData(['uw-writeup', quoteId], data)
      toast.success('Writeup saved')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to save writeup'),
  })

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <LoadingSpinner />
      </div>
    )
  }

  if (isError || !quote) {
    return (
      <div className="flex h-64 flex-col items-center justify-center gap-3 text-slate-500">
        <p className="text-sm">Quote not found.</p>
        <Btn variant="outline" onClick={() => navigate(-1)}><ArrowLeft className="h-3.5 w-3.5" /> Go back</Btn>
      </div>
    )
  }

  const isInlandMarine = quote.lineOfBusiness === 'InlandMarine'
  const isAutoLiability = quote.lineOfBusiness === 'AutoLiability'
  const isAutoPhysicalDamage = quote.lineOfBusiness === 'AutoPhysicalDamage'
  const isGeneralLiability = quote.lineOfBusiness === 'GeneralLiability'
  const isAuto = AUTO_LOBS.has(quote.lineOfBusiness)
  const openBlockers = checklist.filter((i) => i.isBlocker && !i.isCompleted).length
  const canBind = (quote.status === 'Quoted' || quote.status === 'Submitted') && openBlockers === 0
  const canReduce = quote.status !== 'Bound' && quote.status !== 'Cancelled' && quote.status !== 'Expired' && canCreatePolicies && !quote.commissionOverride
  const canGenerateInlandMarineProposal = quote.lineOfBusiness === 'InlandMarine' && !!ratingSnapshot && ratingSnapshot.grandTotalPremium > 0
  const otherQuotes = siblingQuotes.filter((q) => q.id !== quote.id)
  const writeupReadOnly = writeup?.status !== 'Draft'
  const patchWriteupPayload = (patch: Partial<IMWriteupPayload>) =>
    setWriteupPayload((current) => ({ ...current, ...patch }))
  const addWriteupCondition = () => {
    const text = newWriteupCondition.trim()
    if (!text) return
    setWriteupConditions((conditions) => [
      ...conditions,
      { id: crypto.randomUUID(), text, required: true, satisfied: false, sortOrder: conditions.length },
    ])
    setNewWriteupCondition('')
  }
  const removeWriteupCondition = (id: string) => {
    setWriteupConditions((conditions) => conditions.filter((condition) => condition.id !== id))
  }

  const ratedTotalPremium = ratingSnapshot?.grandTotalPremium ?? quote.totalPremium

  // Commission: show the rated premium basis when a rating snapshot exists.
  const agentCommRate = quote.commissionOverride?.agentRate ?? quote.agentCommissionRate
  const agentCommAmt = quote.commissionOverride?.agentCommissionAmount ?? ratedTotalPremium * agentCommRate
  const carrierCommAmt = ratedTotalPremium * quote.carrierCommissionRate
  const smmRetentionAmt = ratedTotalPremium * quote.smmRetentionRate

  const ratedEquipmentValues = ratingSnapshot?.lines
    .map((line) => parseLineInputs(line.inputs)?.value)
    .filter((value): value is number => typeof value === 'number') ?? []
  const totalTiv = writeup?.equipment.totalTiv ?? (ratedEquipmentValues.length > 0 ? ratedEquipmentValues.reduce((sum, value) => sum + value, 0) : null)
  const anyOneItemLimit = writeup?.equipment.largestUnitTiv ?? (ratedEquipmentValues.length > 0 ? Math.max(...ratedEquipmentValues) : null)
  const displayLimit = quote.lineOfBusiness === 'InlandMarine' ? totalTiv : quote.limit

  // Premium breakdown
  const manualPremium = ratingSnapshot?.manualPremium ?? quote.premiumAmount
  const schedMod = ratingSnapshot?.scheduleModifier ?? 0
  const schedCredit = schedMod < 0 ? Math.abs(manualPremium * schedMod) : 0
  const schedDebit = schedMod > 0 ? manualPremium * schedMod : 0
  const openRatingPanel = () => {
    setShowRating(true)
    window.setTimeout(() => ratingPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 0)
  }

  return (
    <div className="min-h-full bg-slate-50">
      {/* Sticky top bar */}
      <div className="sticky top-0 z-10 flex items-center justify-between border-b border-slate-200 bg-white px-6 py-2.5">
        <nav className="flex items-center gap-1.5 text-xs text-slate-500">
          <Link to="/submissions" className="hover:text-slate-700">Submissions</Link>
          <ChevronRight className="h-3 w-3" />
          <Link to={`/insureds/${quote.insuredId}`} className="hover:text-slate-700">{quote.insuredName}</Link>
          <ChevronRight className="h-3 w-3" />
          <Link to={`/submissions/${quote.submissionId}`} className="hover:text-slate-700">{quote.submissionNumber}</Link>
          <ChevronRight className="h-3 w-3" />
          <span className="font-medium text-slate-700">{quote.quoteNumber}</span>
        </nav>
        <Btn variant="ghost" onClick={() => navigate(`/submissions/${quote.submissionId}`)}>
          <ArrowLeft className="h-3.5 w-3.5" /> Back to submission
        </Btn>
      </div>

      <div className="mx-auto max-w-screen-xl px-6 py-5">
        {/* Header */}
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h1 className="flex flex-wrap items-center gap-2.5 text-xl font-semibold text-slate-900">
              {quote.carrierName}
              <span className="rounded-md bg-slate-100 px-2 py-0.5 font-mono text-xs font-medium text-slate-500">
                {quote.quoteNumber}
              </span>
              <span className={`sd-pill ${STATUS_PILL[quote.status]}`}>
                {quote.status}
              </span>
            </h1>
            <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
              <span className="inline-flex items-center gap-1.5 rounded border border-sky-300 bg-sky-50 px-2 py-0.5 text-xs font-bold text-sky-800">
                {LOB_LABELS[quote.lineOfBusiness]}
                <span className="font-normal text-slate-400">· locked at creation</span>
              </span>
              <span className="text-slate-300">·</span>
              <span>Insured <Link to={`/insureds/${quote.insuredId}`} className="font-semibold text-slate-700 hover:text-sky-700">{quote.insuredName}</Link></span>
              <span className="text-slate-300">·</span>
              <span>Submission <Link to={`/submissions/${quote.submissionId}`} className="font-semibold text-sky-700 hover:text-sky-800">{quote.submissionNumber}</Link></span>
            </div>
          </div>
          <div className="flex flex-shrink-0 flex-wrap items-center justify-end gap-2">
            <Btn variant={showRating ? 'primary' : 'outline'} onClick={() => setShowRating((v) => !v)}>
              <Calculator className="h-3.5 w-3.5" /> Rate
            </Btn>
            {canGenerateInlandMarineProposal && (
              <MenuButton label={<><FileText className="h-3.5 w-3.5" /> Proposal</>}>
                <MenuItem onClick={() => previewInlandMarineProposalMutation.mutate()} disabled={previewInlandMarineProposalMutation.isPending}>
                  <FileText className="h-3.5 w-3.5" /> Preview
                </MenuItem>
                <MenuItem onClick={() => saveInlandMarineProposalMutation.mutate()} disabled={saveInlandMarineProposalMutation.isPending}>
                  <Download className="h-3.5 w-3.5" /> Generate & file PDF
                </MenuItem>
                <MenuItem onClick={() => sendInlandMarineProposalDraftMutation.mutate()} disabled={sendInlandMarineProposalDraftMutation.isPending}>
                  <FileOutput className="h-3.5 w-3.5" /> Send proposal
                </MenuItem>
              </MenuButton>
            )}
            {canBind && (
              <Btn variant="primary" onClick={() => setShowBind(true)}>
                <CheckCircle2 className="h-3.5 w-3.5" /> Bind quote
              </Btn>
            )}
            <MenuButton label={<MoreHorizontal className="h-4 w-4" />} variant="ghost">
              {canReduce && (
                <MenuItem onClick={() => { setShowReduce((v) => !v); setOverrideMode('dollar'); setOverrideInput('') }}>
                  <TrendingDown className="h-3.5 w-3.5" /> Reduce commission
                </MenuItem>
              )}
              <MenuItem>
                <Copy className="h-3.5 w-3.5" /> Duplicate quote
              </MenuItem>
            </MenuButton>
          </div>
        </div>

        {/* Stage bar */}
        <StageBar status={quote.status} issuedDate={quote.issuedDate} />

        {showRating && (
          <div ref={ratingPanelRef} className="mb-5 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
            <QuoteRatingPanel
              quoteId={quoteId!}
              submissionId={quote.submissionId}
              lineOfBusiness={quote.lineOfBusiness}
              isBound={quote.status === 'Bound'}
            />
          </div>
        )}

        {showReduce && (
          <div className="mb-5 rounded-xl border border-amber-200 bg-amber-50 p-4 shadow-sm">
            <div className="mb-3 flex items-center gap-2">
              <TrendingDown className="h-4 w-4 text-amber-700" />
              <div>
                <h3 className="text-sm font-semibold text-amber-900">Reduce agent commission</h3>
                <p className="text-xs text-amber-700">Carrier net and SMM commission stay unchanged. Agent give-back reduces total premium.</p>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <div
                className="flex overflow-hidden rounded-md bg-white text-sm"
                style={{ border: '1px solid var(--line)' }}
              >
                <button
                  type="button"
                  onClick={() => setOverrideMode('dollar')}
                  className={`px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-200 ${overrideMode === 'dollar' ? 'bg-sky-700 text-white' : 'text-slate-600 hover:bg-slate-50'}`}
                >
                  $ Give-back
                </button>
                <button
                  type="button"
                  onClick={() => setOverrideMode('rate')}
                  className={`px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-200 ${overrideMode === 'rate' ? 'bg-sky-700 text-white' : 'text-slate-600 hover:bg-slate-50'}`}
                  style={{ borderLeft: '1px solid var(--line)' }}
                >
                  % New rate
                </button>
              </div>
              <input
                type="number"
                min="0"
                step={overrideMode === 'dollar' ? '1' : '0.01'}
                value={overrideInput}
                onChange={(e) => setOverrideInput(e.target.value)}
                placeholder={overrideMode === 'dollar' ? 'e.g. 500' : 'e.g. 8.5'}
                className="sims-input"
                style={{ width: 144 }}
              />
              <Btn
                variant="primary"
                disabled={commissionOverrideMutation.isPending}
                onClick={() => {
                  const val = Number.parseFloat(overrideInput)
                  if (!Number.isFinite(val) || val <= 0) {
                    toast.error('Enter a valid amount')
                    return
                  }
                  const data: CommissionOverrideRequest = overrideMode === 'dollar'
                    ? { givebackAmount: val }
                    : { newAgentRate: val / 100 }
                  commissionOverrideMutation.mutate(data)
                }}
              >
                <Check className="h-3.5 w-3.5" /> Apply
              </Btn>
              <Btn variant="outline" onClick={() => setShowReduce(false)}>
                <X className="h-3.5 w-3.5" /> Cancel
              </Btn>
            </div>
          </div>
        )}

        {/* Summary strip */}
        <div className="mb-5 grid grid-cols-4 gap-3 max-[900px]:grid-cols-2 max-[600px]:grid-cols-1">
          <div className="rounded-xl border border-sky-200 bg-sky-50 p-4 shadow-sm">
            <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-wide text-sky-600">Total premium</div>
            <div className="text-3xl font-semibold tracking-tight text-sky-900">{fmt(ratedTotalPremium)}</div>
            <div className="mt-2 text-xs text-sky-700">
              {ratingSnapshot ? (
                <><span className="font-semibold">Rated</span> from current snapshot</>
              ) : (
                <span className="text-sky-600/70">No rating snapshot yet</span>
              )}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Commission (agent)</div>
            <div className="text-3xl font-semibold tracking-tight text-slate-800">{fmt(agentCommAmt)}</div>
            <div className="mt-2 text-xs text-slate-500">
              <span className="font-semibold">{formatPercent(agentCommRate)}</span>
              {quote.commissionOverride && <span className="ml-1.5 rounded bg-amber-100 px-1 py-0.5 text-[10px] font-semibold text-amber-700">override</span>}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">{quote.lineOfBusiness === 'InlandMarine' ? 'Total TIV' : 'Limit / Deductible'}</div>
            <div className="text-2xl font-semibold tracking-tight text-slate-800">
              {displayLimit ? fmt(displayLimit) : '—'}
              {quote.deductible != null && (
                <span className="ml-1.5 text-lg font-medium text-slate-400">/ {fmt(quote.deductible)}</span>
              )}
            </div>
            <div className="mt-2 text-xs text-slate-500">
              {quote.lineOfBusiness === 'InlandMarine' ? (
                anyOneItemLimit != null ? <>Any one item <span className="font-semibold">{fmt(anyOneItemLimit)}</span></> : <span className="text-slate-300">No scheduled items rated</span>
              ) : (
                <>
                  {quote.uninsuredMotoristLimit ? `UM ${fmt(quote.uninsuredMotoristLimit)}` : ''}
                  {quote.medicalPaymentsLimit ? `  Med pay ${fmt(quote.medicalPaymentsLimit)}` : ''}
                  {!quote.uninsuredMotoristLimit && !quote.medicalPaymentsLimit ? <span className="text-slate-300">No additional limits</span> : ''}
                </>
              )}
            </div>
          </div>
          <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Term</div>
            <div className="text-lg font-semibold tracking-tight text-slate-800">{formatDate(quote.effectiveDate)}</div>
            <div className="mt-2 text-xs text-slate-500">
              Expires <span className="font-semibold">{formatDate(quote.expirationDate)}</span>
            </div>
          </div>
        </div>

        {/* Two-column layout */}
        <div className="grid grid-cols-[1fr_300px] gap-5 max-[1100px]:grid-cols-1">

          {/* ── Main column ── */}
          <div className="space-y-4 min-w-0">

            {/* Coverage limits */}
            <Card>
              <CardHead title="Coverage limits" right={<Btn variant="ghost" onClick={openRatingPanel}><Edit2 className="h-3.5 w-3.5" />Edit</Btn>} />
              <div className="grid grid-cols-4 gap-x-6 gap-y-4 p-5 max-[800px]:grid-cols-2">
                {quote.lineOfBusiness === 'InlandMarine' && (
                  <>
                    <KV label="Total TIV" value={totalTiv != null ? formatCurrency(totalTiv) : '—'} />
                    <KV label="Any one item limit" value={anyOneItemLimit != null ? formatCurrency(anyOneItemLimit) : '—'} />
                  </>
                )}
                {quote.limit != null && (
                  <KV label="Occurrence limit" value={formatCurrency(quote.limit)} />
                )}
                {quote.deductible != null && (
                  <KV label="Deductible" value={formatCurrency(quote.deductible)} />
                )}
                {quote.uninsuredMotoristLimit != null && (
                  <KV label="Uninsured motorist" value={formatCurrency(quote.uninsuredMotoristLimit)} />
                )}
                {quote.medicalPaymentsLimit != null && (
                  <KV label="Medical payments" value={formatCurrency(quote.medicalPaymentsLimit)} />
                )}
              </div>
              {quote.coverageDescription && (
                <div className="border-t border-slate-100 px-5 py-3">
                  <p className="text-xs font-semibold uppercase tracking-wide text-slate-400 mb-1">Coverage description</p>
                  <p className="text-sm text-slate-600 leading-relaxed">{quote.coverageDescription}</p>
                </div>
              )}
              {ratingSnapshot && ratingSnapshot.lines.length > 0 && (
                <div className="border-t border-slate-100">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-slate-200 bg-slate-50 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
                        <th className="px-5 py-2 text-left">Exposure</th>
                        <th className="px-4 py-2 text-right">Premium</th>
                      </tr>
                    </thead>
                    <tbody>
                      {ratingSnapshot.lines.map((line, i) => (
                        <tr key={i} className="border-b border-slate-100 last:border-0 hover:bg-slate-50">
                          <td className="px-5 py-2.5 font-medium text-slate-700">{line.exposureRef}</td>
                          <td className="px-4 py-2.5 text-right font-mono tabular-nums text-slate-800">{fmtFull(line.linePremium)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>

            {/* Auto safety panel */}
            {isAuto && (
              <Card className="overflow-hidden">
                <CardHead
                  title={<span className="flex items-center gap-2"><ShieldCheck className="h-4 w-4 text-slate-500" />Auto safety</span>}
                />
                <QuoteAutoSafetyPanel quoteId={quoteId!} />
              </Card>
            )}

            {/* Premium breakdown */}
            <Card>
              <CardHead title="Premium breakdown" />
              <div className="divide-y divide-dashed divide-slate-100 px-5 py-2">
                {[
                  { label: 'Manual premium', value: fmtFull(manualPremium), negative: false },
                  ...(ratingSnapshot && schedCredit > 0 ? [{ label: `Schedule credit (${formatPercent(Math.abs(schedMod))})`, value: `(${fmtFull(schedCredit)})`, negative: true }] : []),
                  ...(ratingSnapshot && schedDebit > 0 ? [{ label: `Schedule debit (${formatPercent(schedMod)})`, value: fmtFull(schedDebit), negative: false }] : []),
                  { label: 'Taxes & fees', value: fmtFull(quote.taxesAndFees), negative: false },
                ].map((row, i) => (
                  <div key={i} className="flex items-center justify-between py-2.5 text-sm">
                    <span className="text-slate-500">{row.label}</span>
                    <span className={`tabular-nums font-medium ${row.negative ? 'text-emerald-700' : 'text-slate-800'}`}>{row.value}</span>
                  </div>
                ))}
                <div className="flex items-center justify-between py-3 text-sm font-bold text-slate-900">
                  <span>Total premium</span>
                  <span className="text-base tabular-nums">{fmtFull(ratedTotalPremium)}</span>
                </div>
                <div className="flex items-center justify-between py-2 text-xs text-slate-400">
                  <span>Agency commission ({formatPercent(agentCommRate)})</span>
                  <span className="tabular-nums font-medium text-sky-800">{fmtFull(agentCommAmt)}</span>
                </div>
              </div>
            </Card>

            {ratingSnapshot && (
              <QuotePolicyFormsCard quoteId={quoteId!} canManage={canCreatePolicies} />
            )}

            {/* Inline UW Writeup */}
            <Card>
              <CardHead
                title={<span className="flex items-center gap-2"><Edit2 className="h-4 w-4 text-slate-500" />Underwriting writeup</span>}
                right={
                  <>
                    <WriteupStatusPill status={writeup?.status} />
                    <Link to={`/quotes/${quoteId}/writeup`} className="sd-btn outline sm">Open full writeup</Link>
                    {!writeupReadOnly && (
                      <Btn variant="primary" disabled={saveWriteupMutation.isPending || writeupLoading || writeupIsError} onClick={() => saveWriteupMutation.mutate()}>
                        <Save className="h-3.5 w-3.5" /> Save draft
                      </Btn>
                    )}
                  </>
                }
              />
              {writeupLoading ? (
                <div className="flex h-28 items-center justify-center"><LoadingSpinner /></div>
              ) : writeupIsError || !writeup ? (
                <div className="px-5 py-4 text-sm text-red-700">Underwriting writeup could not be loaded.</div>
              ) : (
                <div>
                  {writeupReadOnly && (
                    <div className="border-t border-slate-100 bg-slate-50 px-5 py-3 text-xs font-medium text-slate-500">
                      This writeup is {writeup.status.toLowerCase()} and can be reviewed here. Reopen the full writeup workflow to change status.
                    </div>
                  )}

                  <InlineWriteupSection number="01" title="Account summary" defaultOpen>
                    <div className="grid grid-cols-2 gap-4 max-[700px]:grid-cols-1">
                      <KV label="Underwriter" value={writeup.uwName} />
                      <KV label="Assistant UW" value={writeup.assistantUWName} />
                      <KV label="Agent" value={writeup.agentName} />
                      <KV label="Insured" value={writeup.insuredName} />
                      <KV label="Line" value={LOB_LABELS[quote.lineOfBusiness]} />
                      <KV label="Policy type" value={writeup.policyType} />
                      <KV label="Effective" value={formatDate(writeup.effectiveDate)} />
                      <KV label="Operation type" value={writeup.operationType} />
                    </div>
                    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-600">
                      {writeup.address || 'No address on the writeup yet.'}
                    </div>
                  </InlineWriteupSection>

                  {(isAutoPhysicalDamage || isGeneralLiability) && (
                    <InlineWriteupSection number="02" title="Program / market">
                      <InlineWriteupTextarea
                        label={isAutoPhysicalDamage ? 'Program / market selection' : 'Program / market'}
                        value={writeupPayload.programMarket}
                        readOnly={writeupReadOnly}
                        rows={2}
                        onChange={(value) => patchWriteupPayload({ programMarket: value })}
                      />
                    </InlineWriteupSection>
                  )}

                  <InlineWriteupSection number={isAutoPhysicalDamage || isGeneralLiability ? '03' : '02'} title={isInlandMarine ? 'Referral triggers' : 'Reason(s) for referral'}>
                    <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
                      <InlineWriteupCheckbox label={isInlandMarine ? 'Loss ratio over 55%' : '4-year loss ratio over 50%'} checked={writeupPayload.referralLossRatioOver55} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralLossRatioOver55: value })} />
                      {isInlandMarine && (
                        <>
                          <InlineWriteupCheckbox label="Any one piece over $500k" checked={writeupPayload.referralPieceOver500k} auto={writeup.autoReferralPieceOver500k} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralPieceOver500k: value })} />
                          <InlineWriteupCheckbox label="Total TIV over $2M" checked={writeupPayload.referralTivOver2mil} auto={writeup.autoReferralTivOver2mil} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralTivOver2mil: value })} />
                        </>
                      )}
                      <InlineWriteupCheckbox label={isInlandMarine ? 'Any loss over $400k' : 'Any loss over $50k'} checked={isInlandMarine ? writeupPayload.referralLossOver400k : !!writeupPayload.referralLossOver50k} readOnly={writeupReadOnly} onChange={(value) => isInlandMarine ? patchWriteupPayload({ referralLossOver400k: value }) : patchWriteupPayload({ referralLossOver50k: value })} />
                      {(isAutoLiability || isAutoPhysicalDamage) && (
                        <>
                          <InlineWriteupCheckbox label="FMCSA conditional / unsatisfactory" checked={!!writeupPayload.referralFmcsaConditional} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralFmcsaConditional: value })} />
                          <InlineWriteupCheckbox label="BASIC over threshold" checked={!!writeupPayload.referralBasicOverThreshold} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralBasicOverThreshold: value })} />
                          <InlineWriteupCheckbox label="Schedule credit over 20%" checked={!!writeupPayload.referralScheduleCreditOver20} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralScheduleCreditOver20: value })} />
                          <InlineWriteupCheckbox label="Premium over $100k" checked={!!writeupPayload.referralPremiumOver100k} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralPremiumOver100k: value })} />
                          <InlineWriteupCheckbox label="Owner-op over 30%" checked={!!writeupPayload.referralOwnerOperatorOver30} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralOwnerOperatorOver30: value })} />
                        </>
                      )}
                      {isAutoPhysicalDamage && (
                        <>
                          <InlineWriteupCheckbox label="Rate reduction over 5%" checked={!!writeupPayload.referralRateReduction} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralRateReduction: value })} />
                          <InlineWriteupCheckbox label="Unit ACV / stated amount over cap" checked={!!writeupPayload.referralUnitOverCap} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralUnitOverCap: value })} />
                          <InlineWriteupCheckbox label="30+ power units or premium over $100k" checked={!!writeupPayload.referralPowerUnitsOrPremium} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralPowerUnitsOrPremium: value })} />
                          <InlineWriteupCheckbox label="TIV one location over threshold" checked={!!writeupPayload.referralTivLocationThreshold} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralTivLocationThreshold: value })} />
                          <InlineWriteupCheckbox label="Tornado / hail TIV exposure" checked={!!writeupPayload.referralTornadoHail} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralTornadoHail: value })} />
                          <InlineWriteupCheckbox label="Coastal APD exposure" checked={!!writeupPayload.referralCoastalApd} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralCoastalApd: value })} />
                          <InlineWriteupCheckbox label="Credit score below threshold" checked={!!writeupPayload.referralCreditScoreLow} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralCreditScoreLow: value })} />
                        </>
                      )}
                      {isGeneralLiability && (
                        <>
                          <InlineWriteupCheckbox label="UW credit over 20%" checked={!!writeupPayload.referralGlUwCreditOver20} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralGlUwCreditOver20: value })} />
                          <InlineWriteupCheckbox label="Logging revenue below program threshold" checked={!!writeupPayload.referralGlRevenueBelowThreshold} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralGlRevenueBelowThreshold: value })} />
                          <InlineWriteupCheckbox label="Sawmill / lumberyard operations" checked={!!writeupPayload.referralSawmillOps} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralSawmillOps: value })} />
                          <InlineWriteupCheckbox label="Residential work" checked={!!writeupPayload.referralResidentialWork} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralResidentialWork: value })} />
                          <InlineWriteupCheckbox label="Burning exposure" checked={!!writeupPayload.referralBurningExposure} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralBurningExposure: value })} />
                          <InlineWriteupCheckbox label="Payroll change over 25%" checked={!!writeupPayload.referralPayrollChangeOver25} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralPayrollChangeOver25: value })} />
                          <InlineWriteupCheckbox label="Subcontractors without COI / hold harmless" checked={!!writeupPayload.referralSubcontractorControls} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ referralSubcontractorControls: value })} />
                        </>
                      )}
                    </div>
                    <InlineWriteupTextarea label="Other referral notes" value={writeupPayload.referralOtherText} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ referralOtherText: value })} />
                    <InlineWriteupTextarea label="Reason submitted" value={writeupPayload.reasonSubmitted} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ reasonSubmitted: value })} />
                  </InlineWriteupSection>

                  <InlineWriteupSection number={isAutoPhysicalDamage || isGeneralLiability ? '04' : '03'} title="Losses">
                    {!isInlandMarine && (
                      <InlineWriteupTextarea label="Loss synopsis" value={writeupPayload.lossSynopsis} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ lossSynopsis: value })} />
                    )}
                    <InlineWriteupTextarea label="Loss mitigation actions" value={writeupPayload.lossMitigationActions} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ lossMitigationActions: value })} />
                    <InlineWriteupTextarea
                      label={isInlandMarine ? 'Losses over $25k' : isGeneralLiability ? 'GL BI / attorney / losses over $50k' : 'Losses over $50k'}
                      value={isInlandMarine ? writeupPayload.lossesOver25kDescription : writeupPayload.lossesOver50kDescription}
                      readOnly={writeupReadOnly}
                      onChange={(value) => isInlandMarine ? patchWriteupPayload({ lossesOver25kDescription: value }) : patchWriteupPayload({ lossesOver50kDescription: value })}
                    />
                  </InlineWriteupSection>

                  {(isInlandMarine || isAutoPhysicalDamage) && (
                    <InlineWriteupSection number="05" title={isAutoPhysicalDamage ? 'Vehicles, values and CAB' : 'Equipment and values'}>
                      <div className="grid grid-cols-4 gap-3 max-[900px]:grid-cols-2 max-[560px]:grid-cols-1">
                        <KV label="Largest unit" value={fmt(writeup.equipment.largestUnitTiv)} />
                        <KV label="Total TIV" value={fmt(writeup.equipment.totalTiv)} />
                        <KV label={isAutoPhysicalDamage ? 'Total power units' : 'Scheduled items'} value={writeup.equipment.totalCount} />
                        <KV label="Other units" value={writeup.equipment.countOther} />
                      </div>
                      <InlineWriteupCheckbox label={isAutoPhysicalDamage ? 'Values independently verified' : 'Equipment values checked'} checked={writeupPayload.eqValueChecked} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ eqValueChecked: value })} />
                      {isAutoPhysicalDamage && (
                        <>
                          <InlineWriteupTextarea label="Max concentration at one location" value={writeupPayload.maxConcentrationOneLocation} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ maxConcentrationOneLocation: value })} />
                          <InlineWriteupTextarea label="CAB alerts / FMCSA / ISS rating notes" value={writeupPayload.cabAlertsNotes} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ cabAlertsNotes: value })} />
                        </>
                      )}
                      <InlineWriteupTextarea label={isAutoPhysicalDamage ? 'Vehicles and values narrative' : 'Equipment narrative'} value={writeupPayload.narrativeEquipment} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ narrativeEquipment: value })} />
                    </InlineWriteupSection>
                  )}

                  {isAutoLiability && (
                    <InlineWriteupSection number="04" title="Vehicles, FMCSA and CAB">
                      <InlineWriteupTextarea label="Vehicle / power unit summary" value={writeupPayload.narrativeEquipment} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ narrativeEquipment: value })} />
                      <InlineWriteupTextarea label="CAB alerts" value={writeupPayload.cabAlertsNotes} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ cabAlertsNotes: value })} />
                      <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
                        <InlineWriteupTextarea label="FMCSA safety rating" value={writeupPayload.fmcsaSafetyRating} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ fmcsaSafetyRating: value })} />
                        <InlineWriteupTextarea label="ISS / CAB rating" value={writeupPayload.issCabRating} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ issCabRating: value })} />
                      </div>
                    </InlineWriteupSection>
                  )}

                  {isGeneralLiability && (
                    <InlineWriteupSection number="05" title="Exposures and ISO class codes">
                      <InlineWriteupTextarea label="Class code exposure notes" value={writeupPayload.glClassExposureNotes} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ glClassExposureNotes: value })} />
                    </InlineWriteupSection>
                  )}

                  <InlineWriteupSection number={isAutoLiability ? '05' : isInlandMarine || isAutoPhysicalDamage || isGeneralLiability ? '06' : '04'} title={isAuto ? 'Operations and fleet metrics' : isGeneralLiability ? 'GL operations review' : 'Operations and metrics'}>
                    {isInlandMarine && (
                      <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
                        <InlineWriteupCheckbox label="Waterborne exposure" checked={writeupPayload.waterborneExposure} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ waterborneExposure: value })} />
                        <InlineWriteupCheckbox label="Recommendations outstanding" checked={writeupPayload.recommendationsOutstanding} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ recommendationsOutstanding: value })} />
                      </div>
                    )}
                    {isGeneralLiability && (
                      <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
                        <InlineWriteupTextarea label="Risk characteristics" value={writeupPayload.glRiskCharacteristics} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ glRiskCharacteristics: value })} />
                        <InlineWriteupTextarea label="Subcontractor and contract controls" value={writeupPayload.glSubcontractorControls} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ glSubcontractorControls: value })} />
                      </div>
                    )}
                    <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
                      <label className="block">
                        <span className="sims-field-label">Last inspection date</span>
                        <input
                          type="date"
                          value={writeupPayload.lastInspectionDate ?? ''}
                          readOnly={writeupReadOnly}
                          onChange={(e) => patchWriteupPayload({ lastInspectionDate: e.target.value })}
                          className="sims-input mt-1"
                        />
                      </label>
                      <InlineWriteupCheckbox label="Website reviewed" checked={writeupPayload.websiteReviewed === true} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ websiteReviewed: value })} />
                    </div>
                    <InlineWriteupTextarea label="Operations narrative" value={writeupPayload.narrativeOperators} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ narrativeOperators: value })} />
                    <InlineWriteupTextarea label="Recommendations detail" value={writeupPayload.recommendationsDetail} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ recommendationsDetail: value })} />
                    <InlineWriteupTextarea label="Website issues" value={writeupPayload.websiteIssues} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ websiteIssues: value })} />
                  </InlineWriteupSection>

                  {(isAutoLiability || isAutoPhysicalDamage) && (
                    <InlineWriteupSection number={isAutoLiability ? '06' : '07'} title="Drivers">
                      <div className="grid grid-cols-2 gap-3 max-[700px]:grid-cols-1">
                        <InlineWriteupTextarea label="# of drivers" value={writeupPayload.driverCount} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ driverCount: value })} />
                        <InlineWriteupTextarea label="Driver age span" value={writeupPayload.driverAgeSpan} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ driverAgeSpan: value })} />
                        <InlineWriteupTextarea label="Driver turnover %" value={writeupPayload.driverTurnoverPercent} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ driverTurnoverPercent: value })} />
                        <InlineWriteupTextarea label="Owner-op %" value={writeupPayload.ownerOperatorPercent} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ ownerOperatorPercent: value })} />
                      </div>
                      <InlineWriteupCheckbox label="MVRs in file within 90 days" checked={writeupPayload.mvrInFile === true} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ mvrInFile: value })} />
                      <InlineWriteupTextarea label="Drivers to exclude or watch" value={writeupPayload.driversWatchNotes} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ driversWatchNotes: value })} />
                    </InlineWriteupSection>
                  )}

                  <InlineWriteupSection number={isAutoLiability ? '07' : isAutoPhysicalDamage ? '08' : isInlandMarine || isGeneralLiability ? '07' : '05'} title="Underwriting notes">
                    {isInlandMarine && (
                      <InlineWriteupTextarea label="Fire suppression" value={writeupPayload.narrativeFireSuppression} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ narrativeFireSuppression: value })} />
                    )}
                    <InlineWriteupTextarea label={isAutoLiability || isAutoPhysicalDamage ? 'Drivers' : 'Other concerns'} value={isAutoLiability || isAutoPhysicalDamage ? writeupPayload.narrativeDrivers : writeupPayload.narrativeOtherConcerns} readOnly={writeupReadOnly} onChange={(value) => isAutoLiability || isAutoPhysicalDamage ? patchWriteupPayload({ narrativeDrivers: value }) : patchWriteupPayload({ narrativeOtherConcerns: value })} />
                    {(isAutoLiability || isAutoPhysicalDamage) && (
                      <>
                        <InlineWriteupTextarea label="CAB / FMCSA" value={writeupPayload.narrativeCabFmcsa} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ narrativeCabFmcsa: value })} />
                        <InlineWriteupTextarea label="Additional interests and contracts" value={writeupPayload.narrativeAdditionalInterests} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ narrativeAdditionalInterests: value })} />
                      </>
                    )}
                    {isGeneralLiability && (
                      <>
                        <InlineWriteupTextarea label="Exposure changes" value={writeupPayload.glExposureChanges} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ glExposureChanges: value })} />
                        <InlineWriteupTextarea label="Subcontractors" value={writeupPayload.glSubcontractorsNarrative} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ glSubcontractorsNarrative: value })} />
                        <InlineWriteupTextarea label="Endorsements and additional interests" value={writeupPayload.glEndorsementsNarrative} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ glEndorsementsNarrative: value })} />
                      </>
                    )}
                  </InlineWriteupSection>

                  {!isInlandMarine && (
                    <InlineWriteupSection number={isAutoLiability || isGeneralLiability ? '08' : '09'} title="Requested terms / pricing">
                      <InlineWriteupTextarea label="Pricing rationale" value={writeupPayload.pricingRationale} readOnly={writeupReadOnly} onChange={(value) => patchWriteupPayload({ pricingRationale: value })} />
                      <InlineWriteupTextarea label="Special terms / endorsements" value={writeupPayload.specialTerms} readOnly={writeupReadOnly} rows={2} onChange={(value) => patchWriteupPayload({ specialTerms: value })} />
                    </InlineWriteupSection>
                  )}

                  <InlineWriteupSection number={isInlandMarine ? '07' : isAutoLiability || isGeneralLiability ? '09' : isAutoPhysicalDamage ? '10' : '06'} title="Conditions">
                    <div className="space-y-2">
                      {writeupConditions.length === 0 ? (
                        <div className="rounded-lg border border-dashed border-slate-200 bg-white px-3 py-3 text-sm text-slate-500">No conditions added yet.</div>
                      ) : writeupConditions.map((condition, index) => (
                        <div key={condition.id || index} className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2">
                          <input
                            type="checkbox"
                            checked={condition.satisfied}
                            disabled={writeupReadOnly}
                            onChange={(e) => setWriteupConditions((conditions) => conditions.map((row, rowIndex) => rowIndex === index ? { ...row, satisfied: e.target.checked } : row))}
                            className="h-4 w-4 rounded border-slate-300"
                          />
                          <input
                            value={condition.text}
                            readOnly={writeupReadOnly}
                            onChange={(e) => setWriteupConditions((conditions) => conditions.map((row, rowIndex) => rowIndex === index ? { ...row, text: e.target.value } : row))}
                            className="sims-input h-8 flex-1"
                          />
                          {!writeupReadOnly && (
                            <button type="button" className="sims-icon-btn hover:text-red-500" onClick={() => removeWriteupCondition(condition.id)} title="Remove condition">
                              <Trash2 className="h-4 w-4" />
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                    {!writeupReadOnly && (
                      <div className="flex gap-2 max-[560px]:flex-col">
                        <input
                          value={newWriteupCondition}
                          onChange={(e) => setNewWriteupCondition(e.target.value)}
                          className="sims-input flex-1"
                          placeholder="Add condition..."
                        />
                        <Btn variant="outline" onClick={addWriteupCondition}><Plus className="h-3.5 w-3.5" /> Add</Btn>
                      </div>
                    )}
                  </InlineWriteupSection>

                  <InlineWriteupSection number={isInlandMarine ? '08' : isAutoLiability || isGeneralLiability ? '10' : isAutoPhysicalDamage ? '11' : '07'} title="Loss control and recommendation">
                    {!isInlandMarine && (
                      <InlineWriteupTextarea label="Loss control analysis" value={writeupPayload.lossControlAnalysis} readOnly={writeupReadOnly} rows={3} onChange={(value) => patchWriteupPayload({ lossControlAnalysis: value })} />
                    )}
                    <InlineWriteupTextarea label="Decision rationale" value={writeupPayload.decisionRationale} readOnly={writeupReadOnly} rows={4} onChange={(value) => patchWriteupPayload({ decisionRationale: value })} />
                  </InlineWriteupSection>
                </div>
              )}
            </Card>

            {/* Bind checklist */}
            <ChecklistCard quoteId={quoteId!} />

            {/* Documents */}
            <DocumentsCard quoteId={quoteId!} />

            {/* Activity */}
            <ActivityCard quoteId={quoteId!} />

            {/* Notes */}
            <NotesCard quoteId={quoteId!} />
          </div>

          {/* ── Sidebar ── */}
          <div className="flex flex-col gap-4 max-[1100px]:flex-row max-[1100px]:flex-wrap max-[600px]:flex-col">

            {/* Bind invoice preview */}
            <Card>
              <div className="px-4 py-3">
                <h3 className="mb-3 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Bind invoice preview</h3>
                {invoicePreview ? (
                  <>
                    {[
                      { label: 'Premium', value: formatCurrency(invoicePreview.grossPremium) },
                      { label: 'Taxes & fees', value: formatCurrency(invoicePreview.totalFees) },
                      { label: 'Invoice total', value: formatCurrency(invoicePreview.totalAmount) },
                    ].map((row) => (
                      <div key={row.label} className="flex items-center justify-between border-b border-slate-100 py-2 text-xs last:border-0">
                        <span className="text-slate-500">{row.label}</span>
                        <span className="font-medium text-slate-700">{row.value}</span>
                      </div>
                    ))}
                    <div className="mt-3 space-y-1">
                      {invoicePreview.lines.length > 0 ? invoicePreview.lines.map((line) => (
                        <div key={`${line.feeCode}-${line.feeDisplayName}`} className="flex items-start justify-between rounded-md bg-slate-50 px-2 py-1.5 text-[11px]">
                          <span className="min-w-0 pr-2 text-slate-600">
                            <span className="font-medium text-slate-700">{line.feeDisplayName}</span>
                            <span className="ml-1 text-slate-400">{line.feeCategory}</span>
                          </span>
                          <span className="font-semibold tabular-nums text-slate-800">{formatCurrency(line.amount)}</span>
                        </div>
                      )) : (
                        <div className="rounded-md bg-amber-50 px-2 py-2 text-[11px] font-medium text-amber-700">
                          No automatic fee rules match this quote.
                        </div>
                      )}
                    </div>
                  </>
                ) : (
                  <div className="rounded-md bg-slate-50 px-2 py-2 text-[11px] text-slate-500">
                    Fee preview will appear when the quote is loaded.
                  </div>
                )}
              </div>
            </Card>

            {/* Bind CTA */}
            {(canBind || ((quote.status === 'Quoted' || quote.status === 'Submitted') && openBlockers > 0)) && (
              <div className={`rounded-xl p-4 text-white shadow-sm ${canBind ? 'bg-sky-600' : 'bg-amber-500'}`}>
                <p className="mb-0.5 text-[10.5px] font-semibold uppercase tracking-wide text-white/70">
                  {canBind ? 'Ready to bind' : `${openBlockers} checklist item${openBlockers !== 1 ? 's' : ''} remaining`}
                </p>
                <p className="text-2xl font-semibold">{fmt(ratedTotalPremium)}</p>
                <p className="mb-4 text-xs text-white/70">{formatDate(quote.effectiveDate)} effective</p>
                <button
                  disabled={!canBind}
                  onClick={() => canBind && setShowBind(true)}
                  className="flex w-full items-center justify-center gap-2 rounded-lg bg-white py-2 text-sm font-semibold text-sky-800 hover:bg-sky-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-white/80 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <CheckCircle2 className="h-4 w-4" /> Bind this quote
                </button>
              </div>
            )}

            {/* Quote details */}
            <Card>
              <div className="px-4 py-3">
                <h3 className="mb-3 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Quote details</h3>
                {[
                  { label: 'Quote #', value: quote.quoteNumber },
                  { label: 'Carrier', value: quote.carrierName },
                  { label: 'Line', value: LOB_LABELS[quote.lineOfBusiness] },
                  { label: 'Status', value: quote.status },
                  { label: 'Effective', value: formatDate(quote.effectiveDate) },
                  { label: 'Expires', value: formatDate(quote.expirationDate) },
                  ...(quote.policyNumber ? [{ label: 'Policy #', value: quote.policyNumber }] : []),
                  ...(quote.isFilingState ? [{ label: 'Filing state', value: 'Yes' }] : []),
                ].map((row) => (
                  <div key={row.label} className="flex items-start justify-between border-b border-slate-100 py-2 text-xs last:border-0">
                    <span className="text-slate-400">{row.label}</span>
                    <span className="ml-3 text-right font-medium text-slate-700">{row.value}</span>
                  </div>
                ))}
                {ratingSnapshot && (
                  <div className="mt-2 rounded-lg bg-slate-50 px-2 py-2 text-[11px] leading-5 text-slate-500">
                    Rated by <span className="font-medium text-slate-700">{ratingSnapshot.ratedByName ?? 'System'}</span> on <span className="font-medium text-slate-700">{formatDate(ratingSnapshot.ratedAt)}</span>
                  </div>
                )}
              </div>
            </Card>

            {/* Commission breakdown */}
            <Card>
              <div className="px-4 py-3">
                <h3 className="mb-3 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Commission split</h3>
                {[
                  { label: 'Carrier commission', rate: quote.carrierCommissionRate, amt: carrierCommAmt },
                  { label: 'SMM retention', rate: quote.smmRetentionRate, amt: smmRetentionAmt },
                  { label: 'Agent commission', rate: agentCommRate, amt: agentCommAmt },
                ].map((row) => (
                  <div key={row.label} className="flex items-center justify-between border-b border-slate-100 py-2 text-xs last:border-0">
                    <span className="text-slate-500">{row.label}</span>
                    <span className="font-medium text-slate-700">{formatPercent(row.rate)} · {fmt(row.amt)}</span>
                  </div>
                ))}
                {quote.commissionOverride && (
                  <div className="mt-2 flex items-center gap-1.5 rounded bg-amber-50 px-2 py-1.5 text-[10.5px] font-medium text-amber-700">
                    <TrendingDown className="h-3 w-3" /> Override applied by {quote.commissionOverride.overrideBy}
                  </div>
                )}
              </div>
            </Card>

            {/* Other quotes (comparison) */}
            {otherQuotes.length > 0 && (
              <Card>
                <div className="px-4 py-3">
                  <h3 className="mb-3 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Other quotes</h3>
                  {otherQuotes.map((q) => (
                    <Link
                      key={q.id}
                      to={`/quotes/${q.id}`}
                      className="flex items-center justify-between border-b border-slate-100 py-2.5 text-xs last:border-0 hover:bg-slate-50"
                    >
                      <span className="min-w-0">
                        <div className="font-medium text-slate-700 truncate">{q.carrierName}</div>
                        <div className="font-mono text-slate-400">{q.quoteNumber}</div>
                      </span>
                      <span className={`ml-2 flex-shrink-0 font-semibold tabular-nums ${q.status === 'Declined' ? 'text-red-600' : 'text-slate-800'}`}>
                        {q.status === 'Declined' ? 'Declined' : fmt(q.totalPremium)}
                      </span>
                    </Link>
                  ))}
                  <Link
                    to={`/submissions/${quote.submissionId}`}
                    className="sd-btn outline sm mt-2 w-full"
                  >
                    View all quotes
                  </Link>
                </div>
              </Card>
            )}

            {/* Quick facts */}
            <Card>
              <div className="px-4 py-3">
                <h3 className="mb-3 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">Quick facts</h3>
                {[
                  { label: 'Submission', value: quote.submissionNumber },
                  { label: 'Insured', value: quote.insuredName },
                  { label: 'Created', value: formatDate(quote.createdAt) },
                  ...(quote.boundDate ? [{ label: 'Bound', value: formatDate(quote.boundDate) }] : []),
                  ...(quote.policyNumber ? [{ label: 'Policy', value: quote.policyNumber }] : []),
                ].map((row) => (
                  <div key={row.label} className="flex items-start justify-between border-b border-slate-100 py-2 text-xs last:border-0">
                    <span className="text-slate-400">{row.label}</span>
                    <span className="ml-3 text-right font-medium text-slate-700">{row.value}</span>
                  </div>
                ))}
              </div>
            </Card>
          </div>
        </div>
      </div>

      {showBind && (
        <BindModal
          quoteId={quoteId!}
          effectiveDate={quote.effectiveDate}
          expirationDate={quote.expirationDate}
          onClose={() => setShowBind(false)}
        />
      )}
    </div>
  )
}
