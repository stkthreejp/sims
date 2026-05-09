import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { Activity, AlertTriangle, BarChart3, CheckCircle2, Clock3, MapPin, RefreshCw, ShieldAlert, ShieldCheck, Truck, X, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import type { AutoSafetyDetail, AutoSafetyRiskLevel } from '@/types/quote.types'

type Props = {
  quoteId: string
}

type AutoSafetyTab = 'safer' | 'radius' | 'events' | 'history'
type DetailSelection = { kind: string; title: string; basic?: string } | null

const riskStyle: Record<AutoSafetyRiskLevel, string> = {
  Unknown: 'bg-slate-100 text-slate-600 border-slate-200',
  Acceptable: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  Watch: 'bg-amber-50 text-amber-700 border-amber-200',
  High: 'bg-red-50 text-red-700 border-red-200',
}

export function QuoteAutoSafetyPanel({ quoteId }: Props) {
  const qc = useQueryClient()
  const [activeTab, setActiveTab] = useState<AutoSafetyTab>('safer')
  const [detailSelection, setDetailSelection] = useState<DetailSelection>(null)
  const { data, error, isLoading, isError } = useQuery({
    queryKey: ['quote-auto-safety', quoteId],
    queryFn: () => quotesApi.getAutoSafety(quoteId),
    retry: false,
  })
  const refreshMutation = useMutation({
    mutationFn: () => quotesApi.refreshAutoSafety(quoteId),
    onSuccess: (result) => {
      qc.setQueryData(['quote-auto-safety', quoteId], result.summary)
      toast.success('FMCSA data refreshed', {
        description: `${result.inspectionRowsImported} inspections, ${result.violationRowsImported} violations, ${result.crashRowsImported} crashes`,
      })
    },
    onError: (err: any) => {
      const msg = err?.response?.data?.errorMessage ?? 'FMCSA refresh failed'
      toast.error(msg)
    },
  })
  const detailQuery = useQuery({
    queryKey: ['quote-auto-safety-details', quoteId, detailSelection?.kind, detailSelection?.basic],
    queryFn: () => quotesApi.getAutoSafetyDetails(quoteId, detailSelection!.kind, detailSelection?.basic),
    enabled: !!detailSelection,
  })

  if (isLoading) {
    return (
      <div className="px-5 py-4 bg-slate-50 border-y border-slate-200 text-sm text-slate-600">
        Opening auto safety profile...
      </div>
    )
  }

  if (isError || !data) {
    const message = getLoadErrorMessage(error)
    return (
      <div className="px-5 py-4 bg-red-50 border-y border-red-100 text-sm text-red-700">
        <div className="font-semibold">Auto safety profile could not be loaded.</div>
        <div className="mt-1 text-red-600">{message}</div>
      </div>
    )
  }

  if (data.status !== 'Ready') {
    return (
      <div className="px-5 py-4 bg-slate-50 border-t border-slate-200">
        <div className="flex items-start gap-3 rounded border border-slate-200 bg-white p-4">
          <AlertTriangle className="mt-0.5 h-4 w-4 text-amber-600" />
          <div className="flex-1">
            <div className="text-sm font-semibold text-slate-800">Auto Safety</div>
            <p className="mt-1 text-sm text-slate-600">{data.message ?? 'FMCSA data is not available yet.'}</p>
            {data.usDotNumber && <p className="mt-2 text-xs text-slate-500">USDOT {data.usDotNumber}</p>}
          </div>
          {data.status === 'NoData' && (
            <button
              onClick={() => refreshMutation.mutate()}
              disabled={refreshMutation.isPending}
              className="inline-flex items-center gap-2 rounded border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${refreshMutation.isPending ? 'animate-spin' : ''}`} />
              {refreshMutation.isPending ? 'Refreshing' : 'Refresh FMCSA'}
            </button>
          )}
        </div>
      </div>
    )
  }

  const oos = data.oos
  const accident = data.accidentSummary
  const overallOosCount = oos.overallOosCount ?? Math.max(oos.driverOosCount, oos.vehicleOosCount)
  const overallOosRate = oos.overallOosRate ?? (oos.inspectionCount === 0 ? null : Math.round(overallOosCount * 10000 / oos.inspectionCount) / 100)
  const driverInspectionCount = oos.driverInspectionCount ?? oos.inspectionCount
  const vehicleInspectionCount = oos.vehicleInspectionCount ?? oos.inspectionCount
  const hazmatInspectionCount = oos.hazmatInspectionCount ?? 0
  const hazmatOosCount = oos.hazmatOosCount ?? 0

  return (
    <div className="px-5 py-4 bg-slate-50 border-t border-slate-200 space-y-4">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          {data.overallRiskLevel === 'High' ? <ShieldAlert className="h-4 w-4 text-red-600" /> : <ShieldCheck className="h-4 w-4 text-slate-700" />}
          <h3 className="text-sm font-semibold text-slate-800">Auto Safety</h3>
          <span className={`inline-flex items-center rounded border px-2 py-0.5 text-xs font-semibold ${riskStyle[data.overallRiskLevel]}`}>
            {data.overallRiskLevel}
          </span>
        </div>
        <div className="text-xs text-slate-500">
          {data.snapshotMonth ? `Snapshot ${data.snapshotMonth}` : 'No scored snapshot'}{data.methodologyVersion ? ` - ${data.methodologyVersion}` : ''}
        </div>
        <button
          onClick={() => refreshMutation.mutate()}
          disabled={refreshMutation.isPending}
          className="inline-flex items-center gap-2 rounded border border-slate-300 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${refreshMutation.isPending ? 'animate-spin' : ''}`} />
          {refreshMutation.isPending ? 'Refreshing' : 'Refresh FMCSA'}
        </button>
      </div>

      <div className="grid grid-cols-4 gap-3">
        <Metric label="Carrier" value={data.carrierName ?? 'Unknown'} />
        <Metric label="USDOT" value={data.usDotNumber ?? 'None'} />
        <Metric label="Power Units" value={data.powerUnits?.toLocaleString() ?? '-'} />
        <Metric label="Drivers" value={data.driverCount?.toLocaleString() ?? '-'} />
      </div>

      {data.summaryFlags.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {data.summaryFlags.map((flag) => (
            <span key={flag} className="inline-flex items-center gap-1 rounded border border-amber-200 bg-amber-50 px-2 py-1 text-xs font-medium text-amber-800">
              <AlertTriangle className="h-3 w-3" /> {flag}
            </span>
          ))}
        </div>
      )}

      <div className="grid grid-cols-4 gap-2 text-xs font-semibold text-slate-600">
        <SectionPill label="SAFER / OOS" active={activeTab === 'safer'} onClick={() => setActiveTab('safer')} />
        <SectionPill label="Radius" active={activeTab === 'radius'} onClick={() => setActiveTab('radius')} />
        <SectionPill label="Events" active={activeTab === 'events'} onClick={() => setActiveTab('events')} />
        <SectionPill label="CSA / History" active={activeTab === 'history'} onClick={() => setActiveTab('history')} />
      </div>

      {activeTab === 'safer' && (
        <div className="grid grid-cols-3 gap-3">
          <div className="col-span-2 rounded border bg-white p-3">
            <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-slate-600">
              <Truck className="h-3.5 w-3.5" /> SAFER / OOS
            </div>
            <div className="grid grid-cols-4 gap-3">
              <OosMetric label="Overall" count={oos.inspectionCount} oosCount={overallOosCount} rate={overallOosRate} onClick={() => setDetailSelection({ kind: 'overall-oos', title: 'Overall OOS Events' })} />
              <OosMetric label="Driver" count={driverInspectionCount} oosCount={oos.driverOosCount} rate={oos.driverOosRate} onClick={() => setDetailSelection({ kind: 'driver-oos', title: 'Driver OOS Events' })} />
              <OosMetric label="Vehicle" count={vehicleInspectionCount} oosCount={oos.vehicleOosCount} rate={oos.vehicleOosRate} onClick={() => setDetailSelection({ kind: 'vehicle-oos', title: 'Vehicle OOS Events' })} />
              <OosMetric label="Hazmat" count={hazmatInspectionCount} oosCount={hazmatOosCount} rate={oos.hazmatOosRate ?? null} onClick={() => setDetailSelection({ kind: 'hazmat-oos', title: 'Hazmat OOS Events' })} />
            </div>
            <p className="mt-3 text-xs text-slate-400">National average comparisons are next backend fields.</p>
          </div>

          <div className="rounded border bg-white p-3">
            <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-slate-600">
              <Activity className="h-3.5 w-3.5" /> Accident Summary
            </div>
            <div className="grid grid-cols-2 gap-2">
              <Metric label="Fatal" value={(accident?.fatalCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'fatal-crash', title: 'Fatal Crashes' })} />
              <Metric label="Injury" value={(accident?.injuryCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'injury-crash', title: 'Injury Crashes' })} />
              <Metric label="Tow" value={(accident?.towCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'tow-crash', title: 'Tow-Only Crashes' })} />
              <Metric label="Reportable" value={(accident?.totalReportableCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'reportable-crash', title: 'Reportable Crashes' })} />
            </div>
            <p className="mt-3 text-xs text-slate-400">
              SAFER-style reportable crash events; ratio: {accident?.accidentToPowerUnitRatio == null ? '-' : `${accident.accidentToPowerUnitRatio}%`}
            </p>
          </div>
        </div>
      )}

      {activeTab === 'radius' && (
        <div className="rounded border bg-white p-3">
          <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-slate-600">
            <MapPin className="h-3.5 w-3.5" /> Radius Of Operations
          </div>
          {data.geographicHotspots.length === 0 ? (
            <p className="text-sm text-slate-400">No inspection location concentration yet.</p>
          ) : (
            <div className="grid grid-cols-5 gap-2">
              {data.geographicHotspots.map((h) => (
                <div key={h.state} className="rounded bg-slate-50 p-2">
                  <div className="text-sm font-semibold text-slate-800">{h.state}</div>
                  <div className="text-xs text-slate-500">{h.inspectionCount} insp - {h.violationCount} viol</div>
                </div>
              ))}
            </div>
          )}
          <div className="mt-3 grid grid-cols-4 gap-2 text-xs">
            {['<50 mi', '50-100 mi', '100-250 mi', '250+ mi'].map((band) => (
              <div key={band} className="rounded bg-slate-50 px-2 py-1 text-center text-slate-400">{band}: -</div>
            ))}
          </div>
        </div>
      )}

      {activeTab === 'events' && (
        <div className="rounded border bg-white">
          <div className="border-b px-4 py-2 text-xs font-semibold uppercase text-slate-600">Events</div>
          {data.recentSevereEvents.length === 0 ? (
            <p className="px-4 py-3 text-sm text-slate-400">No recent high-severity or OOS events in the imported window.</p>
          ) : (
            <div className="divide-y">
              {data.recentSevereEvents.map((event, idx) => (
                <div key={`${event.date}-${idx}`} className="grid grid-cols-[120px_160px_1fr_80px] gap-3 px-4 py-2 text-sm">
                  <span className="text-slate-500">{new Date(event.date).toLocaleDateString()}</span>
                  <span className="font-medium text-slate-700">{event.eventType}</span>
                  <span className="truncate text-slate-600">{event.description}</span>
                  <span className="text-right text-slate-500">{event.state ?? '-'}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {activeTab === 'history' && (
        <div className="grid grid-cols-3 gap-3">
          <div className="col-span-2 rounded border bg-white">
            <div className="flex items-center gap-2 border-b px-4 py-2 text-xs font-semibold uppercase text-slate-600">
              <BarChart3 className="h-3.5 w-3.5" /> CSA / BASICs
            </div>
            <div className="grid grid-cols-7 divide-x">
              {data.basics.map((b) => (
                <button
                  key={b.basic}
                  type="button"
                  onClick={() => setDetailSelection({ kind: 'basic', title: `${b.basic} Details`, basic: b.basic })}
                  className="min-w-0 p-3 text-left hover:bg-slate-50"
                >
                  <div className="truncate text-xs font-semibold text-slate-700" title={b.basic}>{b.basic}</div>
                  <div className="mt-2 flex items-center gap-1 text-xs">
                    {b.isPrioritized ? <XCircle className="h-3.5 w-3.5 text-red-600" /> : <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />}
                    <span className={b.isPrioritized ? 'text-red-700' : 'text-slate-500'}>
                      {b.percentile == null ? `${b.eventCount} events` : `${b.percentile}%`}
                    </span>
                  </div>
                  {b.outOfServiceCount > 0 && <div className="mt-1 text-xs text-amber-700">{b.outOfServiceCount} OOS</div>}
                </button>
              ))}
            </div>
          </div>

          <div className="rounded border bg-white p-3">
            <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-slate-600">
              <Clock3 className="h-3.5 w-3.5" /> History Snapshot
            </div>
            <Metric label="Data Refreshed" value={data.dataRefreshedAt ? new Date(data.dataRefreshedAt).toLocaleDateString() : '-'} compact />
            <div className="mt-3 space-y-1 text-xs text-slate-400">
              <div>Inspection trend: next</div>
              <div>BASIC history: next</div>
              <div>MCS-150 changes: next</div>
            </div>
          </div>
        </div>
      )}
      {detailSelection && (
        <AutoSafetyDetailDrawer
          title={detailSelection.title}
          items={detailQuery.data ?? []}
          isLoading={detailQuery.isLoading}
          onClose={() => setDetailSelection(null)}
        />
      )}
    </div>
  )
}

function SectionPill({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded border px-3 py-2 text-center transition ${active ? 'border-blue-300 bg-blue-50 text-blue-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`}
    >
      {label}
    </button>
  )
}

function OosMetric({ label, count, oosCount, rate, onClick }: { label: string; count: number; oosCount: number; rate: number | null; onClick?: () => void }) {
  return (
    <button type="button" onClick={onClick} className="rounded bg-slate-50 p-2 text-left hover:bg-slate-100">
      <div className="text-[11px] font-semibold uppercase text-slate-500">{label}</div>
      <div className="mt-1 text-sm font-semibold text-slate-800">{rate == null ? '-' : `${rate}%`}</div>
      <div className="mt-1 text-xs text-slate-500">{oosCount.toLocaleString()} OOS / {count.toLocaleString()} insp</div>
    </button>
  )
}

function AutoSafetyDetailDrawer({ title, items, isLoading, onClose }: { title: string; items: AutoSafetyDetail[]; isLoading: boolean; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-end bg-slate-900/20 p-4">
      <div className="mt-16 max-h-[72vh] w-full max-w-2xl overflow-y-auto rounded border border-slate-200 bg-white shadow-xl">
        <div className="sticky top-0 flex items-center justify-between border-b bg-white px-4 py-3">
          <div>
            <h3 className="text-sm font-semibold text-slate-900">{title}</h3>
            <p className="mt-1 text-xs text-slate-500">{isLoading ? 'Loading details...' : `${items.length} FMCSA event${items.length === 1 ? '' : 's'}`}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded p-1 text-slate-500 hover:bg-slate-100">
            <X className="h-4 w-4" />
          </button>
        </div>

        {isLoading ? (
          <div className="p-5 text-sm text-slate-500">Loading event details...</div>
        ) : items.length === 0 ? (
          <div className="p-5 text-sm text-slate-500">No matching FMCSA events found.</div>
        ) : (
          <div className="divide-y">
            {items.map((item, idx) => (
              <div key={`${item.reportNumber}-${idx}`} className="px-4 py-3 text-sm">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold text-slate-800">{item.category}</div>
                    <div className="mt-0.5 text-xs text-slate-500">{formatDate(item.date)} · Report {item.reportNumber} · {item.source}</div>
                  </div>
                  <div className="text-xs font-medium text-slate-500">{[item.city, item.state].filter(Boolean).join(', ') || '-'}</div>
                </div>
                <div className="mt-2 text-slate-600">{item.description}</div>
                <div className="mt-3 grid grid-cols-2 gap-3">
                  {(item.location || item.agency || item.countyCode) && (
                    <DetailSection
                      title="Location"
                      items={[
                        ['Place', item.location],
                        ['Agency', item.agency],
                        ['County', item.countyCode ? `Code ${item.countyCode}` : null],
                      ]}
                    />
                  )}
                  {item.conditions && <DetailSection title="Crash Conditions" items={splitDetailItems(item.conditions)} />}
                  {item.vehicleInfo && <DetailSection title="Vehicle Information" items={splitDetailItems(item.vehicleInfo)} />}
                  {item.crashEvents && <DetailSection title="Crash Events" items={splitDetailItems(item.crashEvents)} />}
                </div>
                {item.basic && <div className="mt-2 text-xs text-slate-400">{item.basic}</div>}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function DetailSection({ title, items }: { title: string; items: Array<[string, string | null | undefined]> }) {
  const visible = items.filter(([, value]) => !!value)
  if (visible.length === 0) return null
  return (
    <div className="rounded border border-slate-100 bg-slate-50/70 px-3 py-2">
      <div className="mb-1.5 text-[11px] font-semibold uppercase text-slate-500">{title}</div>
      <div className="space-y-1">
        {visible.map(([label, value]) => (
          <div key={label} className="grid grid-cols-[96px_1fr] gap-2 text-xs">
            <span className="text-slate-400">{label}</span>
            <span className="font-medium text-slate-700">{value}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

function splitDetailItems(value: string): Array<[string, string]> {
  return value.split('|').map((part, idx) => {
    const trimmed = part.trim()
    const separator = trimmed.indexOf(':')
    if (separator === -1) return [`Item ${idx + 1}`, trimmed]
    return [trimmed.slice(0, separator).trim(), trimmed.slice(separator + 1).trim()]
  })
}

function formatDate(value: string) {
  const [year, month, day] = value.split('-')
  if (!year || !month || !day) return value
  return `${Number(month)}/${Number(day)}/${year}`
}

function getLoadErrorMessage(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Please try again.'
  if (!error.response) return 'The API is not reachable. Check that the backend app is running and the frontend API URL is configured.'

  const data = error.response.data as {
    errorMessage?: string
    ErrorMessage?: string
    detail?: string
    title?: string
  } | undefined

  return data?.errorMessage
    ?? data?.ErrorMessage
    ?? data?.detail
    ?? data?.title
    ?? `Request failed with status ${error.response.status}.`
}

function Metric({ label, value, compact = false, onClick }: { label: string; value: string; compact?: boolean; onClick?: () => void }) {
  const content = (
    <>
      <div className="text-[11px] font-semibold uppercase text-slate-500">{label}</div>
      <div className="mt-1 truncate text-sm font-semibold text-slate-800" title={value}>{value}</div>
    </>
  )
  if (onClick) {
    return (
      <button type="button" onClick={onClick} className={`${compact ? '' : 'rounded border bg-white p-3'} text-left hover:bg-slate-50`}>
        {content}
      </button>
    )
  }
  return (
    <div className={compact ? '' : 'rounded border bg-white p-3'}>
      {content}
    </div>
  )
}
