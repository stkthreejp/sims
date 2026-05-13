// Schedule of Forms & Endorsements — page 4 of 5. Matches Direction A.

function ProposalAForms() {
  const p = window.PROPOSAL;
  const forms = window.PROPOSAL_FORMS || [];

  return (
    <div className="propA-sheet forms-sheet">
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
      <section className="propA-title forms-title">
        <div className="propA-kicker">
          <span>Inland Marine</span> · Schedule of Forms · 2026
          <span className="forms-page">Page 4 of 5</span>
        </div>
        <h1 className="propA-h1">Forms &amp;<br/>Endorsements.</h1>
        <div className="forms-policyline">
          <div><span>Named Insured</span><b>{p.insured}</b></div>
          <div><span>Policy №</span><b>{p.proposalNo}</b></div>
          <div><span>Effective</span><b>{p.effFrom}</b></div>
          <div><span>Agency</span><b>Specialty Market Managers, LLC</b></div>
        </div>
      </section>

      {/* FORMS TABLE */}
      <section className="forms-table-section">
        <h3 className="propA-h3">Schedule of Forms &amp; Endorsements <span className="num">10</span></h3>
        <p className="forms-intro">
          The forms and endorsements listed below are attached to and form part of the bound policy.
        </p>
        <table className="forms-table">
          <thead>
            <tr>
              <th className="f-num">No.</th>
              <th className="f-form">Form Number</th>
              <th className="f-edition">Edition</th>
              <th className="f-title">Title</th>
            </tr>
          </thead>
          <tbody>
            {forms.map((f, i) => (
              <tr key={i}>
                <td className="f-num">{String(i + 1).padStart(2, '0')}</td>
                <td className="f-form">{f.form}</td>
                <td className="f-edition">{f.edition || '—'}</td>
                <td className="f-title">{f.title}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="forms-count">
          <span>Total forms attached</span>
          <b>{forms.length}</b>
        </div>
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

window.ProposalAForms = ProposalAForms;
