# FMCSA Auto Safety Plan

## Goal

Add an Auto Safety section to quote detail for auto-related lines of business. The section should give underwriters a fast, defensible view of a motor carrier's FMCSA safety profile using official SMS outputs when available and locally calculated underwriting signals when official scores are missing or stale.

Auto-related quote lines:
- AutoLiability
- AutoPhysicalDamage
- CommercialAuto

## CAB Report Reference Target

The CAB report for NAPCO TRUCKING INC / USDOT 1853487 is the underwriting benchmark for the eventual SIMS Auto Safety experience. SIMS should not try to recreate every CAB page, but the quote detail should work toward these decision-ready views:

- SAFER / OOS: 24-month overall, vehicle, driver, and hazmat inspection counts; OOS counts; OOS percentages; national average comparisons; and accident summary.
- Radius: inspection distance from the insured's primary address, distance bands, out-of-radius alerts, and map/hotspot view.
- Events: recent crashes, OOS inspections, severe violations, driver disqualifications, and reportable accident details.
- CSA / BASICs: official or imported BASIC measures, percentiles/alerts, inspection counts included in SMS, and OOS counts by BASIC where available.
- History: bi-annual inspection and violation trend buckets, MCS-150 fleet history, contact/address history, ISS history, and BASIC score history.

For the NAPCO reference report, the CAB/SAFER target values include:

- Overall OOS: 82 inspections, 21 OOS, 25.60%, national average 20.18%.
- Vehicle OOS: 77 inspections, 20 OOS, 26.00%, national average 22.26%.
- Driver OOS: 82 inspections, 3 OOS, 3.70%, national average 6.67%.
- Hazmat OOS: 0 inspections, 0 OOS, 0.00%, national average 4.44%.
- Accident summary: 0 fatal, 9 injury, 5 tow, 14 total reportable, 28.00% accident-to-power-unit ratio.
- Bi-annual inspection trend: 36-30, 30-24, 24-18, 18-12, 12-6, and 6-0 month buckets.
- BASIC summary: SMS inspection counts, driver/vehicle OOS counts, BASIC alert thresholds, and BASIC history over time.

SIMS labels should make source clear:

- Official FMCSA / SMS: values imported from official monthly SMS output or FMCSA-published datasets.
- SAFER-style: values calculated by SIMS from FMCSA inspection/crash data using the same general 24-month underwriting window.
- SIMS signal: internal rollups, trend flags, radius alerts, and UI prioritization.

## Data Sources

### DOT / Socrata API

Use DOT's Socrata API for direct data acquisition instead of manual spreadsheet download where the dataset supports API access.

Initial datasets:
- Company Census File
- Vehicle Inspection File
- Vehicle Inspections and Violations
- Crash File

The API path should be wrapped behind a local `FmcsaSocrataClient` so the rest of SIMS does not depend on Socrata query syntax or dataset column names.

### Official SMS Output Files

Use official FMCSA monthly SMS output files for defensible BASIC measures, percentiles, and prioritization flags.

Reason: SIMS can calculate useful internal measures from raw inspection and violation data, but true SMS percentiles are relative to the monthly carrier comparison population and FMCSA sufficiency rules. Official SMS output should be treated as the source of truth for displayed FMCSA SMS scores.

Planned imports:
- SMS AB Pass
- SMS C Pass
- SMS AB PassProperty
- SMS C PassProperty

## Backend Phases

### Phase 1 - Foundation

Status: started.

Scope:
- Store USDOT number on insureds.
- Add FMCSA tables for carrier snapshots, inspections, violations, crashes, scoring runs, and BASIC scores.
- Add quote-scoped Auto Safety API endpoint.
- Return clear empty states for missing DOT and no imported FMCSA data.

Verification:
- `dotnet build backend/SIMS.sln`
- `npx tsc -b`

### Phase 2 - Socrata Client

Scope:
- Add `FmcsaSocrataClient`.
- Fetch carrier census by USDOT on demand.
- Fetch inspections, violations, and crashes by USDOT with paging.
- Store import metadata: source, dataset id, row count, run status, and last refresh.
- Add a manual refresh endpoint for a single quote/insured.

