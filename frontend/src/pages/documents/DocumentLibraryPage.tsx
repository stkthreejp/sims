import { useState, type ChangeEvent, type MouseEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Upload, FileText, Pencil, Trash2, Search, LayoutTemplate } from 'lucide-react'
import { toast } from 'sonner'
import { documentTemplatesApi } from '@/api/documentTemplates.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { getApiErrorMessage } from '@/lib/apiError'
import { ENTITY_TYPE_LABELS, type TemplateEntityType } from '@/lib/templateTags'
import { importWordDocument } from '@/lib/wordImport'
import type { DocumentTemplateKind, DocumentTemplateListItem } from '@/types/documentTemplate.types'
import { formatDateTime } from '@/lib/utils'

const ENTITY_TYPE_VARIANTS: Record<TemplateEntityType, string> = {
  General: 'draft',
  Quote: 'quoted',
  Policy: 'bound',
  Submission: 'inprogress',
  Carrier: 'submitted',
  Agent: 'bound',
}

const ALL_TYPES: (TemplateEntityType | 'All')[] = ['All', 'General', 'Quote', 'Policy', 'Submission', 'Carrier', 'Agent']
const ALL_KINDS: (DocumentTemplateKind | 'All')[] = ['All', 'Document', 'Email', 'DocumentAndEmail']

const KIND_LABELS: Record<DocumentTemplateKind, string> = {
  Document: 'Document',
  Email: 'Email',
  DocumentAndEmail: 'Document + Email',
}

const KIND_VARIANTS: Record<DocumentTemplateKind, string> = {
  Document: 'draft',
  Email: 'quoted',
  DocumentAndEmail: 'bound',
}

const filterButtonStyle = (active: boolean) => ({
  borderColor: active ? 'var(--accent)' : 'var(--line-2)',
  background: active ? 'var(--accent)' : 'var(--surface)',
  color: active ? '#fff' : 'var(--ink-3)',
})

