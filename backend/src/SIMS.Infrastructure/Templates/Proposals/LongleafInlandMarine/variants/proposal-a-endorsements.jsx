// Optional Endorsements page — matches Direction A.

function ProposalAEndorsements() {
  const p = window.PROPOSAL;
  const endorsements = window.PROPOSAL_ENDORSEMENTS || [];
  const totalEndorsementPrem = endorsements
    .filter(e => e.included)
    .reduce((sum, e) => sum + (e.premiumNum || 0), 0);
  const fmt = n => '$' + n.toLocaleString('en-US', {minimumFractionDigits:0, maximumFractionDigits:0});

  return (
    <div className="propA-sheet endo-sheet">
      {/* HEADER */}
      <header className="propA-header">
        <div className="propA-logo">
          <img src="assets/longleaf-logo.png" alt="Longleaf Insurance"/>
        </div>
        <div className="propA-meta">
          <b>Sustainable Forestry Insurance</b>
          Glenelg, MD &nbsp;·&nbsp; +1 877 298 1318
        </div>
      </header>

      {/* TITLE band */}
      <section className="propA-title endo-title">
        <div className="propA-kicker">
          <span>Inland Marine</span> · Optional Endorsements · 2026
          <span className="endo-page">Page 3 of 5</span>
        </div>
        <h1 className="propA-h1">Optional<br/>Endorsements.</h1>
        <div className="endo-policyline">
          <div><span>Insured</span><b>{p.insured}</b></div>
          <div><span>Proposal №</span><b>{p.proposalNo}</b></div>
          <div><span>Period</span><b>{p.effFrom} — {p.effTo}</b></div>
        </div>
      </section>

      {/* NOTICE */}
      <section className="endo-notice">
        <div className="endo-noticeBar">Important</div>
        <p>
          The following endorsements <b>shall not apply unless stipulated as being included in the optional endorsements schedule of this form</b> (hereinafter "the optional endorsements Schedule") or added to the optional endorsements Schedule by endorsement prior to the time of the loss.
        </p>
      </section>

      {/* INDEX TABLE */}
      <section className="endo-table-section">
        <h3 className="propA-h3">Index of Optional Endorsements <span className="num">06</span></h3>
        <table className="endo-table">
          <thead>
            <tr>
              <th className="e-num">No.</th>
              <th className="e-name">Endorsement</th>
              <th className="e-limits">Limits of Liability</th>
              <th className="e-status">Status</th>
              <th className="e-money">Premium</th>
            </tr>
          </thead>
          <tbody>
            {endorsements.map((e, i) => (
              <tr key={i} className={e.included ? '' : 'excluded'}>
                <td className="e-num">{i + 1}</td>
                <td className="e-name">
                  <b>{e.name}</b>
                  {e.note ? <div className="e-note">{e.note}</div> : null}
                </td>
                <td className="e-limits">
                  {e.limits.map((l, j) => (
                    <div key={j} className="e-limitRow">
                      <span className="e-limitLabel">{l.label}</span>
                      <span className="e-limitVal">{l.value}</span>
                    </div>
                  ))}
                </td>
                <td className="e-status">
                  <span className={`e-statusPill ${e.included ? 'inc' : 'exc'}`}>
                    {e.included ? 'Included' : 'Excluded'}
                  </span>
                </td>
                <td className="e-money">
                  {e.included ? (e.premium || '—') : '—'}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan="4">Total Optional Endorsement Premium</td>
              <td className="e-money">{fmt(totalEndorsementPrem)}</td>
            </tr>
          </tfoot>
        </table>
      </section>

      {/* COVERAGE NOTES */}
      <section className="endo-notes">
        <h3 className="propA-h3">Notes <span className="num">07</span></h3>
        <ul>
          <li>Premiums shown above are <b>in addition</b> to the base Inland Marine premium quoted on the proposal cover.</li>
          <li>"Included" status requires the endorsement to be reflected on the bound policy schedule. "Excluded" coverages are not in force.</li>
          <li>Limits and premium are subject to underwriting review and may change based on additional information.</li>
        </ul>
      </section>

      <div className="propA-spacer"></div>

      {/* FOOTER */}
      <footer className="propA-footer">
        <div>
          <span>Specialty Market Managers, LLC</span>
          <b>3881 Ten Oaks Rd. 2E · Glenelg, MD 21737</b>
        </div>
        <div className="right">
          <span>submissions@longleaf-ins.com</span>
          <b>longleaf-ins.com</b>
        </div>
      </footer>
    </div>
  );
}

window.ProposalAEndorsements = ProposalAEndorsements;
