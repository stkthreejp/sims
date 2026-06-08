# SIMS UI Guide

The visual contract between design and engineering for SIMS.

## What's in here

- **`SIMS UI Guide.html`** — open this in a browser. The full brand & UI guide, top-to-bottom: foundations, components, patterns, page recipes, and the "avoid this" list. Live rendered examples sit next to every spec.
- **`tokens.css`** — the locked design vocabulary. Every color, type size, radius, shadow, and spacing value referenced in the guide. **This is canonical.** Mirror its names into the app's theme system (CSS vars, Tailwind config, TS theme module — whatever fits the stack).
- **`assets/smm-symbol.png`** — the brand mark used in the sidebar.

## How to use it

1. Open `SIMS UI Guide.html` in any modern browser. No build step, no server needed.
2. Read top-to-bottom once. Refer back by section number when building.
3. When you hit a need the guide doesn't cover — a new token, a new component, a new layout — **stop**. Propose the addition (name + value + rationale), land it in `tokens.css` first, then use it.

## The rule

Nothing visual ships in code that isn't either documented here or added to `tokens.css` first. As long as both sides say `--accent-ink` and mean `#064778`, design and code stay in sync.

---

_v1.0 · May 2026_
