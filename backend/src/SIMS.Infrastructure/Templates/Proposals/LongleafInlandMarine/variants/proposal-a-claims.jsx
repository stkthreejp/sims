// Claims Instructions page — page 4 of the proposal. Matches Direction A.

function ProposalAClaims() {
  const p = window.PROPOSAL;
  return (
    <div className="propA-sheet claims-sheet">
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
      <section className="propA-title claims-title">
        <div className="propA-kicker">
          <span>Inland Marine</span> · Claims Instructions · 2026
          <span className="claims-page">Page 5 of 5</span>
        </div>
        <h1 className="propA-h1">In the event<br/>of a claim.</h1>
        <p className="claims-lede">
          In the event of a Loss and in accordance with the Claims Notification provision as stated in the policy wording, the Insured <b>must notify the Insurance Advisor as soon as possible.</b>
        </p>
      </section>

      {/* CONTACT CARD */}
      <section className="claims-contact-section">
        <h3 className="propA-h3">Claims Handler <span className="num">08</span></h3>
        <div className="claims-contact">
          <div className="claims-contactLeft">
            <div className="claims-contactLabel">Report All Losses To</div>
            <div className="claims-contactName">Equipment Claims Management, LLC</div>
          </div>
          <div className="claims-contactGrid">
            <div className="claims-contactItem">
              <span>Phone</span>
              <b>901 · 757 · 7976</b>
            </div>
            <div className="claims-contactItem">
              <span>Email</span>
              <b>claims@ecmclaims.com</b>
            </div>
            <div className="claims-contactItem wide">
              <span>Address</span>
              <b>2129 South Germantown Road, Suite 219<br/>Germantown, TN 38138 · USA</b>
            </div>
          </div>
        </div>
      </section>

      {/* DOCUMENTS NEEDED */}
      <section className="claims-docs-section">
        <h3 className="propA-h3">Documents to Provide <span className="num">09</span></h3>
        <p className="claims-docs-intro">
          To speed up handling, please provide the following to the Insurance Advisor or appointed Loss Adjustor as soon as possible after the loss:
        </p>
        <ol className="claims-docs">
          <li>
            <span className="claims-docNum">01</span>
            <div>
              <b>Registration &amp; title documents</b>
              <span>For the equipment or vehicle(s) involved in the loss.</span>
            </div>
          </li>
          <li>
            <span className="claims-docNum">02</span>
            <div>
              <b>Operator employment or lease agreement</b>
              <span>For the driver/operator involved, with up-to-date driver's license and Motor Vehicle Record.</span>
            </div>
          </li>
          <li>
            <span className="claims-docNum">03</span>
            <div>
              <b>Police incident report</b>
              <span>If issued — or information needed for the Insurer to obtain it.</span>
            </div>
          </li>
          <li>
            <span className="claims-docNum">04</span>
            <div>
              <b>Other applicable insurance</b>
              <span>Identification of any other insurance available to respond to the loss.</span>
            </div>
          </li>
          <li>
            <span className="claims-docNum">05</span>
            <div>
              <b>Lien holders &amp; loss payees</b>
              <span>Any parties with a financial interest in the equipment or vehicle(s).</span>
            </div>
          </li>
        </ol>
        <div className="claims-additional">
          Additional documents may be requested as the loss adjuster progresses with the investigation.
        </div>
      </section>

      {/* IMPORTANT WARNINGS */}
      <section className="claims-warnings-section">
        <div className="claims-warning">
          <div className="claims-warningBar">Duty to Co-operate</div>
          <p>The Insured has a duty to co-operate with the Loss Adjuster and any representative of the Insurer.</p>
        </div>
        <div className="claims-warning red">
          <div className="claims-warningBar red">Failure to Comply</div>
          <p>Failure to provide documentation or co-operate <b>shall invalidate the claim</b> under this policy.</p>
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

window.ProposalAClaims = ProposalAClaims;
