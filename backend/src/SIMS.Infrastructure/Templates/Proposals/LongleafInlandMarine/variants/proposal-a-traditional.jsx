// Direction A — Traditional / corporate refinement.
// Sits in the same family as the existing Longleaf Flyer Master.
const { useEffect } = React;

const palA = {
  pine: "#174517",
  pineDeep: "#0f3010",
  gold: "#c9a227",
  goldDeep: "#b89a16",
  bark: "#5d4f40",
  cab: "#db504a",
  paper: "#f7f4ec",
  ink: "#1c1c1a",
  rule: "#d7d3c5",
  paperDeep: "#ece8d8",
};

function ProposalA() {
  const p = window.PROPOSAL;
  return (
    <div className="propA-sheet">
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

      {/* TITLE BAND */}
      <section className="propA-title">
        <div className="propA-kicker">
          <span>Inland Marine</span> · Proposal of Insurance · 2026
        </div>
        <h1 className="propA-h1">From stump to deck,<br/>we keep the machines moving.</h1>
        <div className="propA-titlemeta">
          <div><span>Proposal №</span><b>{p.proposalNo}</b></div>
          <div><span>Issued</span><b>{p.quoteDate}</b></div>
          <div><span>Underwriter</span><b>{p.underwriter}</b></div>
        </div>
      </section>

      {/* INSURED + POLICY */}
      <section className="propA-insured">
        <div className="propA-insured-left">
          <div className="propA-label">Prepared for</div>
          <div className="propA-insured-name">{p.insured}</div>
          <div className="propA-insured-dba">{p.dba}</div>
          <div className="propA-insured-addr">
            {p.address1}<br/>{p.address2}
          </div>
          <div className="propA-locations">{p.locations}</div>
        </div>
        <div className="propA-insured-right">
          <div className="propA-policy-row">
            <div className="propA-label">Carrier</div>
            <div className="propA-policy-val">
              <b>{p.company}</b>
              <span>{p.carrierMeta}</span>
            </div>
          </div>
          <div className="propA-policy-row twoCol">
            <div>
              <div className="propA-label">Effective</div>
              <div className="propA-policy-val"><b>{p.effFrom}</b></div>
            </div>
            <div>
              <div className="propA-label">Expires</div>
              <div className="propA-policy-val"><b>{p.effTo}</b></div>
            </div>
          </div>
        </div>
      </section>

      {/* COVERAGE GRID */}
      <section className="propA-coverage">
        <h3 className="propA-h3">Coverage Summary <span className="num">01</span></h3>
        <div className="propA-grid">
          <div className="propA-stat propA-statBig">
            <div className="propA-statLabel">Total Insured Value</div>
            <div className="propA-statVal">{p.tiv}</div>
            <div className="propA-statSub">Scheduled equipment · per attached schedule</div>
          </div>
          <div className="propA-stat">
            <div className="propA-statLabel">Per-Item Limit</div>
            <div className="propA-statVal sm">{p.perItem}</div>
          </div>
          <div className="propA-stat">
            <div className="propA-statLabel">Aggregate Limit</div>
            <div className="propA-statVal sm">{p.aggregate}</div>
          </div>
          <div className="propA-stat">
            <div className="propA-statLabel">Deductible</div>
            <div className="propA-statVal sched">{p.deductible}</div>
          </div>
        </div>

        <div className="propA-endorsements">
          <div className="propA-endo">
            <span>Debris Removal</span>
            <b>{p.debris}</b>
          </div>
          <div className="propA-endo">
            <span>Rental Reimbursement</span>
            <b>{p.rental}</b>
          </div>
          <div className="propA-endo">
            <span>Towing &amp; Storage</span>
            <b>{p.towing}</b>
          </div>
        </div>
      </section>

      {/* PREMIUM TABLE */}
      <section className="propA-premium">
        <h3 className="propA-h3">Premium &amp; Fees <span className="num">02</span></h3>
        <table className="propA-table">
          <tbody>
            {p.fees.map(([label, val], i) => (
              <tr key={i}>
                <td>{label}</td>
                <td className="num">{val}</td>
              </tr>
            ))}
            <tr className="total">
              <td>Total Annual Premium</td>
              <td className="num">{p.total}</td>
            </tr>
          </tbody>
        </table>
      </section>

      {/* CONDITIONS */}
      <section className="propA-conditions">
        <h3 className="propA-h3">Quote Conditions <span className="num">03</span></h3>
        <ul>
          {p.conditions.map((c, i) => <li key={i}>{c}</li>)}
        </ul>
      </section>

      {/* SIGNATURE */}
      <section className="propA-sign">
        <div className="propA-signLeft">
          <div className="propA-label">Coverage Bound By</div>
          <div className="propA-sigRow">
            <div className="propA-sigField">
              <div className="propA-sigLine"></div>
              <span>Printed Name</span>
            </div>
            <div className="propA-sigField">
              <div className="propA-sigLine"></div>
              <span>Authorized Signature</span>
            </div>
            <div className="propA-sigField sm">
              <div className="propA-sigLine"></div>
              <span>Date</span>
            </div>
          </div>
        </div>
        <div className="propA-signRight">
          <div className="propA-disclaimer">
            <b>Please see attached proposal for a complete breakdown of equipment, limits, and coverages.</b> This document is a proposal of insurance for the applicant listed above. It is not to be used as proof of coverage. Coverage will not be bound unless signed by an authorized representative of the applicant.
          </div>
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

window.ProposalA = ProposalA;
