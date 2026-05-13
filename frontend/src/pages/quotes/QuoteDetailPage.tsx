import { useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, Calculator, Check, CheckCircle2, ChevronRight, Copy, Download,
  Edit2, FileOutput, FileText, MoreHorizontal, Pin, Plus, RefreshCw,
  ShieldCheck, Trash2, TrendingDown, Upload, X,
} from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import { LOB_LABELS, type CommissionOverrideRequest, type PolicyLineOfBusiness, type QuoteStatus } from '@/types/quote.types'
import { QuoteAutoSafetyPanel } from '@/components/quotes/QuoteAutoSafetyPanel'
import { QuoteRatingPanel } from '@/components/quotes/QuoteRatingPanel'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { GenerateDocumentModal } from '@/components/documents/GenerateDocumentModal'
import { attachmentsApi } from '@/api/attachments.api'
import { documentGenerationApi } from '@/api/documentGeneration.api'
import { uwWriteupApi } from '@/api/uwWriteup.api'
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
  Draft:     'bg-slate-100 text-slate-600',
  Submitted: 'bg-blue-50 text-blue-700',
  Quoted:    'bg-sky-100 text-sky-700',
  Bound:     'bg-blue-100 text-blue-900',
  Declined:  'bg-red-50 text-red-700',
  Cancelled: 'bg-slate-100 text-slate-500',
  Expired:   'bg-amber-50 text-amber-700',
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
    <div className={`rounded-xl border border-slate-200 bg-white shadow-sm ${className}`}>
      {children}
    </div>
  )
}

function CardHead({
  title, count, right,
}: { title: React.ReactNode; count?: React.ReactNode; right?: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-slate-200 px-5 py-3.5">
      <h2 className="text-sm font-semibold text-slate-800">
        {title}
        {count != null && (
          <span className="ml-2 font-normal text-slate-400 text-xs">{count}</span>
        )}
      </h2>
      {right && <div className="flex items-center gap-2">{right}</div>}
    </div>
  )
}

function KV({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <div className="mb-1 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400">{label}</div>
      <div className="text-sm font-medium text-slate-800 break-words">{value ?? '—'}</div>
    </div>
  )
}

function Btn({ children, onClick, variant = 'ghost', disabled, className = '', type = 'button' }: {
  children: React.ReactNode; onClick?: () => void; variant?: 'ghost' | 'outline' | 'primary' | 'danger'
  disabled?: boolean; className?: string; type?: 'button' | 'submit'
}) {
  const base = 'inline-flex items-center gap-1.5 rounded-lg px-3 h-8 text-xs font-medium whitespace-nowrap transition-colors disabled:opacity-50'
  const cls: Record<string, string> = {
    ghost:   'text-slate-600 hover:bg-slate-100 hover:text-slate-800',
    outline: 'border border-slate-200 bg-white text-slate-700 hover:border-sky-300 hover:bg-sky-50',
    primary: 'bg-sky-600 text-white hover:bg-sky-700',
    danger:  'border border-red-200 bg-white text-red-600 hover:bg-red-50',
  }
  return (
    <button type={type} disabled={disabled} onClick={onClick} className={`${base} ${cls[variant]} ${className}`}>
      {children}
    </button>
  )
}

