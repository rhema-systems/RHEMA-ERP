'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Gauge,
  RefreshCw,
  Search,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { procurementSupplierPerformanceService as service } from '@/services/procurement-supplier-performance.service';

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';
const formatScore = (value?: number) =>
  value === undefined || value === null ? '—' : value.toFixed(2);
const shortHash = (value?: string) =>
  value ? `${value.slice(0, 12)}…${value.slice(-8)}` : '—';

export default function SupplierPerformanceScorecardsPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canRead =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage') ||
    hasPermission('procurement.supplier.approve');
  const canCalculate =
    hasPermission('procurement.supplier.review') ||
    hasPermission('procurement.supplier.manage');
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState<string>();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [supplierId, setSupplierId] = useState('');

  const summary = useQuery({
    queryKey: ['supplier-performance-summary'],
    queryFn: service.summary,
    enabled: canRead,
  });
  const history = useQuery({
    queryKey: ['supplier-performance-history', search],
    queryFn: () =>
      service.search({ search: search || undefined, page: 1, pageSize: 100 }),
    enabled: canRead,
  });
  const detail = useQuery({
    queryKey: ['supplier-performance-detail', selectedId],
    queryFn: () => service.get(selectedId ?? ''),
    enabled: canRead && Boolean(selectedId),
  });
  const suppliers = useQuery({
    queryKey: ['supplier-performance-suppliers'],
    queryFn: service.supplierOptions,
    enabled: canRead,
  });

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ['supplier-performance-summary'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-performance-history'],
      }),
      queryClient.invalidateQueries({
        queryKey: ['supplier-performance-detail'],
      }),
    ]);
  };

  const calculate = useMutation({
    mutationFn: () =>
      service.calculate({
        businessPartnerId: supplierId,
        idempotencyKey: crypto.randomUUID(),
        sourceType: 'SupplierPerformanceAdministration',
        sourceReference: 'Manual governed scorecard calculation',
      }),
    onSuccess: async (result) => {
      setSelectedId(result.id);
      setSupplierId('');
      setDialogOpen(false);
      await invalidate();
      toast.success('Governed supplier performance scorecard calculated');
    },
    onError: (error: Error) => toast.error(error.message),
  });

  if (!canRead) {
    return (
      <Alert variant="destructive">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>Access denied</AlertTitle>
        <AlertDescription>
          Supplier-review permission is required for this control.
        </AlertDescription>
      </Alert>
    );
  }

  const data = summary.data;
  const selected = detail.data;

  return (
    <div className="space-y-6 p-6" data-testid="supplier-performance-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Supplier Performance Scorecards
          </h1>
          <p className="text-sm text-muted-foreground">
            Immutable DEC-011 scorecards derived from PO, receipt, quality,
            price, response, complaint, and contract evidence.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void invalidate()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button
            disabled={!canCalculate || !data?.policyAvailable}
            onClick={() => setDialogOpen(true)}
          >
            <Gauge className="mr-2 h-4 w-4" />
            Calculate scorecard
          </Button>
        </div>
      </div>

      {!data?.policyAvailable && (
        <Alert data-testid="supplier-performance-policy-gate">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>DEC-011 controlled release gate</AlertTitle>
          <AlertDescription>
            {data?.policyReleaseGate ??
              'Publish and evidence one complete performance-scorecard policy before calculating.'}
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 md:grid-cols-5">
        {[
          ['Suppliers', data?.supplierCount ?? 0],
          ['Scored', data?.scoredSupplierCount ?? 0],
          ['Current', data?.currentScorecardCount ?? 0],
          ['Below minimum', data?.belowMinimumCount ?? 0],
          ['Coverage gaps', data?.insufficientCoverageCount ?? 0],
        ].map(([label, value]) => (
          <Card key={String(label)}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">{label}</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-semibold">{value}</CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader className="space-y-3">
          <CardTitle>Immutable scorecard history</CardTitle>
          <div className="relative max-w-md">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-9"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search supplier or scorecard reference"
            />
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Reference</TableHead>
                <TableHead>Supplier</TableHead>
                <TableHead>Score / band</TableHead>
                <TableHead>Coverage</TableHead>
                <TableHead>Calculated</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(history.data?.items ?? []).map((item) => (
                <TableRow
                  key={item.id}
                  className="cursor-pointer"
                  onClick={() => setSelectedId(item.id)}
                >
                  <TableCell className="font-mono text-xs">
                    {item.scorecardReference}
                  </TableCell>
                  <TableCell>
                    <div className="font-medium">{item.partnerName}</div>
                    <div className="text-xs text-muted-foreground">
                      {item.partnerCode}
                    </div>
                  </TableCell>
                  <TableCell>
                    {formatScore(item.overallScore)} ·{' '}
                    {item.performanceBand ?? 'Unresolved'}
                  </TableCell>
                  <TableCell>{item.dataCoveragePercent.toFixed(2)}%</TableCell>
                  <TableCell>{formatDate(item.calculatedAtUtc)}</TableCell>
                  <TableCell>
                    <Badge variant={item.isCurrent ? 'default' : 'secondary'}>
                      {item.dataStatus}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
              {!history.isLoading && (history.data?.items.length ?? 0) === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="h-24 text-center">
                    No governed scorecards have been calculated.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {selected && (
        <div className="grid gap-4 xl:grid-cols-[1.1fr_1.9fr]">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                {selected.minimumScoreBreached ? (
                  <AlertTriangle className="h-5 w-5 text-amber-600" />
                ) : (
                  <CheckCircle2 className="h-5 w-5 text-emerald-600" />
                )}
                {selected.scorecardReference}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <span className="text-muted-foreground">Score</span>
                  <p className="font-semibold">
                    {formatScore(selected.overallScore)}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Band</span>
                  <p className="font-semibold">
                    {selected.performanceBand ?? 'Unresolved'}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Coverage</span>
                  <p>{selected.dataCoveragePercent.toFixed(2)}%</p>
                </div>
                <div>
                  <span className="text-muted-foreground">Minimum</span>
                  <p>{selected.minimumScore.toFixed(2)}</p>
                </div>
              </div>
              <div>
                <span className="text-muted-foreground">Period</span>
                <p>
                  {formatDate(selected.periodStartUtc)} –{' '}
                  {formatDate(selected.periodEndUtc)}
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">Policy</span>
                <p>
                  {selected.policyProfileCode}/v{selected.policyProfileVersion}
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">Source hash</span>
                <p className="font-mono text-xs">
                  {shortHash(selected.sourceSnapshotHash)}
                </p>
              </div>
              <div>
                <span className="text-muted-foreground">Integrity hash</span>
                <p className="font-mono text-xs">
                  {shortHash(selected.integrityHash)}
                </p>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Activity className="h-5 w-5" />
                Explainable measures
              </CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Measure</TableHead>
                    <TableHead>Score</TableHead>
                    <TableHead>Weight</TableHead>
                    <TableHead>Observations</TableHead>
                    <TableHead>Source</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {selected.measures.map((measure) => (
                    <TableRow key={measure.metric}>
                      <TableCell className="font-medium">
                        {measure.metric}
                      </TableCell>
                      <TableCell>{formatScore(measure.score)}</TableCell>
                      <TableCell>{measure.weightPercent.toFixed(2)}%</TableCell>
                      <TableCell>{measure.observationCount}</TableCell>
                      <TableCell className="max-w-sm text-xs">
                        {measure.missingReason ?? measure.source}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              {selected.findings.length > 0 && (
                <div className="mt-4 space-y-2">
                  {selected.findings.map((finding) => (
                    <Alert key={`${finding.code}-${finding.message}`}>
                      <AlertTriangle className="h-4 w-4" />
                      <AlertTitle>{finding.code}</AlertTitle>
                      <AlertDescription>{finding.message}</AlertDescription>
                    </Alert>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Calculate governed scorecard</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Supplier</Label>
            <Select value={supplierId} onValueChange={setSupplierId}>
              <SelectTrigger>
                <SelectValue placeholder="Select supplier" />
              </SelectTrigger>
              <SelectContent>
                {(suppliers.data ?? []).map((supplier) => (
                  <SelectItem key={supplier.id} value={supplier.id}>
                    {supplier.code} · {supplier.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              The server derives every measure and retains source, policy,
              supplier-control, and risk hashes. No score can be entered here.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={!supplierId || calculate.isPending}
              onClick={() => calculate.mutate()}
            >
              Calculate
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
