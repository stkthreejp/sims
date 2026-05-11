import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Upload, FileText, Pencil, Trash2, Search, LayoutTemplate } from 'lucide-react'
import { toast } from 'sonner'
import { documentTemplatesApi } from '@/api/documentTemplates.api'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { EmptyState } from '@/components/common/EmptyState'
import { ENTITY_TYPE_LABELS, type TemplateEntityType } from '@/lib/templateTags'
import type { DocumentTemplateListItem } from '@/types/documentTemplate.types'
import { formatDateTime } from '@/lib/utils'

const ENTITY_TYPE_COLORS: Record<TemplateEntityType, string> = {
  General:    'bg-slate-100 text-slate-600',
  Quote:      'bg-cyan-100 text-cyan-700',
  Policy:     'bg-blue-100 text-blue-700',
  Submission: 'bg-yellow-100 text-yellow-700',
  Carrier:    'bg-purple-100 text-purple-700',
  Agent:      'bg-green-100 text-green-700',
}

const ALL_TYPES: (TemplateEntityType | 'All')[] = ['All', 'General', 'Quote', 'Policy', 'Submission', 'Carrier', 'Agent']

export function DocumentLibraryPage() {
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [filter, setFilter] = useState<TemplateEntityType | 'All'>('All')
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(false)

  const { data: templates = [], isLoading } = useQuery({
    queryKey: ['document-templates', { filter, showInactive }],
    queryFn: () => documentTemplatesApi.getAll(
      filter === 'All' ? undefined : filter,
      showInactive
    ),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => documentTemplatesApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['document-templates'] })
      toast.success('Template deleted')
    },
    onError: () => toast.error('Failed to delete template'),
  })

  const displayed = templates.filter((t) =>
    search === '' ||
    t.name.toLowerCase().includes(search.toLowerCase()) ||
    t.description?.toLowerCase().includes(search.toLowerCase())
  )

  // Group by entity type for display
  const grouped = ALL_TYPES.filter((t) => t !== 'All').reduce<Record<string, DocumentTemplateListItem[]>>(
    (acc, type) => {
      const items = displayed.filter((t) => t.entityType === type)
      if (items.length > 0) acc[type] = items
      return acc
    },
    {}
  )

  const handleImportDoc = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''

    // Dynamic import — only load mammoth when needed
    const mammoth = await import('mammoth/mammoth.browser')
    const arrayBuffer = await file.arrayBuffer()
    const result = await mammoth.convertToHtml({ arrayBuffer })

    // Navigate to editor with pre-filled content
    navigate('/document-library/new', {
      state: {
        importedHtml: result.value,
        importedName: file.name.replace(/\.(doc|docx)$/i, ''),
      },
    })
  }

  return (
    <div>
      <PageHeader
        title="Document Library"
        description="Manage document templates for policies, submissions, and more"
        actions={
          <div className="flex gap-2">
            <label className="flex items-center gap-1.5 px-3 py-2 border border-slate-300 text-slate-700 text-sm font-medium rounded-md hover:bg-slate-50 cursor-pointer transition-colors">
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
              onClick={() => navigate('/document-library/new')}
              className="flex items-center gap-1.5 px-3 py-2 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 transition-colors"
            >
              <Plus className="h-4 w-4" />
              New Template
            </button>
          </div>
        }
      />

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3 mb-5">
        {/* Type filter pills */}
        <div className="flex gap-1.5 flex-wrap">
          {ALL_TYPES.map((type) => (
            <button
              key={type}
              onClick={() => setFilter(type)}
              className={`px-3 py-1.5 rounded-full text-xs font-medium border transition-colors ${
                filter === type
                  ? 'bg-blue-600 text-white border-blue-600'
                  : 'bg-white text-slate-600 border-slate-300 hover:border-blue-400'
              }`}
            >
              {type}
            </button>
          ))}
        </div>

        {/* Search */}
        <div className="relative ml-auto">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-slate-400" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search templates…"
            className="pl-8 pr-3 py-1.5 border border-slate-200 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 w-52"
          />
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer">
          <input
            type="checkbox"
            checked={showInactive}
            onChange={(e) => setShowInactive(e.target.checked)}
            className="rounded border-slate-300"
          />
          Show inactive
        </label>
      </div>

      {/* Content */}
      {isLoading ? (
        <LoadingSpinner />
      ) : displayed.length === 0 ? (
        <EmptyState
          icon={LayoutTemplate}
          title="No templates yet"
          description='Click "New Template" to create one or import from Word.'
        />
      ) : filter !== 'All' ? (
        // Flat list when filtered to one type
        <TemplateGrid
          templates={displayed}
          onEdit={(id) => navigate(`/document-library/${id}`)}
          onDelete={(id, name) => { if (confirm(`Delete "${name}"?`)) deleteMutation.mutate(id) }}
        />
      ) : (
        // Grouped by entity type
        <div className="space-y-8">
          {Object.entries(grouped).map(([type, items]) => (
            <div key={type}>
              <div className="flex items-center gap-2 mb-3">
                <span className={`px-2.5 py-1 rounded-full text-xs font-semibold ${ENTITY_TYPE_COLORS[type as TemplateEntityType]}`}>
                  {ENTITY_TYPE_LABELS[type as TemplateEntityType]}
                </span>
                <span className="text-sm text-slate-400">{items.length} template{items.length !== 1 ? 's' : ''}</span>
              </div>
              <TemplateGrid
                templates={items}
                onEdit={(id) => navigate(`/document-library/${id}`)}
                onDelete={(id, name) => { if (confirm(`Delete "${name}"?`)) deleteMutation.mutate(id) }}
              />
            </div>
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
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {templates.map((t) => (
        <div
          key={t.id}
          className="group bg-white border border-slate-200 rounded-lg p-4 hover:border-blue-300 hover:shadow-sm transition-all cursor-pointer"
          onClick={() => onEdit(t.id)}
        >
          <div className="flex items-start justify-between gap-2 mb-2">
            <div className="flex items-center gap-2">
              <FileText className="h-4 w-4 text-blue-500 shrink-0 mt-0.5" />
              <p className="text-sm font-medium text-slate-900 leading-tight">{t.name}</p>
            </div>
            <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"
              onClick={(e) => e.stopPropagation()}>
              <button
                onClick={() => onEdit(t.id)}
                className="p-1 rounded text-slate-400 hover:text-blue-600 hover:bg-blue-50"
                title="Edit"
              >
                <Pencil className="h-3.5 w-3.5" />
              </button>
              <button
                onClick={() => onDelete(t.id, t.name)}
                className="p-1 rounded text-slate-400 hover:text-red-600 hover:bg-red-50"
                title="Delete"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          </div>

          {t.description && (
            <p className="text-xs text-slate-500 mb-2 line-clamp-2">{t.description}</p>
          )}

          <div className="flex items-center justify-between mt-3 pt-3 border-t border-slate-100">
            <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${ENTITY_TYPE_COLORS[t.entityType]}`}>
              {ENTITY_TYPE_LABELS[t.entityType]}
            </span>
            {!t.isActive && (
              <span className="text-xs text-slate-400 italic">Inactive</span>
            )}
          </div>
          <p className="text-xs text-slate-400 mt-1">
            Updated {formatDateTime(t.updatedAt)}
          </p>
        </div>
      ))}
    </div>
  )
}
