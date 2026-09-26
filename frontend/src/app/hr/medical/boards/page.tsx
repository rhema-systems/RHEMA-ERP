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
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import { medicalHealthService } from '@/services/hr/medical-health.service';
import {
  MEDICAL_BOARD_OUTCOME_LABEL,
  MEDICAL_BOARD_PURPOSE_LABEL,
  boardStatusLabel,
  type MedicalBoardPurpose,
  type MedicalBoardStatus,
} from '@/types/hr/medical-board';

/** Radix Select cannot carry an empty value, so "none chosen" is a sentinel. */
const NONE = '__none';

const PURPOSES = Object.keys(MEDICAL_BOARD_PURPOSE_LABEL) as MedicalBoardPurpose[];

/**
 * The medical board register.
 *
 * ⚠ A board rules on one employee — about an absence, an injury at work, their fitness for duty or a
 * medical retirement (round 5, lane K1: it used to read as sick leave only). It is a Medical-module
 * record: leave reads it to satisfy its evidence rule and separation reads it to justify a medical
 * retirement, but neither writes to it, and nothing on this screen acts on a recommendation.
 */
export default function MedicalBoardsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [status, setStatus] = useState<MedicalBoardStatus | 'all'>('all');
  const [purposeFilter, setPurposeFilter] = useState<MedicalBoardPurpose | 'all'>('all');
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  // the request form
  const [employeeId, setEmployeeId] = useState('');
  const [purpose, setPurpose] = useState<MedicalBoardPurpose | ''>('');
  const [reason, setReason] = useState('');
  const [facilityId, setFacilityId] = useState(NONE);
  const [examId, setExamId] = useState(NONE);

  const queryKey = ['hr', 'medical-boards', status, purposeFilter, search];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      medicalBoardService.list({
        status: status === 'all' ? undefined : status,
        purpose: purposeFilter === 'all' ? undefined : purposeFilter,
        search: search || undefined,
        pageSize: 100,
      }),
  });

  // ── Pickers (lane K3). Loaded only while the dialog is open. ──────────────────────────────
  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities', 'active'],
    queryFn: () => medicalFacilityService.getActiveFacilities(),
    enabled: open,
  });

  // ⚠ The examinations offered are the chosen employee's own: the server refuses anybody else's,
  // so offering one would be offering a refusal. Reached through the employee's health profile.
  const { data: profile } = useQuery({
    queryKey: ['hr', 'medical-profiles', 'by-employee', employeeId],
    queryFn: () => medicalHealthService.getProfileByEmployee(employeeId),
    enabled: open && !!employeeId,
  });
  const { data: exams = [] } = useQuery({
    queryKey: ['hr', 'medical-profiles', profile?.id, 'exams'],
    queryFn: () => medicalHealthService.getExamsByProfile(profile?.id ?? ''),
    enabled: open && !!profile?.id,
  });

  const rows = data?.items ?? [];

  const reset = () => {
    setEmployeeId('');
    setPurpose('');
    setReason('');
    setFacilityId(NONE);
    setExamId(NONE);
  };

  const submit = async () => {
    if (!employeeId || !purpose || !reason.trim()) return;
    setBusy(true);
    try {
      const board = await medicalBoardService.request({
        employeeId,
        purpose,
        reason: reason.trim(),
        facilityId: facilityId === NONE ? null : facilityId,
        basedOnExamId: examId === NONE ? null : examId,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'medical-boards'] });
      toast({ title: 'Board requested', description: `${board.boardNumber} is awaiting its members.` });
      setOpen(false);
      reset();
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
        description="Panels convened to rule on an employee's health — an absence, an injury at work, fitness for duty or retirement"
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
                <SelectItem value="Cancelled">Cancelled or dissolved</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="board-purpose-filter">Purpose</Label>
            <Select value={purposeFilter} onValueChange={(v) => setPurposeFilter(v as MedicalBoardPurpose | 'all')}>
              <SelectTrigger id="board-purpose-filter" className="w-52">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                {PURPOSES.map((p) => (
                  <SelectItem key={p} value={p}>{MEDICAL_BOARD_PURPOSE_LABEL[p]}</SelectItem>
                ))}
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
              description="A board is convened when somebody's health has to be ruled on by a panel — after extended sick leave, an injury at work, a question about their fitness for duty, or before a medical retirement."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Board</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Purpose</TableHead>
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
                    <TableCell>{MEDICAL_BOARD_PURPOSE_LABEL[b.purpose] ?? b.purpose}</TableCell>
                    <TableCell>{b.requestedOn?.slice(0, 10)}</TableCell>
                    <TableCell><StatusBadge status={boardStatusLabel(b)} /></TableCell>
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

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) reset(); }}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Request a medical board</DialogTitle>
            <DialogDescription>
              The board is created with nobody on it. Appoint its members, then convene it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Employee <span className="text-red-500">*</span></Label>
              <EmployeePicker
                value={employeeId || null}
                onChange={(id) => { setEmployeeId(id ?? ''); setExamId(NONE); }}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="board-purpose">
                What the board is for <span className="text-red-500">*</span>
              </Label>
              <Select value={purpose} onValueChange={(v) => setPurpose(v as MedicalBoardPurpose)}>
                <SelectTrigger id="board-purpose">
                  <SelectValue placeholder="Choose the question the board is asked" />
                </SelectTrigger>
                <SelectContent>
                  {PURPOSES.map((p) => (
                    <SelectItem key={p} value={p}>{MEDICAL_BOARD_PURPOSE_LABEL[p]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {/* ⚠ Said here, because it decides whether leave can ever rest on this board. */}
              <p className="text-xs text-muted-foreground">
                Only a board about an absence — extended sick leave, an injury on duty, or other —
                can satisfy a leave type&apos;s medical-board rule, and only once it has reported in
                the leave year being counted.
              </p>
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
                placeholder="e.g. sick leave this year has passed the point at which a board must sit."
              />
              <p className="text-xs text-muted-foreground">
                Required. A board convened without a stated question is one nobody can tell whether
                it answered.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="board-facility">Facility</Label>
              <Select value={facilityId} onValueChange={setFacilityId}>
                <SelectTrigger id="board-facility"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Not recorded</SelectItem>
                  {facilities.map((f) => (
                    <SelectItem key={f.id} value={f.id}>{f.facilityName}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="board-exam">Based on an examination</Label>
              <Select value={examId} onValueChange={setExamId} disabled={!employeeId}>
                <SelectTrigger id="board-exam"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {exams.map((x) => (
                    <SelectItem key={x.id} value={x.id}>
                      {x.examDate?.slice(0, 10)}
                      {x.result ? ` · ${MEDICAL_BOARD_OUTCOME_LABEL[x.result] ?? x.result}` : ''}
                      {x.facilityName ? ` · ${x.facilityName}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {!employeeId
                  ? 'Choose the employee first — only their own examinations are offered.'
                  : exams.length === 0
                    ? 'No examinations are recorded for this employee.'
                    : 'Only this employee’s own examinations are offered.'}
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={busy}>Cancel</Button>
            <Button onClick={submit} disabled={busy || !employeeId || !purpose || !reason.trim()}>
              Request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
