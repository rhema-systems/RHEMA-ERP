import type { OrganogramDimension, OrganogramNode } from '@/types/hr/organogram';

/**
 * Colour rules for the chart, written against CSS custom properties so the light and dark values
 * live in one style block (see `OrgChart.tsx`) and never in JavaScript.
 *
 * Palette provenance: the categorical eight, the blue sequential ramp and the four status colours
 * are the reference data-viz palette, validated for colour-vision deficiency and contrast against
 * this app's card surfaces (#ffffff light, #202020 dark) on 2026-09-08. Level bands use the
 * categorical slots in fixed order; a level deeper than the eighth folds into the last slot rather
 * than cycling, so no two adjacent levels can share a colour by accident.
 */

export const LEVEL_SLOTS = 8;

/** CSS variable for a level's band colour. Relative depth 0 is the focus root. */
export function levelVar(relDepth: number): string {
  const slot = Math.min(Math.max(relDepth, 0), LEVEL_SLOTS - 1) + 1;
  return `var(--org-cat-${slot})`;
}

export type HeatMode = 'none' | 'headcount' | 'fill' | 'span';

export interface HeatModeSpec {
  key: HeatMode;
  label: string;
  /** Which dimensions the mode means something on. */
  dimensions: OrganogramDimension[];
  description: string;
}

export const HEAT_MODES: HeatModeSpec[] = [
  { key: 'none', label: 'Level colours', dimensions: ['units', 'positions', 'people', 'teams', 'locations'], description: 'Each level of the hierarchy in its own colour.' },
  { key: 'headcount', label: 'Headcount', dimensions: ['units', 'teams'], description: 'Darker means more staff at and below the node.' },
  { key: 'fill', label: 'Vacancy', dimensions: ['positions', 'units', 'teams'], description: 'Whether posts are held, under strength or vacant.' },
  { key: 'span', label: 'Span of control', dimensions: ['people'], description: 'How many people report directly to each person.' },
];

export function heatModesFor(dimension: OrganogramDimension): HeatModeSpec[] {
  return HEAT_MODES.filter((m) => m.dimensions.includes(dimension));
}

export interface HeatSwatch {
  /** A CSS colour expression, usually a var(). */
  color: string;
  label: string;
  /** Status swatches carry an icon name so colour never stands alone. */
  status?: 'good' | 'warning' | 'serious' | 'critical' | 'info';
}

export interface HeatScale {
  mode: HeatMode;
  /** Legend entries, in display order. */
  legend: HeatSwatch[];
  /** Per-node colour, or null to fall back to the level band. */
  colorFor: (node: OrganogramNode) => HeatSwatch | null;
}

/** Thresholds for the sequential ramp: quantile edges over the non-zero values seen. */
function quantileEdges(values: number[], steps: number): number[] {
  const sorted = values.filter((v) => v > 0).sort((a, b) => a - b);
  if (!sorted.length) return [];
  const edges: number[] = [];
  for (let i = 1; i < steps; i++) {
    const idx = Math.min(sorted.length - 1, Math.floor((i / steps) * sorted.length));
    edges.push(sorted[idx]);
  }
  // Collapse duplicate edges so a distribution of mostly-equal values yields fewer, honest bands.
  return edges.filter((e, i) => i === 0 || e > edges[i - 1]);
}

const SEQ_STEPS = 5;

function sequentialScale(
  mode: HeatMode,
  nodes: OrganogramNode[],
  valueOf: (n: OrganogramNode) => number | null,
  unit: string,
): HeatScale {
  const values = nodes.map(valueOf).filter((v): v is number => v !== null);
  const edges = quantileEdges(values, SEQ_STEPS);
  const bands = edges.length + 1;
  const legend: HeatSwatch[] = [{ color: 'var(--org-seq-0)', label: `No ${unit}` }];
  for (let b = 0; b < bands; b++) {
    const lo = b === 0 ? 1 : edges[b - 1];
    const hi = b < edges.length ? edges[b] - 1 : null;
    const label = hi === null ? `${lo}+` : lo === hi ? `${lo}` : `${lo}–${hi}`;
    legend.push({ color: `var(--org-seq-${Math.min(b + 1, SEQ_STEPS)})`, label });
  }
  return {
    mode,
    legend,
    colorFor: (node) => {
      const v = valueOf(node);
      if (v === null) return null;
      if (v <= 0) return legend[0];
      let band = 0;
      while (band < edges.length && v >= edges[band]) band++;
      return legend[band + 1];
    },
  };
}