Success criteria:
- Entering a USDOT number can populate or refresh carrier identity.
- A quote Auto Safety panel can display imported inspection/violation/crash counts without manual spreadsheet handling.

### Phase 3 - Official SMS Output Import

Scope:
- Add import tables or import metadata for official SMS output snapshots.
- Store monthly BASIC scores by USDOT and snapshot month.
- Mark each score with methodology/source version.
- Surface official measures, percentiles, and prioritization flags in the Auto Safety panel.

Success criteria:
- Underwriters can distinguish official FMCSA SMS scores from SIMS-calculated underwriting signals.
- Historical score snapshots can be compared month over month.

### Phase 4 - Internal Underwriting Signals

Scope:
- Calculate 24-month overall, vehicle, driver, and hazmat inspection counts.
- Calculate overall, driver, vehicle, and hazmat OOS counts and rates.
- Store or configure national average comparison values by category.
- Calculate reportable accident counts by fatal, injury, tow, and total reportable.
- Calculate accident-to-power-unit ratio where power unit count is available.
- Collapse repeated same-group violations within one inspection for internal scoring summaries.
- Show recent severe/OOS/disqualifying events.
- Show trends by month and BASIC.
- Show bi-annual trend buckets matching the CAB-style view: 36-30, 30-24, 24-18, 18-12, 12-6, and 6-0 months.

Success criteria:
- If official SMS output is not yet available for a carrier, the panel still provides useful underwriting context without presenting it as official SMS.
- For a known reference DOT such as 1853487, SIMS OOS/SAFER-style counts can be reconciled against the CAB report or SAFER page within expected source/timing differences.

### Phase 5 - Inspection Map and Radius

Scope:
- Store insured geocode fields: latitude, longitude, geocode precision.
- Store inspection geocode fields: location text, city, county, state, latitude, longitude, geocode precision.
- Calculate inspection distance from the insured's primary address.
- Add distance bands: 50, 100, 250, 500 miles.
- Add out-of-radius summary: count and percentage of inspections beyond 100 miles and beyond the submitted vehicle radius when available.
- Add an Inspection Map view inside the Auto Safety panel.

Map behavior:
- Plot inspections when coordinates are available.
- Use city/county/state geocoding as a fallback when exact coordinates are missing.
- Draw radius rings around the insured address.
- Color points by clean inspection, violation, OOS, or severe event.

Success criteria:
- Underwriters can see whether inspections cluster near expected operations or far from the submitted footprint.
- If map precision is low, the UI clearly falls back to a hotspot list instead of implying exact locations.

### Phase 6 - History and Trend Views

Scope:
- Store monthly or periodic carrier snapshots for power units, drivers, address, phone, authority status, and MCS-150 updates.
- Store monthly ISS and BASIC score snapshots where official files are available.
- Add history DTOs for fleet size, BASIC score trend, ISS trend, inspection trend, violation trend, and contact/address history.
- Add a compact History tab in Auto Safety with small trend charts and a change log.

Success criteria:
- Underwriters can see whether the carrier is growing/shrinking, whether scores are deteriorating or improving, and whether addresses/contact details changed materially.
- History is summarized first, with raw detail available only when needed.

## UI Plan

Auto Safety panel sections:
- Carrier identity: USDOT, legal name, power units, drivers, snapshot month.
- Risk summary: high/watch/acceptable/unknown.
- SAFER / OOS: overall, vehicle, driver, hazmat OOS counts/rates, national average comparison, and accident summary.
- Official SMS BASICs: measure, percentile, alert/prioritization flag, included inspections, and OOS counts.
- Events: recent severe violations, OOS inspections, driver disqualifications, and reportable crashes.
- Radius: distance bands, out-of-radius alert, hotspots, and map when coordinates exist.
- History: inspection/violation trend buckets, MCS-150 fleet history, ISS history, BASIC history, and address/contact changes.
- Data freshness: last Socrata refresh and latest official SMS month.

## Implementation Notes

- Do not query Socrata live on every quote page load.
- Cache imported data locally and refresh intentionally.
- Keep official SMS output values separate from locally calculated signals.
- Treat Socrata dataset IDs and column names as configuration or constants isolated inside the client.
- Keep the quote endpoint underwriter-focused; do not leak raw import shape to the frontend.
