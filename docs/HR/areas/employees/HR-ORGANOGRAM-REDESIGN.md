# Organogram — the redesigned screen at `/hr/organogram`

> **Status: BUILT and VERIFIED to the API level 2026-09-08. Browser walk still outstanding (§9).**
> Replaces the slice-4 renderer described in the Tier B tail survey. Triggered by demo feedback
> (connectors illegible, no full-screen view). Frontend: `frontend/src/components/hr/organogram/`.
> Backend: three additive fields on `OrganogramNodeDto`, no migration.

## 1. What changed and why

| Feedback / gap | Before | Now |
|---|---|---|
| Connecting lines not legible | 1px CSS borders in the theme's hairline `--border` colour (near-white on light) | 2px SVG strokes in a dedicated ink (`#6b6b66` light, `#a3a29b` dark), rounded elbows, **non-scaling** so they stay 2px at every zoom. Dotted advisory lines rendered for the first time. |
| No full view | Zoom was a CSS transform inside a scrolling card, no pan | Pan by drag, wheel zoom around the cursor, pinch, fit-to-screen, minimap, zoom slider. **Full screen** pins the chart over the page and fullscreens the document. |
| Wide rows unreadable | 6,105 people under one root rendered as one row | Childless siblings stack into columns with a spine and stubs (classic idiom). Rows still page at 40 with a "Show more" card. |
| No way to find things | Filter-to-hits only | Hit stepper (n of m, Enter / Shift+Enter), matches-only or highlight-in-place, focus-on-branch with breadcrumb, keyboard navigation. |
| No print / export | None | PNG, PDF (A4 / A3 fitted, or one page at actual size), browser print, Excel, CSV. Scope: what is on screen, selected branch fully expanded, or the whole chart. Title block with tenant, view, filters, timestamp. |
| Thin detail | Flat key/value list | Drawer with KPIs, placement chain, direct children, position holders, contact links, deep links to the record, focus and export actions. |
| Nothing to compare | — | Colour by level (default), headcount (units, teams), vacancy status (positions, units, teams), span of control (people). Legend always names what colour means. |
| Not shareable | — | Dimension, structure, focus, selection, depth, layout, density, colouring, search and filters live in the URL. "Copy link to this box" on every card. |

## 2. Architecture

```
page.tsx                 data (react-query), URL state, coverage strip, dimension tabs
└─ OrgChart.tsx          orchestrator: overrides, paging, search, focus, fullscreen, export
   ├─ visible-tree.ts    data forest → what renders (open/closed, filters, paging, pins)   [pure]
   ├─ layout.ts          tidy-tree layout for fixed-size cards, stacking, transposition     [pure]
   ├─ heat.ts            level bands, heat scales, colour tokens                            [pure]
   ├─ view-state.ts      URL ⇄ view state                                                   [pure]
   ├─ export-data.ts     tree → rows/columns for Excel and CSV                              [pure]
   ├─ OrgCanvas.tsx      pan/zoom viewport, culling, SVG edge layer
   ├─ OrgNodeCard.tsx    one card, three densities, toggle, actions menu
   ├─ OrgDetailPanel.tsx the drawer
   ├─ OrgToolbar.tsx     search + view menu + zoom + export + fullscreen
   ├─ OrgLegend.tsx / OrgMinimap.tsx
   ├─ StaticOrgChart.tsx the export document (always light, hex only)
   ├─ OrgExportDialog.tsx / export.ts (html2canvas, jsPDF, xlsx, print window)
   └─ org-styles.ts      one stylesheet string, rendered in a <style> tag
```

Cards are a fixed size per density, so the layout is computed without measuring the DOM and the
export is laid out by the same numbers as the screen. The pure modules have vitest coverage
(`*.test.ts`, 32 assertions).

## 3. Guards on live data

TDC's live people dimension is 6,287 nodes with 6,105 under one root. The screen never mounts more
than the viewport plus a margin, and:

- subtrees below "Levels open" mount nothing until opened;
- any row wider than 40 pages behind a "Show more" card; only search hits and the selection are pinned past the page;
- "Expand everything" and "Expand everything below" refuse above 2,500 cards with a toast;
- image export refuses above 2,500 cards; Excel and CSV still run for any size.

## 4. Backend enrichment (additive, no migration)

| Field | Dimension | Meaning |
|---|---|---|
| `positionCount` | units | active posts attached to the unit |
| `vacantPositionCount` | units | of those, posts nobody on strength holds — the positions view's own "Vacant" predicate |
| `holders[]`, `holdersTruncated` | positions | names of on-strength holders, capped at 25 (`OrganogramNodeDto.HoldersCap`) |
| `meta.Phone`, `meta.Extension`, `meta["Staff level"]`, `meta["Date employed"]` | people | drawer detail; the dimension is already `HR.Employee.Read`-gated |