// ── Bind checklist ─────────────────────────────────────────────────────────────

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
        <div className="absolute right-0 top-9 z-20 min-w-48 overflow-hidden rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
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
      className="flex w-full items-center gap-2 px-3 py-2 text-left text-xs font-medium text-slate-600 hover:bg-slate-50 hover:text-slate-900 disabled:cursor-not-allowed disabled:opacity-50"
    >
      {children}
    </button>
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
                className={`flex h-5 w-5 flex-shrink-0 items-center justify-center rounded border transition-colors disabled:cursor-default ${
                  item.isCompleted
                    ? 'border-emerald-500 bg-emerald-500 text-white'
                    : 'border-slate-300 bg-white hover:border-sky-400'
                }`}
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-sm rounded-xl border border-slate-200 bg-white p-6 shadow-xl">
        <div className="mb-5 flex items-center justify-between">
          <h2 className="text-base font-semibold text-slate-800">Bind quote</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600"><X className="h-4 w-4" /></button>
        </div>
        <div className="space-y-4">
          {(['boundDate', 'effectiveDate', 'expirationDate'] as const).map((k) => (
            <label key={k} className="block">
              <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-400">
                {k === 'boundDate' ? 'Bound date' : k === 'effectiveDate' ? 'Effective date' : 'Expiration date'}
              </span>
              <input
                type="date"
                value={form[k]}
                onChange={(e) => setForm((f) => ({ ...f, [k]: e.target.value }))}
                className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-800 focus:border-sky-400 focus:outline-none focus:ring-1 focus:ring-sky-400"
              />
            </label>
          ))}
        </div>
        <div className="mt-6 flex justify-end gap-2">
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
            className="w-full resize-none rounded-lg border border-slate-200 p-3 text-sm text-slate-800 placeholder:text-slate-400 focus:border-sky-400 focus:outline-none focus:ring-1 focus:ring-sky-400"
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
                  className={`rounded p-1 transition-colors ${note.isPinned ? 'text-amber-500 hover:text-amber-600' : 'text-slate-300 hover:text-slate-500'}`}
                  title={note.isPinned ? 'Unpin' : 'Pin'}
                >
                  <Pin className="h-3.5 w-3.5" />
                </button>
                <button
                  onClick={() => { if (confirm('Delete this note?')) deleteMutation.mutate(note.id) }}
                  className="rounded p-1 text-slate-300 transition-colors hover:text-red-500"
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
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
              <th className="px-5 py-2.5 text-left">File</th>
              <th className="px-4 py-2.5 text-left">Size</th>
              <th className="px-4 py-2.5 text-left">Uploaded by</th>
              <th className="px-4 py-2.5 text-left">Date</th>
              <th className="w-16 px-4 py-2.5" />
            </tr>
          </thead>
          <tbody>
            {attachments.map((a, i) => (
              <tr key={a.id} className={`${i < attachments.length - 1 ? 'border-b border-slate-100' : ''} hover:bg-slate-50`}>
                <td className="px-5 py-3">
                  <div className="flex items-center gap-2">
                    <FileText className="h-3.5 w-3.5 flex-shrink-0 text-slate-400" />
                    <span className="cursor-pointer font-medium text-sky-700 hover:text-sky-800" onClick={() => downloadMutation.mutate(a.id)}>
                      {a.fileName}
                    </span>
                  </div>
                  {a.description && <p className="mt-0.5 pl-5.5 text-xs text-slate-400">{a.description}</p>}
                </td>
                <td className="px-4 py-3 font-mono text-xs text-slate-400">{fmtBytes(a.fileSizeBytes)}</td>
                <td className="px-4 py-3 text-slate-600">{a.uploadedByName}</td>
                <td className="px-4 py-3 font-mono text-xs text-slate-400">{formatDate(a.createdAt)}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center justify-end gap-1">
                    <button
                      onClick={() => downloadMutation.mutate(a.id)}
                      className="rounded p-1 text-slate-300 hover:text-sky-600"
                      title="Download"
                    >
                      <Download className="h-3.5 w-3.5" />
                    </button>
                    <button
                      onClick={() => { if (confirm('Delete this document?')) deleteMutation.mutate(a.id) }}
                      className="rounded p-1 text-slate-300 hover:text-red-500"
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

  const { data: writeup } = useQuery({
    queryKey: ['uw-writeup', quoteId],
    queryFn: () => uwWriteupApi.get(quoteId!),
    enabled: !!quoteId,
  })

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
      window.open(data.generatedDocument.url, '_blank', 'noopener,noreferrer')
      toast.success('Proposal draft created and filed')
    },
    onError: (e: any) => toast.error(e?.response?.data?.errorMessage ?? 'Failed to create proposal draft'),
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

  const isAuto = AUTO_LOBS.has(quote.lineOfBusiness)
  const openBlockers = checklist.filter((i) => i.isBlocker && !i.isCompleted).length
  const canBind = (quote.status === 'Quoted' || quote.status === 'Submitted') && openBlockers === 0
  const canReduce = quote.status !== 'Bound' && quote.status !== 'Cancelled' && quote.status !== 'Expired' && canCreatePolicies && !quote.commissionOverride
  const canGenerateInlandMarineProposal = quote.lineOfBusiness === 'InlandMarine' && !!ratingSnapshot && ratingSnapshot.grandTotalPremium > 0
  const otherQuotes = siblingQuotes.filter((q) => q.id !== quote.id)

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
              <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold before:block before:h-1.5 before:w-1.5 before:rounded-full before:bg-current ${STATUS_PILL[quote.status]}`}>
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
          <div className="mb-5 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
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
                <h2 className="text-sm font-semibold text-amber-900">Reduce agent commission</h2>
                <p className="text-xs text-amber-700">Carrier net and SMM commission stay unchanged. Agent give-back reduces total premium.</p>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <div className="flex overflow-hidden rounded-md border border-amber-300 bg-white text-sm">
                <button
                  type="button"
                  onClick={() => setOverrideMode('dollar')}
                  className={`px-3 py-2 ${overrideMode === 'dollar' ? 'bg-amber-700 text-white' : 'text-amber-800'}`}
                >
                  $ Give-back
                </button>
                <button
                  type="button"
                  onClick={() => setOverrideMode('rate')}
                  className={`border-l border-amber-200 px-3 py-2 ${overrideMode === 'rate' ? 'bg-amber-700 text-white' : 'text-amber-800'}`}
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
                className="w-36 rounded-md border border-amber-300 bg-white px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-amber-400"
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
              <CardHead title="Coverage limits" right={<Btn variant="ghost"><Edit2 className="h-3.5 w-3.5" />Edit</Btn>} />
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

{/* Inline UW Writeup */}
            <Card>
              <CardHead
                title={<span className="flex items-center gap-2"><Edit2 className="h-4 w-4 text-slate-500" />Underwriting writeup</span>}
                right={<span className="rounded-md bg-slate-100 px-2 py-1 text-[10.5px] font-semibold uppercase tracking-wide text-slate-500">{writeup?.status ?? 'Draft'}</span>}
              />
              <div className="divide-y divide-slate-100">
                {[
                  { n: '01', title: 'Account summary', body: writeup ? `${writeup.insuredName} · ${writeup.lob} · ${writeup.policyType} account effective ${formatDate(writeup.effectiveDate)}.` : 'Writeup context will load here.' },
                  { n: '02', title: 'Risk description & operations', body: writeup?.payload.narrativeOperators || writeup?.operationType || 'No operation narrative entered yet.' },
                  { n: '03', title: 'Equipment & values', body: writeup ? `${writeup.equipment.totalCount} scheduled item${writeup.equipment.totalCount !== 1 ? 's' : ''}; total TIV ${fmt(writeup.equipment.totalTiv)}; largest item ${fmt(writeup.equipment.largestUnitTiv)}.` : 'Equipment summary will load here.' },
                  { n: '04', title: 'Decision rationale', body: writeup?.payload.decisionRationale || 'No decision rationale entered yet.' },
                ].map((section, index) => (
                  <div key={section.n} className={index === 0 ? 'bg-white' : 'bg-slate-50/60'}>
                    <div className="flex items-center gap-3 px-5 py-3">
                      <span className="font-mono text-[11px] font-semibold text-slate-400">{section.n}</span>
                      <span className="text-sm font-semibold text-slate-800">{section.title}</span>
                      <span className={`ml-auto rounded-md border px-2 py-0.5 text-[10.5px] font-semibold ${section.body.startsWith('No ') ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-emerald-200 bg-emerald-50 text-emerald-700'}`}>
                        {section.body.startsWith('No ') ? 'open' : 'complete'}
                      </span>
                    </div>
                    <p className="px-5 pb-4 pl-[4.5rem] text-sm leading-6 text-slate-600">{section.body}</p>
                  </div>
                ))}
              </div>
            </Card>

            {/* Bind checklist */}
            <ChecklistCard quoteId={quoteId!} />

            {/* Documents */}
            <DocumentsCard quoteId={quoteId!} />

            {/* Notes */}
            <NotesCard quoteId={quoteId!} />
          </div>

          {/* ── Sidebar ── */}
          <div className="flex flex-col gap-4 max-[1100px]:flex-row max-[1100px]:flex-wrap max-[600px]:flex-col">

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
                  className="flex w-full items-center justify-center gap-2 rounded-lg bg-white py-2 text-sm font-semibold text-sky-800 hover:bg-sky-50 disabled:cursor-not-allowed disabled:opacity-60"
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
                    className="mt-2 flex w-full items-center justify-center rounded-lg border border-slate-200 py-1.5 text-xs font-medium text-slate-500 hover:bg-slate-50"
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
