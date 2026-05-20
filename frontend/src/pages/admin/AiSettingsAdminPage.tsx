import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { Bot, Save, ShieldCheck } from 'lucide-react'
import { toast } from 'sonner'
import { aiSettingsApi } from '@/api/aiSettings.api'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { PageHeader } from '@/components/common/PageHeader'
import type { AiUseCaseModelSetting } from '@/types/aiSettings.types'

const USE_CASE_LABELS: Record<string, string> = {
  DocumentExtraction: 'Document extraction',
  RiskScoring: 'Risk scoring',
  ReferralJudgment: 'Referral judgment',
  NarrativeDrafting: 'Narrative drafting',
  BatchTriage: 'Batch triage',
}

const inputCls = 'w-full rounded border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-blue-400'

type Draft = {
  modelId: string
  promptVersion: string
  changeReason: string
}

export function AiSettingsAdminPage() {
  const qc = useQueryClient()
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})

  const { data: models = [], isLoading: loadingModels } = useQuery({
    queryKey: ['admin', 'ai-settings', 'models'],
    queryFn: aiSettingsApi.getModels,
  })

  const { data: settings = [], isLoading: loadingSettings } = useQuery({
    queryKey: ['admin', 'ai-settings', 'settings'],
    queryFn: aiSettingsApi.getSettings,
  })

  const { data: auditLog = [] } = useQuery({
    queryKey: ['admin', 'ai-settings', 'audit-log'],
    queryFn: aiSettingsApi.getAuditLog,
  })

  const settingsByUseCase = useMemo(
    () => new Map(settings.map((setting) => [setting.useCase, setting])),
    [settings],
  )

  const updateSetting = useMutation({
    mutationFn: ({ useCase, draft }: { useCase: string; draft: Draft }) =>
      aiSettingsApi.updateSetting(useCase, {
        aiModelRegistryId: draft.modelId,
        promptVersion: draft.promptVersion,
        changeReason: draft.changeReason,
      }),
    onSuccess: (_saved, vars) => {
      toast.success('AI model setting saved')
      setDrafts((current) => {
        const next = { ...current }
        delete next[vars.useCase]
        return next
      })
      qc.invalidateQueries({ queryKey: ['admin', 'ai-settings'] })
    },
    onError: (err) => {
      const message = axios.isAxiosError(err)
        ? err.response?.data?.errorMessage ?? 'AI model setting could not be saved'
        : 'AI model setting could not be saved'
      toast.error(message)
    },
  })

  if (loadingModels || loadingSettings) return <LoadingSpinner />

  const orderedSettings = [...settings].sort((a, b) => sortUseCase(a.useCase) - sortUseCase(b.useCase))

  function draftFor(setting: AiUseCaseModelSetting): Draft {
    return drafts[setting.useCase] ?? {
      modelId: setting.model.id,
      promptVersion: setting.promptVersion,
      changeReason: '',
    }
  }

  function setDraft(useCase: string, patch: Partial<Draft>) {
    const current = settingsByUseCase.get(useCase)
    if (!current) return

    setDrafts((prev) => ({
      ...prev,
      [useCase]: {
        ...(prev[useCase] ?? {
          modelId: current.model.id,
          promptVersion: current.promptVersion,
          changeReason: '',
        }),
        ...patch,
      },
    }))
  }

  return (
    <div className="p-6 space-y-5">
      <PageHeader
        title="AI Settings"
        subtitle="Approved model choices for underwriting AI workflows"
      />

      <section className="rounded-lg border bg-white">
        <div className="flex items-center justify-between gap-4 border-b px-5 py-4">
          <div className="flex items-center gap-2">
            <Bot className="h-4 w-4 text-slate-500" />
            <h2 className="text-sm font-semibold text-slate-800">Use Case Model Selection</h2>
          </div>
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700">
            <ShieldCheck className="h-3.5 w-3.5" />
            Admin controlled
          </span>
        </div>

        <div className="divide-y">
          {orderedSettings.map((setting) => {
            const draft = draftFor(setting)
            const allowedModels = models.filter((model) =>
              model.active && model.allowedUseCases.includes(setting.useCase)
            )
            const changed = draft.modelId !== setting.model.id || draft.promptVersion !== setting.promptVersion
            const canSave = changed && draft.changeReason.trim().length > 0 && !updateSetting.isPending

            return (
              <div key={setting.useCase} className="grid gap-4 px-5 py-4 lg:grid-cols-[220px_1fr_220px_1fr_auto]">
                <div>
                  <div className="text-sm font-semibold text-slate-900">{USE_CASE_LABELS[setting.useCase] ?? setting.useCase}</div>
                  <div className="mt-1 text-xs text-slate-500">Current: {setting.model.provider}</div>
                </div>

                <label>
                  <span className="text-xs font-medium text-slate-600">Approved model</span>
                  <select
                    value={draft.modelId}
                    onChange={(e) => setDraft(setting.useCase, { modelId: e.target.value })}
                    className={inputCls}
                    disabled={setting.useCase === 'DocumentExtraction'}
                  >
                    {allowedModels.map((model) => (
                      <option key={model.id} value={model.id}>
                        {model.displayName} ({model.provider})
                      </option>
                    ))}
                  </select>
                  <div className="mt-1 text-xs text-slate-400">{setting.model.modelId}</div>
                </label>

                <label>
                  <span className="text-xs font-medium text-slate-600">Prompt version</span>
                  <input
                    value={draft.promptVersion}
                    onChange={(e) => setDraft(setting.useCase, { promptVersion: e.target.value })}
                    className={inputCls}
                    disabled={setting.useCase === 'DocumentExtraction'}
                  />
                </label>

                <label>
                  <span className="text-xs font-medium text-slate-600">Change reason</span>
                  <input
                    value={draft.changeReason}
                    onChange={(e) => setDraft(setting.useCase, { changeReason: e.target.value })}
                    className={inputCls}
                    placeholder={changed ? 'Required before saving' : 'No pending change'}
                    disabled={!changed}
                  />
                  <div className="mt-1 text-xs text-slate-400">
                    {setting.model.costNotes ?? 'No cost note recorded'}
                  </div>
                </label>

                <div className="flex items-start justify-end pt-5">
                  <button
                    type="button"
                    onClick={() => updateSetting.mutate({ useCase: setting.useCase, draft })}
                    disabled={!canSave}
                    className="inline-flex items-center gap-2 rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                  >
                    <Save className="h-4 w-4" />
                    Save
                  </button>
                </div>
              </div>
            )
          })}
        </div>
      </section>

      <section className="rounded-lg border bg-white">
        <div className="border-b px-5 py-4">
          <h2 className="text-sm font-semibold text-slate-800">Recent Changes</h2>
        </div>
        {auditLog.length === 0 ? (
          <div className="px-5 py-6 text-sm text-slate-500">No AI model setting changes have been recorded yet.</div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <th className="px-4 py-3">Use Case</th>
                <th className="px-4 py-3">Prompt</th>
                <th className="px-4 py-3">Reason</th>
                <th className="px-4 py-3">Changed</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {auditLog.slice(0, 10).map((row) => (
                <tr key={row.id}>
                  <td className="px-4 py-3 font-medium text-slate-800">{USE_CASE_LABELS[row.useCase] ?? row.useCase}</td>
                  <td className="px-4 py-3 text-slate-600">{row.newPromptVersion}</td>
                  <td className="px-4 py-3 text-slate-600">{row.changeReason}</td>
                  <td className="px-4 py-3 text-slate-500">{new Date(row.changedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}

function sortUseCase(useCase: string) {
  return ['DocumentExtraction', 'RiskScoring', 'ReferralJudgment', 'NarrativeDrafting', 'BatchTriage'].indexOf(useCase)
}
