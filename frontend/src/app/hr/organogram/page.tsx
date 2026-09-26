'use client';

import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Building2, RefreshCw, ShieldAlert, TriangleAlert, Unlink } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { OrgChart } from '@/components/hr/organogram/OrgChart';
import {
  DEFAULT_VIEW_STATE,
  parseViewState,
  serializeViewState,
  type OrganogramViewState,
} from '@/components/hr/organogram/view-state';
import { useTenant } from '@/contexts/TenantContext';
import { organogramService } from '@/services/hr/organogram.service';
import { locationStructureService } from '@/services/hr/location-structure.service';
import {
  ORGANOGRAM_DIMENSIONS,
  buildTree,
  measureHealth,
  type OrganogramDimension,
  type OrganogramHealth,
} from '@/types/hr/organogram';

/**
 * The organogram — one screen, one renderer, one view per dimension (plan Decision 5), redrawn
 * 2026-09-08 after demo feedback that the connectors were illegible and the chart could not be
 * put in full view.
 *
 * The page owns the data and the URL; `OrgChart` owns the picture. Everything a reader might want
 * to send to a colleague — dimension, focus, selection, depth, layout, colouring, search — lives in
 * the query string, so a link opens the same view.
 *
 * ⚠ On TDC's live data only 181 of 6,286 employees have a manager recorded and no unit has a head,
 * so a chart drawn without comment would look like a flat organisation rather than an unpopulated
 * one. The coverage strip says which it is, every time — see [[hr-deferred-modules]].
 */

export default function OrganogramPage() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <OrganogramScreen />
    </Suspense>
  );
}

