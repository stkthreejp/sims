import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { Activity, AlertTriangle, BarChart3, CheckCircle2, Clock3, MapPin, RefreshCw, ShieldAlert, ShieldCheck, Truck, X, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import type { AutoSafetyBasic, AutoSafetyDetail, AutoSafetyIss, AutoSafetyRiskLevel, AutoSafetyTrendBucket } from '@/types/quote.types'

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

      <div className="grid grid-cols-5 gap-3">
        <Metric label="Carrier" value={data.carrierName ?? 'Unknown'} />
        <Metric label="USDOT" value={data.usDotNumber ?? 'None'} />
        <Metric label="Power Units" value={data.powerUnits?.toLocaleString() ?? '-'} />
        <Metric label="Drivers" value={data.driverCount?.toLocaleString() ?? '-'} />
        <IssStoplight iss={data.iss} />
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
        <div className="space-y-3">
          <div className="rounded border bg-white">
            <div className="flex items-center gap-2 border-b px-4 py-2 text-xs font-semibold uppercase text-slate-600">
              <BarChart3 className="h-3.5 w-3.5" /> CSA / BASICs
            </div>
            <div className="grid grid-cols-7 divide-x">
              {data.basics.map((b) => (
                <BasicKpiTile
                  key={b.basic}
                  basic={b}
                  onClick={() => setDetailSelection({ kind: 'basic', title: `${b.basic} Details`, basic: b.basic })}
                />
              ))}
            </div>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2 grid grid-cols-2 gap-3">
              <TrendChart
                title="Inspection History"
                totalLabel="Total Inspections"
                oosLabel="OOS Inspections"
                buckets={data.inspectionTrend ?? []}
                onBucketClick={(bucket) => setDetailSelection({ kind: 'inspection-trend', title: `Inspections ${bucket.label} Months Ago`, basic: bucket.label })}
              />
              <TrendChart
                title="Violation History"
                totalLabel="Total Violations"
                oosLabel="OOS Violations"
                buckets={data.violationTrend ?? []}
                onBucketClick={(bucket) => setDetailSelection({ kind: 'violation-trend', title: `Violations ${bucket.label} Months Ago`, basic: bucket.label })}
              />
            </div>

            <div className="rounded border bg-white p-3">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase text-slate-600">
                <Clock3 className="h-3.5 w-3.5" /> History Snapshot
              </div>
              <Metric label="Data Refreshed" value={data.dataRefreshedAt ? new Date(data.dataRefreshedAt).toLocaleDateString() : '-'} compact />
              <div className="mt-3 space-y-1 text-xs text-slate-400">
                <div>BASIC history: next</div>
                <div>MCS-150 changes: next</div>
                <div>Snapshot report: next</div>
              </div>
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

function IssStoplight({ iss }: { iss?: AutoSafetyIss }) {
  const status = iss?.status ?? 'Unknown'
  const lights = [
    { key: 'Red', className: status === 'Red' ? 'bg-red-500' : 'bg-slate-200' },
    { key: 'Yellow', className: status === 'Yellow' ? 'bg-amber-400' : 'bg-slate-200' },
    { key: 'Green', className: status === 'Green' ? 'bg-emerald-500' : 'bg-slate-200' },
  ]
  return (
    <div className="rounded border bg-white p-3">
      <div className="flex items-center justify-between gap-2">
        <div>
          <div className="text-[11px] font-semibold uppercase text-slate-500">ISS</div>
          <div className="mt-1 truncate text-sm font-semibold text-slate-800" title={iss?.label ?? 'Pending'}>
            {iss?.score == null ? 'Pending' : iss.score}
          </div>
        </div>
        <div className="flex h-12 w-7 flex-col items-center justify-center gap-1 rounded border border-slate-300 bg-slate-50">
          {lights.map((light) => <span key={light.key} className={`h-2.5 w-2.5 rounded-full ${light.className}`} />)}
        </div>
      </div>
      <div className="mt-1 truncate text-[11px] text-slate-400" title={iss?.label ?? iss?.source ?? 'Pending ISS source'}>
        {iss?.label ?? iss?.source ?? 'Pending ISS source'}
      </div>
    </div>
  )
}

function BasicKpiTile({ basic, onClick }: { basic: AutoSafetyBasic; onClick: () => void }) {
  const risk = getBasicRisk(basic)
  const dialValue = risk.value
  const color = risk.color === 'red' ? '#dc2626' : risk.color === 'yellow' ? '#d97706' : risk.color === 'green' ? '#059669' : '#94a3b8'
  const bgColor = risk.color === 'red' ? 'bg-red-50' : risk.color === 'yellow' ? 'bg-amber-50' : risk.color === 'green' ? 'bg-emerald-50' : 'bg-slate-50'
  const textColor = risk.color === 'red' ? 'text-red-700' : risk.color === 'yellow' ? 'text-amber-700' : risk.color === 'green' ? 'text-emerald-700' : 'text-slate-500'
  const dash = `${Math.max(0, Math.min(100, dialValue))}, 100`

  return (
    <button type="button" onClick={onClick} className="min-w-0 p-3 text-left hover:bg-slate-50">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate text-xs font-semibold text-slate-700" title={basic.basic}>{basic.basic}</div>
          <div className="mt-1 text-[10px] font-semibold uppercase text-slate-400">{basic.scoreSource}</div>
        </div>
        <div className={`relative flex h-11 w-11 shrink-0 items-center justify-center rounded-full ${bgColor}`}>
          <svg viewBox="0 0 36 36" className="h-9 w-9 -rotate-90">
            <circle cx="18" cy="18" r="15.5" fill="none" stroke="#e2e8f0" strokeWidth="4" />
            <circle cx="18" cy="18" r="15.5" fill="none" stroke={color} strokeWidth="4" strokeDasharray={dash} pathLength="100" strokeLinecap="round" />
          </svg>
          <span className={`absolute text-[10px] font-bold ${textColor}`}>{risk.label}</span>
        </div>
      </div>
      <div className="mt-2 flex items-center gap-1 text-xs">
        {risk.color === 'red' ? <XCircle className="h-3.5 w-3.5 text-red-600" /> : <CheckCircle2 className={`h-3.5 w-3.5 ${risk.color === 'yellow' ? 'text-amber-600' : 'text-emerald-600'}`} />}
        <span className={textColor}>{risk.status}</span>
      </div>
      <div className="mt-1 text-xs text-slate-500">
        {basic.measure == null ? 'Measure -' : `Measure ${basic.measure}`}
        {basic.percentile == null ? '' : ` | ${basic.percentile}%`}
      </div>
      <div className="mt-1 text-xs text-slate-500">{basic.eventCount} events{basic.outOfServiceCount > 0 ? ` | ${basic.outOfServiceCount} OOS` : ''}</div>
    </button>
  )
}

function getBasicRisk(basic: AutoSafetyBasic): { color: 'green' | 'yellow' | 'red' | 'gray'; value: number; label: string; status: string } {
  if (basic.isPrioritized) return { color: 'red', value: 100, label: '!', status: 'Alert' }
  if (basic.percentile != null) {
    if (basic.percentile >= 75) return { color: 'red', value: basic.percentile, label: `${Math.round(basic.percentile)}`, status: 'High' }
    if (basic.percentile >= 50) return { color: 'yellow', value: basic.percentile, label: `${Math.round(basic.percentile)}`, status: 'Watch' }
    return { color: 'green', value: basic.percentile, label: `${Math.round(basic.percentile)}`, status: 'Clear' }
  }
  if (basic.scoreSource === 'Official SMS' && basic.measure != null) {
    return { color: 'gray', value: 0, label: 'N/A', status: 'Inconclusive' }
  }

  const eventPressure = Math.min(60, basic.eventCount * 1.5)
  const oosPressure = Math.min(40, basic.outOfServiceCount * 6)
  const value = Math.round(eventPressure + oosPressure)
  if (value >= 70) return { color: 'yellow', value, label: `${value}`, status: 'Watch' }
  return { color: 'green', value, label: `${value}`, status: basic.scoreSource === 'Official SMS' ? 'No alert' : 'Signal' }
}

function TrendChart({
  title,
  totalLabel,
  oosLabel,
  buckets,
  onBucketClick,
}: {
  title: string
  totalLabel: string
  oosLabel: string
  buckets: AutoSafetyTrendBucket[]
  onBucketClick: (bucket: AutoSafetyTrendBucket) => void
}) {
  const max = Math.max(1, ...buckets.map((b) => b.totalCount))

  return (
    <div className="rounded border bg-white p-3">
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="text-xs font-semibold uppercase text-slate-600">{title}</div>
        <div className="flex items-center gap-3 text-[11px] text-slate-500">
          <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-sm bg-red-500" />{oosLabel}</span>
          <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-sm bg-emerald-500" />{totalLabel}</span>
        </div>
      </div>
      <div className="grid h-36 grid-cols-6 items-end gap-2 border-b border-slate-200 px-1 pb-2">
        {buckets.map((bucket) => {
          const totalHeight = Math.max(6, Math.round((bucket.totalCount / max) * 112))
          const oosHeight = bucket.outOfServiceCount === 0 ? 0 : Math.max(4, Math.round((bucket.outOfServiceCount / max) * 112))
          return (
            <button
              type="button"
              key={bucket.label}
              onClick={() => onBucketClick(bucket)}
              className="group flex h-32 flex-col justify-end rounded px-1 hover:bg-slate-50"
              title={`${bucket.label} months ago: ${bucket.outOfServiceCount} OOS / ${bucket.totalCount} total`}
            >
              <div className="relative mx-auto w-full max-w-9 rounded-t bg-emerald-500/80 transition group-hover:bg-emerald-600" style={{ height: `${totalHeight}px` }}>
                {oosHeight > 0 && <div className="absolute bottom-0 left-0 right-0 rounded-t bg-red-500" style={{ height: `${oosHeight}px` }} />}
              </div>
            </button>
          )
        })}
      </div>
      <div className="mt-2 grid grid-cols-6 gap-2 text-center text-[11px] text-slate-500">
        {buckets.map((bucket) => (
          <button key={bucket.label} type="button" onClick={() => onBucketClick(bucket)} className="rounded px-1 py-1 hover:bg-slate-50">
            <div className="font-medium text-slate-600">{bucket.label}</div>
            <div>{bucket.outOfServiceCount}/{bucket.totalCount}</div>
            <div>{bucket.outOfServiceRate == null ? '-' : `${bucket.outOfServiceRate}%`}</div>
          </button>
        ))}
      </div>
    </div>
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
