import { describe, expect, it } from 'vitest';
import { buildTree, type OrganogramNode } from '@/types/hr/organogram';
import {
  buildVisibleForest,
  fullyExpandedCount,
  idsBelow,
  keepPaths,
  moreIdFor,
  searchPredicate,
  type VisibleForestOptions,
} from './visible-tree';

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

// root ─ a ─ a1, a2
//      └ b ─ b1 ─ b1x
//      └ c (inactive)
//      └ 50 leaves under wide
const nodes: OrganogramNode[] = [
  n('root', null),
  n('a', 'root'),
  n('a1', 'a'),
  n('a2', 'a', { isVacant: true }),
  n('b', 'root'),
  n('b1', 'b'),
  n('b1x', 'b1', { name: 'Needle' }),
  n('c', 'root', { isActive: false }),
  n('wide', 'root'),
  ...Array.from({ length: 50 }, (_, i) => n(`w${i}`, 'wide')),
];
const roots = buildTree(nodes);

const base = (): VisibleForestOptions => ({
  roots,
  baseDepth: 0,
  expandDepth: 2,
  overrides: {},
  keep: null,
  pinned: null,
  hideInactive: false,
  maxDepth: null,
  pageSize: 40,
  pageShown: {},
});

const ids = (items: ReturnType<typeof buildVisibleForest>['items']): string[] => {
  const out: string[] = [];
  const walk = (list: typeof items) => {
    for (const i of list) {
      out.push(i.id);
      walk(i.children);
    }
  };
  walk(items);
  return out;
};

describe('buildVisibleForest', () => {
  it('opens to the depth rule and nothing further', () => {
    const v = buildVisibleForest(base());
    const seen = ids(v.items);
    expect(seen).toContain('a1'); // depth 2 renders (its parent at depth 1 is open)
    expect(seen).toContain('b1');
    expect(seen).not.toContain('b1x'); // depth 3: b1 is closed under "2 levels open"
    expect(v.items[0].children.find((c) => c.id === 'b')?.children[0].isOpen).toBe(false);
  });

  it('pages a wide row and inserts one placeholder for the rest', () => {
    const v = buildVisibleForest({ ...base(), expandDepth: 3 });
    const wide = v.items[0].children.find((c) => c.id === 'wide');
    expect(wide?.children).toHaveLength(41);
    const more = wide?.children[40];
    expect(more?.kind).toBe('more');
    expect(more?.id).toBe(moreIdFor('wide'));
    expect(more?.hidden).toBe(10);
  });

  it('shows a further page when the user has asked for it', () => {
    const v = buildVisibleForest({ ...base(), expandDepth: 3, pageShown: { wide: 80 } });
    const wide = v.items[0].children.find((c) => c.id === 'wide');
    expect(wide?.children).toHaveLength(50);
    expect(wide?.children.some((c) => c.kind === 'more')).toBe(false);
  });

  it('pins a node past the page so a search hit is always on screen', () => {
    const v = buildVisibleForest({ ...base(), expandDepth: 3, pinned: new Set(['w47']) });
    const wide = v.items[0].children.find((c) => c.id === 'wide');
    expect(wide?.children.map((c) => c.id)).toContain('w47');
    expect(wide?.children.find((c) => c.kind === 'more')?.hidden).toBe(9);
  });

  it('renders only the kept paths and opens them past the depth rule', () => {
    const { keep, hits } = keepPaths(roots, searchPredicate('needle'));
    expect(hits).toEqual(['b1x']);
    const v = buildVisibleForest({ ...base(), keep, expandDepth: 1 });
    expect(ids(v.items)).toEqual(['root', 'b', 'b1', 'b1x']);
    expect(v.count).toBe(4);
  });

  it('lets an explicit collapse win over a kept path', () => {
    const { keep } = keepPaths(roots, searchPredicate('needle'));
    const v = buildVisibleForest({ ...base(), keep, overrides: { b: false } });
    expect(ids(v.items)).toEqual(['root', 'b']);
  });

  it('hides inactive nodes and their subtrees when asked', () => {
    const v = buildVisibleForest({ ...base(), hideInactive: true });
    expect(ids(v.items)).not.toContain('c');
    expect(ids(buildVisibleForest(base()).items)).toContain('c');
  });

  it('caps the rendered depth', () => {
    const v = buildVisibleForest({ ...base(), expandDepth: 99, maxDepth: 1 });
    const seen = ids(v.items);
    expect(seen).toContain('a');
    expect(seen).not.toContain('a1');
  });

  it('measures depth relative to a focus root', () => {
    const b = roots[0].children.find((c) => c.node.id === 'b');
    if (!b) throw new Error('b missing');
    const v = buildVisibleForest({ ...base(), roots: [b], baseDepth: b.depth, expandDepth: 1 });
    expect(ids(v.items)).toEqual(['b', 'b1']);
    expect(v.items[0].depth).toBe(0);
  });
});

describe('helpers', () => {
  it('keepPaths keeps ancestors of every hit', () => {
    const { keep } = keepPaths(roots, (node) => node.isVacant);
    expect([...keep].sort()).toEqual(['a', 'a2', 'root']);
  });

  it('idsBelow and fullyExpandedCount agree with subtreeSize', () => {
    expect(idsBelow(roots)).toHaveLength(nodes.length);
    expect(fullyExpandedCount(roots)).toBe(nodes.length);
  });
});