const STATUS: Record<NonNullable<HeatSwatch['status']>, string> = {
  good: 'var(--org-status-good)',
  warning: 'var(--org-status-warning)',
  serious: 'var(--org-status-serious)',
  critical: 'var(--org-status-critical)',
  info: 'var(--org-cat-1)',
};

function fillScale(dimension: OrganogramDimension): HeatScale {
  const full: HeatSwatch = { color: STATUS.good, label: 'Fully held', status: 'good' };
  const under: HeatSwatch = { color: STATUS.warning, label: 'Under strength', status: 'warning' };
  const vacant: HeatSwatch = { color: STATUS.critical, label: 'Vacant', status: 'critical' };
  const over: HeatSwatch = { color: STATUS.info, label: 'Over establishment', status: 'info' };
  const none: HeatSwatch = { color: 'var(--org-seq-0)', label: 'No posts' };

  if (dimension === 'units') {
    return {
      mode: 'fill',
      legend: [full, under, vacant, none],
      colorFor: (n) => {
        if (n.positionCount === null || n.positionCount === 0) return none;
        const vac = n.vacantPositionCount ?? 0;
        if (vac === 0) return full;
        if (vac >= n.positionCount) return vacant;
        return under;
      },
    };
  }
  // positions and teams: holders (or members) against establishment (or cap)
  return {
    mode: 'fill',
    legend: [full, under, vacant, over],
    colorFor: (n) => {
      const current = n.employeeCount ?? 0;
      const expected = n.expectedHeadcount;
      if (current === 0) return vacant;
      if (expected === null || expected === 0) return full;
      if (current < expected) return under;
      if (current > expected) return over;
      return full;
    },
  };
}

export function buildHeatScale(
  mode: HeatMode,
  dimension: OrganogramDimension,
  nodes: OrganogramNode[],
): HeatScale | null {
  switch (mode) {
    case 'none':
      return null;
    case 'headcount':
      return sequentialScale('headcount', nodes, (n) => n.totalEmployeeCount ?? n.employeeCount, 'staff');
    case 'span':
      return sequentialScale('span', nodes, (n) => n.employeeCount, 'reports');
    case 'fill':
      return fillScale(dimension);
  }
}

/**
 * The colour tokens, light then dark. Hex on purpose: html2canvas cannot parse oklch(), and the
 * export renders these same cards.
 */
export const ORG_COLOR_TOKENS_LIGHT = `
  --org-edge: #6b6b66;
  --org-edge-dim: #c3c2b7;
  --org-edge-path: #2a78d6;
  --org-canvas: #f6f6f4;
  --org-grid: #e6e5e0;
  --org-cat-1: #2a78d6; --org-cat-2: #eb6834; --org-cat-3: #1baf7a; --org-cat-4: #eda100;
  --org-cat-5: #e87ba4; --org-cat-6: #008300; --org-cat-7: #4a3aa7; --org-cat-8: #e34948;
  --org-seq-0: #e6e5e0; --org-seq-1: #b7d3f6; --org-seq-2: #86b6ef; --org-seq-3: #5598e7;
  --org-seq-4: #2a78d6; --org-seq-5: #184f95;
  --org-status-good: #0ca30c; --org-status-warning: #fab219; --org-status-serious: #ec835a;
  --org-status-critical: #d03b3b;
  --org-hit: #eda100;
`;

export const ORG_COLOR_TOKENS_DARK = `
  --org-edge: #a3a29b;
  --org-edge-dim: #4a4a47;
  --org-edge-path: #3987e5;
  --org-canvas: #161616;
  --org-grid: #242424;
  --org-cat-1: #3987e5; --org-cat-2: #d95926; --org-cat-3: #199e70; --org-cat-4: #c98500;
  --org-cat-5: #d55181; --org-cat-6: #008300; --org-cat-7: #9085e9; --org-cat-8: #e66767;
  --org-seq-0: #2c2c2a; --org-seq-1: #1c5cab; --org-seq-2: #256abf; --org-seq-3: #3987e5;
  --org-seq-4: #6da7ec; --org-seq-5: #9ec5f4;
  --org-status-good: #0ca30c; --org-status-warning: #fab219; --org-status-serious: #ec835a;
  --org-status-critical: #d03b3b;
  --org-hit: #c98500;
`;
