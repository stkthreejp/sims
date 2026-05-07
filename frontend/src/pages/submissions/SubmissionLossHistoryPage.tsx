import { useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Check, Plus, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { submissionsApi } from '@/api/submissions.api'
import { submissionLossHistoryApi } from '@/api/submissionLossHistory.api'
import { LOB_LABELS, ACTIVE_LOBS } from '@/types/quote.types'
import type { LossClaimStatus, LossPremiumBasis, SubmissionLossClaimCreate, SubmissionLossYear, SubmissionLossYearCreate } from '@/types/submissionLossHistory.types'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'

const inputStyle: CSSProperties = {
  width: '100%',
  border: '1px solid var(--line)',
  borderRadius: 6,
  padding: '6px 8px',
  fontSize: 13,
  fontFamily: 'inherit',
  background: 'var(--surface)',
}

const labelStyle: CSSProperties = {
  display: 'block',
  fontSize: 10.5,
  fontWeight: 600,
  color: 'var(--ink-3)',
  marginBottom: 4,
  textTransform: 'uppercase',
  letterSpacing: '.04em',
}

function fmtMoney(n: number | null | undefined) {
  if (n == null) return '$0'
  return '$' + n.toLocaleString('en-US', { maximumFractionDigits: 0 })
}

function fmtPct(n: number | null | undefined) {
  if (n == null) return '-'
  return `${(n * 100).toFixed(1)}%`
}

function emptyYearForm(): SubmissionLossYearCreate {
  const currentYear = new Date().getFullYear()
  return {
    policyYear: currentYear - 1,
    premiumAmount: 0,
    premiumBasis: 'Projected',
    isSmmWritten: false,
    paidOverride: 0,
    reservedOverride: 0,
    expenseOverride: 0,
  }
}

function emptyClaimForm(): SubmissionLossClaimCreate {
  return { status: 'Closed', paid: 0, reserved: 0, expense: 0 }
}

export function SubmissionLossHistoryPage() {
  const { id } = useParams<{ id: string }>()
  const qc = useQueryClient()
  const [showYearForm, setShowYearForm] = useState(false)
  const [editingYearId, setEditingYearId] = useState<string | null>(null)
  const [yearForm, setYearForm] = useState<SubmissionLossYearCreate>(emptyYearForm())
  const [claimYearId, setClaimYearId] = useState<string | null>(null)
  const [claimForm, setClaimForm] = useState<SubmissionLossClaimCreate>(emptyClaimForm())

  const { data: submission, isLoading: submissionLoading } = useQuery({
    queryKey: ['submissions', id],
    queryFn: () => submissionsApi.getById(id!),
    enabled: !!id,
  })

  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ['submission-loss-history-summary', id],
    queryFn: () => submissionLossHistoryApi.getSummary(id!),
    enabled: !!id,
  })

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['submission-loss-history-summary', id] })
    qc.invalidateQueries({ queryKey: ['submission-loss-history-years', id] })
  }

  const saveYear = useMutation({
    mutationFn: (dto: SubmissionLossYearCreate) =>
      editingYearId
        ? submissionLossHistoryApi.updateYear(id!, editingYearId, dto)
        : submissionLossHistoryApi.createYear(id!, dto),
    onSuccess: () => {
      invalidate()
      setShowYearForm(false)
      setEditingYearId(null)
      setYearForm(emptyYearForm())
      toast.success('Loss year saved')
    },
    onError: () => toast.error('Could not save loss year'),
  })

  const deleteYear = useMutation({
    mutationFn: (yearId: string) => submissionLossHistoryApi.deleteYear(id!, yearId),
    onSuccess: () => {
      invalidate()
      toast.success('Loss year removed')
    },
  })

  const saveClaim = useMutation({
    mutationFn: (dto: SubmissionLossClaimCreate) => submissionLossHistoryApi.createClaim(id!, claimYearId!, dto),
    onSuccess: () => {
      invalidate()
      setClaimYearId(null)
      setClaimForm(emptyClaimForm())
      toast.success('Claim saved')
    },
    onError: () => toast.error('Could not save claim'),
  })

  const deleteClaim = useMutation({
    mutationFn: (claimId: string) => submissionLossHistoryApi.deleteClaim(id!, claimId),
    onSuccess: () => {
      invalidate()
      toast.success('Claim removed')
    },
  })

  const years = summary?.years ?? []
  const claims = useMemo(() => years.flatMap((y) => y.claims.map((c) => ({ ...c, year: y.policyYear, lob: y.lineOfBusiness }))), [years])

  if (submissionLoading || summaryLoading) return <LoadingSpinner />
  if (!submission) return <p style={{ padding: 24, color: 'var(--ink-3)' }}>Submission not found.</p>

  const setYear = (key: keyof SubmissionLossYearCreate, value: string | boolean) => {
    setYearForm((form) => ({
      ...form,
      [key]: ['policyYear', 'premiumAmount', 'paidOverride', 'reservedOverride', 'expenseOverride'].includes(key)
        ? Number(value) || 0
        : value,
    }))
  }

  const setClaim = (key: keyof SubmissionLossClaimCreate, value: string) => {
    setClaimForm((form) => ({
      ...form,
      [key]: ['paid', 'reserved', 'expense'].includes(key) ? Number(value) || 0 : value,
    }))
  }

  const startEditYear = (year: SubmissionLossYear) => {
    setEditingYearId(year.id)
    setYearForm({
      policyYear: year.policyYear,
      lineOfBusiness: year.lineOfBusiness ?? undefined,
      carrierName: year.carrierName ?? undefined,
      policyNumber: year.policyNumber ?? undefined,
      premiumAmount: year.premiumAmount,
      premiumBasis: year.premiumBasis,
      isSmmWritten: year.isSmmWritten,
      source: year.source ?? undefined,
      asOfDate: year.asOfDate ?? undefined,
      paidOverride: year.paidOverride ?? undefined,
      reservedOverride: year.reservedOverride ?? undefined,
      expenseOverride: year.expenseOverride ?? undefined,
      notes: year.notes ?? undefined,
    })
    setShowYearForm(true)
  }

  return (
    <div style={{ background: 'var(--bg)' }}>
      <Link to={`/submissions/${id}`} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12.5, color: 'var(--ink-3)', fontWeight: 500, marginBottom: 14, textDecoration: 'none' }}>
        <ArrowLeft size={13} /> Back to {submission.submissionNumber}
      </Link>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 24, marginBottom: 14 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 24, fontWeight: 600, letterSpacing: '-.015em' }}>Loss History Analysis</h1>
          <div style={{ marginTop: 6, color: 'var(--ink-3)', fontSize: 13 }}>
            {submission.insuredName} · <span style={{ fontFamily: 'var(--font-mono)' }}>{submission.submissionNumber}</span>
          </div>
        </div>
        <button className="sd-btn primary" onClick={() => { setShowYearForm(true); setEditingYearId(null); setYearForm(emptyYearForm()) }}>
          <Plus size={13} /> Add year
        </button>
      </div>

      <div className="sd-metrics">
        <div className="sd-metric accent"><div className="k">Loss ratio</div><div className="v">{fmtPct(summary?.lossRatio)}</div><div className="s">{fmtMoney(summary?.totalIncurred)} incurred</div></div>
        <div className="sd-metric"><div className="k">Total premium</div><div className="v">{fmtMoney(summary?.totalPremium)}</div><div className="s">{summary?.yearCount ?? 0} years</div></div>
        <div className="sd-metric"><div className="k">Claims</div><div className="v">{summary?.claimCount ?? 0}</div><div className="s">{fmtMoney(summary?.averageSeverity)} average severity</div></div>
        <div className="sd-metric"><div className="k">Open reserve</div><div className="v">{fmtMoney(summary?.openReserve)}</div><div className="s">{fmtMoney(summary?.largestLoss)} largest loss</div></div>
      </div>

      <section className="sd-card" style={{ marginBottom: 14 }}>
        <div className="sd-card-head">
          <h3>Annual experience <span className="cnt">{years.length}</span></h3>
          <button className="sd-btn sm primary" onClick={() => { setShowYearForm(true); setEditingYearId(null); setYearForm(emptyYearForm()) }}><Plus size={12} /> Add year</button>
        </div>

        {showYearForm && (
          <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
            <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>{editingYearId ? 'Edit loss year' : 'Add loss year'}</div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
              <div><label style={labelStyle}>Policy year</label><input type="number" value={yearForm.policyYear} onChange={(e) => setYear('policyYear', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>LOB</label><select value={yearForm.lineOfBusiness ?? ''} onChange={(e) => setYear('lineOfBusiness', e.target.value)} style={inputStyle}><option value="">- Select -</option>{ACTIVE_LOBS.map((l) => <option key={l} value={l}>{LOB_LABELS[l]}</option>)}</select></div>
              <div><label style={labelStyle}>Carrier</label><input value={yearForm.carrierName ?? ''} onChange={(e) => setYear('carrierName', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Policy #</label><input value={yearForm.policyNumber ?? ''} onChange={(e) => setYear('policyNumber', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Premium</label><input type="number" value={yearForm.premiumAmount} onChange={(e) => setYear('premiumAmount', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Premium basis</label><select value={yearForm.premiumBasis} onChange={(e) => setYear('premiumBasis', e.target.value as LossPremiumBasis)} style={inputStyle}><option value="Projected">Projected</option><option value="Actual">Actual</option></select></div>
              <div><label style={labelStyle}>As of</label><input type="date" value={yearForm.asOfDate ?? ''} onChange={(e) => setYear('asOfDate', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Source</label><input value={yearForm.source ?? ''} onChange={(e) => setYear('source', e.target.value)} placeholder="Loss runs, SMM, manual" style={inputStyle} /></div>
              <div><label style={labelStyle}>Paid rollup</label><input type="number" value={yearForm.paidOverride ?? 0} onChange={(e) => setYear('paidOverride', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Reserved rollup</label><input type="number" value={yearForm.reservedOverride ?? 0} onChange={(e) => setYear('reservedOverride', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Expense rollup</label><input type="number" value={yearForm.expenseOverride ?? 0} onChange={(e) => setYear('expenseOverride', e.target.value)} style={inputStyle} /></div>
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, paddingTop: 20, fontSize: 13, color: 'var(--ink-2)' }}>
                <input type="checkbox" checked={yearForm.isSmmWritten} onChange={(e) => setYear('isSmmWritten', e.target.checked)} />
                SMM-written prior year
              </label>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
              <button className="sd-btn primary sm" disabled={saveYear.isPending} onClick={() => saveYear.mutate(yearForm)}><Check size={13} /> Save</button>
              <button className="sd-btn outline sm" onClick={() => { setShowYearForm(false); setEditingYearId(null); setYearForm(emptyYearForm()) }}><X size={13} /> Cancel</button>
            </div>
          </div>
        )}

        {years.length === 0 && !showYearForm ? (
          <div style={{ padding: '36px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>
            <div style={{ color: 'var(--ink-2)', fontWeight: 600, marginBottom: 4 }}>No loss history on file</div>
            <button className="sd-btn outline sm" style={{ marginTop: 10 }} onClick={() => setShowYearForm(true)}><Plus size={13} /> Add loss year</button>
          </div>
        ) : (
          <table className="sd-table">
            <thead><tr><th>Year</th><th>LOB</th><th>Carrier</th><th className="num">Premium</th><th>Basis</th><th className="num">Paid</th><th className="num">Reserved</th><th className="num">Expense</th><th className="num">Incurred</th><th className="num">LR</th><th /></tr></thead>
            <tbody>
              {years.map((y) => (
                <tr key={y.id}>
                  <td className="id">{y.policyYear}</td>
                  <td>{y.lineOfBusiness ? <span className="sd-lob">{LOB_LABELS[y.lineOfBusiness as keyof typeof LOB_LABELS] ?? y.lineOfBusiness}</span> : '-'}</td>
                  <td className="primary-cell">{y.carrierName ?? '-'}</td>
                  <td className="num">{fmtMoney(y.premiumAmount)}</td>
                  <td>{y.premiumBasis}</td>
                  <td className="num">{fmtMoney(y.paid)}</td>
                  <td className="num">{fmtMoney(y.reserved)}</td>
                  <td className="num">{fmtMoney(y.expense)}</td>
                  <td className="num">{fmtMoney(y.incurred)}</td>
                  <td className="num">{fmtPct(y.lossRatio)}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 4 }}>
                      <button className="sd-btn ghost sm" onClick={() => startEditYear(y)}>Edit</button>
                      <button className="sd-btn ghost sm" onClick={() => { setClaimYearId(y.id); setClaimForm(emptyClaimForm()) }}><Plus size={12} /> Claim</button>
                      <button className="sd-btn ghost sm" style={{ color: 'var(--bad-fg)' }} onClick={() => { if (confirm('Remove this loss year?')) deleteYear.mutate(y.id) }}><Trash2 size={12} /></button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="sd-card">
        <div className="sd-card-head"><h3>Claim detail <span className="cnt">{claims.length}</span></h3></div>
        {claimYearId && (
          <div style={{ padding: '14px 16px', borderBottom: '1px solid var(--line-2)', background: 'var(--surface-2)' }}>
            <div style={{ fontWeight: 600, fontSize: 13, marginBottom: 10 }}>Add claim</div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10 }}>
              <div><label style={labelStyle}>Date of loss</label><input type="date" value={claimForm.dateOfLoss ?? ''} onChange={(e) => setClaim('dateOfLoss', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Claim #</label><input value={claimForm.claimNumber ?? ''} onChange={(e) => setClaim('claimNumber', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Status</label><select value={claimForm.status} onChange={(e) => setClaim('status', e.target.value as LossClaimStatus)} style={inputStyle}><option value="Closed">Closed</option><option value="Open">Open</option></select></div>
              <div><label style={labelStyle}>Coverage</label><input value={claimForm.coverageType ?? ''} onChange={(e) => setClaim('coverageType', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Paid</label><input type="number" value={claimForm.paid} onChange={(e) => setClaim('paid', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Reserved</label><input type="number" value={claimForm.reserved} onChange={(e) => setClaim('reserved', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Expense</label><input type="number" value={claimForm.expense} onChange={(e) => setClaim('expense', e.target.value)} style={inputStyle} /></div>
              <div><label style={labelStyle}>Description</label><input value={claimForm.description ?? ''} onChange={(e) => setClaim('description', e.target.value)} style={inputStyle} /></div>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
              <button className="sd-btn primary sm" disabled={saveClaim.isPending} onClick={() => saveClaim.mutate(claimForm)}><Check size={13} /> Save claim</button>
              <button className="sd-btn outline sm" onClick={() => { setClaimYearId(null); setClaimForm(emptyClaimForm()) }}><X size={13} /> Cancel</button>
            </div>
          </div>
        )}
        {claims.length === 0 ? (
          <div style={{ padding: '28px 16px', textAlign: 'center', color: 'var(--ink-3)', fontSize: 12.5 }}>No individual claims entered yet.</div>
        ) : (
          <table className="sd-table">
            <thead><tr><th>Year</th><th>Date</th><th>Claim #</th><th>Status</th><th>Description</th><th className="num">Paid</th><th className="num">Reserved</th><th className="num">Expense</th><th className="num">Incurred</th><th /></tr></thead>
            <tbody>
              {claims.map((c) => (
                <tr key={c.id}>
                  <td className="id">{c.year}</td>
                  <td>{c.dateOfLoss ?? '-'}</td>
                  <td className="id">{c.claimNumber ?? '-'}</td>
                  <td><span className={`sd-pill ${c.status === 'Open' ? 'inprogress' : 'bound'}`}>{c.status}</span></td>
                  <td className="primary-cell">{c.description ?? c.coverageType ?? '-'}</td>
                  <td className="num">{fmtMoney(c.paid)}</td>
                  <td className="num">{fmtMoney(c.reserved)}</td>
                  <td className="num">{fmtMoney(c.expense)}</td>
                  <td className="num">{fmtMoney(c.incurred)}</td>
                  <td><button className="sd-btn ghost sm" style={{ color: 'var(--bad-fg)' }} onClick={() => { if (confirm('Remove claim?')) deleteClaim.mutate(c.id) }}><Trash2 size={12} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}