export function DocumentLibraryPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [filter, setFilter] = useState<TemplateEntityType | 'All'>('All')
  const [kindFilter, setKindFilter] = useState<DocumentTemplateKind | 'All'>('All')
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(false)

  const { data: templates = [], isLoading, isError, error, refetch } = useQuery({
    queryKey: ['document-templates', { filter, kindFilter, showInactive }],
    queryFn: () => documentTemplatesApi.getAll(
      filter === 'All' ? undefined : filter,
      showInactive,
      kindFilter === 'All' ? undefined : kindFilter
    ),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => documentTemplatesApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['document-templates'] })
      toast.success('Template deleted')
    },
    onError: (err) => toast.error(getApiErrorMessage(err, 'Failed to delete template')),
  })

  const displayed = templates.filter((t) =>
    search === '' ||
    t.name.toLowerCase().includes(search.toLowerCase()) ||
    t.description?.toLowerCase().includes(search.toLowerCase())
  )

  const grouped = ALL_TYPES.filter((t) => t !== 'All').reduce<Record<string, DocumentTemplateListItem[]>>(
    (acc, type) => {
      const items = displayed.filter((t) => t.entityType === type)
      if (items.length > 0) acc[type] = items
      return acc
    },
    {}
  )

  const handleImportDoc = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''

    try {
      const importedHtml = await importWordDocument(file)

      navigate('/document-library/new', {
        state: {
          importedHtml,
          importedName: file.name.replace(/\.(doc|docx)$/i, ''),
        },
      })
    } catch (e) {
      toast.error(getApiErrorMessage(e))
    }
  }

  return (
    <div>
      <PageHeader
        title="Document Library"
        subtitle="Manage document templates for policies, submissions, and more"
        action={
          <div className="flex gap-2">
            <label className="sd-btn">
              <Upload className="h-4 w-4" />
              Import from Word
              <input
                type="file"
                accept=".doc,.docx"
                className="hidden"
                onChange={handleImportDoc}
              />
            </label>
            <button
              type="button"
              onClick={() => navigate('/document-library/new')}
              className="sd-btn primary"
            >
              <Plus className="h-4 w-4" />
              New Template
            </button>
          </div>
        }
      />

      <div className="sd-card mb-5">
        <div className="sd-card-body">
          <div className="flex flex-wrap items-center gap-3">
            <div className="flex flex-wrap gap-1.5">
              {ALL_TYPES.map((type) => (
                <button
                  key={type}
                  type="button"
                  onClick={() => setFilter(type)}
                  className="sd-btn sm"
                  style={filterButtonStyle(filter === type)}
                >
                  {type}
                </button>
              ))}
            </div>

            <div className="flex flex-wrap gap-1.5">
              {ALL_KINDS.map((kind) => (
                <button
                  key={kind}
                  type="button"
                  onClick={() => setKindFilter(kind)}
                  className="sd-btn sm"
                  style={filterButtonStyle(kindFilter === kind)}
                >
                  {kind === 'All' ? 'All Kinds' : KIND_LABELS[kind]}
                </button>
              ))}
            </div>

            <div className="relative ml-auto min-w-[220px]">
              <Search className="absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2" style={{ color: 'var(--ink-4)' }} />
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search templates..."
                className="sims-input pl-8"
              />
            </div>

            <label className="flex cursor-pointer items-center gap-2" style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>
              <input
                type="checkbox"
                checked={showInactive}
                onChange={(e) => setShowInactive(e.target.checked)}
                className="rounded border-slate-300"
              />
              Show inactive
            </label>
          </div>
        </div>
      </div>

      {isError ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : isLoading ? (
        <LoadingSpinner />
      ) : displayed.length === 0 ? (
        <EmptyState
          icon={LayoutTemplate}
          title="No templates yet"
          description="Create a template or import a Word document to start building the library."
          action={
            <button type="button" onClick={() => navigate('/document-library/new')} className="sd-btn primary sm">
              New Template
            </button>
          }
        />
      ) : filter !== 'All' ? (
        <TemplateGrid
          templates={displayed}
          onEdit={(id) => navigate(`/document-library/${id}`)}
          onDelete={(id, name) => { if (confirm(`Delete "${name}"?`)) deleteMutation.mutate(id) }}
        />
      ) : (
        <div className="space-y-6">
          {Object.entries(grouped).map(([type, items]) => (
            <section key={type} className="space-y-3">
              <div className="flex items-center gap-2">
                <span className={`sd-pill ${ENTITY_TYPE_VARIANTS[type as TemplateEntityType]}`}>
                  {ENTITY_TYPE_LABELS[type as TemplateEntityType]}
                </span>
                <span style={{ color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
                  {items.length} template{items.length !== 1 ? 's' : ''}
                </span>
              </div>
              <TemplateGrid
                templates={items}
                onEdit={(id) => navigate(`/document-library/${id}`)}
                onDelete={(id, name) => { if (confirm(`Delete "${name}"?`)) deleteMutation.mutate(id) }}
              />
            </section>
          ))}
        </div>
      )}
    </div>
  )
}

function TemplateGrid({
  templates,
  onEdit,
  onDelete,
}: {
  templates: DocumentTemplateListItem[]
  onEdit: (id: string) => void
  onDelete: (id: string, name: string) => void
}) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {templates.map((t) => (
        <article
          key={t.id}
          className="sd-card cursor-pointer transition-colors"
          onClick={() => onEdit(t.id)}
        >
          <div className="sd-card-body">
            <div className="mb-3 flex items-start justify-between gap-2">
              <div className="flex min-w-0 items-start gap-2">
                <FileText className="mt-0.5 h-4 w-4 shrink-0" style={{ color: 'var(--accent)' }} />
                <p className="min-w-0 leading-snug" style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>
                  {t.name}
                </p>
              </div>
              <div className="flex shrink-0 gap-1" onClick={(e: MouseEvent<HTMLDivElement>) => e.stopPropagation()}>
                <button
                  type="button"
                  onClick={() => onEdit(t.id)}
                  className="sims-icon-btn"
                  title="Edit"
                  aria-label={`Edit ${t.name}`}
                >
                  <Pencil className="h-3.5 w-3.5" />
                </button>
                <button
                  type="button"
                  onClick={() => onDelete(t.id, t.name)}
                  className="sims-icon-btn hover:text-red-500"
                  title="Delete"
                  aria-label={`Delete ${t.name}`}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>

            {t.description && (
              <p className="mb-3 line-clamp-2" style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>
                {t.description}
              </p>
            )}

            <div className="mt-3 flex items-center justify-between gap-2 border-t pt-3" style={{ borderColor: 'var(--line-2)' }}>
              <div className="flex flex-wrap items-center gap-1.5">
                <span className={`sd-pill ${ENTITY_TYPE_VARIANTS[t.entityType]}`}>
                  {ENTITY_TYPE_LABELS[t.entityType]}
                </span>
                <span className={`sd-pill ${KIND_VARIANTS[t.kind]}`}>
                  {KIND_LABELS[t.kind]}
                </span>
              </div>
              {!t.isActive && (
                <span className="sd-pill draft">Inactive</span>
              )}
            </div>
            <p style={{ margin: '8px 0 0', color: 'var(--ink-4)', fontSize: 'var(--fs-sm)' }}>
              Updated {formatDateTime(t.updatedAt)}
            </p>
          </div>
        </article>
      ))}
    </div>
  )
}
