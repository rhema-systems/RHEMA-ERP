'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Gavel, Loader2, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import {
  MEDICAL_BOARD_OUTCOME_LABEL,
  type MedicalBoardStatus,
} from '@/types/hr/medical-board';

/**
 * The medical board register.
 *
 * ⚠ A board rules on one employee's fitness. It is a Medical-module record: leave reads it to
 * satisfy its evidence rule and separation reads it to justify a medical retirement, but neither
 * writes to it, and nothing on this screen acts on a recommendation.
 */
export default function MedicalBoardsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [status, setStatus] = useState<MedicalBoardStatus | 'all'>('all');
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [employeeId, setEmployeeId] = useState('');
  const [reason, setReason] = useState('');

  const queryKey = ['hr', 'medical-boards', status, search];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      medicalBoardService.list({
        status: status === 'all' ? undefined : status,
        search: search || undefined,
        pageSize: 100,
      }),
  });

  const rows = data?.items ?? [];

  const submit = async () => {
    if (!employeeId || !reason.trim()) return;
    setBusy(true);
    try {
      const board = await medicalBoardService.request({ employeeId, reason: reason.trim() });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'medical-boards'] });
      toast({ title: 'Board requested', description: `${board.boardNumber} is awaiting its members.` });
      setOpen(false);
      setEmployeeId('');
      setReason('');
      router.push(`/hr/medical/boards/${board.id}`);
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Could not request the board.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Medical boards"
        description="Panels convened to rule on an employee's fitness for duty"
        backHref="/hr/medical"
        actions={
          <Button onClick={() => setOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> Request a board
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 pt-6">
          <div className="space-y-2">
            <Label htmlFor="board-status">Status</Label>
            <Select value={status} onValueChange={(v) => setStatus(v as MedicalBoardStatus | 'all')}>
              <SelectTrigger id="board-status" className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Requested">Requested</SelectItem>
                <SelectItem value="Convened">Convened</SelectItem>
                <SelectItem value="Concluded">Concluded</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="board-search">Search</Label>
            <Input
              id="board-search"
              className="w-72"
              placeholder="Board number, employee or reason"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Gavel}
              title="No medical boards"
              description="A board is convened when somebody's fitness for duty has to be ruled on — commonly after extended sick leave."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Board</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Requested</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Finding</TableHead>
                  <TableHead>Members</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((b) => (
                  <TableRow
                    key={b.id}
                    className="cursor-pointer"
                    onClick={() => router.push(`/hr/medical/boards/${b.id}`)}
                  >
                    <TableCell className="font-medium">{b.boardNumber}</TableCell>
                    <TableCell>
                      <div>{b.employeeName}</div>
                      <div className="text-xs text-muted-foreground">{b.employeeNumber}</div>
                    </TableCell>
                    <TableCell>{b.requestedOn?.slice(0, 10)}</TableCell>
                    <TableCell><StatusBadge status={b.status} /></TableCell>
                    <TableCell className="text-muted-foreground">
                      {b.outcome ? MEDICAL_BOARD_OUTCOME_LABEL[b.outcome] : '—'}
                      {b.recommendsMedicalRetirement && (
                        <span className="ml-2 text-xs font-medium text-amber-700 dark:text-amber-300">
                          retirement advised
                        </span>
                      )}
                    </TableCell>
                    <TableCell>{b.members?.length ?? 0}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Request a medical board</DialogTitle>
            <DialogDescription>
              The board is created with nobody on it. Appoint its members, then convene it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Employee <span className="text-red-500">*</span></Label>
              <EmployeePicker value={employeeId || null} onChange={(id) => setEmployeeId(id ?? '')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="board-reason">
                Why a board is needed <span className="text-red-500">*</span>
              </Label>
              <Textarea
                id="board-reason"
                rows={3}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="e.g. cumulative sick leave has passed the point at which a board must sit."
              />
              <p className="text-xs text-muted-foreground">
                Required. A board convened without a stated question is one nobody can tell whether
                it answered.
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={busy}>Cancel</Button>
            <Button onClick={submit} disabled={busy || !employeeId || !reason.trim()}>
              Request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