function OrganogramScreen() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const { currentTenant } = useTenant();

  const [view, setView] = useState<OrganogramViewState>(() => parseViewState(searchParams));
  const onView = useCallback((patch: Partial<OrganogramViewState>) => setView((prev) => ({ ...prev, ...patch })), []);

  // Write the view to the URL, debounced so typing a search does not spam history.
  const firstWrite = useRef(true);
  useEffect(() => {
    if (firstWrite.current) {
      firstWrite.current = false;
      return;
    }
    const handle = setTimeout(() => {
      const qs = serializeViewState(view);
      const current = searchParams?.toString() ?? '';
      if (qs !== current) router.replace(`${pathname}${qs ? `?${qs}` : ''}`, { scroll: false });
    }, 250);
    return () => clearTimeout(handle);
  }, [view, pathname, router, searchParams]);

  const spec = ORGANOGRAM_DIMENSIONS.find((d) => d.key === view.dimension) ?? ORGANOGRAM_DIMENSIONS[0];

  // The locations view cannot be fetched without a structure, and there is no server-side default:
  // the endpoint answers a clean 400 on an empty id. Resolve the tenant's default before asking.
  const structuresQuery = useQuery({
    queryKey: ['location-structures', 'organogram'],
    queryFn: () => locationStructureService.getAll(),
    enabled: spec.needsStructure,
    staleTime: 5 * 60 * 1000,
  });
  const structures = useMemo(() => structuresQuery.data ?? [], [structuresQuery.data]);

  useEffect(() => {
    if (!spec.needsStructure || !structures.length) return;
    if (view.structureId && structures.some((s) => s.id === view.structureId)) return;
    onView({ structureId: (structures.find((s) => s.isDefault) ?? structures[0]).id });
  }, [spec.needsStructure, view.structureId, structures, onView]);

  const ready = !spec.needsStructure || !!view.structureId;

  const chartQuery = useQuery({
    queryKey: ['organogram', view.dimension, spec.needsStructure ? view.structureId : ''],
    queryFn: () => organogramService.byDimension(view.dimension, view.structureId || undefined),
    enabled: ready,
    // The people payload is ~2.3 MB. Re-fetching it on every tab switch is the difference between
    // a snappy screen and a sluggish one; the org structure does not change by the minute.
    staleTime: 5 * 60 * 1000,
  });

  const nodes = useMemo(() => chartQuery.data?.nodes ?? [], [chartQuery.data]);
  const roots = useMemo(() => buildTree(nodes), [nodes]);
  const health = useMemo(() => measureHealth(nodes, roots), [nodes, roots]);
  const forbidden = (chartQuery.error as { status?: number } | null)?.status === 403;
  const structureName = spec.needsStructure ? structures.find((s) => s.id === view.structureId)?.name ?? null : null;

  const switchDimension = (next: string) => {
    setView((prev) => ({
      ...prev,
      dimension: next as OrganogramDimension,
      focusId: null,
      selectedId: null,
      query: '',
      heat: DEFAULT_VIEW_STATE.heat,
      vacantOnly: false,
    }));
  };

  const generated = chartQuery.data?.generatedAtUtc ? new Date(chartQuery.data.generatedAtUtc) : null;

  return (
    <div className="flex min-h-[calc(100dvh-4rem)] flex-col gap-4 p-6">
      <PageHeader
        title="Organogram"
        description="The organisation drawn from its own records — units, posts, reporting lines, teams and sites."
        actions={
          <div className="flex items-center gap-3">
            {generated && (
              <span className="text-muted-foreground hidden text-xs sm:inline">
                As at {generated.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
              </span>
            )}
            <Button variant="outline" size="sm" onClick={() => chartQuery.refetch()} disabled={chartQuery.isFetching}>
              <RefreshCw className={chartQuery.isFetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
              Refresh
            </Button>
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <Tabs value={view.dimension} onValueChange={switchDimension}>
          <TabsList>
            {ORGANOGRAM_DIMENSIONS.map((d) => (
              <TabsTrigger key={d.key} value={d.key}>
                {d.label}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
        <p className="text-muted-foreground text-sm">{spec.description}</p>
        {spec.needsStructure && (
          <Select value={view.structureId} onValueChange={(v) => onView({ structureId: v, focusId: null, selectedId: null })}>
            <SelectTrigger className="ml-auto w-[16rem]">
              <SelectValue placeholder="Choose a location structure" />
            </SelectTrigger>
            <SelectContent>
              {structures.map((s) => (
                <SelectItem key={s.id} value={s.id}>
                  {s.name}
                  {s.isDefault ? ' (default)' : ''}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>

      {forbidden ? (
        <Alert>
          <ShieldAlert className="h-4 w-4" />
          <AlertTitle>The people view is restricted</AlertTitle>
          <AlertDescription>
            The reporting-line chart is the personnel register — every employee, with contact details — so it is
            limited to HR and tenant administrators. The units, positions, teams and locations views are open to you.
          </AlertDescription>
        </Alert>
      ) : chartQuery.isError ? (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>The {spec.label.toLowerCase()} chart could not be loaded</AlertTitle>
          <AlertDescription>{(chartQuery.error as Error)?.message}</AlertDescription>
        </Alert>
      ) : chartQuery.isLoading || !ready ? (
        <PageSkeleton chartOnly />
      ) : !nodes.length ? (
        <EmptyState
          icon={Building2}
          title={`No ${spec.label.toLowerCase()} to chart`}
          description="Nothing has been recorded for this dimension yet."
        />
      ) : (
        <>
          <CoverageStrip dimension={view.dimension} label={spec.label} health={health} />
          <OrgChart
            className="min-h-[34rem] flex-1"
            roots={roots}
            nodes={nodes}
            dimension={view.dimension}
            dimensionLabel={spec.label}
            structureName={structureName}
            view={view}
            onView={onView}
            tenant={{ name: currentTenant?.name ?? null, logoUrl: currentTenant?.logoUrl ?? null }}
          />
        </>
      )}
    </div>
  );
}

/**
 * What the chart cannot say for itself: the shape of the data, as numbers. A tree whose every node
 * is a root is not a hierarchy, and a reader looking at boxes has no way to tell an organisation
 * that is genuinely flat from one whose reporting lines were never entered.
 */
function CoverageStrip({ dimension, label, health }: { dimension: OrganogramDimension; label: string; health: OrganogramHealth }) {
  const { realNodeCount, unlinkedCount, maxDepth, widestFanOut, vacantCount, inactiveCount, headcount } = health;
  const placed = realNodeCount - unlinkedCount;
  const unlinkedShare = realNodeCount ? Math.round((unlinkedCount / realNodeCount) * 100) : 0;
  const mostlyUnplaced = realNodeCount > 0 && unlinkedShare >= 50;

  const tiles: { label: string; value: string; hint?: string }[] = [
    { label, value: realNodeCount.toLocaleString() },
    { label: 'Levels deep', value: String(Math.max(0, maxDepth - (health.hasSyntheticRoot ? 1 : 0)) || 1) },
    { label: 'Widest branch', value: widestFanOut.toLocaleString() },
    {
      label: 'Placed in the hierarchy',
      value: realNodeCount ? `${Math.round((placed / realNodeCount) * 100)}%` : '—',
      hint: `${placed.toLocaleString()} of ${realNodeCount.toLocaleString()} have a parent recorded`,
    },
  ];
  if (dimension === 'units') {
    tiles.push({ label: 'Staff placed', value: (headcount ?? 0).toLocaleString(), hint: 'On strength, attached to a unit' });
    tiles.push({ label: 'Units without a head', value: vacantCount.toLocaleString() });
  } else if (dimension === 'positions') {
    tiles.push({ label: 'Holders', value: (headcount ?? 0).toLocaleString(), hint: 'People on strength holding a post' });
    tiles.push({ label: 'Vacant posts', value: vacantCount.toLocaleString() });
  } else if (dimension === 'teams') {
    tiles.push({ label: 'Teams without a lead', value: vacantCount.toLocaleString() });
  }
  if (inactiveCount > 0) tiles.push({ label: 'Inactive', value: inactiveCount.toLocaleString() });

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6 xl:grid-cols-7">
        {tiles.map((t) => (
          <div key={t.label} className="bg-card rounded-lg border px-3 py-2" title={t.hint}>
            <div className="text-xl font-semibold leading-tight">{t.value}</div>
            <div className="text-muted-foreground truncate text-xs">{t.label}</div>
          </div>
        ))}
      </div>
      {mostlyUnplaced ? (
        <Alert>
          <Unlink className="h-4 w-4" />
          <AlertTitle>Most {label.toLowerCase()} are not placed in the hierarchy</AlertTitle>
          <AlertDescription>
            {unlinkedCount.toLocaleString()} of {realNodeCount.toLocaleString()} ({unlinkedShare}%) sit at the top because
            {dimension === 'people' ? ' no manager is recorded against them' : ' no parent is recorded against them'}. They are
            grouped under one placeholder. This chart is a picture of what has been entered, not of a flat organisation;
            only {placed.toLocaleString()} {label.toLowerCase()} are actually placed.
          </AlertDescription>
        </Alert>
      ) : unlinkedCount > 1 ? (
        <p className="text-muted-foreground text-xs">
          {unlinkedCount} {label.toLowerCase()} have no parent recorded and are shown side by side under a placeholder root.
        </p>
      ) : null}
    </div>
  );
}

function PageSkeleton({ chartOnly = false }: { chartOnly?: boolean }) {
  return (
    <div className={chartOnly ? 'space-y-3' : 'space-y-4 p-6'}>
      {!chartOnly && (
        <>
          <Skeleton className="h-9 w-64" />
          <Skeleton className="h-10 w-96" />
        </>
      )}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-14" />
        ))}
      </div>
      <Skeleton className="h-[34rem] w-full rounded-xl" />
    </div>
  );
}
