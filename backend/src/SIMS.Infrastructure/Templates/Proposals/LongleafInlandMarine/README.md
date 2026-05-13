# Longleaf Inland Marine Proposal — Direction A

Complete 5-page proposal package. Letter (8.5 × 11) print-ready. All pages share the same brand system.

## Pages

| File                | Content                                                       |
|---------------------|---------------------------------------------------------------|
| `index.html`        | Full 5-page document (all pages stacked, prints with breaks)  |
| `cover.html`        | **Page 1** — Proposal cover (insured, coverage, premium, sign)|
| `schedule.html`     | **Page 2** — Equipment schedule + loss payees                 |
| `endorsements.html` | **Page 3** — Optional endorsements (include/exclude)          |
| `forms.html`        | **Page 4** — Schedule of forms & endorsements                 |
| `claims.html`       | **Page 5** — Claims instructions + handler contact            |

## Files

```
index.html
cover.html / schedule.html / endorsements.html / forms.html / claims.html
variants/
  proposal-a.css                          ← shared header/footer + cover styles
  proposal-a-traditional.jsx              ← cover component
  proposal-a-schedule.css / .jsx          ← schedule page
  proposal-a-endorsements.css / .jsx       ← endorsements page
  proposal-a-forms.css / .jsx             ← forms schedule page
  proposal-a-claims.css / .jsx            ← claims page
  data.js                                 ← shared proposal data (insured, dates, premium)
  data-schedule.js                        ← equipment + loss payees
  data-endorsements.js                    ← optional endorsements
  data-forms.js                           ← forms & endorsements list
assets/
  longleaf-logo.png                       ← brand logo
```

## Data wiring

Each page reads one or more globals defined in the `data*.js` files:

- `window.PROPOSAL` — proposal metadata: insured, dates, totals, fees, conditions
- `window.PROPOSAL_EQUIPMENT` — schedule rows
- `window.PROPOSAL_LOSS_PAYEES` — loss payees
- `window.PROPOSAL_ENDORSEMENTS` — optional endorsements
- `window.PROPOSAL_FORMS` — attached forms & endorsements list

Replace sample values with live data (or merge tags) at integration time.

## Fonts

Loaded from Google Fonts at runtime:
- **Barlow Condensed** (display) — 500/700/900, italic
- **Source Sans 3** (body) — 400/500/600/700

If your environment can't reach Google Fonts, self-host both families and update the `<link>` tags.

## To print / save as PDF

Open `index.html`, then Cmd+P / Ctrl+P → "Save as PDF" → Letter, no margins. All five pages print with automatic breaks.
