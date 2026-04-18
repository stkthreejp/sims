import { useState, useEffect } from 'react'
import { useNavigate, useParams, useLocation } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Save, Upload, ToggleLeft, ToggleRight, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import { documentTemplatesApi } from '@/api/documentTemplates.api'
import { TemplateEditor } from '@/components/editor/TemplateEditor'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { ENTITY_TYPE_LABELS, type TemplateEntityType } from '@/lib/templateTags'

const ENTITY_TYPES: TemplateEntityType[] = ['General', 'Policy', 'Submission', 'Carrier', 'Agent']

interface LocationState {
  importedHtml?: string
  importedName?: string
}

export function TemplateEditorPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const location = useLocation()
  const qc = useQueryClient()
  const isNew = !id || id === 'new'
  const state = location.state as LocationState | null

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [entityType, setEntityType] = useState<TemplateEntityType>('General')
  const [isActive, setIsActive] = useState(true)
  const [content, setContent] = useState('<p></p>')
  const [isDirty, setIsDirty] = useState(false)

  // Load existing template
  const { data: template, isLoading } = useQuery({
    queryKey: ['document-templates', id],
    queryFn: () => documentTemplatesApi.getById(id!),
    enabled: !isNew,
  })

  // Populate form when template loads
  useEffect(() => {
    if (template) {
      setName(template.name)
      setDescription(template.description ?? '')
      setEntityType(template.entityType)
      setIsActive(template.isActive)
      setContent(template.htmlContent)
    }
  }, [template])

  // Pre-fill from Word import (new template)
  useEffect(() => {
    if (isNew && state?.importedHtml) {
      setContent(state.importedHtml)
      if (state.importedName) setName(state.importedName)
      setIsDirty(true)
    }
  }, [isNew, state])

  const createMutation = useMutation({
    mutationFn: () => documentTemplatesApi.create({ name, description: description || undefined, entityType, htmlContent: content }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ['document-templates'] })
      toast.success('Template created')
      setIsDirty(false)
      navigate(`/document-library/${created.id}`, { replace: true })
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to create template'),
  })

  const updateMutation = useMutation({
    mutationFn: () => documentTemplatesApi.update(id!, { name, description: description || undefined, entityType, htmlContent: content, isActive }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['document-templates'] })
      toast.success('Template saved')
      setIsDirty(false)
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to save template'),
  })

  const isSaving = createMutation.isPending || updateMutation.isPending

  const handleSave = () => {
    if (!name.trim()) { toast.error('Template name is required'); return }
    if (!content || content === '<p></p>') { toast.error('Template content cannot be empty'); return }
    isNew ? createMutation.mutate() : updateMutation.mutate()
  }

  const handleImportDoc = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''
    try {
      const mammoth = await import('mammoth/mammoth.browser')
      const arrayBuffer = await file.arrayBuffer()
      const result = await mammoth.convertToHtml({ arrayBuffer })
      setContent(result.value)
      if (!name && file.name) setName(file.name.replace(/\.(doc|docx)$/i, ''))
      setIsDirty(true)
      toast.success('Word document imported')
    } catch {
      toast.error('Failed to import Word document')
    }
  }

  if (!isNew && isLoading) return <LoadingSpinner />

  return (
    <div className="flex flex-col h-full">
      {/* Header bar */}
      <div className="flex items-center gap-3 px-6 py-3 border-b border-slate-200 bg-white shrink-0">
        <button
          onClick={() => navigate('/document-library')}
          className="flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          Library
        </button>

        <div className="h-4 w-px bg-slate-200" />

        {/* Template name */}
        <input
          value={name}
          onChange={(e) => { setName(e.target.value); setIsDirty(true) }}
          placeholder="Template name…"
          className="flex-1 text-sm font-medium border-0 focus:outline-none focus:ring-0 bg-transparent placeholder:text-slate-400 min-w-0"
        />

        {/* Entity type */}
        <select
          value={entityType}
          onChange={(e) => { setEntityType(e.target.value as TemplateEntityType); setIsDirty(true) }}
          className="text-sm border border-slate-200 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
        >
          {ENTITY_TYPES.map((t) => (
            <option key={t} value={t}>{ENTITY_TYPE_LABELS[t]}</option>
          ))}
        </select>

        {/* Active toggle (edit only) */}
        {!isNew && (
          <button
            onClick={() => { setIsActive((v) => !v); setIsDirty(true) }}
            className={`flex items-center gap-1.5 text-sm transition-colors ${isActive ? 'text-green-600' : 'text-slate-400'}`}
            title={isActive ? 'Active' : 'Inactive'}
          >
            {isActive
              ? <ToggleRight className="h-5 w-5" />
              : <ToggleLeft className="h-5 w-5" />}
            <span className="hidden sm:inline">{isActive ? 'Active' : 'Inactive'}</span>
          </button>
        )}

        {/* Import from Word */}
        <label className="flex items-center gap-1.5 px-3 py-1.5 text-sm border border-slate-300 rounded-md hover:bg-slate-50 cursor-pointer transition-colors text-slate-600">
          <Upload className="h-3.5 w-3.5" />
          Import Word
          <input type="file" accept=".doc,.docx" className="hidden" onChange={handleImportDoc} />
        </label>

        {/* Save */}
        <button
          onClick={handleSave}
          disabled={isSaving || !isDirty}
          className="flex items-center gap-1.5 px-4 py-1.5 bg-blue-600 text-white text-sm font-medium rounded-md hover:bg-blue-700 disabled:opacity-40 transition-colors"
        >
          {isSaving
            ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
            : <Save className="h-3.5 w-3.5" />}
          {isSaving ? 'Saving…' : isDirty ? 'Save*' : 'Saved'}
        </button>
      </div>

      {/* Description bar */}
      <div className="px-6 py-2 border-b border-slate-100 bg-slate-50 shrink-0">
        <input
          value={description}
          onChange={(e) => { setDescription(e.target.value); setIsDirty(true) }}
          placeholder="Add a description (optional)…"
          className="w-full text-xs text-slate-500 border-0 bg-transparent focus:outline-none focus:ring-0 placeholder:text-slate-400"
        />
      </div>

      {/* Editor */}
      <div className="flex-1 overflow-auto p-6">
        <TemplateEditor
          content={content}
          onChange={(html) => { setContent(html); setIsDirty(true) }}
          entityType={entityType}
        />
      </div>
    </div>
  )
}
