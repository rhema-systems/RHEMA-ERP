'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  Building2,
  ChevronDown,
  ChevronRight,
  Mail,
  Phone,
  Search,
  Users,
  X,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  DIRECTORY_PAGE_SIZE,
  staffDirectoryService,
  type StaffDirectoryEntry,
} from '@/services/hr/staff-directory.service';
import { buildTree, type OrganogramTreeNode } from '@/types/hr/organogram';
import { useDebounce } from '@/hooks/use-debounce';
import { cn } from '@/lib/utils';

/**
 * The staff directory (area 25 slice 13a).
 *
 * ⚠ **Everything is server-side.** 8,007 people is ~4.1 MB of JSON; the old habit of fetching a
 * list and filtering it in a `useMemo` would ship the whole staff register to every browser and
 * then hide most of it. Search, unit filter and paging all go to the API, and the tree on the
 * left is the only thing held in memory (48 nodes, ~17 KB).
 *
 * ⚠ **Selecting a unit includes everything beneath it.** That is not a convenience — on this
 * tenant "Finance Department" has one direct member and 7,896 people in its child unit, so a
 * direct-membership browse would have shown one person and looked broken.
 *
 * The phone columns are empty on DEFAULT (no employee has a business number or extension), so
 * the row degrades to name / role / unit / email rather than rendering a row of dashes.
 */

