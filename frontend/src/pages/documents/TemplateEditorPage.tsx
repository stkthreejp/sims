import { useEffect, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Loader2, Save, ToggleLeft, ToggleRight, Upload } from 'lucide-react'
import { toast } from 'sonner'
import { documentTemplatesApi } from '@/api/documentTemplates.api'
import { policyFormsApi } from '@/api/policyForms.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { TemplateEditor } from '@/components/editor/TemplateEditor'
import { ENTITY_TYPE_LABELS, type TemplateEntityType } from '@/lib/templateTags'
import { importWordDocument } from '@/lib/wordImport'
import type { DocumentTemplateKind } from '@/types/documentTemplate.types'

const ENTITY_TYPES: TemplateEntityType[] = ['General', 'Quote', 'Policy', 'Submission', 'Carrier', 'Agent']
const TEMPLATE_KINDS: { value: DocumentTemplateKind; label: string }[] = [
  { value: 'Document', label: 'Document' },
  { value: 'Email', label: 'Email' },
  { value: 'DocumentAndEmail', label: 'Document + Email' },
]

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
  const [entityType, setEntityType] = useState<TemplateEntityType>('Policy')
  const [kind, setKind] = useState<DocumentTemplateKind>('Document')
  const [isActive, setIsActive] = useState(true)
  const [documentContent, setDocumentContent] = useState('<p></p>')
  const [subjectTemplate, setSubjectTemplate] = useState('')
  const [emailBodyHtml, setEmailBodyHtml] = useState('<p></p>')
  const [activeBody, setActiveBody] = useState<'document' | 'email'>('document')
  const [isDirty, setIsDirty] = useState(false)

  const { data: template, isLoading } = useQuery({
    queryKey: ['document-templates', id],
    queryFn: () => documentTemplatesApi.getById(id!),
    enabled: !isNew,
  })

  const { data: approvedTags = [] } = useQuery({
    queryKey: ['policy-form-tags'],
    queryFn: policyFormsApi.getTags,
  })

  useEffect(() => {
    if (template) {
      setName(template.name)
      setDescription(template.description ?? '')
      setEntityType(template.entityType)
      setKind(template.kind)
      setIsActive(template.isActive)
      setDocumentContent(template.htmlContent || '<p></p>')
      setSubjectTemplate(template.subjectTemplate ?? '')
      setEmailBodyHtml(template.emailBodyHtml || '<p></p>')
      setActiveBody(template.kind === 'Email' ? 'email' : 'document')
    }
  }, [template])

  useEffect(() => {
    if (isNew && state?.importedHtml) {
      setDocumentContent(state.importedHtml)
      if (state.importedName) setName(state.importedName)
      setIsDirty(true)
    }
  }, [isNew, state])

  const createMutation = useMutation({
    mutationFn: () =>
      documentTemplatesApi.create({
        name,
        description: description || undefined,
        entityType,
        kind,
        htmlContent: kind === 'Email' ? '' : documentContent,
        subjectTemplate: kind === 'Document' ? undefined : subjectTemplate,
        emailBodyHtml: kind === 'Document' ? undefined : emailBodyHtml,
      }),
    onSuccess: (created) => {
      qc.invalidateQueries({ queryKey: ['document-templates'] })
      toast.success('Template created')
      setIsDirty(false)
      navigate(`/document-library/${created.id}`, { replace: true })
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to create template'),
  })

  const updateMutation = useMutation({
    mutationFn: () =>
      documentTemplatesApi.update(id!, {
        name,
        description: description || undefined,
        entityType,
        kind,
        htmlContent: kind === 'Email' ? '' : documentContent,
        subjectTemplate: kind === 'Document' ? undefined : subjectTemplate,
        emailBodyHtml: kind === 'Document' ? undefined : emailBodyHtml,
        isActive,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['document-templates'] })
      toast.success('Template saved')
      setIsDirty(false)
    },
    onError: (err: any) => toast.error(err?.response?.data?.errorMessage ?? 'Failed to save template'),
  })

  const isSaving = createMutation.isPending || updateMutation.isPending

  const handleSave = () => {
    if (!name.trim()) {
      toast.error('Template name is required')
      return
    }
    if (kind !== 'Email' && (!documentContent || documentContent === '<p></p>')) {
      toast.error('Document content cannot be empty')
      return
    }
    if (kind !== 'Document' && !subjectTemplate.trim()) {
      toast.error('Email subject is required')
      return
    }
    if (kind !== 'Document' && (!emailBodyHtml || emailBodyHtml === '<p></p>')) {
      toast.error('Email body cannot be empty')
      return
    }
    isNew ? createMutation.mutate() : updateMutation.mutate()
  }

  const handleKindChange = (nextKind: DocumentTemplateKind) => {
    setKind(nextKind)
    setActiveBody(nextKind === 'Email' ? 'email' : 'document')
    setIsDirty(true)
  }

  const handleImportDoc = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return
    event.target.value = ''
    try {
      const importedHtml = await importWordDocument(file)
      if (activeBody === 'email') setEmailBodyHtml(importedHtml)
      else setDocumentContent(importedHtml)
      if (!name && file.name) setName(file.name.replace(/\.(doc|docx)$/i, ''))
      setIsDirty(true)
      toast.success('Word document imported')
    } catch {
      toast.error('Failed to import Word document')
    }
  }

  if (!isNew && isLoading) return <LoadingSpinner />

  const editorContent = activeBody === 'email' ? emailBodyHtml : documentContent

  return (
    <div className="flex h-full flex-col" style={{ background: 'var(--bg)' }}>
      <div
        className="flex shrink-0 flex-wrap items-center gap-3 px-7 py-4"
        style={{ borderBottom: '1px solid var(--line)', background: 'var(--surface)' }}
      >
        <button type="button" onClick={() => navigate('/document-library')} className="sd-btn outline sm">
          <ArrowLeft className="h-4 w-4" />
          Library
        </button>

        <div className="h-6 w-px" style={{ background: 'var(--line)' }} />

        <input
          value={name}
          onChange={(event) => {
            setName(event.target.value)
            setIsDirty(true)
          }}
          placeholder="Template name..."
          className="min-w-[220px] flex-1 border-0 bg-transparent px-0 py-1 focus:outline-none"
          style={{ color: 'var(--ink)', fontSize: 'var(--fs-xl)', fontWeight: 600 }}
        />

        <select
          value={entityType}
          onChange={(event) => {
            setEntityType(event.target.value as TemplateEntityType)
            setIsDirty(true)
          }}
          className="sims-select w-auto"
        >
          {ENTITY_TYPES.map((type) => (
            <option key={type} value={type}>
              {ENTITY_TYPE_LABELS[type]}
            </option>
          ))}
        </select>

        <select value={kind} onChange={(event) => handleKindChange(event.target.value as DocumentTemplateKind)} className="sims-select w-auto">
          {TEMPLATE_KINDS.map((templateKind) => (
            <option key={templateKind.value} value={templateKind.value}>
              {templateKind.label}
            </option>
          ))}
        </select>

        {!isNew && (
          <button
            type="button"
            onClick={() => {
              setIsActive((value) => !value)
              setIsDirty(true)
            }}
            className="sd-btn outline sm"
            title={isActive ? 'Active' : 'Inactive'}
          >
            {isActive ? <ToggleRight className="h-4 w-4" /> : <ToggleLeft className="h-4 w-4" />}
            {isActive ? 'Active' : 'Inactive'}
          </button>
        )}

        <label className="sd-btn outline sm cursor-pointer">
          <Upload className="h-4 w-4" />
          Import Word
          <input type="file" accept=".doc,.docx" className="hidden" onChange={handleImportDoc} />
        </label>

        <button type="button" onClick={handleSave} disabled={isSaving || !isDirty} className="sd-btn primary sm">
          {isSaving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {isSaving ? 'Saving...' : isDirty ? 'Save' : 'Saved'}
        </button>
      </div>

      <div className="shrink-0 px-7 py-3" style={{ borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
        <input
          value={description}
          onChange={(event) => {
            setDescription(event.target.value)
            setIsDirty(true)
          }}
          placeholder="Add a description (optional)..."
          className="w-full border-0 bg-transparent px-0 py-0 focus:outline-none"
          style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}
        />
      </div>

      {kind !== 'Document' && (
        <div className="shrink-0 px-7 py-3" style={{ borderBottom: '1px solid var(--line-2)', background: 'var(--surface)' }}>
          <label className="sims-field-label">Email subject</label>
          <input
            value={subjectTemplate}
            onChange={(event) => {
              setSubjectTemplate(event.target.value)
              setIsDirty(true)
            }}
            placeholder="Email subject..."
            className="mt-1 w-full border-0 bg-transparent px-0 py-0 focus:outline-none"
            style={{ color: 'var(--ink)', fontSize: 'var(--fs-body)' }}
          />
        </div>
      )}

      {kind === 'DocumentAndEmail' && (
        <div className="shrink-0 px-7 py-3" style={{ borderBottom: '1px solid var(--line-2)', background: 'var(--surface)' }}>
          <div className="exp-lob-switch">
            <button type="button" onClick={() => setActiveBody('document')} className={`exp-lob ${activeBody === 'document' ? 'active' : ''}`}>
              Document Body
            </button>
            <button type="button" onClick={() => setActiveBody('email')} className={`exp-lob ${activeBody === 'email' ? 'active' : ''}`}>
              Email Body
            </button>
          </div>
        </div>
      )}

      <div className="flex-1 overflow-auto p-7">
        <TemplateEditor
          content={editorContent}
          onChange={(html) => {
            activeBody === 'email' ? setEmailBodyHtml(html) : setDocumentContent(html)
            setIsDirty(true)
          }}
          entityType={entityType}
          approvedTags={approvedTags}
        />
      </div>
    </div>
  )
}
