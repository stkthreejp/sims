import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { Activity, AlertTriangle, BarChart3, CheckCircle2, Clock3, FileText, MapPin, RefreshCw, ShieldAlert, ShieldCheck, Truck, X, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { quotesApi } from '@/api/quotes.api'
import { getGoogleMapsApiKey } from '@/lib/clientConfig'
import type { AutoSafetyBasic, AutoSafetyDetail, AutoSafetyIss, AutoSafetyRadiusSummary, AutoSafetyRiskLevel, AutoSafetySnapshotHistory, AutoSafetyTrendBucket } from '@/types/quote.types'

type Props = {
  quoteId: string
}

type AutoSafetyTab = 'safer' | 'radius' | 'events' | 'history'
type DetailSelection = { kind: string; title: string; basic?: string } | null

const MAPS_SCRIPT_ID = 'google-maps-places'

function loadGoogleMaps(): Promise<void> {
  return new Promise((resolve, reject) => {
    if ((window as any).google?.maps) {
      resolve()
      return
    }

    const key = getGoogleMapsApiKey()
    if (!key) {
      reject(new Error('Google Maps API key is not configured'))
      return
    }

    const existing = document.getElementById(MAPS_SCRIPT_ID)
    if (existing) {
      existing.addEventListener('load', () => resolve())
      existing.addEventListener('error', reject)
      return
    }

    const script = document.createElement('script')
    script.id = MAPS_SCRIPT_ID
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&libraries=places`
    script.async = true
    script.defer = true
    script.onload = () => resolve()
    script.onerror = reject
    document.head.appendChild(script)
  })
}

const riskStyle: Record<AutoSafetyRiskLevel, CSSProperties> = {
  Unknown: { background: 'var(--pill-draft-bg)', color: 'var(--pill-draft-fg)' },
  Acceptable: { background: 'var(--good-bg)', color: 'var(--good-fg)' },
  Watch: { background: 'var(--warn-bg)', color: 'var(--warn-fg)' },
  High: { background: 'var(--bad-bg)', color: 'var(--bad-fg)' },
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
  const reportMutation = useMutation({
    mutationFn: () => quotesApi.generateAutoSafetyReport(quoteId),
    onSuccess: (attachment) => {
      qc.invalidateQueries({ queryKey: ['quote-attachments', quoteId] })
      qc.invalidateQueries({ queryKey: ['attachments', 'Policy', quoteId] })
      toast.success('Auto safety report saved', {
        description: `${attachment.fileName} is now in Documents.`,
      })
    },
    onError: (err: any) => {
      const msg = err?.response?.data?.errorMessage ?? 'Auto safety report could not be created'
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
      <div style={{ padding: '14px 16px', borderBlock: '1px solid var(--line-2)', background: 'var(--surface-2)', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>
        Opening auto safety profile...
      </div>
    )
  }

  if (isError || !data) {
    const message = getLoadErrorMessage(error)
    return (
      <div style={{ padding: '14px 16px', borderBlock: '1px solid #f3c6be', borderLeft: '4px solid #b33a2a', background: 'var(--surface)', color: 'var(--bad-fg)', fontSize: 'var(--fs-body)' }}>
        <div style={{ fontWeight: 600 }}>Auto safety profile could not be loaded.</div>
        <div style={{ marginTop: 4, color: 'var(--bad-fg)' }}>{message}</div>
      </div>
    )
  }

  if (data.status !== 'Ready') {
    return (
      <div style={{ padding: '14px 16px', borderTop: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
        <div className="flex items-start gap-3 sd-card" style={{ padding: 14 }}>
          <AlertTriangle size={16} style={{ marginTop: 2, color: 'var(--warn-fg)' }} />
          <div className="flex-1">
            <div style={{ color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>Auto Safety</div>
            <p style={{ margin: '4px 0 0', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>{data.message ?? 'FMCSA data is not available yet.'}</p>
            {data.usDotNumber && <p style={{ margin: '8px 0 0', color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>USDOT {data.usDotNumber}</p>}
          </div>
          {data.status === 'NoData' && (
            <button
              onClick={() => refreshMutation.mutate()}
              disabled={refreshMutation.isPending}
              className="sd-btn outline sm"
            >
              <RefreshCw size={14} className={refreshMutation.isPending ? 'animate-spin' : ''} />
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
    <div className="space-y-4" style={{ padding: '14px 16px', borderTop: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          {data.overallRiskLevel === 'High' ? <ShieldAlert size={16} style={{ color: 'var(--bad-fg)' }} /> : <ShieldCheck size={16} style={{ color: 'var(--ink-3)' }} />}
          <h3 style={{ margin: 0, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>Auto Safety</h3>
          <span className="sd-pill" style={riskStyle[data.overallRiskLevel]}>
            {data.overallRiskLevel}
          </span>
        </div>
        <div style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>
          {data.snapshotMonth ? `Snapshot ${data.snapshotMonth}` : 'No scored snapshot'}{data.methodologyVersion ? ` - ${data.methodologyVersion}` : ''}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => reportMutation.mutate()}
            disabled={reportMutation.isPending}
            className="sd-btn outline sm"
          >
            <FileText size={14} />
            {reportMutation.isPending ? 'Saving' : 'Save Report'}
          </button>
          <button
            onClick={() => refreshMutation.mutate()}
            disabled={refreshMutation.isPending}
            className="sd-btn outline sm"
          >
            <RefreshCw size={14} className={refreshMutation.isPending ? 'animate-spin' : ''} />
            {refreshMutation.isPending ? 'Refreshing' : 'Refresh FMCSA'}
          </button>
        </div>
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
            <span key={flag} className="sd-lob" style={{ background: 'var(--warn-bg)', color: 'var(--warn-fg)', border: '1px solid #f5d9a8' }}>
              <AlertTriangle size={12} /> {flag}
            </span>
          ))}
        </div>
      )}

      <div className="grid grid-cols-4 gap-2">
        <SectionPill label="SAFER / OOS" active={activeTab === 'safer'} onClick={() => setActiveTab('safer')} />
        <SectionPill label="Radius" active={activeTab === 'radius'} onClick={() => setActiveTab('radius')} />
        <SectionPill label="Events" active={activeTab === 'events'} onClick={() => setActiveTab('events')} />
        <SectionPill label="CSA / History" active={activeTab === 'history'} onClick={() => setActiveTab('history')} />
      </div>

      {activeTab === 'safer' && (
        <div className="grid grid-cols-3 gap-3">
          <div className="col-span-2 sd-card">
            <div className="sd-card-body">
            <div className="sims-field-label flex items-center gap-2">
              <Truck size={14} /> SAFER / OOS
            </div>
            <div className="grid grid-cols-4 gap-3">
              <OosMetric label="Overall" count={oos.inspectionCount} oosCount={overallOosCount} rate={overallOosRate} nationalAverage={oos.overallNationalAverageRate} onClick={() => setDetailSelection({ kind: 'overall-oos', title: 'Overall OOS Events' })} />
              <OosMetric label="Driver" count={driverInspectionCount} oosCount={oos.driverOosCount} rate={oos.driverOosRate} nationalAverage={oos.driverNationalAverageRate} onClick={() => setDetailSelection({ kind: 'driver-oos', title: 'Driver OOS Events' })} />
              <OosMetric label="Vehicle" count={vehicleInspectionCount} oosCount={oos.vehicleOosCount} rate={oos.vehicleOosRate} nationalAverage={oos.vehicleNationalAverageRate} onClick={() => setDetailSelection({ kind: 'vehicle-oos', title: 'Vehicle OOS Events' })} />
              <OosMetric label="Hazmat" count={hazmatInspectionCount} oosCount={hazmatOosCount} rate={oos.hazmatOosRate ?? null} nationalAverage={oos.hazmatNationalAverageRate} onClick={() => setDetailSelection({ kind: 'hazmat-oos', title: 'Hazmat OOS Events' })} />
            </div>
            </div>
          </div>

          <div className="sd-card">
            <div className="sd-card-body">
            <div className="sims-field-label flex items-center gap-2">
              <Activity size={14} /> Accident Summary
            </div>
            <div className="grid grid-cols-2 gap-2">
              <Metric label="Fatal" value={(accident?.fatalCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'fatal-crash', title: 'Fatal Crashes' })} />
              <Metric label="Injury" value={(accident?.injuryCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'injury-crash', title: 'Injury Crashes' })} />
              <Metric label="Tow" value={(accident?.towCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'tow-crash', title: 'Tow-Only Crashes' })} />
              <Metric label="Reportable" value={(accident?.totalReportableCount ?? 0).toLocaleString()} compact onClick={() => setDetailSelection({ kind: 'reportable-crash', title: 'Reportable Crashes' })} />
            </div>
            <p style={{ margin: '12px 0 0', color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>
              SAFER-style reportable crash events; ratio: {accident?.accidentToPowerUnitRatio == null ? '-' : `${accident.accidentToPowerUnitRatio}%`}
            </p>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'radius' && (
        <div className="sd-card">
          <div className="sd-card-body">
          <div className="sims-field-label flex items-center gap-2">
            <MapPin size={14} /> Radius Of Operations
          </div>
          {data.radiusSummary?.note && (
            <div className="mb-3 rounded border px-3 py-2 text-xs" style={{ borderColor: 'var(--warn-bg)', background: 'var(--warn-bg)', color: 'var(--warn-fg)' }}>
              <span className="font-semibold">{data.radiusSummary.precision}:</span> {data.radiusSummary.note}
            </div>
          )}
          {data.radiusSummary?.precisionCounts?.some((p) => p.count > 0) && (
            <div className="mb-3 flex flex-wrap gap-2 text-xs">
              {data.radiusSummary.precisionCounts
                .filter((p) => p.count > 0)
                .map((p) => (
                  <span key={p.label} className="rounded border px-2 py-1" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
                    {p.label}: <span className="font-semibold" style={{ color: 'var(--ink)' }}>{p.count.toLocaleString()}</span>
                  </span>
                ))}
            </div>
          )}
          <InteractiveRadiusMap summary={data.radiusSummary} />
          {data.geographicHotspots.length === 0 ? (
            <p style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>No inspection location concentration yet.</p>
          ) : (
            <div className="grid grid-cols-5 gap-2">
              {data.radiusSummary.mapPoints.length > 0 ? data.radiusSummary.mapPoints.slice(0, 5).map((h, idx) => (
                <div key={`${h.label}-${idx}`} className="rounded p-2" style={{ background: 'var(--surface-2)' }}>
                  <div className="flex items-center gap-1 text-sm font-semibold" style={{ color: 'var(--ink)' }}>
                    <span className="grid h-5 w-5 place-items-center rounded-full text-[10px]" style={{ background: 'var(--line)', color: 'var(--ink-2)' }}>{idx + 1}</span>
                    <span className="truncate">{h.label}</span>
                  </div>
                  <div className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>{h.inspectionCount} insp - {h.outOfServiceCount} OOS</div>
                  <div className="mt-0.5 text-[11px]" style={{ color: 'var(--ink-4)' }}>{h.precision}</div>
                </div>
              )) : data.geographicHotspots.map((h) => (
                <div key={h.state} className="rounded p-2" style={{ background: 'var(--surface-2)' }}>
                  <div className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{h.state}</div>
                  <div className="text-xs" style={{ color: 'var(--ink-3)' }}>{h.inspectionCount} insp - {h.violationCount} viol</div>
                </div>
              ))}
            </div>
          )}
          <div className="mt-3 grid grid-cols-5 gap-2 text-xs">
            {(data.radiusSummary?.bands ?? []).map((band) => (
              <div
                key={band.label}
                className="rounded px-2 py-2 text-center"
                style={{ background: 'var(--surface-2)' }}
              >
                <div className="font-semibold" style={{ color: 'var(--ink-2)' }}>{band.label}</div>
                <div className="mt-1" style={{ color: 'var(--ink-3)' }}>{band.inspectionCount.toLocaleString()} insp</div>
                {band.outOfServiceCount > 0 && <div className="mt-0.5" style={{ color: 'var(--bad-fg)' }}>{band.outOfServiceCount.toLocaleString()} OOS</div>}
              </div>
            ))}
          </div>
          </div>
        </div>
      )}

      {activeTab === 'events' && (
        <div className="sd-card">
          <div className="sd-card-head"><h3>Events</h3></div>
          {data.recentSevereEvents.length === 0 ? (
            <p style={{ margin: 0, padding: '14px 16px', color: 'var(--ink-3)', fontSize: 'var(--fs-body)' }}>No recent high-severity or OOS events in the imported window.</p>
          ) : (
            <div>
              {data.recentSevereEvents.map((event, idx) => (
                <div key={`${event.date}-${idx}`} className="grid grid-cols-[120px_160px_1fr_80px] gap-3 px-4 py-2" style={{ borderBottom: '1px solid var(--line-2)', fontSize: 'var(--fs-body)' }}>
                  <span style={{ color: 'var(--ink-3)' }}>{new Date(event.date).toLocaleDateString()}</span>
                  <span style={{ color: 'var(--ink-2)', fontWeight: 500 }}>{event.eventType}</span>
                  <span className="truncate" style={{ color: 'var(--ink-2)' }}>{event.description}</span>
                  <span className="text-right" style={{ color: 'var(--ink-3)' }}>{event.state ?? '-'}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {activeTab === 'history' && (
        <div className="space-y-3">
          <div className="sd-card">
            <div className="sd-card-head">
              <h3><BarChart3 size={14} /> CSA / BASICs</h3>
            </div>
            <div className="grid grid-cols-7" style={{ borderTop: '1px solid var(--line-2)' }}>
              {data.basics.map((b) => (
                <BasicKpiTile
                  key={b.basic}
                  basic={b}
                  powerUnits={data.powerUnits}
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

            <div className="sd-card">
              <div className="sd-card-body">
              <div className="sims-field-label flex items-center gap-2">
                <Clock3 size={14} /> History Snapshot
              </div>
              <Metric label="Data Refreshed" value={data.dataRefreshedAt ? new Date(data.dataRefreshedAt).toLocaleDateString() : '-'} compact />
              <SnapshotHistoryList history={data.snapshotHistory ?? []} />
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

function SnapshotHistoryList({ history }: { history: AutoSafetySnapshotHistory[] }) {
  if (history.length === 0) {
    return (
      <div className="mt-3 rounded p-3 text-xs" style={{ background: 'var(--surface-2)', color: 'var(--ink-3)' }}>
        Historical snapshots will appear as monthly FMCSA jobs accumulate records.
      </div>
    )
  }

  return (
    <div className="mt-3 space-y-2">
      {history.slice(0, 6).map((snapshot) => {
        const topBasics = snapshot.basics
          .filter((b) => b.percentile != null || b.measure != null || b.eventCount > 0 || b.outOfServiceCount > 0)
          .slice(0, 2)
        return (
          <div key={snapshot.snapshotMonth} className="rounded border p-2" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
            <div className="flex items-center justify-between gap-2">
              <div className="text-xs font-semibold" style={{ color: 'var(--ink)' }}>{snapshot.snapshotMonth}</div>
              <div className="text-[11px]" style={{ color: 'var(--ink-3)' }}>
                {snapshot.powerUnits == null ? '-' : `${snapshot.powerUnits.toLocaleString()} PU`}
                {snapshot.driverCount == null ? '' : ` | ${snapshot.driverCount.toLocaleString()} drv`}
              </div>
            </div>
            {snapshot.mileage != null && (
              <div className="mt-1 text-[11px]" style={{ color: 'var(--ink-3)' }}>
                Mileage {snapshot.mileage.toLocaleString()}{snapshot.mileageYear ? ` (${snapshot.mileageYear})` : ''}
              </div>
            )}
            {topBasics.length > 0 ? (
              <div className="mt-1 space-y-1">
                {topBasics.map((basic) => (
                  <div key={basic.basic} className="flex items-center justify-between gap-2 text-[11px]">
                    <span className="truncate" style={{ color: 'var(--ink-3)' }}>{basic.basic}</span>
                    <span style={basic.isPrioritized || (basic.percentile ?? 0) >= 75 ? { fontWeight: 600, color: 'var(--bad-fg)' } : { color: 'var(--ink-3)' }}>
                      {basic.percentile == null ? `M ${basic.measure ?? '-'}` : `${Math.round(basic.percentile)}%`}
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <div className="mt-1 text-[11px]" style={{ color: 'var(--ink-4)' }}>Carrier snapshot only</div>
            )}
          </div>
        )
      })}
    </div>
  )
}

function SectionPill({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`sd-btn sm ${active ? '' : 'outline'}`}
      style={active ? { background: 'var(--accent-soft)', color: 'var(--accent-ink)', border: '1px solid var(--accent-light)' } : undefined}
    >
      {label}
    </button>
  )
}

function InteractiveRadiusMap({ summary }: { summary: AutoSafetyRadiusSummary }) {
  const mapRef = useRef<HTMLDivElement>(null)
  const mapInstanceRef = useRef<any>(null)
  const circleRef = useRef<any>(null)
  const [radiusMiles, setRadiusMiles] = useState(100)
  const [customRadius, setCustomRadius] = useState('100')
  const [mapError, setMapError] = useState<string | null>(null)

  const usablePoints = useMemo(
    () => [...summary.mapPoints]
      .filter((point) => point.latitude != null && point.longitude != null)
      .sort((a, b) => b.inspectionCount - a.inspectionCount)
      .slice(0, 20),
    [summary.mapPoints]
  )

  const radiusSummary = useMemo(() => {
    if (summary.baseLatitude == null || summary.baseLongitude == null) {
      return { inspections: 0, oos: 0, points: 0 }
    }

    const inside = usablePoints.filter((point) =>
      milesBetween(summary.baseLatitude!, summary.baseLongitude!, point.latitude, point.longitude) <= radiusMiles
    )

    return {
      points: inside.length,
      inspections: inside.reduce((sum, point) => sum + point.inspectionCount, 0),
      oos: inside.reduce((sum, point) => sum + point.outOfServiceCount, 0),
    }
  }, [radiusMiles, summary.baseLatitude, summary.baseLongitude, usablePoints])

  useEffect(() => {
    if (!mapRef.current || summary.baseLatitude == null || summary.baseLongitude == null || usablePoints.length === 0) return

    let cancelled = false
    loadGoogleMaps()
      .then(() => {
        if (cancelled || !mapRef.current) return
        setMapError(null)

        const g = (window as any).google
        const base = { lat: summary.baseLatitude!, lng: summary.baseLongitude! }
        const map = new g.maps.Map(mapRef.current, {
          center: base,
          zoom: 7,
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: false,
          clickableIcons: false,
          gestureHandling: 'cooperative',
        })
        mapInstanceRef.current = map

        const bounds = new g.maps.LatLngBounds()
        bounds.extend(base)

        const baseMarker = new g.maps.Marker({
          position: base,
          map,
          label: { text: 'HQ', color: '#ffffff', fontSize: '11px', fontWeight: '700' },
          title: 'Insured base location',
          icon: {
            path: g.maps.SymbolPath.CIRCLE,
            fillColor: '#2563eb',
            fillOpacity: 1,
            strokeColor: '#ffffff',
            strokeWeight: 2,
            scale: 12,
          },
          zIndex: 20,
        })
        const baseInfo = new g.maps.InfoWindow({
          content: '<strong>Insured base location</strong><br>Radius starts here.',
        })
        baseMarker.addListener('click', () => baseInfo.open({ anchor: baseMarker, map }))

        usablePoints.forEach((point, index) => {
          const position = { lat: point.latitude, lng: point.longitude }
          bounds.extend(position)
          const hasOos = point.outOfServiceCount > 0
          const scale = Math.max(7, Math.min(16, 6 + Math.sqrt(point.inspectionCount)))
          const marker = new g.maps.Marker({
            position,
            map,
            label: { text: `${index + 1}`, color: '#ffffff', fontSize: '10px', fontWeight: '700' },
            title: `${point.label} - ${point.inspectionCount} inspections, ${point.outOfServiceCount} OOS`,
            icon: {
              path: g.maps.SymbolPath.CIRCLE,
              fillColor: hasOos ? '#dc2626' : '#059669',
              fillOpacity: 0.9,
              strokeColor: '#ffffff',
              strokeWeight: 2,
              scale,
            },
            zIndex: hasOos ? 15 : 10,
          })
          const info = new g.maps.InfoWindow({
            content: `<strong>${escapeHtml(point.label)}</strong><br>${point.inspectionCount} inspections / ${point.outOfServiceCount} OOS<br>Precision: ${escapeHtml(point.precision)}`,
          })
          marker.addListener('click', () => info.open({ anchor: marker, map }))
        })

        const circle = new g.maps.Circle({
          map,
          center: base,
          radius: radiusMiles * 1609.344,
          editable: true,
          draggable: false,
          fillColor: '#2563eb',
          fillOpacity: 0.1,
          strokeColor: '#2563eb',
          strokeOpacity: 0.7,
          strokeWeight: 2,
        })
        circleRef.current = circle
        circle.addListener('radius_changed', () => {
          const nextMiles = Math.max(1, Math.round(circle.getRadius() / 1609.344))
          setRadiusMiles(nextMiles)
          setCustomRadius(String(nextMiles))
        })

        map.fitBounds(bounds)
      })
      .catch(() => setMapError('Interactive map unavailable. Check the Google Maps browser key and Maps JavaScript API.'))

    return () => {
      cancelled = true
    }
    // radiusMiles is read only to size the circle at creation; the effect below
    // syncs subsequent changes, so re-running here (and recreating the map) is intentional to avoid.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [summary.baseLatitude, summary.baseLongitude, usablePoints])

  useEffect(() => {
    if (circleRef.current) circleRef.current.setRadius(radiusMiles * 1609.344)
  }, [radiusMiles])

  if (summary.baseLatitude == null || summary.baseLongitude == null || usablePoints.length === 0) return null

  const applyRadius = (value: number) => {
    const next = Math.max(1, Math.min(1000, Math.round(value)))
    setRadiusMiles(next)
    setCustomRadius(String(next))
  }

  return (
    <div className="mb-3 overflow-hidden rounded border" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
      <div className="flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)' }}>
        <div className="flex items-center gap-2 text-xs" style={{ color: 'var(--ink-3)' }}>
          <span className="font-semibold" style={{ color: 'var(--ink)' }}>{radiusMiles.toLocaleString()} mi radius</span>
          <span>{radiusSummary.points.toLocaleString()} points inside</span>
          <span>{radiusSummary.inspections.toLocaleString()} insp inside</span>
          <span style={{ color: radiusSummary.oos > 0 ? 'var(--bad-fg)' : 'var(--ink-3)' }}>{radiusSummary.oos.toLocaleString()} OOS</span>
        </div>
        <div className="flex items-center gap-1">
          {[50, 100, 250].map((value) => (
            <button
              key={value}
              type="button"
              onClick={() => applyRadius(value)}
              className={`sd-btn sm ${radiusMiles === value ? 'primary' : 'outline'}`}
            >
              {value}
            </button>
          ))}
          <input
            value={customRadius}
            onChange={(e) => setCustomRadius(e.target.value)}
            onBlur={() => applyRadius(Number(customRadius) || radiusMiles)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') applyRadius(Number(customRadius) || radiusMiles)
            }}
            className="sims-input text-right"
            style={{ width: 64, height: 28 }}
          />
          <span className="text-xs" style={{ color: 'var(--ink-3)' }}>mi</span>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3 border-b px-3 py-2 text-[11px]" style={{ borderColor: 'var(--line-2)', color: 'var(--ink-3)' }}>
        <span className="inline-flex items-center gap-1"><span className="h-2.5 w-2.5 rounded-full" style={{ background: 'var(--accent)' }} /> Insured base</span>
        <span className="inline-flex items-center gap-1"><span className="h-2.5 w-2.5 rounded-full" style={{ background: 'var(--good-fg)' }} /> Inspection point</span>
        <span className="inline-flex items-center gap-1"><span className="h-2.5 w-2.5 rounded-full" style={{ background: 'var(--bad-fg)' }} /> OOS activity</span>
        {summary.precisionCounts
          .filter((p) => p.count > 0)
          .slice(0, 4)
          .map((p) => (
            <span key={p.label} className="rounded px-1.5 py-0.5" style={{ background: 'var(--surface-2)' }}>
              {p.label}: {p.count.toLocaleString()}
            </span>
          ))}
      </div>
      {mapError ? (
        <div className="p-3 text-xs" style={{ color: 'var(--warn-fg)' }}>{mapError}</div>
      ) : (
        <div ref={mapRef} className="h-[300px] w-full" />
      )}
      <div className="border-t px-3 py-2 text-[11px]" style={{ borderColor: 'var(--line-2)', color: 'var(--ink-3)' }}>
        Drag the circle edge to resize. Marker size follows inspection count. County/state points are directional when exact inspection coordinates are not available.
      </div>
    </div>
  )
}

function OosMetric({ label, count, oosCount, rate, nationalAverage, onClick }: { label: string; count: number; oosCount: number; rate: number | null; nationalAverage: number | null; onClick?: () => void }) {
  const difference = rate == null || nationalAverage == null ? null : Math.round((rate - nationalAverage) * 100) / 100
  const differenceColor: string = difference == null
    ? 'var(--ink-4)'
    : difference > 0
      ? 'var(--bad-fg)'
      : difference < 0
        ? 'var(--good-fg)'
        : 'var(--ink-3)'

  return (
    <button type="button" onClick={onClick} className="text-left" style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-md)', background: 'var(--surface-2)', padding: 8 }}>
      <div className="sims-field-label">{label}</div>
      <div style={{ marginTop: 4, color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }}>{rate == null ? '-' : `${rate}%`}</div>
      <div style={{ marginTop: 4, color: 'var(--ink-3)', fontSize: 'var(--fs-sm)' }}>{oosCount.toLocaleString()} OOS / {count.toLocaleString()} insp</div>
      <div style={{ marginTop: 4, color: 'var(--ink-4)', fontSize: 'var(--fs-xs)' }}>
        Natl {nationalAverage == null ? '-' : `${nationalAverage}%`}
        {difference != null && <span className="ml-1" style={{ color: differenceColor }}>{difference > 0 ? '+' : ''}{difference}%</span>}
      </div>
    </button>
  )
}

function IssStoplight({ iss }: { iss?: AutoSafetyIss }) {
  const status = iss?.status ?? 'Unknown'
  const recommendation = iss?.label?.replace(' estimate', '') ?? 'Pending'
  const lights = [
    { key: 'Red', activeColor: 'var(--bad-fg)' },
    { key: 'Yellow', activeColor: 'var(--warn-fg)' },
    { key: 'Green', activeColor: 'var(--good-fg)' },
  ]
  return (
    <div className="sd-card" style={{ padding: 12 }}>
      <div className="flex items-center justify-between gap-2">
        <div>
          <div className="sims-field-label">SIMS ISS</div>
          <div className="mt-1 truncate" style={{ color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }} title={`${recommendation}${iss?.score == null ? '' : ` (${iss.score})`}`}>
            {iss?.score == null ? recommendation : `${recommendation} ${iss.score}`}
          </div>
        </div>
        <div className="flex h-12 w-7 flex-col items-center justify-center gap-1" style={{ border: '1px solid var(--line)', borderRadius: 'var(--r-md)', background: 'var(--surface-2)' }}>
          {lights.map((light) => <span key={light.key} className="h-2.5 w-2.5 rounded-full" style={{ background: status === light.key ? light.activeColor : 'var(--line)' }} />)}
        </div>
      </div>
      <div className="mt-1 truncate" style={{ color: 'var(--ink-3)', fontSize: 'var(--fs-xs)' }} title={iss?.explanation ?? iss?.source ?? 'Pending ISS source'}>
        Basis: {iss?.basis ?? 'Pending'}
      </div>
    </div>
  )
}

function BasicKpiTile({ basic, powerUnits, onClick }: { basic: AutoSafetyBasic; powerUnits: number | null; onClick: () => void }) {
  const risk = getBasicRisk(basic, powerUnits)
  const source = getBasicSourceLabel(basic.scoreSource)
  const confidence = getBasicConfidenceLabel(basic)
  const dialValue = risk.value
  const color = risk.color === 'red' ? '#dc2626' : risk.color === 'yellow' ? '#d97706' : risk.color === 'green' ? '#059669' : '#94a3b8'
  const dialBg = risk.color === 'red' ? 'var(--bad-bg)' : risk.color === 'yellow' ? 'var(--warn-bg)' : risk.color === 'green' ? 'var(--good-bg)' : 'var(--surface-2)'
  const statusColor = risk.color === 'red' ? 'var(--bad-fg)' : risk.color === 'yellow' ? 'var(--warn-fg)' : risk.color === 'green' ? 'var(--good-fg)' : 'var(--ink-3)'
  const dash = `${Math.max(0, Math.min(100, dialValue))}, 100`

  return (
    <button type="button" onClick={onClick} className="min-w-0 p-3 text-left hover:bg-slate-50">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate text-xs font-semibold" style={{ color: 'var(--ink-2)' }} title={basic.basic}>{basic.basic}</div>
          <div className="mt-1 flex flex-wrap gap-1">
            <span className={`inline-flex rounded border px-1.5 py-0.5 text-[10px] font-semibold uppercase ${source.className}`} style={source.style}>
              {source.label}
            </span>
            <span className={`inline-flex rounded border px-1.5 py-0.5 text-[10px] font-semibold uppercase ${confidence.className}`} style={confidence.style} title={confidence.title}>
              {confidence.label}
            </span>
          </div>
        </div>
        <div className="relative flex h-11 w-11 shrink-0 items-center justify-center rounded-full" style={{ background: dialBg }}>
          <svg viewBox="0 0 36 36" className="h-9 w-9 -rotate-90">
            <circle cx="18" cy="18" r="15.5" fill="none" stroke="#e2e8f0" strokeWidth="4" />
            <circle cx="18" cy="18" r="15.5" fill="none" stroke={color} strokeWidth="4" strokeDasharray={dash} pathLength="100" strokeLinecap="round" />
          </svg>
          <span className="absolute text-[10px] font-bold" style={{ color: statusColor }}>{risk.label}</span>
        </div>
      </div>
      <div className="mt-2 flex items-center gap-1 text-xs">
        {risk.color === 'red' ? <XCircle className="h-3.5 w-3.5" style={{ color: 'var(--bad-fg)' }} /> : <CheckCircle2 className="h-3.5 w-3.5" style={{ color: risk.color === 'yellow' ? 'var(--warn-fg)' : 'var(--good-fg)' }} />}
        <span style={{ color: statusColor }}>{risk.status}</span>
      </div>
      <div className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>
        {basic.measure == null ? 'Measure -' : `Measure ${basic.measure}`}
        {basic.percentile == null ? '' : ` | ${basic.percentile}%`}
      </div>
      <div className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>{basic.eventCount} events{basic.outOfServiceCount > 0 ? ` | ${basic.outOfServiceCount} OOS` : ''}</div>
      {(basic.recentEventCount > 0 || basic.recentOutOfServiceCount > 0) && (
        <div className="mt-1 text-[11px]" style={{ color: 'var(--ink-4)' }}>
          12 mo: {basic.recentEventCount} events{basic.recentOutOfServiceCount > 0 ? ` | ${basic.recentOutOfServiceCount} OOS` : ''}
        </div>
      )}
    </button>
  )
}

function getBasicSourceLabel(source: string): { label: string; className: string; style: CSSProperties } {
  if (source === 'Official SMS' || source === 'Official SMS measure') {
    return { label: 'Official', className: '', style: { borderColor: 'var(--accent-light)', background: 'var(--accent-soft)', color: 'var(--accent-ink)' } }
  }
  if (source === 'SIMS peer percentile') {
    return { label: 'SIMS peer', className: 'border-violet-200 bg-violet-50 text-violet-700', style: {} }
  }
  return { label: 'SIMS signal', className: '', style: { borderColor: 'var(--line)', background: 'var(--surface-2)', color: 'var(--ink-3)' } }
}

function getBasicConfidenceLabel(basic: AutoSafetyBasic): { label: string; title: string; className: string; style: CSSProperties } {
  if (basic.scoreSource === 'Official SMS' && basic.percentile != null) {
    return {
      label: 'High conf',
      title: 'Official SMS percentile is available for this BASIC.',
      className: '',
      style: { borderColor: 'var(--good-fg)', background: 'var(--good-bg)', color: 'var(--good-fg)' },
    }
  }
  if (basic.scoreSource === 'Official SMS' || basic.scoreSource === 'Official SMS measure') {
    return {
      label: 'Med conf',
      title: 'Official SMS measure is available, but percentile is not public.',
      className: '',
      style: { borderColor: 'var(--accent-light)', background: 'var(--accent-soft)', color: 'var(--accent-ink)' },
    }
  }
  if (basic.scoreSource === 'SIMS peer percentile') {
    return {
      label: 'Med conf',
      title: 'SIMS percentile is estimated from the imported peer dataset.',
      className: 'border-violet-200 bg-violet-50 text-violet-700',
      style: {},
    }
  }
  return {
    label: 'Directional',
    title: 'SIMS signal is based on imported inspections, violations, crashes, and recent OOS activity.',
    className: '',
    style: { borderColor: 'var(--warn-bg)', background: 'var(--warn-bg)', color: 'var(--warn-fg)' },
  }
}

function getBasicRisk(basic: AutoSafetyBasic, powerUnits: number | null): { color: 'green' | 'yellow' | 'red' | 'gray'; value: number; label: string; status: string } {
  if (basic.isPrioritized) return { color: 'red', value: 100, label: '!', status: 'Alert' }
  const hasNoEvents = basic.eventCount === 0 && basic.outOfServiceCount === 0 && basic.recentEventCount === 0 && basic.recentOutOfServiceCount === 0
  if (basic.basic === 'Hazardous Materials Compliance' && hasNoEvents) {
    return { color: 'gray', value: 0, label: 'N/A', status: 'Inconclusive' }
  }
  if (basic.percentile != null) {
    if (basic.percentile >= 75) return { color: 'red', value: basic.percentile, label: `${Math.round(basic.percentile)}`, status: 'High' }
    if (basic.percentile >= 50) return { color: 'yellow', value: basic.percentile, label: `${Math.round(basic.percentile)}`, status: 'Watch' }
    return { color: 'green', value: basic.percentile, label: `${Math.round(basic.percentile)}`, status: 'Clear' }
  }

  const exposure = Math.max(1, powerUnits ?? 10)
  if (basic.basic === 'Crash Indicator' && basic.eventCount > 0) {
    const value = Math.min(100, Math.round((basic.eventCount / exposure) * 310))
    if (value >= 75) return { color: 'red', value, label: `${value}`, status: 'SIMS signal' }
    if (value >= 50) return { color: 'yellow', value, label: `${value}`, status: 'Watch' }
    return { color: 'green', value, label: `${value}`, status: 'Signal' }
  }

  const eventRate = basic.eventCount / exposure
  const oosRate = basic.outOfServiceCount / exposure
  const recentEventRate = basic.recentEventCount / exposure
  const recentOosRate = basic.recentOutOfServiceCount / exposure
  const eventPressure = Math.min(35, eventRate * 28)
  const oosPressure = Math.min(30, oosRate * 42)
  const recentPressure = Math.min(30, recentEventRate * 32 + recentOosRate * 45)
  const value = Math.round(eventPressure + oosPressure + recentPressure)
  if (value === 0 && basic.scoreSource === 'Official SMS' && basic.measure != null) {
    return { color: 'gray', value: 0, label: 'N/A', status: 'Inconclusive' }
  }
  if (value >= 75) return { color: 'red', value, label: `${value}`, status: basic.percentile == null ? 'SIMS signal' : 'High' }
  if (value >= 50) return { color: 'yellow', value, label: `${value}`, status: 'Watch' }
  return { color: 'green', value, label: `${value}`, status: basic.scoreSource === 'Official SMS' ? 'SIMS signal' : 'Signal' }
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
    <div className="rounded border p-3" style={{ background: 'var(--surface)' }}>
      <div className="mb-2 flex items-center justify-between gap-3">
        <div className="text-xs font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>{title}</div>
        <div className="flex items-center gap-3 text-[11px]" style={{ color: 'var(--ink-3)' }}>
          <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-sm" style={{ background: 'var(--bad-fg)' }} />{oosLabel}</span>
          <span className="inline-flex items-center gap-1"><span className="h-2 w-2 rounded-sm" style={{ background: 'var(--good-fg)' }} />{totalLabel}</span>
        </div>
      </div>
      <div className="grid h-36 grid-cols-6 items-end gap-2 border-b px-1 pb-2" style={{ borderColor: 'var(--line)' }}>
        {buckets.map((bucket) => {
          const totalHeight = Math.max(6, Math.round((bucket.totalCount / max) * 112))
          const oosHeight = bucket.outOfServiceCount === 0 ? 0 : Math.max(4, Math.round((bucket.outOfServiceCount / max) * 112))
          return (
            <button
              type="button"
              key={bucket.label}
              onClick={() => onBucketClick(bucket)}
              className="group flex h-32 flex-col justify-end rounded px-1 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-200"
              title={`${bucket.label} months ago: ${bucket.outOfServiceCount} OOS / ${bucket.totalCount} total`}
            >
              <div className="relative mx-auto w-full max-w-9 rounded-t transition group-hover:bg-emerald-600" style={{ height: `${totalHeight}px`, background: 'var(--good-fg)' }}>
                {oosHeight > 0 && <div className="absolute bottom-0 left-0 right-0 rounded-t" style={{ height: `${oosHeight}px`, background: 'var(--bad-fg)' }} />}
              </div>
            </button>
          )
        })}
      </div>
      <div className="mt-2 grid grid-cols-6 gap-2 text-center text-[11px]" style={{ color: 'var(--ink-3)' }}>
        {buckets.map((bucket) => (
          <button key={bucket.label} type="button" onClick={() => onBucketClick(bucket)} className="rounded px-1 py-1 hover:bg-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-200">
            <div className="font-medium" style={{ color: 'var(--ink-2)' }}>{bucket.label}</div>
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
    <div className="fixed inset-0 z-50 flex items-start justify-end p-4" style={{ background: 'rgba(12,17,28,0.20)' }}>
      <div className="mt-16 max-h-[72vh] w-full max-w-3xl overflow-y-auto rounded border shadow-xl" style={{ borderColor: 'var(--line)', background: 'var(--surface)' }}>
        <div className="sticky top-0 flex items-center justify-between border-b px-4 py-3" style={{ background: 'var(--surface)', borderColor: 'var(--line)' }}>
          <div>
            <h3 className="text-sm font-semibold" style={{ color: 'var(--ink)' }}>{title}</h3>
            <p className="mt-1 text-xs" style={{ color: 'var(--ink-3)' }}>{isLoading ? 'Loading details...' : `${items.length} FMCSA event${items.length === 1 ? '' : 's'}`}</p>
          </div>
          <button type="button" onClick={onClose} className="sims-icon-btn">
            <X className="h-4 w-4" />
          </button>
        </div>

        {isLoading ? (
          <div className="p-5 text-sm" style={{ color: 'var(--ink-3)' }}>Loading event details...</div>
        ) : items.length === 0 ? (
          <div className="p-5 text-sm" style={{ color: 'var(--ink-3)' }}>No matching FMCSA events found.</div>
        ) : (
          <div className="divide-y">
            {items.map((item, idx) => (
              <div key={`${item.reportNumber}-${idx}`} className="px-4 py-3 text-sm">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold" style={{ color: 'var(--ink)' }}>{item.category}</span>
                      {item.isOutOfService && <span className="rounded border px-1.5 py-0.5 text-[11px] font-semibold" style={{ borderColor: 'var(--bad-fg)', background: 'var(--bad-bg)', color: 'var(--bad-fg)' }}>OOS</span>}
                      {item.isDriverDisqualifying && <span className="rounded border px-1.5 py-0.5 text-[11px] font-semibold" style={{ borderColor: 'var(--bad-fg)', background: 'var(--bad-bg)', color: 'var(--bad-fg)' }}>Driver disq</span>}
                      {item.basic && <span className="rounded border px-1.5 py-0.5 text-[11px] font-medium" style={{ borderColor: 'var(--line)', background: 'var(--surface-2)', color: 'var(--ink-3)' }}>{item.basic}</span>}
                    </div>
                    <div className="mt-0.5 text-xs" style={{ color: 'var(--ink-3)' }}>{formatDate(item.date)} · Report {item.reportNumber} · {item.source}</div>
                  </div>
                  <div className="text-xs font-medium" style={{ color: 'var(--ink-3)' }}>{[item.city, item.state].filter(Boolean).join(', ') || '-'}</div>
                </div>
                <div className="mt-2 rounded border px-3 py-2" style={{ borderColor: 'var(--line-2)', background: 'var(--surface)', color: 'var(--ink-2)' }}>{item.description}</div>
                <div className="mt-3 grid grid-cols-2 gap-3">
                  {(item.violationCode || item.violationGroup || item.unitNumber || item.severityWeight != null || item.oosWeight != null) && (
                    <DetailSection
                      title="Violation"
                      items={[
                        ['Code', item.violationCode],
                        ['Group', item.violationGroup],
                        ['Unit', item.unitNumber],
                        ['Severity', item.severityWeight == null ? null : String(item.severityWeight)],
                        ['OOS weight', item.oosWeight == null ? null : String(item.oosWeight)],
                      ]}
                    />
                  )}
                  {(item.location || item.agency || item.countyCode) && (
                    <DetailSection
                      title="Location"
                      items={[
                        ['Place', item.location],
                        ['Agency', item.agency],
                        ['County', item.countyCode],
                      ]}
                    />
                  )}
                  {item.conditions && <DetailSection title={item.violationCode ? 'Inspection' : 'Crash Conditions'} items={splitDetailItems(item.conditions)} />}
                  {item.vehicleInfo && <DetailSection title="Vehicle Information" items={splitDetailItems(item.vehicleInfo)} />}
                  {item.crashEvents && <DetailSection title={item.violationCode ? 'Related Violations' : 'Crash Events'} items={splitDetailItems(item.crashEvents)} wide />}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function DetailSection({ title, items, wide = false }: { title: string; items: Array<[string, string | null | undefined]>; wide?: boolean }) {
  const visible = items.filter(([, value]) => !!value)
  if (visible.length === 0) return null
  return (
    <div className={`rounded border px-3 py-2 ${wide ? 'col-span-2' : ''}`} style={{ borderColor: 'var(--line-2)', background: 'var(--surface-2)' }}>
      <div className="mb-1.5 text-[11px] font-semibold uppercase" style={{ color: 'var(--ink-3)' }}>{title}</div>
      <div className="space-y-1">
        {visible.map(([label, value]) => (
          <div key={label} className="grid grid-cols-[96px_1fr] gap-2 text-xs">
            <span style={{ color: 'var(--ink-4)' }}>{label}</span>
            <span className="font-medium" style={{ color: 'var(--ink-2)' }}>{value}</span>
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

function milesBetween(lat1: number, lon1: number, lat2: number, lon2: number) {
  const toRadians = (value: number) => value * Math.PI / 180
  const dLat = toRadians(lat2 - lat1)
  const dLon = toRadians(lon2 - lon1)
  const a = Math.sin(dLat / 2) ** 2
    + Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.sin(dLon / 2) ** 2
  return 3958.8 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}

function escapeHtml(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
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
      <div className="sims-field-label">{label}</div>
      <div className="mt-1 truncate" style={{ color: 'var(--ink)', fontSize: 'var(--fs-body)', fontWeight: 600 }} title={value}>{value}</div>
    </>
  )
  if (onClick) {
    return (
      <button type="button" onClick={onClick} className="text-left" style={compact ? undefined : { border: '1px solid var(--line)', borderRadius: 'var(--r-lg)', background: 'var(--surface)', padding: 12, boxShadow: 'var(--shadow-sm)' }}>
        {content}
      </button>
    )
  }
  return (
    <div style={compact ? undefined : { border: '1px solid var(--line)', borderRadius: 'var(--r-lg)', background: 'var(--surface)', padding: 12, boxShadow: 'var(--shadow-sm)' }}>
      {content}
    </div>
  )
}
