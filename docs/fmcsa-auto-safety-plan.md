# FMCSA Auto Safety Plan

## Goal

Add an Auto Safety section to quote detail for auto-related lines of business. The section should give underwriters a fast, defensible view of a motor carrier's FMCSA safety profile using official SMS outputs when available and locally calculated underwriting signals when official scores are missing or stale.

Auto-related quote lines:
- AutoLiability
- AutoPhysicalDamage
- CommercialAuto

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
- Calculate 24-month inspection count.
- Calculate driver and vehicle OOS rates.
- Collapse repeated same-group violations within one inspection for internal scoring summaries.
- Show recent severe/OOS/disqualifying events.
- Show trends by month and BASIC.

Success criteria:
- If official SMS output is not yet available for a carrier, the panel still provides useful underwriting context without presenting it as official SMS.

### Phase 5 - Inspection Map and Radius

Scope:
- Store insured geocode fields: latitude, longitude, geocode precision.
- Store inspection geocode fields: location text, city, county, state, latitude, longitude, geocode precision.
- Calculate inspection distance from the insured's primary address.
- Add distance bands: 50, 100, 250, 500 miles.
- Add an Inspection Map view inside the Auto Safety panel.

Map behavior:
- Plot inspections when coordinates are available.
- Use city/county/state geocoding as a fallback when exact coordinates are missing.
- Draw radius rings around the insured address.
- Color points by clean inspection, violation, OOS, or severe event.

Success criteria:
- Underwriters can see whether inspections cluster near expected operations or far from the submitted footprint.
- If map precision is low, the UI clearly falls back to a hotspot list instead of implying exact locations.

## UI Plan

Auto Safety panel sections:
- Carrier identity: USDOT, legal name, power units, drivers, snapshot month.
- Risk summary: high/watch/acceptable/unknown.
- Official SMS BASICs: measure, percentile, prioritization flag.
- SIMS underwriting signals: OOS intensity, severe events, inspection recency.
- Geographic view: distance bands, hotspots, and map when coordinates exist.
- Data freshness: last Socrata refresh and latest official SMS month.

## Implementation Notes

- Do not query Socrata live on every quote page load.
- Cache imported data locally and refresh intentionally.
- Keep official SMS output values separate from locally calculated signals.
- Treat Socrata dataset IDs and column names as configuration or constants isolated inside the client.
- Keep the quote endpoint underwriter-focused; do not leak raw import shape to the frontend.
