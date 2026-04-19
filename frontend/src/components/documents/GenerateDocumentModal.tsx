import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { X, FileText, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import { documentGenerationApi } from '@/api/documentGeneration.api'

type Props = {
  entityType: string
  entityId: string
  onClose: () => void
}

export function GenerateDocumentModal({ entityType, entityId, onClose }: Props) {
  const [selectedTemplateId, setSelectedTemplateId] = useState('')

  const { data: templates = [], isLoading: loadingTemplates } = useQuery({
    queryKey: ['document-templates', entityType],
    queryFn: () => documentGenerationApi.getTemplates(entityType),
  })

  const generateMutation = useMutation({
    mutationFn: () =>
      documentGenerationApi.generate({ templateId: selectedTemplateId, entityType, entityId }),
    onSuccess: (data) => {
      window.open(data.url, '_blank', 'noopener,noreferrer')
      toast.success('Document generated')
      onClose()
    },
    onError: (e: any) =>
      toast.error(e?.response?.data?.errorMessage ?? 'Failed to generate document'),
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-md mx-4">
        <div className="flex items-center justify-between px-5 py-4 border-b">
          <div className="flex items-center gap-2">
            <FileText className="h-4 w-4 text-slate-500" />
            <h2 className="text-sm font-semibold text-slate-900">Generate Document</h2>
          </div>
          <button onClick={onClose} className="p-1 rounded hover:bg-slate-100">
            <X className="h-4 w-4 text-slate-500" />
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Template *</label>
            {loadingTemplates ? (
              <div className="flex items-center gap-2 text-sm text-slate-400 py-2">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Loading templates…
              </div>
            ) : templates.length === 0 ? (
              <p className="text-sm text-slate-400 py-2">No templates available for {entityType}.</p>
            ) : (
              <select
                value={selectedTemplateId}
                onChange={(e) => setSelectedTemplateId(e.target.value)}
                className="w-full border rounded px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">— Select a template —</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            )}
          </div>
        </div>

        <div className="flex justify-end gap-2 px-5 py-4 border-t bg-slate-50 rounded-b-lg">
          <button
            onClick={onClose}
            className="px-3 py-1.5 border rounded text-sm hover:bg-white"
          >
            Cancel
          </button>
          <button
            disabled={!selectedTemplateId || generateMutation.isPending}
            onClick={() => generateMutation.mutate()}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-blue-600 text-white rounded text-sm hover:bg-blue-700 disabled:opacity-50"
          >
            {generateMutation.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
            Generate
          </button>
        </div>
      </div>
    </div>
  )
}
