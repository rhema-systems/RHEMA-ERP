'use client';

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { FilterX, PlusCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';
import { UnitChangeLog } from '@/components/hr/organization/UnitChangeLog';
import { UnitHistoryEntryDialog } from '@/components/hr/organization/UnitHistoryEntryDialog';
import { organizationUnitHistoryService } from '@/services/hr/organization-unit-history.service';
import type {
  OrganizationUnitChangeType,
  OrganizationUnitHistoryEntry,
} from '@/types/hr/organization';

const PAGE_SIZE = 20;
const ANY = 'any';

const CHANGE_TYPES: OrganizationUnitChangeType[] = ['Restructure', 'Leadership Change', 'Other'];

/**
 * The organisation-unit change log, across every unit.
 *
 * The unit's own tab answers "what has happened to this department"; this answers "what changed
 * across the organisation this quarter", which is the read the endpoint was built for and the one
 * nothing could reach — the register did not exist, and until slice 5 the paged endpoint took a page
 * number and nothing else, so narrowing to a unit or a date range was not possible at all.
 *
 * Every filter is applied server-side. Filtering a page of 20 in the browser would answer the
 * question about that page rather than about the organisation.
 *
 * Demo feedback round 2: the unit filter is the level-first picker (O-6), and an administrator
 * can record an entry by hand or correct one's dates from here (O-3b).
 */
export default function OrganizationUnitHistoryPage() {
  const queryClient = useQueryClient();
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canAuthorHistory = hasAnyPermission(['HR.Employee.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [page, setPage] = useState(1);
  const [unitId, setUnitId] = useState<string>('');
  const [changeType, setChangeType] = useState<string>(ANY);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [recording, setRecording] = useState(false);
  const [correcting, setCorrecting] = useState<OrganizationUnitHistoryEntry | null>(null);

  // The server refuses a start after an end with a 400. Holding the query back while the pair is
  // inverted keeps the screen from asking a question it already knows the answer to.
  const rangeInverted = Boolean(startDate && endDate && startDate > endDate);

  const query = useMemo(
    () => ({
      pageNumber: page,
      pageSize: PAGE_SIZE,
      ...(unitId ? { unitId } : {}),
      ...(changeType !== ANY ? { changeType: changeType as OrganizationUnitChangeType } : {}),
      ...(startDate ? { startDate } : {}),
      ...(endDate ? { endDate } : {}),
    }),
    [page, unitId, changeType, startDate, endDate],
  );

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'organization-unit-history', query],
    queryFn: () => organizationUnitHistoryService.getPaged(query),
    enabled: !rangeInverted,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'organization-unit-history'] });

  const filtered = Boolean(unitId) || changeType !== ANY || Boolean(startDate) || Boolean(endDate);

  const onFilterChange = (apply: () => void) => {
    apply();
    setPage(1);
  };

  const clearFilters = () => {
    setUnitId('');
    setChangeType(ANY);
    setStartDate('');
    setEndDate('');
    setPage(1);
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Unit Change Log"
        description="Every recorded creation, restructure and change of unit head, newest first."
        actions={
          canAuthorHistory ? (
            <Button variant="outline" onClick={() => setRecording(true)}>
              <PlusCircle className="mr-2 h-4 w-4" /> Record an entry
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>
            Dates match the day a change took effect, not the day it was recorded.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-5">
            <div className="md:col-span-2">
              <OrganizationUnitPicker
                idPrefix="filter-unit"
                value={unitId}
                onChange={(id) => onFilterChange(() => setUnitId(id))}
                allowNone="Any unit"
                levelLabel="Level"
                unitLabel="Unit"
                className="grid gap-4 sm:grid-cols-2"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="changeType">Change</Label>
              <Select
                value={changeType}
                onValueChange={(v) => onFilterChange(() => setChangeType(v))}
              >
                <SelectTrigger id="changeType">
                  <SelectValue placeholder="Any change" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>Any change</SelectItem>
                  {CHANGE_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="startDate">Effective from</Label>
              <Input
                id="startDate"
                type="date"
                value={startDate}
                onChange={(e) => onFilterChange(() => setStartDate(e.target.value))}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="endDate">Effective to</Label>
              <Input
                id="endDate"
                type="date"
                value={endDate}
                onChange={(e) => onFilterChange(() => setEndDate(e.target.value))}
              />
            </div>
          </div>

          {rangeInverted && (
            <p className="mt-3 text-sm text-destructive">
              The start date is after the end date, so no range is being asked for.
            </p>
          )}

          {filtered && (
            <div className="mt-4 flex justify-end">
              <Button variant="outline" size="sm" onClick={clearFilters}>
                <FilterX className="mr-2 h-4 w-4" /> Clear filters
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Changes</CardTitle>
          <CardDescription>
            {data
              ? `${data.totalCount} ${data.totalCount === 1 ? 'entry' : 'entries'}${
                  filtered ? ' matching these filters' : ' in total'
                }.`
              : 'Loading…'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <UnitChangeLog
            entries={data?.items ?? []}
            isLoading={isLoading && !rangeInverted}
            showUnit
            onEdit={canAuthorHistory ? (entry) => setCorrecting(entry) : undefined}
            emptyTitle={filtered ? 'No changes match these filters' : 'No changes recorded yet'}
            emptyDescription={
              filtered
                ? 'Widen the date range, or clear the filters to see everything recorded.'
                : 'Creating a unit, reparenting it or changing its head writes an entry here. A rename does not — a log that records everything is one nobody reads.'
            }
          />

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-end space-x-2 py-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPrevious}
              >
                Previous
              </Button>
              <div className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNext}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <UnitHistoryEntryDialog open={recording} onOpenChange={setRecording} onDone={refresh} />
      <UnitHistoryEntryDialog
        open={!!correcting}
        onOpenChange={(next) => {
          if (!next) setCorrecting(null);
        }}
        entry={correcting}
        onDone={refresh}
      />
    </div>
  );
}
