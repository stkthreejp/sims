import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Pin, PinOff, Pencil, Trash2, Plus, X, Check } from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { LOB_LABELS, QUOTE_STATUS_LABELS } from '@/types/quote.types'
import { formatCurrency } from '@/lib/utils'
import type { Note } from '@/types/quote.types'
import { DocumentsSection } from '@/components/documents/DocumentsSection'
import { usePermissions } from '@/hooks/usePermissions'

export function PolicyDetailPage() {
  const { id } = useParams<{ id: string }>()
  const qc = useQueryClient()

  const [noteSubject, setNoteSubject] = useState('')
  const [noteBody, setNoteBody] = useState('')
  const [showNoteForm, setShowNoteForm] = useState(false)
  const [editingNote, setEditingNote] = useState<Note | null>(null)
  const [editSubject, setEditSubject] = useState('')
  const [editBody, setEditBody] = useState('')

  const { data: policy, isLoading } = useQuery({
    queryKey: ['quotes', id],
    queryFn: () => quotesApi.getById(id!),
  })

  const { data: notes = [] } = useQuery({
    queryKey: ['quotes', id, 'notes'],
    queryFn: () => quotesApi.getNotes(id!),
    enabled: !!id,
  })

  const createNoteMutation = useMutation({
    mutationFn: () => quotesApi.createNote(id!, { subject: noteSubject || undefined, body: noteBody }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', id, 'notes'] })
      setNoteSubject('')
      setNoteBody('')
      setShowNoteForm(false)
      toast.success('Note added')
    },
    onError: () => toast.error('Failed to add note'),
  })

  const updateNoteMutation = useMutation({
    mutationFn: (note: Note) => quotesApi.updateNote(id!, note.id, { subject: editSubject || undefined, body: editBody }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', id, 'notes'] })
      setEditingNote(null)
      toast.success('Note updated')
    },
    onError: () => toast.error('Failed to update note'),
  })

  const deleteNoteMutation = useMutation({
    mutationFn: (noteId: string) => quotesApi.deleteNote(id!, noteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['quotes', id, 'notes'] })
      toast.success('Note deleted')
    },
    onError: () => toast.error('Failed to delete note'),
  })

  const togglePinMutation = useMutation({
    mutationFn: (noteId: string) => quotesApi.togglePinNote(id!, noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['quotes', id, 'notes'] }),
  })

  const { canCreateNotes, canEditNotes, canDeleteNotes, canUploadAttachments, canDeleteAttachments } = usePermissions()

  if (isLoading) return <LoadingSpinner />
  if (!policy) return <p className="p-6 text-slate-500">Policy not found.</p>

  const sortedNotes = [...notes].sort((a, b) => {
    if (a.isPinned !== b.isPinned) return a.isPinned ? -1 : 1
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center gap-2 text-sm text-slate-500">
        <Link to={`/insureds/${policy.insuredId}`} className="hover:text-slate-900">{policy.insuredName}</Link>
        <span>/</span>
        <Link to={`/submissions/${policy.submissionId}`} className="hover:text-slate-900">{policy.submissionNumber}</Link>
        <span>/</span>
        <span className="text-slate-700">{policy.policyNumber ?? policy.quoteNumber}</span>
      </div>

      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">{policy.policyNumber ?? policy.quoteNumber}</h1>
          <p className="text-sm text-slate-500 mt-0.5">{policy.insuredName} · {policy.carrierName}</p>
        </div>
        <span className="inline-flex px-2.5 py-1 rounded-full text-xs font-medium bg-green-100 text-green-700">
          {QUOTE_STATUS_LABELS[policy.status]}
        </span>
      </div>

      {/* Policy details */}
      <div className="bg-white border rounded-lg p-5 grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Line of Business</p>
          <p className="font-medium">{LOB_LABELS[policy.lineOfBusiness]}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Carrier</p>
          <p className="font-medium">{policy.carrierName}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Effective Date</p>
          <p className="font-medium">{new Date(policy.effectiveDate).toLocaleDateString()}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Expiration Date</p>
          <p className="font-medium">{new Date(policy.expirationDate).toLocaleDateString()}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Premium</p>
          <p className="font-medium">{formatCurrency(policy.premiumAmount)}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Taxes & Fees</p>
          <p className="font-medium">{formatCurrency(policy.taxesAndFees)}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Total Premium</p>
          <p className="font-medium">{formatCurrency(policy.totalPremium)}</p>
        </div>
        <div>
          <p className="text-xs text-slate-500 mb-0.5">Commission</p>
          <p className="font-medium">{formatCurrency(policy.commissionAmount)} ({(policy.commissionRate * 100).toFixed(1)}%)</p>
        </div>
        {policy.limit != null && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Limit</p>
            <p className="font-medium">{formatCurrency(policy.limit)}</p>
          </div>
        )}
        {policy.deductible != null && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Deductible</p>
            <p className="font-medium">{formatCurrency(policy.deductible)}</p>
          </div>
        )}
        {policy.boundDate && (
          <div>
            <p className="text-xs text-slate-500 mb-0.5">Bound Date</p>
            <p className="font-medium">{new Date(policy.boundDate).toLocaleDateString()}</p>
          </div>
        )}
        {policy.coverageDescription && (
          <div className="col-span-2 md:col-span-4">
            <p className="text-xs text-slate-500 mb-0.5">Coverage Description</p>
            <p className="text-slate-700">{policy.coverageDescription}</p>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Notes */}
        <div className="bg-white border rounded-lg">
          <div className="flex items-center justify-between px-5 py-4 border-b">
            <h2 className="text-sm font-semibold text-slate-900">Notes ({notes.length})</h2>
            {!showNoteForm && canCreateNotes && (
              <button
                onClick={() => setShowNoteForm(true)}
                className="flex items-center gap-1 text-sm text-blue-600 hover:underline"
              >
                <Plus className="h-3.5 w-3.5" /> Add Note
              </button>
            )}
          </div>

          {showNoteForm && (
            <div className="px-5 py-4 border-b bg-slate-50 space-y-3">
              <input
                type="text"
                placeholder="Subject (optional)"
                value={noteSubject}
                onChange={(e) => setNoteSubject(e.target.value)}
                className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <textarea
                placeholder="Note body *"
                value={noteBody}
                onChange={(e) => setNoteBody(e.target.value)}
                rows={3}
                className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
              />
              <div className="flex gap-2">
                <button
                  disabled={!noteBody.trim() || createNoteMutation.isPending}
                  onClick={() => createNoteMutation.mutate()}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
                >
                  <Check className="h-3.5 w-3.5" /> Save
                </button>
                <button onClick={() => { setShowNoteForm(false); setNoteSubject(''); setNoteBody('') }} className="flex items-center gap-1.5 px-3 py-1.5 border rounded text-sm hover:bg-white">
                  <X className="h-3.5 w-3.5" /> Cancel
                </button>
              </div>
            </div>
          )}

          <div className="divide-y">
            {sortedNotes.length === 0 && !showNoteForm && (
              <p className="text-sm text-slate-400 px-5 py-8 text-center">No notes yet.</p>
            )}
            {sortedNotes.map((note) => (
              <div key={note.id} className={`px-5 py-4 ${note.isPinned ? 'bg-yellow-50' : ''}`}>
                {editingNote?.id === note.id ? (
                  <div className="space-y-2">
                    <input
                      type="text"
                      placeholder="Subject (optional)"
                      value={editSubject}
                      onChange={(e) => setEditSubject(e.target.value)}
                      className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                    <textarea
                      value={editBody}
                      onChange={(e) => setEditBody(e.target.value)}
                      rows={3}
                      className="w-full border rounded px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
                    />
                    <div className="flex gap-2">
                      <button
                        disabled={!editBody.trim() || updateNoteMutation.isPending}
                        onClick={() => updateNoteMutation.mutate(note)}
                        className="flex items-center gap-1.5 px-3 py-1 bg-blue-600 text-white rounded text-xs hover:bg-blue-700 disabled:opacity-50"
                      >
                        <Check className="h-3 w-3" /> Save
                      </button>
                      <button onClick={() => setEditingNote(null)} className="flex items-center gap-1.5 px-3 py-1 border rounded text-xs hover:bg-white">
                        <X className="h-3 w-3" /> Cancel
                      </button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0 flex-1">
                        {note.subject && <p className="text-sm font-medium text-slate-900">{note.subject}</p>}
                        <p className="text-sm text-slate-700 whitespace-pre-wrap mt-0.5">{note.body}</p>
                        <p className="text-xs text-slate-400 mt-1">
                          {note.createdByName} · {new Date(note.createdAt).toLocaleDateString()}
                        </p>
                      </div>
                      <div className="flex gap-1 shrink-0">
                        {canEditNotes && (
                          <button onClick={() => togglePinMutation.mutate(note.id)} className="p-1 rounded hover:bg-slate-100">
                            {note.isPinned
                              ? <PinOff className="h-3.5 w-3.5 text-yellow-500" />
                              : <Pin className="h-3.5 w-3.5 text-slate-400" />}
                          </button>
                        )}
                        {canEditNotes && (
                          <button onClick={() => { setEditingNote(note); setEditSubject(note.subject ?? ''); setEditBody(note.body) }} className="p-1 rounded hover:bg-slate-100">
                            <Pencil className="h-3.5 w-3.5 text-slate-400" />
                          </button>
                        )}
                        {canDeleteNotes && (
                          <button onClick={() => { if (confirm('Delete note?')) deleteNoteMutation.mutate(note.id) }} className="p-1 rounded hover:bg-slate-100">
                            <Trash2 className="h-3.5 w-3.5 text-slate-400 hover:text-red-500" />
                          </button>
                        )}
                      </div>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>
        </div>

      </div>

      {/* Documents */}
      <div className="bg-white border rounded-lg p-5">
        <DocumentsSection entityType="Policy" entityId={id!} canUpload={canUploadAttachments} canDelete={canDeleteAttachments} />
      </div>
    </div>
  )
}