function initials(name: string) {
  const parts = name.split(/\s+/).filter(Boolean);
  if (!parts.length) return '·';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** One branch of the browse tree. Collapsed by default below the roots — 48 nodes, 5 deep. */
function UnitBranch({
  entry,
  selectedId,
  onSelect,
  defaultOpen,
}: {
  entry: OrganogramTreeNode;
  selectedId: string | null;
  onSelect: (id: string, name: string) => void;
  defaultOpen: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  const hasChildren = entry.children.length > 0;
  const selected = selectedId === entry.node.id;
  // The subtree total, not the direct count — it is what selecting this unit will actually show.
  const reach = entry.node.totalEmployeeCount ?? entry.node.employeeCount ?? 0;

  return (
    <div>
      <div
        className={cn(
          'flex items-center gap-1 rounded-md pr-2 text-sm hover:bg-muted/60',
          selected && 'bg-muted font-medium',
        )}
      >
        {hasChildren ? (
          <button
            type="button"
            onClick={() => setOpen((o) => !o)}
            className="p-1 text-muted-foreground"
            aria-label={open ? `Collapse ${entry.node.name}` : `Expand ${entry.node.name}`}
          >
            {open ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
          </button>
        ) : (
          <span className="w-[22px]" />
        )}
        <button
          type="button"
          onClick={() => onSelect(entry.node.id, entry.node.name)}
          className="flex-1 truncate py-1 text-left"
          title={entry.node.name}
        >
          {entry.node.name}
        </button>
        {reach > 0 && <span className="shrink-0 text-xs text-muted-foreground">{reach}</span>}
      </div>

      {open && hasChildren && (
        <div className="ml-3 border-l pl-2">
          {entry.children.map((child) => (
            <UnitBranch
              key={child.node.id}
              entry={child}
              selectedId={selectedId}
              onSelect={onSelect}
              defaultOpen={false}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function PersonRow({ person }: { person: StaffDirectoryEntry }) {
  const phone = person.extension ?? person.businessNumber;
  return (
    <Link
      href={`/me/directory/${person.id}`}
      className="flex items-center gap-3 border-b px-4 py-3 last:border-b-0 hover:bg-muted/50"
    >
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
        {initials(person.fullName)}
      </span>

      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2">
          <span className="truncate font-medium">{person.fullName}</span>
          {person.isSelf && (
            <Badge variant="secondary" className="shrink-0 text-[10px]">
              You
            </Badge>
          )}
        </span>
        <span className="block truncate text-sm text-muted-foreground">{person.positionTitle}</span>
      </span>

      <span className="hidden min-w-0 flex-1 sm:block">
        <span className="block truncate text-sm">{person.organizationUnitName ?? '—'}</span>
        <span className="block truncate text-xs text-muted-foreground">
          {person.locationName ?? ''}
        </span>
      </span>

      <span className="hidden min-w-0 flex-1 md:block">
        <span className="flex items-center gap-1.5 truncate text-sm text-muted-foreground">
          <Mail className="h-3.5 w-3.5 shrink-0" />
          <span className="truncate">{person.emailAddress}</span>
        </span>
        {/* Rendered only when there is a number — every employee on DEFAULT has none. */}
        {phone && (
          <span className="flex items-center gap-1.5 truncate text-xs text-muted-foreground">
            <Phone className="h-3 w-3 shrink-0" />
            {phone}
          </span>
        )}
      </span>
    </Link>
  );
}

export default function StaffDirectoryPage() {
  const [rawSearch, setRawSearch] = useState('');
  const [unit, setUnit] = useState<{ id: string; name: string } | null>(null);
  const [page, setPage] = useState(1);

  // Typed characters must not each become a query against 8,000 rows.
  const search = useDebounce(rawSearch, 300);

  const tree = useQuery({
    queryKey: ['me', 'directory', 'units'],
    queryFn: () => staffDirectoryService.getUnitTree(),
    staleTime: 5 * 60 * 1000,
  });

  const roots = useMemo(() => buildTree(tree.data ?? []), [tree.data]);

  const results = useQuery({
    queryKey: ['me', 'directory', { search, unitId: unit?.id ?? null, page }],
    queryFn: () =>
      staffDirectoryService.search({
        search,
        organizationUnitId: unit?.id,
        page,
        pageSize: DIRECTORY_PAGE_SIZE,
      }),
    // Without this the list blanks between pages and the layout jumps on every keystroke.
    placeholderData: keepPreviousData,
  });

  const resetTo = (fn: () => void) => {
    fn();
    setPage(1);
  };

  const data = results.data;
  const people = data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Staff directory"
        description="Find a colleague — who they are, what they do and where they sit."
        backHref="/me"
      />

      <div className="grid gap-6 lg:grid-cols-[260px_1fr]">
        {/* ── Browse by organisation unit ─────────────────────────────────── */}
        <Card className="h-fit lg:sticky lg:top-4">
          <CardContent className="space-y-2 p-3">
            <div className="flex items-center justify-between px-1">
              <span className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                <Building2 className="h-3.5 w-3.5" /> Browse
              </span>
              {unit && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-xs"
                  onClick={() => resetTo(() => setUnit(null))}
                >
                  Clear
                </Button>
              )}
            </div>

            {tree.isLoading ? (
              <div className="space-y-2 p-1">
                <Skeleton className="h-5" />
                <Skeleton className="h-5" />
                <Skeleton className="h-5" />
              </div>
            ) : roots.length === 0 ? (
              <p className="px-1 py-2 text-xs text-muted-foreground">
                No organisation units are set up yet.
              </p>
            ) : (
              <div className="max-h-[60vh] overflow-y-auto">
                {roots.map((root) => (
                  <UnitBranch
                    key={root.node.id}
                    entry={root}
                    selectedId={unit?.id ?? null}
                    onSelect={(id, name) => resetTo(() => setUnit({ id, name }))}
                    defaultOpen
                  />
                ))}
              </div>
            )}
            <p className="px-1 pt-1 text-[11px] leading-snug text-muted-foreground">
              Choosing a unit includes everyone in the units beneath it.
            </p>
          </CardContent>
        </Card>

        {/* ── Search + results ─────────────────────────────────────────────── */}
        <div className="space-y-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={rawSearch}
              onChange={(e) => resetTo(() => setRawSearch(e.target.value))}
              placeholder="Search by name, staff number, email, job title or unit…"
              className="pl-9"
            />
          </div>

          {unit && (
            <div className="flex items-center gap-2">
              <Badge variant="secondary" className="gap-1">
                {unit.name}
                <button type="button" onClick={() => resetTo(() => setUnit(null))} aria-label="Clear unit filter">
                  <X className="h-3 w-3" />
                </button>
              </Badge>
              <span className="text-xs text-muted-foreground">and everything beneath it</span>
            </div>
          )}

          <p className="text-sm text-muted-foreground">
            {results.isLoading
              ? 'Searching…'
              : `${(data?.totalCount ?? 0).toLocaleString()} ${
                  (data?.totalCount ?? 0) === 1 ? 'person' : 'people'
                }`}
          </p>

          <Card>
            <CardContent className="p-0">
              {results.isLoading ? (
                <div className="space-y-3 p-4">
                  <Skeleton className="h-12" />
                  <Skeleton className="h-12" />
                  <Skeleton className="h-12" />
                </div>
              ) : results.isError ? (
                <p className="p-6 text-sm text-muted-foreground">
                  The directory could not be loaded right now. Try again in a moment.
                </p>
              ) : people.length === 0 ? (
                <div className="py-10">
                  <EmptyState
                    icon={Users}
                    title="Nobody matches"
                    description={
                      search
                        ? 'Try a shorter search, or clear the unit filter.'
                        : 'No one is listed in this part of the organisation.'
                    }
                  />
                </div>
              ) : (
                <div className={cn(results.isPlaceholderData && 'opacity-60')}>
                  {people.map((p) => (
                    <PersonRow key={p.id} person={p} />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {(data?.totalPages ?? 0) > 1 && (
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">
                Page {data?.page} of {data?.totalPages}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data?.hasPrevious || results.isFetching}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data?.hasNext || results.isFetching}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
