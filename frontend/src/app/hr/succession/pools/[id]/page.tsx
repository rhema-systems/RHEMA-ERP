'use client';

import { use, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, UserMinus, Users2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { talentPoolService } from '@/services/hr/succession.service';
import { TalentPoolFormDialog } from '@/components/hr/succession/TalentPoolFormDialog';
import { TalentPoolMemberDialog } from '@/components/hr/succession/TalentPoolMemberDialog';
import type { TalentPoolMemberSummary } from '@/types/hr/succession';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const spaced = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z0-9])/g, '$1 $2') : '—');

const READINESS_TONE: Record<string, string> = {
  ReadyNow: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  ReadyIn12Months: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  ReadyIn24Months: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  ReadyIn36PlusMonths: 'bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-200',
  NotReady: 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200',
};

export default function TalentPoolDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editing, setEditing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<TalentPoolMemberSummary | null>(null);
  const [removalReason, setRemovalReason] = useState('');

  const { data: pool, isLoading } = useQuery({
    queryKey: ['talent-pools', id],
    queryFn: () => talentPoolService.getById(id),
  });

  const removeMember = useMutation({
    mutationFn: (member: TalentPoolMemberSummary) =>
      talentPoolService.removeMember(member.id, { removalReason }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['talent-pools'] });
      setRemoving(null);
      setRemovalReason('');
      toast({
        title: 'Member removed',
        // Stated because the record surviving is the point, not an accident.
        description: 'Their membership is kept with the reason recorded — re-adding them later revives it.',
      });
    },
    onError: (error: any) =>
      toast({
        variant: 'destructive',
        title: error?.response?.status === 403 ? 'That is not yours to do' : 'Could not remove the member',
        description: error?.response?.data?.detail ?? error?.message,
      }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!pool) return null;

  const members = [...pool.members].sort((a, b) => a.rank - b.rank);
  const overTarget = pool.targetSize > 0 && pool.currentMemberCount > pool.targetSize;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={pool.name}
        description={pool.description ?? undefined}
        backHref="/hr/succession/pools"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setEditing(true)}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit
            </Button>
            <Button onClick={() => setAdding(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Add member
            </Button>
          </div>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">
              Members
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold">
              {pool.currentMemberCount}
              {pool.targetSize > 0 && (
                <span className="text-base font-normal text-muted-foreground">
                  {' '}/ {pool.targetSize}
                </span>
              )}
            </p>
            {overTarget && (
              <p className="text-xs text-amber-700 dark:text-amber-400">Over target size</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">
              Type
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Badge
              variant="outline"
              style={
                pool.poolTypeColor
                  ? { borderColor: pool.poolTypeColor, color: pool.poolTypeColor }
                  : undefined
              }
            >
              {pool.poolTypeName}
            </Badge>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">
              Owner
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm">{pool.ownerName || '—'}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">
              Feeding
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm">
            {pool.targetPositionTitle ?? (
              <span className="text-muted-foreground">Not tied to a position</span>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardContent className="p-0">
          {members.length === 0 ? (
            <EmptyState
              icon={Users2}
              title="No members yet"
              description="Add people directly, or let an approved succession nomination on an appraisal put them here."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-16">Rank</TableHead>
                  <TableHead>Member</TableHead>
                  <TableHead>Readiness</TableHead>
                  <TableHead>Performance</TableHead>
                  <TableHead>Potential</TableHead>
                  <TableHead>Enrolled</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {members.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell>{m.rank}</TableCell>
                    <TableCell>
                      <div className="font-medium">{m.employeeName}</div>
                      <div className="text-xs text-muted-foreground">
                        {m.employeeNumber}
                        {m.employeePosition && ` · ${m.employeePosition}`}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className={READINESS_TONE[m.readiness] ?? ''}>
                        {spaced(m.readiness)}
                      </Badge>
                    </TableCell>
                    <TableCell>{spaced(m.latestPerformanceRating)}</TableCell>
                    <TableCell>{spaced(m.latestPotentialRating)}</TableCell>
                    <TableCell>{fmtDate(m.enrolledDate)}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" onClick={() => setRemoving(m)}>
                        <UserMinus className="mr-1 h-3.5 w-3.5" />
                        Remove
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <TalentPoolFormDialog open={editing} onOpenChange={setEditing} pool={pool} />
      <TalentPoolMemberDialog open={adding} onOpenChange={setAdding} pool={pool} />

      <Dialog open={!!removing} onOpenChange={(open) => !open && setRemoving(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove {removing?.employeeName}</DialogTitle>
            <DialogDescription>
              The membership is kept with the reason recorded, so the history survives. Adding them
              back later revives this record rather than starting a new one.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={removalReason}
            onChange={(e) => setRemovalReason(e.target.value)}
            placeholder="Why they are leaving the pool"
            rows={3}
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setRemoving(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={removeMember.isPending || removalReason.trim() === ''}
              onClick={() => removing && removeMember.mutate(removing)}
            >
              {removeMember.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Remove from pool
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