Harness: `dev-harness/hr-tierb-tail/run-organogram-enrichment.mjs` (admin, read-only). Cross-checks
the unit post counts against the positions chart so the two views cannot disagree on "vacant".

## 5. Decisions recorded

1. **Engine: custom SVG + HTML**, not React Flow. Full control of export fidelity, no measurement
   round-trip, the collapse/paging logic carried over untouched, and React Flow 11 is deprecated
   upstream.
2. **People export never carries email.** `export-data.ts` strips the `Email` meta key; the About
   sheet says so. Image exports of People are allowed (the on-screen gate already applies).
3. **Enrichment shipped in the same slice**, since the drawer's substance is where the screen
   earns "enterprise".
4. **Full screen fullscreens the document, not the chart element.** Radix menus and selects portal
   to `<body>`; a fullscreen subtree cannot show them. The chart pins itself over the page
   (`.org-immersive`, z-index 50) and asks for document fullscreen; either half working is fine.
5. **Colour palette** is the validated reference data-viz palette (categorical eight, blue
   sequential ramp, four status colours), checked against the app's card surfaces on both themes.
   Status colours always ship with an icon and a word.

## 6. URL parameters

`dim` (units | positions | people | teams | locations), `structure` (locations only), `focus`,
`sel`, `depth` (1–5, 99), `layout` (vertical | horizontal), `density` (compact | comfortable |
detailed), `heat` (none | headcount | fill | span), `q`, `stack=0`, `vacant=1`, `active=1`.
Only values that differ from the defaults are written.

## 7. Keyboard

Arrows move between parent, child and siblings (rotated in the horizontal layout); Enter or Space
toggles; `F` fits; `+` / `−` zoom; `/` focuses search; Home selects the top; Esc closes the
drawer, then clears the selection, then leaves full screen.

## 8. Verification run 2026-09-08

Backend built by the user; API started in Staging on port 5000 per
[[hr-harness-run-environment]]; harness run **twice, 27 passed / 0 failed both times**.

What the harness proved beyond field presence:

- unit post counts and vacant-post counts agree with the positions chart **per unit**, not only in
  aggregate, across all 41 uniquely named units. An aggregate check alone can net two opposite
  errors to zero, so the per-unit form is the one that matters.
- every position's holder list is exactly `min(employeeCount, 25)` long, `holdersTruncated` is set
  iff the count exceeds the cap, and **no holder name contains an `@`** — the privacy rule that
  keeps the open positions view from becoming a contact list.
- holders resolve back to people holding that post: 30 of 30 in a 20-post sample.
- no dimension grew another dimension's fields (units carry no `holders`, positions and people
  carry no `positionCount`), and the synthetic root carries no enrichment at all.

Frontend: 32 vitest assertions passing, ESLint clean, no TypeScript errors in the slice, and the
route compiles and serves 200 under `next dev` — both bare and with every view parameter set
(`?dim=people&layout=horizontal&density=detailed&heat=span&depth=3&q=men&vacant=1`).

### Shape of this database (the UAT/demo dataset, not the 6,286-employee TDC extract)

| Dimension | Nodes | Unlinked | Depth | Widest row |
|---|---|---|---|---|
| Units | 41 | 2 | 5 | 6 |
| Positions | 149 | 28 | 7 | 28 |
| People | 184 | 84 | 6 | 84 |
| Teams | **0** | — | — | — |
| Locations | 11 | — | — | — |

Two consequences worth knowing before a demo:

- **The stacked-leaves layout is exercised here.** People has an 84-wide row under the synthetic
  root and Positions a 28-wide one; both stack into columns instead of one long row.
- **People is 46% unlinked, just under the 50% threshold**, so the strong "most people are not
  placed" alert does *not* fire — the softer one-line note does. That is the intended behaviour at
  this ratio, not a bug, but it means the coverage message differs from what the full TDC extract
  would show.
- **Teams returns nothing on this database**, so that tab shows its empty state. A data question,
  not a code one — seed teams before demonstrating the tab.

## 9. Not done / to verify in a browser

- Not browser-walked in this session (no browser automation available). Verify: drag/zoom feel,
  card menus opening in full screen, PNG/PDF output legibility, print scaling on A3, Excel columns.
- The People view on the live 6,286-employee dataset: confirm the stacked-leaves layout of the
  synthetic root is readable and that "Show more" paging feels right.
- The demo runbook (out of repo) should name the new controls: View menu, Export, Full screen.
