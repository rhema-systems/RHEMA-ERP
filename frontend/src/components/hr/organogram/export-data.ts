import {
  ORGANOGRAM_DIMENSIONS,
  SYNTHETIC_ROOT_ID,
  type OrganogramDimension,
  type OrganogramTreeNode,
} from '@/types/hr/organogram';

/**
 * The tabular export: the tree flattened to one row per node, in reading order (depth-first, so
 * a unit is followed by everything under it), with the full path spelled out so the sheet can be
 * filtered and pivoted without reconstructing the hierarchy.
 *
 * ⚠ The People dimension is the personnel register. Its export never carries email (decision of
 * 2026-09-08): the chart is for structure, and a spreadsheet of every address is a different
 * product with a different owner. The other meta keys are structural and go through.
 */

export interface ExportRow {
  Level: number;
  Name: string;
  Type: string;
  Code: string;
  Parent: string;
  Path: string;
  Head: string;
  Status: string;
  'Direct headcount': number | '';
  'Total headcount': number | '';
  Establishment: number | '';
  Posts: number | '';
  'Vacant posts': number | '';
  Holders: string;
  [extra: string]: string | number;
}

const EXCLUDED_META = new Set(['Email']);

export const BASE_COLUMNS: (keyof ExportRow & string)[] = [
  'Level',
  'Name',
  'Type',
  'Code',
  'Parent',
  'Path',
  'Head',
  'Status',
  'Direct headcount',
  'Total headcount',
  'Establishment',
  'Posts',
  'Vacant posts',
  'Holders',
];

export interface FlattenResult {
  rows: ExportRow[];
  /** Base columns first, then every meta key seen, in first-seen order. */
  columns: string[];
}

export function flattenForExport(
  roots: OrganogramTreeNode[],
  dimension: OrganogramDimension,
): FlattenResult {
  const rows: ExportRow[] = [];
  const metaKeys: string[] = [];
  const hasHeadcount = ORGANOGRAM_DIMENSIONS.find((d) => d.key === dimension)?.hasHeadcount ?? false;

  const walk = (entry: OrganogramTreeNode, path: string[], parentName: string, level: number) => {
    const { node } = entry;
    const synthetic = node.id === SYNTHETIC_ROOT_ID;
    // The synthetic root is the server's device for a multi-rooted forest, not a record. Its
    // children are exported as top-level rows.
    if (!synthetic) {
      const nextPath = [...path, node.name];
      const row: ExportRow = {
        Level: level,
        Name: node.name,
        Type: node.title ?? '',
        Code: node.code ?? '',
        Parent: parentName,
        Path: nextPath.join(' / '),
        Head: node.headName ?? '',
        Status: statusOf(node.isActive, node.isVacant, node.badge),
        'Direct headcount': hasHeadcount && node.employeeCount !== null ? node.employeeCount : '',
        'Total headcount': hasHeadcount && node.totalEmployeeCount !== null ? node.totalEmployeeCount : '',
        Establishment: node.expectedHeadcount ?? '',
        Posts: node.positionCount ?? '',
        'Vacant posts': node.vacantPositionCount ?? '',
        Holders: node.holders?.length
          ? node.holders.join('; ') + (node.holdersTruncated ? '; …' : '')
          : '',
      };
      for (const [k, v] of Object.entries(node.meta)) {
        if (EXCLUDED_META.has(k)) continue;
        if (!metaKeys.includes(k)) metaKeys.push(k);
        row[k] = v;
      }
      rows.push(row);
      for (const child of entry.children) walk(child, nextPath, node.name, level + 1);
      return;
    }
    for (const child of entry.children) walk(child, path, '', level);
  };

  for (const root of roots) walk(root, [], '', 1);

  // Drop base columns no row uses, so a Locations sheet does not carry six empty headcount columns.
  const used = BASE_COLUMNS.filter((c) =>
    ['Level', 'Name', 'Path'].includes(c) || rows.some((r) => r[c] !== '' && r[c] !== undefined),
  );
  return { rows, columns: [...used, ...metaKeys] };
}

function statusOf(isActive: boolean, isVacant: boolean, badge: string | null): string {
  if (!isActive) return 'Inactive';
  if (badge) return badge;
  if (isVacant) return 'Vacant';
  return 'Active';
}

/** Rows as a CSV string, RFC 4180 quoting, with a UTF-8 BOM so Excel opens it correctly. */
export function toCsv(rows: ExportRow[], columns: string[]): string {
  const quote = (v: string | number | undefined) => {
    const s = v === undefined ? '' : String(v);
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [columns.map(quote).join(',')];
  for (const r of rows) lines.push(columns.map((c) => quote(r[c])).join(','));
  return '﻿' + lines.join('\r\n');
}

/** A file-system-safe stem: "organogram-units-2026-09-08-1402". */
export function exportFileStem(dimension: OrganogramDimension, scopeLabel: string, at = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  const stamp = `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}-${pad(at.getHours())}${pad(at.getMinutes())}`;
  return `organogram-${dimension}-${scopeLabel}-${stamp}`.replace(/[^a-z0-9._-]+/gi, '-').replace(/-+/g, '-').toLowerCase();
}
