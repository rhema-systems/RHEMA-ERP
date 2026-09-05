'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Building2,
  Info,
  Minus,
  Plus,
  RefreshCw,
  Search,
  ShieldAlert,
  TriangleAlert,
  Unlink,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
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
import { organogramService } from '@/services/hr/organogram.service';
import { locationStructureService } from '@/services/hr/location-structure.service';
import {
  ORGANOGRAM_DIMENSIONS,
  SYNTHETIC_ROOT_ID,
  buildTree,
  matchesQuery,
  measureHealth,
  type OrganogramDimension,
  type OrganogramNode,
} from '@/types/hr/organogram';

/**
 * The organogram — one screen, one renderer, one view per dimension (plan Decision 5).
 *
 * The endpoints have existed since the port and had never been rendered. Slice 0 proved the engine
 * runs; slice 4 gates it, corrects what it counts, and puts it on screen.
 *
 * ⚠ The screen's job is not only to draw the chart. On TDC's live data only 181 of 6,286 employees
 * have a manager recorded and no unit at all has a head, so a chart drawn without comment would
 * look like a flat organisation rather than an unpopulated one. The coverage panel says which it
 * is, every time, from the numbers in the payload — see [[hr-deferred-modules]], where populating
 * that data is a programme this slice does not own.
 */

const ZOOM_STEPS = [0.5, 0.65, 0.8, 1, 1.2, 1.5];

