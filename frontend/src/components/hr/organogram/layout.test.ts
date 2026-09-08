import { describe, expect, it } from 'vitest';
import type { OrganogramNode } from '@/types/hr/organogram';
import { CARD_SIZES, boundsOf, layoutTree, roundedPath, type LayoutItem } from './layout';

const node = (id: string, extra: Partial<OrganogramNode> = {}): OrganogramNode => ({
  id,
  parentId: null,
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

const item = (id: string, children: LayoutItem[] = [], extra: Partial<LayoutItem> = {}): LayoutItem => ({
  id,
  kind: 'node',
  node: node(id),
  hidden: 0,
  parentId: null,
  children,
  depth: 0,
  childCount: children.length,
  descendantCount: children.length,
  isOpen: children.length > 0,
  ...extra,
});

const overlaps = (a: { x: number; y: number; w: number; h: number }, b: typeof a) =>
  a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;

describe('layoutTree', () => {
  it('centres a parent over its children and never overlaps cards', () => {
    const root = item('r', [item('a'), item('b'), item('c')]);
    const r = layoutTree([root], { stackLeavesFrom: 0 });
    const p = r.byId.get('r');
    const a = r.byId.get('a');
    const c = r.byId.get('c');
    expect(p && a && c).toBeTruthy();
    if (!p || !a || !c) return;
    const childrenMid = (a.x + c.x + c.w) / 2;
    expect(p.x + p.w / 2).toBeCloseTo(childrenMid, 5);
    for (let i = 0; i < r.placed.length; i++)
      for (let j = i + 1; j < r.placed.length; j++)
        expect(overlaps(r.placed[i], r.placed[j])).toBe(false);
    expect(r.edges.filter((e) => !e.to.startsWith('spine:'))).toHaveLength(3);
  });

  it('does not place a closed node’s children', () => {
    const root = item('r', [item('a')], { isOpen: false });
    const r = layoutTree([root]);
    expect(r.placed.map((p) => p.item.id)).toEqual(['r']);
    expect(r.edges).toHaveLength(0);
  });

  it('stacks many childless siblings into columns and keeps the branches in the row', () => {
    const leaves = Array.from({ length: 10 }, (_, i) => item(`l${i}`));
    const branch = item('b', [item('b1')]);
    const root = item('r', [branch, ...leaves]);
    const r = layoutTree([root], { stackLeavesFrom: 4, stackColumnMax: 8 });
    const l0 = r.byId.get('l0');
    const l7 = r.byId.get('l7');
    const l8 = r.byId.get('l8');
    if (!l0 || !l7 || !l8) throw new Error('leaves missing');
    // First column: same x, increasing y. Second column starts a new x at the top.
    expect(l7.x).toBe(l0.x);
    expect(l7.y).toBeGreaterThan(l0.y);
    expect(l8.x).toBeGreaterThan(l0.x);
    expect(l8.y).toBe(l0.y);
    // Two columns, two spines.
    expect(r.edges.filter((e) => e.to.startsWith('spine:'))).toHaveLength(2);
    // Every card still has its own room.
    for (let i = 0; i < r.placed.length; i++)
      for (let j = i + 1; j < r.placed.length; j++)
        expect(overlaps(r.placed[i], r.placed[j])).toBe(false);
    // A stacked chart is far narrower than the same row unstacked.
    const flat = layoutTree([root], { stackLeavesFrom: 0 });
    expect(r.width).toBeLessThan(flat.width / 2);
  });

  it('honours the dotted line type on the child’s edge', () => {
    const child = item('a');
    child.node = node('a', { lineType: 'dotted' });
    const r = layoutTree([item('r', [child, item('b')])], { stackLeavesFrom: 0 });
    expect(r.edges.find((e) => e.to === 'a')?.dotted).toBe(true);
    expect(r.edges.find((e) => e.to === 'b')?.dotted).toBe(false);
  });

  it('transposes for the horizontal orientation and keeps real card sizes', () => {
    const root = item('r', [item('a'), item('b')]);
    const r = layoutTree([root], { orientation: 'horizontal', density: 'comfortable' });
    const p = r.byId.get('r');
    const a = r.byId.get('a');
    const b = r.byId.get('b');
    if (!p || !a || !b) throw new Error('missing');
    expect(p.w).toBe(CARD_SIZES.comfortable.w);
    expect(p.h).toBe(CARD_SIZES.comfortable.h);
    // Children sit to the right of the parent, stacked vertically, with clear air between.
    expect(a.x).toBeGreaterThan(p.x + p.w);
    expect(a.x).toBe(b.x);
    expect(b.y).toBeGreaterThan(a.y + a.h);
    // The edge leaves the parent's right edge and arrives at the child's left edge.
    const e = r.edges.find((x) => x.to === 'a');
    expect(e?.d.startsWith(`M${p.x + p.w},${p.y + p.h / 2}`)).toBe(true);
    expect(e?.d.endsWith(`L${a.x},${a.y + a.h / 2}`)).toBe(true);
    for (let i = 0; i < r.placed.length; i++)
      for (let j = i + 1; j < r.placed.length; j++)
        expect(overlaps(r.placed[i], r.placed[j])).toBe(false);
  });

  it('lays out a "more" placeholder like any other card', () => {
    const more: LayoutItem = { ...item('r__more'), kind: 'more', node: null, hidden: 12 };
    const r = layoutTree([item('r', [item('a'), more])], { stackLeavesFrom: 0 });
    expect(r.byId.get('r__more')).toBeTruthy();
  });

  it('is deterministic', () => {
    const root = item('r', [item('a', [item('a1'), item('a2')]), item('b')]);
    const one = layoutTree([root]);
    const two = layoutTree([root]);
    expect(one.placed.map((p) => [p.item.id, p.x, p.y])).toEqual(two.placed.map((p) => [p.item.id, p.x, p.y]));
  });
});

describe('roundedPath', () => {
  it('draws straight lines without corners and rounds real turns', () => {
    expect(roundedPath([[0, 0], [0, 50], [0, 100]], 10)).toBe('M0,0 L0,100');
    const d = roundedPath([[0, 0], [0, 50], [100, 50], [100, 100]], 10);
    expect(d).toContain('Q0,50');
    expect(d).toContain('Q100,50');
    expect(d.endsWith('L100,100')).toBe(true);
  });

  it('shrinks the corner radius on short segments rather than overshooting', () => {
    const d = roundedPath([[0, 0], [0, 6], [6, 6]], 10);
    // Radius is capped at half the shortest adjacent segment (3), so the curve starts at y=3.
    expect(d).toBe('M0,0 L0,3 Q0,6 3,6 L6,6');
  });
});

describe('boundsOf', () => {
  it('returns null for nothing and the enclosing box otherwise', () => {
    expect(boundsOf([])).toBeNull();
    const r = layoutTree([item('r', [item('a'), item('b')])], { stackLeavesFrom: 0 });
    const b = boundsOf(r.placed);
    expect(b?.x).toBe(Math.min(...r.placed.map((p) => p.x)));
    expect(b && b.y + b.h).toBe(Math.max(...r.placed.map((p) => p.y + p.h)));
  });
});
