import { describe, expect, it } from 'vitest';
import { SYNTHETIC_ROOT_ID, buildTree, type OrganogramNode } from '@/types/hr/organogram';
import { exportFileStem, flattenForExport, toCsv } from './export-data';
import { DEFAULT_VIEW_STATE, parseViewState, serializeViewState } from './view-state';
import { buildHeatScale, levelVar } from './heat';

const n = (id: string, parentId: string | null, extra: Partial<OrganogramNode> = {}): OrganogramNode => ({
  id,
  parentId,
  name: id,
  title: null,
  code: null,
  headName: null,
  imageUrl: null,
  badge: null,
  isVacant: false,
  isActive: true,
  employeeCount: null,
  totalEmployeeCount: null,
  expectedHeadcount: null,
  positionCount: null,
  vacantPositionCount: null,
  holders: null,
  holdersTruncated: false,
  lineType: 'solid',
  meta: {},
  ...extra,
});

describe('flattenForExport', () => {
  it('writes one row per real node in reading order with the full path', () => {
    const roots = buildTree([
      n(SYNTHETIC_ROOT_ID, null, { name: 'Organization' }),
      n('hq', SYNTHETIC_ROOT_ID, { name: 'Head Office', title: 'Directorate', employeeCount: 2, totalEmployeeCount: 12 }),
      n('fin', 'hq', { name: 'Finance', title: 'Department', employeeCount: 10, totalEmployeeCount: 10, headName: 'A. Mensah', positionCount: 4, vacantPositionCount: 1 }),
      n('site', SYNTHETIC_ROOT_ID, { name: 'Site Office', employeeCount: 0, totalEmployeeCount: 0, isVacant: true, badge: 'Vacant lead' }),
    ]);
    const { rows, columns } = flattenForExport(roots, 'units');
    expect(rows.map((r) => r.Name)).toEqual(['Head Office', 'Finance', 'Site Office']);
    expect(rows[1].Path).toBe('Head Office / Finance');
    expect(rows[1].Parent).toBe('Head Office');
    expect(rows[1].Level).toBe(2);
    expect(rows[0].Level).toBe(1);
    expect(rows[1].Head).toBe('A. Mensah');
    expect(rows[1]['Total headcount']).toBe(10);
    expect(rows[1].Posts).toBe(4);
    expect(rows[1]['Vacant posts']).toBe(1);
    expect(rows[2].Status).toBe('Vacant lead');
    expect(columns).not.toContain('Holders');
    expect(columns).toContain('Posts');
  });

  it('never exports an email address from the people view, and carries other meta through', () => {
    const roots = buildTree([
      n('ceo', null, { name: 'K. Boateng', title: 'Managing Director', employeeCount: 1, totalEmployeeCount: 2, meta: { Email: 'k@tdc.example', Unit: 'Executive', Phone: '020 000 0000' } }),
      n('r1', 'ceo', { name: 'E. Owusu', employeeCount: 0, totalEmployeeCount: 0, meta: { Email: 'e@tdc.example', Unit: 'Finance' } }),
    ]);
    const { rows, columns } = flattenForExport(roots, 'people');
    expect(columns).not.toContain('Email');
    expect(columns).toContain('Unit');
    expect(columns).toContain('Phone');
    expect(JSON.stringify(rows)).not.toContain('@tdc.example');
    expect(rows[1].Unit).toBe('Finance');
  });

  it('lists position holders and marks a truncated list', () => {
    const roots = buildTree([
      n('p', null, { name: 'Driver', employeeCount: 26, expectedHeadcount: 30, holders: ['A', 'B'], holdersTruncated: true }),
    ]);
    const { rows } = flattenForExport(roots, 'positions');
    expect(rows[0].Holders).toBe('A; B; …');
    expect(rows[0].Establishment).toBe(30);
  });
});