export default function OrganogramPage() {
  const [dimension, setDimension] = useState<OrganogramDimension>('units');
  const [structureId, setStructureId] = useState<string>('');
  const [query, setQuery] = useState('');
  const [zoomIndex, setZoomIndex] = useState(3);
  const [expandDepth, setExpandDepth] = useState(2);
  const [selected, setSelected] = useState<OrganogramNode | null>(null);

  const spec =
    ORGANOGRAM_DIMENSIONS.find((d) => d.key === dimension) ?? ORGANOGRAM_DIMENSIONS[0];

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
    if (!spec.needsStructure || structureId || !structures.length) return;
    setStructureId((structures.find((s) => s.isDefault) ?? structures[0]).id);
  }, [spec.needsStructure, structureId, structures]);

  const ready = !spec.needsStructure || !!structureId;

  const chartQuery = useQuery({
    queryKey: ['organogram', dimension, structureId],
    queryFn: () => organogramService.byDimension(dimension, structureId || undefined),
    enabled: ready,
    // The people payload is ~2.3 MB. Re-fetching it on every tab switch is the difference between
    // a snappy screen and a sluggish one; the org structure does not change by the minute.
    staleTime: 5 * 60 * 1000,
  });

  const nodes = useMemo(() => chartQuery.data?.nodes ?? [], [chartQuery.data]);
  const roots = useMemo(() => buildTree(nodes), [nodes]);
  const health = useMemo(() => measureHealth(nodes, roots), [nodes, roots]);
  const hitCount = useMemo(
    () => (query.trim() ? nodes.filter((n) => matchesQuery(n, query)).length : 0),
    [nodes, query],
  );

  const forbidden = (chartQuery.error as { status?: number } | null)?.status === 403;

  const switchDimension = (next: string) => {
    setDimension(next as OrganogramDimension);
    setSelected(null);
    setQuery('');
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Organogram"
        description="The organisation drawn from its own records — units, posts, reporting lines and sites."
        actions={
          <Button
            variant="outline"
            size="sm"
            onClick={() => chartQuery.refetch()}
            disabled={chartQuery.isFetching}
          >
            <RefreshCw className={chartQuery.isFetching ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
            Refresh
          </Button>
        }
      />

      <Tabs value={dimension} onValueChange={switchDimension}>
        <TabsList>
          {ORGANOGRAM_DIMENSIONS.map((d) => (
            <TabsTrigger key={d.key} value={d.key}>
              {d.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-[16rem] flex-1">
          <Search className="text-muted-foreground absolute top-1/2 left-2 h-4 w-4 -translate-y-1/2" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={`Search ${spec.label.toLowerCase()} by name, title or code…`}
            className="pl-8"
          />
        </div>

        {spec.needsStructure && (
          <Select value={structureId} onValueChange={setStructureId}>
            <SelectTrigger className="w-[16rem]">
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

        <div className="flex items-center gap-1">
          <span className="text-muted-foreground text-xs">Levels open</span>
          <Select value={String(expandDepth)} onValueChange={(v) => setExpandDepth(Number(v))}>
            <SelectTrigger className="w-[5.5rem]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[1, 2, 3, 4, 5, 99].map((n) => (
                <SelectItem key={n} value={String(n)}>
                  {n === 99 ? 'All' : n}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="icon"
            aria-label="Zoom out"
            disabled={zoomIndex === 0}
            onClick={() => setZoomIndex((i) => Math.max(0, i - 1))}
          >
            <Minus className="h-4 w-4" />
          </Button>
          <span className="text-muted-foreground w-12 text-center text-xs tabular-nums">
            {Math.round(ZOOM_STEPS[zoomIndex] * 100)}%
          </span>
          <Button
            variant="outline"
            size="icon"
            aria-label="Zoom in"
            disabled={zoomIndex === ZOOM_STEPS.length - 1}
            onClick={() => setZoomIndex((i) => Math.min(ZOOM_STEPS.length - 1, i + 1))}
          >
            <Plus className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {forbidden ? (
        <Alert>
          <ShieldAlert className="h-4 w-4" />
          <AlertTitle>The people view is restricted</AlertTitle>
          <AlertDescription>
            The reporting-line chart is the personnel register — every employee, with contact
            details — so it is limited to HR and tenant administrators. The units, positions and
            locations views are open to you.
          </AlertDescription>
        </Alert>
      ) : chartQuery.isError ? (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>The {spec.label.toLowerCase()} chart could not be loaded</AlertTitle>
          <AlertDescription>{(chartQuery.error as Error)?.message}</AlertDescription>
        </Alert>
      ) : (
        <>
          <CoveragePanel
            dimension={dimension}
            label={spec.label}
            description={spec.description}
            health={health}
            loading={chartQuery.isLoading || !ready}
            query={query}
            hitCount={hitCount}
          />

          <div className="grid gap-4 lg:grid-cols-[1fr_20rem]">
            <Card className="overflow-hidden p-0">
              {chartQuery.isLoading || !ready ? (
                <div className="space-y-3 p-6">
                  <Skeleton className="mx-auto h-24 w-52" />
                  <Skeleton className="mx-auto h-24 w-[42rem]" />
                </div>
              ) : !nodes.length ? (
                <EmptyState
                  icon={Building2}
                  title={`No ${spec.label.toLowerCase()} to chart`}
                  description="Nothing has been recorded for this dimension yet."
                />
              ) : query.trim() && hitCount === 0 ? (
                <EmptyState
                  icon={Search}
                  title="Nothing matches that search"
                  description={`No ${spec.label.toLowerCase()} match “${query}”. Clear the search to see the whole chart.`}
                />
              ) : (
                <OrgChart
                  // Remounting on a depth change is deliberate: it drops the per-node overrides so
                  // "Levels open: 3" means the same thing on the tenth use as on the first.
                  key={`${dimension}-${structureId}-${expandDepth}`}
                  roots={roots}
                  dimension={dimension}
                  defaultExpandDepth={expandDepth}
                  query={query}
                  selectedId={selected?.id ?? null}
                  onSelect={setSelected}
                  zoom={ZOOM_STEPS[zoomIndex]}
                />
              )}
            </Card>

            <NodeDetail node={selected} />
          </div>
        </>
      )}
    </div>
  );
}

/**
 * What the chart cannot say for itself.
 *
 * Deliberately states the shape of the data rather than only its size. A tree whose every node is
 * a root is not a hierarchy, and a reader looking at boxes has no way to tell an organisation that
 * is genuinely flat from one whose reporting lines were never entered.
 */
function CoveragePanel({
  dimension,
  label,
  description,
  health,
  loading,
  query,
  hitCount,
}: {
  dimension: OrganogramDimension;
  label: string;
  description: string;
  health: ReturnType<typeof measureHealth>;
  loading: boolean;
  query: string;
  hitCount: number;
}) {
  if (loading) return <Skeleton className="h-16 w-full" />;

  const { realNodeCount, unlinkedCount, maxDepth, widestFanOut } = health;
  const linked = realNodeCount - unlinkedCount;
  const unlinkedShare = realNodeCount ? Math.round((unlinkedCount / realNodeCount) * 100) : 0;
  // One unplaced node is an orphan; most of them unplaced means the hierarchy was never recorded.
  const mostlyUnplaced = realNodeCount > 0 && unlinkedShare >= 50;

  return (
    <Alert variant={mostlyUnplaced ? 'default' : 'default'}>
      {mostlyUnplaced ? <Unlink className="h-4 w-4" /> : <Info className="h-4 w-4" />}
      <AlertTitle className="flex flex-wrap items-center gap-2">
        {realNodeCount.toLocaleString()} {label.toLowerCase()}
        <Badge variant="secondary">{maxDepth} levels deep</Badge>
        <Badge variant="secondary">{widestFanOut.toLocaleString()} widest branch</Badge>
        {query.trim() && <Badge variant="outline">{hitCount} matching “{query}”</Badge>}
      </AlertTitle>
      <AlertDescription className="space-y-1">
        <p>{description}</p>
        {mostlyUnplaced ? (
          <p>
            <strong>
              {unlinkedCount.toLocaleString()} of {realNodeCount.toLocaleString()} ({unlinkedShare}%)
              sit at the top because nothing places them
            </strong>{' '}
            — {dimension === 'people' ? 'no manager is recorded against them' : 'no parent is recorded against them'}.
            They are grouped under one placeholder node. This chart is therefore a picture of what
            has been entered, not of a flat organisation; only {linked.toLocaleString()}{' '}
            {label.toLowerCase()} are actually placed in the hierarchy.
          </p>
        ) : unlinkedCount > 1 ? (
          <p>
            {unlinkedCount} {label.toLowerCase()} have no parent recorded and are shown side by side
            under a placeholder root.
          </p>
        ) : null}
        {dimension === 'units' && (
          <p className="text-xs">
            Headcount is shown as <em>direct · total</em>: staff attached to the unit itself, then
            staff in it and everything beneath it. Leavers are excluded from both.
          </p>
        )}
        {dimension === 'positions' && (
          <p className="text-xs">
            A post reads as vacant when nobody on strength holds it. The second figure is the
            authorised establishment for that post.
          </p>
        )}
        {dimension === 'teams' && (
          <p className="text-xs">
            Membership counts people whose membership is live and who are still on strength. The
            second figure, where present, is the team&rsquo;s cap.
          </p>
        )}
      </AlertDescription>
    </Alert>
  );
}

function NodeDetail({ node }: { node: OrganogramNode | null }) {
  if (!node) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Details</CardTitle>
          <CardDescription>Select a box to see everything recorded against it.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const isSynthetic = node.id === SYNTHETIC_ROOT_ID;
  const rows: [string, string][] = [];
  if (node.title) rows.push(['Type', node.title]);
  if (node.code) rows.push(['Code', node.code]);
  if (node.headName) rows.push(['Head', node.headName]);
  if (node.employeeCount !== null) rows.push(['Direct headcount', String(node.employeeCount)]);
  if (node.totalEmployeeCount !== null)
    rows.push(['Including everything below', String(node.totalEmployeeCount)]);
  if (node.expectedHeadcount !== null)
    rows.push(['Authorised establishment', String(node.expectedHeadcount)]);
  for (const [k, v] of Object.entries(node.meta)) rows.push([k, v]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{node.name}</CardTitle>
        <CardDescription>
          {isSynthetic
            ? 'A placeholder the server adds when a dimension has more than one top-level node. It is not a record.'
            : (node.title ?? 'No type recorded')}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex flex-wrap gap-1">
          {node.badge && <Badge variant="outline">{node.badge}</Badge>}
          {!node.isActive && <Badge variant="outline">Inactive</Badge>}
        </div>
        {rows.length ? (
          <dl className="space-y-2 text-sm">
            {rows.map(([k, v]) => (
              <div key={k} className="grid grid-cols-2 gap-2">
                <dt className="text-muted-foreground">{k}</dt>
                <dd className="break-words">{v}</dd>
              </div>
            ))}
          </dl>
        ) : (
          <p className="text-muted-foreground text-sm">Nothing else is recorded against this node.</p>
        )}
      </CardContent>
    </Card>
  );
}
