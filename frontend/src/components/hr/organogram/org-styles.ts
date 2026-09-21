import { ORG_COLOR_TOKENS_DARK, ORG_COLOR_TOKENS_LIGHT } from './heat';

/**
 * The chart's stylesheet, as one string rendered in a <style> tag by OrgChart so the whole
 * feature stays one folder to delete. Class names are prefixed `org-` and never collide with
 * Tailwind utilities.
 *
 * Legibility decisions, since that was the demo feedback:
 *   - connectors are 2px in a dedicated ink (#6b6b66 on light, #a3a29b on dark — roughly 5:1 and
 *     7:1 against the canvas), never the theme's hairline border colour, and never thinner than
 *     2px on screen at any zoom (the SVG uses non-scaling strokes);
 *   - the selected node's path to the root is drawn heavier in the accent blue;
 *   - every card carries a 5px level band and the name is 14px semibold minimum;
 *   - vacant cards are dashed with a faint diagonal hatch, so the state reads without colour.
 */
export const ORG_CSS = `
.org-root {
  position: relative;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border);
  border-radius: 0.75rem;
  background: var(--org-canvas);
  overflow: hidden;
  ${ORG_COLOR_TOKENS_LIGHT}
}
.dark .org-root { ${ORG_COLOR_TOKENS_DARK} }
/* z-index 50 on purpose: the app header and sidebar are z-50 and earlier in the DOM, so this wins
   over them by order; Radix menus, selects and dialogs are z-50 and portalled to the end of <body>,
   so they win over this. A higher value here would put every menu underneath the chart. */
.org-root.org-immersive { position: fixed; inset: 0; z-index: 50; border-radius: 0; border: 0; }

.org-toolbar { padding: 0.5rem 0.75rem; border-bottom: 1px solid var(--border); background: var(--card); }
.org-toolbar-row { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }
.org-toolbar-group { display: flex; align-items: center; border: 1px solid var(--border); border-radius: 0.5rem; background: var(--background); }
.org-focus-crumbs { display: flex; align-items: center; gap: 0.125rem; padding: 0 0.375rem 0 0.125rem; border: 1px dashed var(--border); border-radius: 0.5rem; height: 2.25rem; max-width: 100%; overflow: hidden; }

.org-stage { position: relative; flex: 1; min-height: 0; }
.org-canvas {
  position: absolute; inset: 0; overflow: hidden; cursor: grab; outline: none; touch-action: none; user-select: none; -webkit-user-select: none;
  background-color: var(--org-canvas);
  background-image: radial-gradient(var(--org-grid) 1px, transparent 1px);
  background-size: 24px 24px;
}
.org-canvas:focus-visible { box-shadow: inset 0 0 0 2px var(--primary); }
.org-canvas-dragging { cursor: grabbing; }
.org-canvas-dragging .org-card { transition: none; }
.org-world { position: absolute; left: 0; top: 0; transform-origin: 0 0; will-change: transform; }
.org-edges { position: absolute; left: 0; top: 0; overflow: visible; pointer-events: none; }
.org-edge { fill: none; stroke: var(--org-edge); stroke-width: 2px; stroke-linecap: round; stroke-linejoin: round; transition: stroke 0.15s; }
.org-edge-dotted { stroke-dasharray: 6 5; }
.org-edge-path { stroke: var(--org-edge-path); stroke-width: 3px; }
.org-edge-dim { stroke: var(--org-edge-dim); }

.org-card {
  position: absolute; left: 0; top: 0; box-sizing: border-box;
  border-radius: 0.625rem; background: var(--card); color: var(--card-foreground);
  border: 1.5px solid var(--border);
  box-shadow: 0 1px 2px rgba(0,0,0,0.06), 0 1px 1px rgba(0,0,0,0.04);
  transition: transform 0.28s cubic-bezier(0.2, 0.8, 0.2, 1), box-shadow 0.15s, opacity 0.2s, border-color 0.15s;
  --org-accent: var(--org-cat-1);
}
.org-card::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 5px; border-radius: 0.5rem 0 0 0.5rem; background: var(--org-accent); }
.org-card:hover { box-shadow: 0 4px 14px rgba(0,0,0,0.10); border-color: color-mix(in srgb, var(--org-accent) 55%, var(--border)); }
.org-card-heat { background: color-mix(in srgb, var(--org-accent) 10%, var(--card)); }
.org-card-selected { border-color: var(--primary); box-shadow: 0 0 0 3px color-mix(in srgb, var(--primary) 30%, transparent), 0 6px 18px rgba(0,0,0,0.12); z-index: 1; }
.org-card-path { border-color: var(--org-edge-path); }
.org-card-hit { box-shadow: 0 0 0 3px var(--org-hit); }
.org-card-hit.org-card-selected { box-shadow: 0 0 0 3px var(--org-hit), 0 0 0 6px color-mix(in srgb, var(--primary) 35%, transparent); }
.org-card-dimmed { opacity: 0.35; }
.org-card-vacant { border-style: dashed; background: repeating-linear-gradient(135deg, transparent 0 10px, color-mix(in srgb, var(--org-status-critical) 6%, transparent) 10px 12px), var(--card); }
.org-card-vacant .org-card-name { color: var(--muted-foreground); }
.org-card-inactive { opacity: 0.6; filter: grayscale(0.4); }
.org-card-dimmed.org-card-inactive { opacity: 0.25; }
.org-card-synthetic { border-style: dashed; background: transparent; --org-accent: var(--org-edge-dim); }

.org-card-body { display: flex; flex-direction: column; justify-content: space-between; width: 100%; height: 100%; padding: 0.5rem 0.625rem 0.5rem 0.875rem; text-align: left; cursor: pointer; background: none; border: 0; color: inherit; border-radius: inherit; }
.org-card-body:focus-visible { outline: 2px solid var(--primary); outline-offset: -2px; }
.org-card-compact .org-card-body { justify-content: center; padding: 0.375rem 0.5rem 0.375rem 0.75rem; }
.org-card-name { font-weight: 600; font-size: 0.875rem; line-height: 1.2; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.org-card-compact .org-card-name { font-size: 0.8125rem; }
.org-card-detailed .org-card-name { font-size: 0.9375rem; }
.org-card-title, .org-card-line3 { font-size: 0.75rem; color: var(--muted-foreground); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: 0.125rem; }
.org-card-detailed .org-card-title { font-size: 0.8125rem; }
.org-card-foot { display: flex; align-items: center; gap: 0.25rem; margin-top: 0.25rem; min-height: 1.125rem; }

.org-avatar { display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; width: 2.25rem; height: 2.25rem; border-radius: 9999px; font-size: 0.75rem; font-weight: 700; color: #fff; background: var(--org-accent); letter-spacing: 0.02em; }
.org-avatar-sm { width: 1.75rem; height: 1.75rem; font-size: 0.6875rem; }
.org-avatar-lg { width: 2.75rem; height: 2.75rem; font-size: 0.875rem; background: var(--primary); color: var(--primary-foreground); }

.org-chip { display: inline-flex; align-items: center; gap: 0.2rem; border-radius: 0.375rem; padding: 0 0.375rem; height: 1.125rem; font-size: 0.6875rem; line-height: 1; background: var(--muted); color: var(--muted-foreground); white-space: nowrap; }
.org-chip-code { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; letter-spacing: 0.01em; }
.org-chip-vacant { color: var(--org-status-critical); background: color-mix(in srgb, var(--org-status-critical) 12%, transparent); }
.org-chip-count { background: transparent; padding-right: 0; }
.org-chip-count strong { color: var(--foreground); font-weight: 700; }
.org-chip-count-compact { margin-left: auto; }

.org-card-menu { position: absolute; top: 0.25rem; right: 0.25rem; display: inline-flex; align-items: center; justify-content: center; width: 1.5rem; height: 1.5rem; border-radius: 0.375rem; border: 0; background: var(--card); color: var(--muted-foreground); opacity: 0; transition: opacity 0.15s, background 0.15s; cursor: pointer; z-index: 2; }
.org-card:hover .org-card-menu, .org-card:focus-within .org-card-menu, .org-card-selected .org-card-menu, .org-card-menu[data-state="open"] { opacity: 1; }
.org-card-menu:hover { background: var(--accent); color: var(--foreground); }

.org-toggle { position: absolute; display: inline-flex; align-items: center; gap: 0.125rem; height: 1.375rem; padding: 0 0.45rem 0 0.3rem; border-radius: 9999px; border: 1.5px solid var(--org-edge); background: var(--card); color: var(--foreground); font-size: 0.6875rem; font-weight: 600; font-variant-numeric: tabular-nums; cursor: pointer; z-index: 2; box-shadow: 0 1px 2px rgba(0,0,0,0.08); transition: background 0.15s, border-color 0.15s; }
.org-toggle:hover { background: var(--accent); border-color: var(--org-edge-path); }
.org-toggle-bottom { left: 50%; bottom: -0.6875rem; transform: translateX(-50%); }
.org-toggle-right { top: 50%; right: -0.75rem; transform: translateY(-50%); }

.org-card-more { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.125rem; border: 1.5px dashed var(--org-edge); background: color-mix(in srgb, var(--card) 70%, transparent); cursor: pointer; color: var(--foreground); }
.org-card-more::before { display: none; }
.org-card-more:hover { background: var(--accent); }

.org-panel { position: absolute; top: 0; right: 0; bottom: 0; width: min(24rem, 100%); background: var(--card); border-left: 1px solid var(--border); box-shadow: -8px 0 24px rgba(0,0,0,0.08); transform: translateX(105%); transition: transform 0.25s ease; display: flex; flex-direction: column; z-index: 5; }
.org-panel-open { transform: translateX(0); }
.org-panel-header { padding: 1rem 1rem 0.75rem; border-bottom: 1px solid var(--border); }
.org-panel-body { flex: 1; overflow: auto; padding: 0.75rem 1rem 1rem; display: flex; flex-direction: column; gap: 1rem; }
.org-panel-body section h4 { font-size: 0.6875rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--muted-foreground); font-weight: 600; margin-bottom: 0.375rem; }
.org-panel-footer { display: flex; flex-wrap: wrap; gap: 0.5rem; padding: 0.75rem 1rem; border-top: 1px solid var(--border); }
.org-kpis { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.5rem; }
.org-kpi { border: 1px solid var(--border); border-radius: 0.5rem; padding: 0.5rem 0.625rem; display: flex; flex-direction: column; gap: 0.125rem; background: var(--background); }
.org-kpi-value { font-size: 1.25rem; font-weight: 600; line-height: 1.1; }
.org-kpi-label { font-size: 0.6875rem; color: var(--muted-foreground); }
.org-crumbs { display: flex; flex-wrap: wrap; align-items: center; gap: 0.125rem; font-size: 0.8125rem; }
.org-crumbs li { display: inline-flex; align-items: center; gap: 0.125rem; }
.org-crumb { color: var(--foreground); background: none; border: 0; padding: 0.125rem 0.25rem; border-radius: 0.25rem; cursor: pointer; text-align: left; }
.org-crumb:hover { background: var(--accent); text-decoration: underline; }
.org-crumb-current { font-weight: 600; }
.org-panel-list { display: flex; flex-direction: column; gap: 0.125rem; font-size: 0.8125rem; }
.org-panel-list-plain li { padding: 0.125rem 0; }
.org-panel-row { display: flex; align-items: center; gap: 0.5rem; width: 100%; text-align: left; padding: 0.375rem 0.5rem; border-radius: 0.375rem; background: none; border: 0; color: inherit; cursor: pointer; }
.org-panel-row:hover { background: var(--accent); }
.org-panel-dl { display: grid; grid-template-columns: minmax(6rem, auto) 1fr; gap: 0.25rem 0.75rem; font-size: 0.8125rem; }
.org-panel-dl dt { color: var(--muted-foreground); }
.org-panel-dl dd { overflow-wrap: anywhere; }

.org-legend { position: absolute; left: 0.75rem; bottom: 0.75rem; z-index: 4; display: flex; gap: 1rem; padding: 0.625rem 0.75rem; border-radius: 0.625rem; border: 1px solid var(--border); background: color-mix(in srgb, var(--card) 92%, transparent); backdrop-filter: blur(6px); font-size: 0.75rem; max-width: min(36rem, calc(100% - 1.5rem)); }
.org-legend h4 { font-size: 0.6875rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--muted-foreground); font-weight: 600; margin-bottom: 0.25rem; }
.org-legend ul { display: flex; flex-direction: column; gap: 0.2rem; }
.org-legend li { display: flex; align-items: center; gap: 0.375rem; white-space: nowrap; }
.org-legend-swatch { display: inline-block; width: 0.75rem; height: 0.75rem; border-radius: 0.1875rem; flex-shrink: 0; }
.org-legend-muted { color: var(--muted-foreground); }
.org-legend-line { stroke: var(--org-edge); stroke-width: 2; fill: none; }
.org-legend-line-dotted { stroke-dasharray: 5 4; }
.org-legend-box { display: inline-block; width: 0.875rem; height: 0.625rem; border-radius: 0.125rem; border: 1.5px solid var(--org-edge); }
.org-legend-box-vacant { border-style: dashed; }
.org-legend-box-inactive { opacity: 0.5; }

.org-minimap-wrap { position: absolute; right: 0.75rem; bottom: 0.75rem; z-index: 4; border-radius: 0.625rem; border: 1px solid var(--border); background: color-mix(in srgb, var(--card) 92%, transparent); backdrop-filter: blur(6px); padding: 0.25rem; overflow: hidden; transition: right 0.25s ease; }
.org-root-panel-open .org-minimap-wrap { right: calc(min(24rem, 100%) + 0.75rem); }
.org-minimap { display: block; cursor: crosshair; }
.org-minimap-card { opacity: 0.55; }
.org-minimap-card-selected { opacity: 1; stroke: var(--foreground); stroke-width: 8; }
.org-minimap-viewport { fill: color-mix(in srgb, var(--primary) 10%, transparent); stroke: var(--primary); stroke-width: 2; }

.org-hint { position: absolute; left: 50%; top: 0.75rem; transform: translateX(-50%); z-index: 4; font-size: 0.75rem; color: var(--muted-foreground); background: color-mix(in srgb, var(--card) 88%, transparent); padding: 0.25rem 0.625rem; border-radius: 9999px; border: 1px solid var(--border); pointer-events: none; white-space: nowrap; }

@media (max-width: 768px) {
  .org-legend, .org-minimap-wrap { display: none; }
}

/* ── export copy: always light, hex only ─────────────────────────────────────── */
.org-export-host { position: fixed; left: -100000px; top: 0; z-index: -1; pointer-events: none; }
.org-static { ${ORG_COLOR_TOKENS_LIGHT} background: #fff; color: #0b0b0b; font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; padding: 28px 40px 24px; box-sizing: border-box; }
.org-static-head { display: flex; align-items: flex-start; gap: 16px; padding-bottom: 14px; border-bottom: 2px solid #0b0b0b; margin-bottom: 22px; }
.org-static-logo { height: 44px; width: auto; max-width: 140px; object-fit: contain; }
.org-static-tenant { font-size: 12px; letter-spacing: 0.08em; text-transform: uppercase; color: #52514e; font-weight: 600; }
.org-static-title { font-size: 22px; font-weight: 700; margin: 2px 0 0; line-height: 1.2; }
.org-static-sub { font-size: 12px; color: #52514e; margin-top: 4px; }
.org-static-muted { color: #898781; font-weight: 400; }
.org-static-stamp { font-size: 11px; color: #52514e; white-space: nowrap; padding-top: 4px; }
.org-static-chart { position: relative; margin: 0 auto; }
.org-static-chart svg { position: absolute; left: 0; top: 0; }
.org-static .org-edge { stroke: #55554f; }
.org-scard { position: absolute; box-sizing: border-box; border-radius: 8px; background: #fff; border: 1.5px solid #c3c2b7; padding: 8px 10px 8px 14px; display: flex; flex-direction: column; justify-content: space-between; overflow: hidden; --org-accent: #2a78d6; }
.org-scard::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 5px; background: var(--org-accent); }
.org-scard-compact { justify-content: center; padding: 6px 8px 6px 12px; }
.org-scard-row { display: flex; align-items: flex-start; gap: 10px; min-width: 0; }
.org-scard-avatar { width: 32px; height: 32px; border-radius: 9999px; background: var(--org-accent); color: #fff; font-size: 11px; font-weight: 700; display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; }
.org-scard-avatar:empty { display: none; }
.org-scard-name { font-weight: 600; font-size: 13.5px; line-height: 1.2; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.org-scard-detailed .org-scard-name { font-size: 15px; }
.org-scard-title { font-size: 11.5px; color: #52514e; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: 2px; }
.org-scard-foot { display: flex; align-items: center; gap: 4px; margin-top: 4px; font-size: 11px; color: #52514e; }
.org-scard-chip { border-radius: 5px; padding: 1px 6px; background: #f0efec; white-space: nowrap; }
.org-scard-code { font-family: ui-monospace, Menlo, Consolas, monospace; }
.org-scard-chip-vacant { background: #fbe3e3; color: #8f1d1d; }
.org-scard-count { margin-left: auto; font-weight: 600; color: #0b0b0b; white-space: nowrap; }
.org-scard-vacant { border-style: dashed; }
.org-scard-vacant .org-scard-name { color: #52514e; }
.org-scard-inactive { opacity: 0.6; }
.org-scard-synthetic { border-style: dashed; --org-accent: #c3c2b7; }
.org-scard-more { display: flex; align-items: center; justify-content: center; border-style: dashed; font-size: 12px; color: #52514e; text-align: center; }
.org-scard-more::before { display: none; }
.org-static-foot { display: flex; align-items: flex-end; justify-content: space-between; gap: 24px; margin-top: 24px; padding-top: 12px; border-top: 1px solid #c3c2b7; }
.org-legend-static { position: static; background: transparent; border: 0; padding: 0; backdrop-filter: none; font-size: 11.5px; color: #0b0b0b; max-width: none; }
.org-legend-static h4 { color: #52514e; }
.org-legend-static .org-legend-line { stroke: #55554f; }
.org-legend-static .org-legend-box { border-color: #55554f; }
.org-static-footnote { font-size: 11px; color: #52514e; max-width: 60ch; text-align: right; }
`;