describe('toCsv', () => {
  it('quotes fields with commas and quotes, and starts with a BOM', () => {
    const csv = toCsv([{ Level: 1, Name: 'Finance, Admin', Type: 'He said "hi"', Code: '', Parent: '', Path: 'x', Head: '', Status: '', 'Direct headcount': 1, 'Total headcount': '', Establishment: '', Posts: '', 'Vacant posts': '', Holders: '' }], ['Level', 'Name', 'Type']);
    expect(csv.charCodeAt(0)).toBe(0xfeff);
    expect(csv).toContain('1,"Finance, Admin","He said ""hi"""');
  });
});

describe('exportFileStem', () => {
  it('is safe and stamped', () => {
    expect(exportFileStem('units', 'Whole chart', new Date(2026, 8, 8, 14, 2))).toBe('organogram-units-whole-chart-2026-09-08-1402');
  });
});

describe('view state round trip', () => {
  it('writes only what differs from the defaults and reads it back', () => {
    expect(serializeViewState(DEFAULT_VIEW_STATE)).toBe('');
    const s = { ...DEFAULT_VIEW_STATE, dimension: 'people' as const, focusId: 'abc', expandDepth: 4, orientation: 'horizontal' as const, heat: 'span' as const, query: 'mensah', stackLeaves: false, vacantOnly: true };
    const qs = serializeViewState(s);
    expect(parseViewState(new URLSearchParams(qs))).toEqual(s);
  });

  it('ignores junk', () => {
    const s = parseViewState(new URLSearchParams('dim=bogus&depth=7&layout=diagonal&heat=rainbow'));
    expect(s).toEqual(DEFAULT_VIEW_STATE);
  });

  it('carries the structure only for the locations view', () => {
    expect(serializeViewState({ ...DEFAULT_VIEW_STATE, structureId: 'x' })).toBe('');
    expect(serializeViewState({ ...DEFAULT_VIEW_STATE, dimension: 'locations', structureId: 'x' })).toBe('dim=locations&structure=x');
  });
});

describe('heat scales', () => {
  it('folds deep levels into the last categorical slot rather than cycling', () => {
    expect(levelVar(0)).toBe('var(--org-cat-1)');
    expect(levelVar(7)).toBe('var(--org-cat-8)');
    expect(levelVar(12)).toBe('var(--org-cat-8)');
  });

  it('bands headcount by quantile with a separate "none" swatch', () => {
    const nodes = [0, 0, 3, 5, 8, 13, 21, 34, 55, 89].map((v, i) => n(`u${i}`, null, { totalEmployeeCount: v, employeeCount: v }));
    const scale = buildHeatScale('headcount', 'units', nodes);
    if (!scale) throw new Error('no scale');
    expect(scale.colorFor(nodes[0])?.label).toBe('No staff');
    const top = scale.colorFor(nodes[9]);
    const low = scale.colorFor(nodes[2]);
    expect(top?.color).not.toBe(low?.color);
    expect(scale.legend[scale.legend.length - 1].label).toMatch(/\+$/);
  });

  it('reads vacancy as a status, never a gradient', () => {
    const scale = buildHeatScale('fill', 'positions', []);
    if (!scale) throw new Error('no scale');
    expect(scale.colorFor(n('p', null, { employeeCount: 0, expectedHeadcount: 2 }))?.status).toBe('critical');
    expect(scale.colorFor(n('p', null, { employeeCount: 1, expectedHeadcount: 2 }))?.status).toBe('warning');
    expect(scale.colorFor(n('p', null, { employeeCount: 2, expectedHeadcount: 2 }))?.status).toBe('good');
    expect(scale.colorFor(n('p', null, { employeeCount: 3, expectedHeadcount: 2 }))?.status).toBe('info');
    const units = buildHeatScale('fill', 'units', []);
    expect(units?.colorFor(n('u', null, { positionCount: 4, vacantPositionCount: 4 }))?.status).toBe('critical');
    expect(units?.colorFor(n('u', null, { positionCount: 0 }))?.label).toBe('No posts');
  });
});
