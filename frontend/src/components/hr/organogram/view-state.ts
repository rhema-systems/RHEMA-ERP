import { ORGANOGRAM_DIMENSIONS, type OrganogramDimension } from '@/types/hr/organogram';
import type { Density, Orientation } from './layout';
import { HEAT_MODES, type HeatMode } from './heat';

/**
 * The part of the screen's state worth putting in the URL: enough that a link opens the same
 * picture for the next reader, and a demo script can say "open this address" rather than "click
 * these six things". Everything else (pan, zoom, per-node overrides) is transient by design.
 */
export interface OrganogramViewState {
  dimension: OrganogramDimension;
  structureId: string;
  focusId: string | null;
  selectedId: string | null;
  expandDepth: number;
  orientation: Orientation;
  density: Density;
  heat: HeatMode;
  query: string;
  stackLeaves: boolean;
  vacantOnly: boolean;
  hideInactive: boolean;
}

export const DEFAULT_VIEW_STATE: OrganogramViewState = {
  dimension: 'units',
  structureId: '',
  focusId: null,
  selectedId: null,
  expandDepth: 2,
  orientation: 'vertical',
  density: 'comfortable',
  heat: 'none',
  query: '',
  stackLeaves: true,
  vacantOnly: false,
  hideInactive: false,
};

const DEPTHS = [1, 2, 3, 4, 5, 99];

export function parseViewState(params: URLSearchParams | null | undefined): OrganogramViewState {
  const s = { ...DEFAULT_VIEW_STATE };
  if (!params) return s;
  const dim = params.get('dim');
  if (dim && ORGANOGRAM_DIMENSIONS.some((d) => d.key === dim)) s.dimension = dim as OrganogramDimension;
  s.structureId = params.get('structure') ?? '';
  s.focusId = params.get('focus') || null;
  s.selectedId = params.get('sel') || null;
  const depth = Number(params.get('depth'));
  if (DEPTHS.includes(depth)) s.expandDepth = depth;
  const layout = params.get('layout');
  if (layout === 'horizontal' || layout === 'vertical') s.orientation = layout;
  const density = params.get('density');
  if (density === 'compact' || density === 'comfortable' || density === 'detailed') s.density = density;
  const heat = params.get('heat');
  if (heat && HEAT_MODES.some((m) => m.key === heat)) s.heat = heat as HeatMode;
  s.query = params.get('q') ?? '';
  if (params.get('stack') === '0') s.stackLeaves = false;
  if (params.get('vacant') === '1') s.vacantOnly = true;
  if (params.get('active') === '1') s.hideInactive = true;
  return s;
}

/** Only the values that differ from the defaults are written, so a fresh page has a clean URL. */
export function serializeViewState(s: OrganogramViewState): string {
  const p = new URLSearchParams();
  if (s.dimension !== DEFAULT_VIEW_STATE.dimension) p.set('dim', s.dimension);
  if (s.structureId && s.dimension === 'locations') p.set('structure', s.structureId);
  if (s.focusId) p.set('focus', s.focusId);
  if (s.selectedId) p.set('sel', s.selectedId);
  if (s.expandDepth !== DEFAULT_VIEW_STATE.expandDepth) p.set('depth', String(s.expandDepth));
  if (s.orientation !== DEFAULT_VIEW_STATE.orientation) p.set('layout', s.orientation);
  if (s.density !== DEFAULT_VIEW_STATE.density) p.set('density', s.density);
  if (s.heat !== DEFAULT_VIEW_STATE.heat) p.set('heat', s.heat);
  if (s.query.trim()) p.set('q', s.query.trim());
  if (!s.stackLeaves) p.set('stack', '0');
  if (s.vacantOnly) p.set('vacant', '1');
  if (s.hideInactive) p.set('active', '1');
  return p.toString();
}
