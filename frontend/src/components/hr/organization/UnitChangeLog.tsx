'use client';

import { History, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';
import type {
  OrganizationUnitChangeType,
  OrganizationUnitHistoryEntry,
} from '@/types/hr/organization';

/**
 * The organisation-unit change log, rendered as sentences rather than as eight id columns.
 *
 * Shared by the register (`/administration/hr/organization/unit-history`) and by the change-log tab
 * on a single unit, so the two cannot describe the same row differently.
 *
 * ⚠ Every name on this payload can legitimately be null while its id is set — the unit a department
 * was moved out of may since have been dissolved, and a former head may since have left. The server
 * resolves those names with the soft-delete filters ignored precisely so they survive, but a name
 * can still be genuinely absent (a hard-deleted row), and blank is not an answer a log may give.
 * `nameOf` is the single place that decides what to say instead.
 *
 * Demo feedback round 2 (O-3b): rows carry notes beside the reason, and a caller that passes
 * `onEdit` gets a correction action per row — dates, reason and notes only, admin-tier on the
 * server. The caller decides whether the viewer may see the action.
 */

const CHANGE_TYPE_STYLES: Record<OrganizationUnitChangeType, string> = {
  Restructure: 'bg-blue-100 text-blue-800 hover:bg-blue-100',
  'Leadership Change': 'bg-amber-100 text-amber-800 hover:bg-amber-100',
  Other: 'bg-muted text-muted-foreground hover:bg-muted',
};

export function ChangeTypeBadge({ type }: { type: OrganizationUnitChangeType }) {
  return <Badge className={CHANGE_TYPE_STYLES[type] ?? CHANGE_TYPE_STYLES.Other}>{type}</Badge>;
}

/** A name that is missing says so; it never renders as an empty cell. */
function nameOf(name?: string | null, id?: string | null): string {
  if (name && name.trim()) return name;
  if (id) return 'a record that no longer exists';
  return '—';
}

/**
 * What this row says happened, in one sentence.
 *
 * The four id pairs encode six distinct events, and collapsing them to "parent changed" would throw
 * away the two that matter most on a live structure: a unit being lifted to the top, and a unit
 * losing its head without gaining another. Since round 2 a unit's creation writes its initial
 * placement, which is the "placed under" case; a root unit's creation and a hand-recorded entry
 * carry no ids and are the `Other` case.
 */
export function changeSentence(entry: OrganizationUnitHistoryEntry): string {
  if (entry.changeType === 'Restructure') {
    if (!entry.previousParentId && entry.newParentId)
      return `Placed under ${nameOf(entry.newParentName, entry.newParentId)}.`;
    if (entry.previousParentId && !entry.newParentId)
      return `Moved to the top of the structure, out of ${nameOf(entry.previousParentName, entry.previousParentId)}.`;
    return `Moved from ${nameOf(entry.previousParentName, entry.previousParentId)} to ${nameOf(
      entry.newParentName,
      entry.newParentId,
    )}.`;
  }

  if (entry.changeType === 'Leadership Change') {
    if (!entry.previousHeadEmployeeId && entry.newHeadEmployeeId)
      return `${nameOf(entry.newHeadEmployeeName, entry.newHeadEmployeeId)} appointed head.`;
    if (entry.previousHeadEmployeeId && !entry.newHeadEmployeeId)
      return `${nameOf(entry.previousHeadEmployeeName, entry.previousHeadEmployeeId)} ceased to be head.`;
    return `Head changed from ${nameOf(
      entry.previousHeadEmployeeName,
      entry.previousHeadEmployeeId,
    )} to ${nameOf(entry.newHeadEmployeeName, entry.newHeadEmployeeId)}.`;
  }

  return 'No change of reporting line or head — an entry recorded by hand, or the unit created at the top of its structure.';
}

/** "04 Aug 2026 — present", or the closed period when a later change superseded it. */
function period(entry: OrganizationUnitHistoryEntry): string {
  return entry.effectiveTo
    ? `${formatDate(entry.effectiveFrom)} — ${formatDate(entry.effectiveTo)}`
    : `${formatDate(entry.effectiveFrom)} — present`;
}

interface UnitChangeLogProps {
  entries: OrganizationUnitHistoryEntry[];
  isLoading?: boolean;
  /** The register names the unit on every row; a single unit's own tab does not repeat it. */
  showUnit?: boolean;
  /** When given, each row gets a "correct" action. The caller gates it on the viewer's tier. */
  onEdit?: (entry: OrganizationUnitHistoryEntry) => void;
  emptyTitle?: string;
  emptyDescription?: string;
}

export function UnitChangeLog({
  entries,
  isLoading,
  showUnit = false,
  onEdit,
  emptyTitle = 'Nothing recorded yet',
  emptyDescription = 'Creating a unit, reparenting it or changing its head writes an entry here. A rename does not — a log that records everything is one nobody reads.',
}: UnitChangeLogProps) {
  const columns = 4 + (showUnit ? 1 : 0) + (onEdit ? 1 : 0);

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead className="w-[150px]">Change</TableHead>
            {showUnit && <TableHead className="w-[200px]">Unit</TableHead>}
            <TableHead>What happened</TableHead>
            <TableHead className="w-[220px]">In force</TableHead>
            <TableHead className="w-[220px]">Recorded</TableHead>
            {onEdit && <TableHead className="w-[60px]"><span className="sr-only">Correct</span></TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {isLoading ? (
            [...Array(4)].map((_, i) => (
              <TableRow key={i}>
                {[...Array(columns)].map((__, j) => (
                  <TableCell key={j}>
                    <Skeleton className="h-4 w-full" />
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : entries.length === 0 ? (
            <TableRow>
              <TableCell colSpan={columns}>
                <EmptyState icon={History} title={emptyTitle} description={emptyDescription} />
              </TableCell>
            </TableRow>
          ) : (
            entries.map((entry) => (
              <TableRow key={entry.id}>
                <TableCell>
                  <ChangeTypeBadge type={entry.changeType} />
                </TableCell>
                {showUnit && (
                  <TableCell className="font-medium">
                    {nameOf(entry.organizationUnitName, entry.organizationUnitId)}
                  </TableCell>
                )}
                <TableCell>
                  <div className="space-y-1">
                    <div>{changeSentence(entry)}</div>
                    {entry.changeReason ? (
                      <div className="text-xs italic text-muted-foreground">
                        &ldquo;{entry.changeReason}&rdquo;
                      </div>
                    ) : (
                      // Rows written before the edit form carried a reason field have none, and so
                      // do rows recorded by someone who left the box empty. Saying so is better than
                      // a blank line that reads like a rendering fault.
                      <div className="text-xs text-muted-foreground">No reason recorded.</div>
                    )}
                    {entry.notes && (
                      <div className="whitespace-pre-line text-xs text-muted-foreground">{entry.notes}</div>
                    )}
                  </div>
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">{period(entry)}</TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  <div>{formatDateTime(entry.createdAt)}</div>
                  <div className="text-xs">
                    {entry.createdBy?.trim() ? `by ${entry.createdBy}` : 'author not recorded'}
                  </div>
                  {entry.updatedAt && entry.updatedAt !== entry.createdAt && (
                    <div className="text-xs">corrected {formatDateTime(entry.updatedAt)}</div>
                  )}
                </TableCell>
                {onEdit && (
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => onEdit(entry)}
                      title="Correct the dates, reason or notes"
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
