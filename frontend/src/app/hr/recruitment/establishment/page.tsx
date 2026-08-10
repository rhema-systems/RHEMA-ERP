'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, FilePlus2, Loader2, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { positionVacancyService } from '@/services/hr/recruitment.service';

/**
 * Establishment vs. actual headcount — the front of the recruitment funnel.
 *
 * A "position vacancy" here is a *gap*, not an advert: a position whose filled count is below its
 * establishment. Reconcile opens one wherever that is true and closes any that have since been
 * filled, and "Raise a requisition" turns a gap into a draft request for headcount.
 */
export default function EstablishmentPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyRole } = useAuth();
  const isHr = hasAnyRole(['SuperAdmin', 'HR']);

  const [onlyVacant, setOnlyVacant] = useState(true);
  const [includeClosed, setIncludeClosed] = useState(false);
  const [reconciling, setReconciling] = useState(false);
  const [raiseFor, setRaiseFor] = useState<string | null>(null);

  const stats = useQuery({
    queryKey: ['hr', 'position-vacancy-stats'],
    queryFn: () => positionVacancyService.getStats(),
  });

  const establishment = useQuery({
    queryKey: ['hr', 'establishment', onlyVacant],
    queryFn: () => positionVacancyService.getEstablishment(null, onlyVacant),
  });

  const vacancies = useQuery({
    queryKey: ['hr', 'position-vacancies', includeClosed],
    queryFn: () => positionVacancyService.getVacancies({ includeClosed }),
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'position-vacancy-stats'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'establishment'] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'position-vacancies'] });
  };

  const reconcile = useMutation({
    mutationFn: () => positionVacancyService.reconcile(),
    onSuccess: async (r) => {
      await refresh();
      toast({
        title: 'Reconciled',
        description: `${r.scanned} positions scanned — ${r.opened} vacancies opened, ${r.closed} closed.`,
      });
    },
    onError: (e: any) =>
      toast({ title: 'Could not reconcile', description: e?.message, variant: 'destructive' }),
  });

  const raise = async () => {
    if (!raiseFor) return false;
    setReconciling(true);
    try {
      const result = await positionVacancyService.raiseRequisition(raiseFor, {});
      await refresh();
      toast({
        title: 'Requisition raised',
        description: `${result.requisitionNumber} saved as a draft.`,
      });
      router.push(`/hr/recruitment/requisitions/${result.requisitionId}`);
      return true;
    } catch (e: any) {
      toast({ title: 'Refused', description: e?.message, variant: 'destructive' });
      return false;
    } finally {
      setReconciling(false);
      setRaiseFor(null);
    }
  };

  const s = stats.data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Establishment"
        description="Where headcount sits against the establishment, and the gaps that follow from it."
        backHref="/hr/recruitment"
        actions={
          isHr && (
            <Button
              variant="outline"
              onClick={() => reconcile.mutate()}
              disabled={reconcile.isPending}
            >
              <RefreshCw className={`mr-2 h-4 w-4 ${reconcile.isPending ? 'animate-spin' : ''}`} />
              Reconcile
            </Button>
          )
        }
      />

      {s && (
        <MetricTiles
          tiles={[
            { label: 'Open gaps', value: s.totalOpen },
            { label: 'Anticipated', value: s.anticipated },
            {
              label: 'Requisition raised',
              value: s.requisitionRaised,
              hint: 'Already asked for',
            },
            {
              label: 'Positions affected',
              value: `${s.positionsWithVacancy} of ${s.totalPositions}`,
            },
          ]}
        />
      )}

      <Tabs defaultValue="gaps">
        <TabsList>
          <TabsTrigger value="gaps">Vacancies</TabsTrigger>
          <TabsTrigger value="establishment">Position establishment</TabsTrigger>
        </TabsList>

        <TabsContent value="gaps" className="space-y-4 pt-4">
          <div className="flex items-center gap-2">
            <Checkbox
              id="includeClosed"
              checked={includeClosed}
              onCheckedChange={(c) => setIncludeClosed(c === true)}
            />
            <Label htmlFor="includeClosed" className="font-normal">
              Include filled and closed
            </Label>
          </div>

          <Card>
            <CardContent className="p-0">
              {vacancies.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (vacancies.data?.length ?? 0) === 0 ? (
                <div className="py-10">
                  <EmptyState
                    icon={Building2}
                    title="No open gaps"
                    description="Every position is at establishment. Reconcile to re-check."
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Position</TableHead>
                      <TableHead>Unit</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>Vacated by</TableHead>
                      <TableHead>Since</TableHead>
                      <TableHead>Classification</TableHead>
                      <TableHead>Status</TableHead>
                      {isHr && <TableHead className="w-40" />}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(vacancies.data ?? []).map((pv) => (
                      <TableRow key={pv.id}>
                        <TableCell className="font-medium">{pv.positionTitle}</TableCell>
                        <TableCell>{pv.organizationUnitName || '—'}</TableCell>
                        <TableCell>{humanizeEnum(pv.reason)}</TableCell>
                        <TableCell>{pv.vacatedByEmployeeName || '—'}</TableCell>
                        <TableCell>{formatDate(pv.vacatedDate)}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {humanizeEnum(pv.classification)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={pv.status} />
                        </TableCell>
                        {isHr && (
                          <TableCell>
                            {pv.staffRequisitionId ? (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() =>
                                  router.push(`/hr/recruitment/requisitions/${pv.staffRequisitionId}`)
                                }
                              >
                                View requisition
                              </Button>
                            ) : (
                              <Button variant="outline" size="sm" onClick={() => setRaiseFor(pv.id)}>
                                <FilePlus2 className="mr-2 h-4 w-4" /> Raise
                              </Button>
                            )}
                          </TableCell>
                        )}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="establishment" className="space-y-4 pt-4">
          <div className="flex items-center gap-2">
            <Checkbox
              id="onlyVacant"
              checked={onlyVacant}
              onCheckedChange={(c) => setOnlyVacant(c === true)}
            />
            <Label htmlFor="onlyVacant" className="font-normal">
              Only positions below establishment
            </Label>
          </div>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Expected against actual</CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {establishment.isLoading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (establishment.data?.length ?? 0) === 0 ? (
                <div className="py-10">
                  <EmptyState
                    title={onlyVacant ? 'Nothing below establishment' : 'No positions'}
                    description={
                      onlyVacant
                        ? 'Every position is at or above its expected headcount.'
                        : 'Positions appear here once the establishment is set.'
                    }
                  />
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Position</TableHead>
                      <TableHead>Unit</TableHead>
                      <TableHead className="text-right">Expected</TableHead>
                      <TableHead className="text-right">Filled</TableHead>
                      <TableHead className="text-right">Gap</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(establishment.data ?? []).map((p) => (
                      <TableRow key={p.positionId}>
                        <TableCell className="font-medium">{p.positionTitle}</TableCell>
                        <TableCell>{p.organizationUnitName || '—'}</TableCell>
                        <TableCell className="text-right tabular-nums">{p.expectedHeadcount}</TableCell>
                        <TableCell className="text-right tabular-nums">{p.filledCount}</TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                          {p.vacantCount}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={raiseFor !== null}
        onOpenChange={(open) => !open && setRaiseFor(null)}
        title="Raise a requisition for this vacancy"
        description="A draft replacement requisition is created from the vacancy — the position, the outgoing employee and the reason are carried across. You can edit it before submitting."
        confirmText={reconciling ? 'Raising…' : 'Raise'}
        onConfirm={raise}
      />
    </div>
  );
}
