// Equipment schedule page — page 2 of the proposal.
// Styled to match Direction A: pine header, paper title band, gold accents.

function ProposalASchedule() {
  const p = window.PROPOSAL;
  const eq = window.PROPOSAL_EQUIPMENT || [];
  const lossPayees = window.PROPOSAL_LOSS_PAYEES || [];

  return (
    <div className="propA-sheet schedule-sheet">
      {/* HEADER (same as cover) */}
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
      <section className="propA-title sched-title">
        <div className="propA-kicker">
          <span>Inland Marine</span> · Equipment Schedule · 2026
          <span className="sched-page">Page 2 of 5</span>
        </div>
        <h1 className="propA-h1">Scheduled<br/>Equipment.</h1>
        <div className="sched-policyline">
          <div>
            <span>Insured</span>
            <b>{p.insured}</b>
          </div>
          <div>
            <span>Proposal №</span>
            <b>{p.proposalNo}</b>
          </div>
          <div>
            <span>Period</span>
            <b>{p.effFrom} — {p.effTo}</b>
          </div>
          <div>
            <span>Total Insured Value</span>
            <b>{p.tiv}</b>
          </div>
        </div>
      </section>

      {/* EQUIPMENT SCHEDULE */}
      <section className="sched-table-section">
        <h3 className="propA-h3">Equipment Schedule <span className="num">04</span></h3>
        <table className="sched-table">
          <thead>
            <tr>
              <th className="c-num">No.</th>
              <th className="c-year">Year</th>
              <th className="c-make">Make</th>
              <th className="c-type">Type</th>
              <th className="c-model">Model</th>
              <th className="c-serial">Serial #</th>
              <th className="c-money">Value</th>
              <th className="c-basis">Basis</th>
              <th className="c-pct">Co-Ins</th>
              <th className="c-money">Ded.</th>
              <th className="c-money">Premium</th>
            </tr>
          </thead>
          <tbody>
            {eq.map((row, i) => (
              <tr key={i}>
                <td className="c-num">{row.no}</td>
                <td className="c-year">{row.year}</td>
                <td className="c-make">{row.make}</td>
                <td className="c-type">{row.type}</td>
                <td className="c-model">{row.model}</td>
                <td className="c-serial">{row.serial}</td>
                <td className="c-money">{row.stated}</td>
                <td className="c-basis">{row.basis}</td>
                <td className="c-pct">{row.coIns}</td>
                <td className="c-money">{row.ded}</td>
                <td className="c-money">{row.prem}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan="10">Total Inland Marine Premium</td>
              <td className="c-money">{p.fees[0][1]}</td>
            </tr>
          </tfoot>
        </table>
      </section>

      {/* LOSS PAYEES */}
      <section className="sched-payees">
        <h3 className="propA-h3">Loss Payees <span className="num">05</span></h3>
        <table className="payees-table">
          <thead>
            <tr>
              <th className="lp-item">Item</th>
              <th className="lp-name">Loss Payee</th>
              <th className="lp-addr">Address</th>
              <th className="lp-city">City</th>
              <th className="lp-state">State</th>
              <th className="lp-zip">Zip</th>
            </tr>
          </thead>
          <tbody>
            {lossPayees.map((lp, i) => (
              <tr key={i}>
                <td className="lp-item">{lp.item}</td>
                <td className="lp-name">{lp.name}</td>
                <td className="lp-addr">{lp.addr}</td>
                <td className="lp-city">{lp.city}</td>
                <td className="lp-state">{lp.state}</td>
                <td className="lp-zip">{lp.zip}</td>
              </tr>
            ))}
          </tbody>
        </table>
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

window.ProposalASchedule = ProposalASchedule;
