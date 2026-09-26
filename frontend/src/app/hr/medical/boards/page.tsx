'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Gavel, Loader2, Plus, Scale } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import {
  EMPTY_CASE,
  MedicalBoardCaseFields,
  NO_EXAM,
  caseDraftComplete,
  type MedicalBoardCaseDraft,
} from '@/components/hr/medical/MedicalBoardCaseFields';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import {
  MEDICAL_BOARD_CASE_STATUS_LABEL,
  MEDICAL_BOARD_KIND_LABEL,
  MEDICAL_BOARD_OUTCOME_LABEL,
  MEDICAL_BOARD_PURPOSE_LABEL,
  boardStatusLabel,
  type MedicalBoardKind,
  type MedicalBoardPurpose,
  type MedicalBoardStatus,
} from '@/types/hr/medical-board';

/** Radix Select cannot carry an empty value, so "none chosen" is a sentinel. */
const NONE = '__none';

const PURPOSES = Object.keys(MEDICAL_BOARD_PURPOSE_LABEL) as MedicalBoardPurpose[];
const KINDS = Object.keys(MEDICAL_BOARD_KIND_LABEL) as MedicalBoardKind[];

/**
 * The medical board register.
 *
 * ⚠ A board is a PANEL that hears cases (round 5, lane K-II-a): each employee before it is a case,
 * asked its own question — an absence, an injury at work, fitness for duty or a medical retirement —
 * and decided on its own finding. Leave reads the case about its employee to satisfy its evidence
 * rule and separation reads one to justify a medical retirement, but neither writes to it, and
 * nothing on this screen acts on a recommendation.
 */
export default function MedicalBoardsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [status, setStatus] = useState<MedicalBoardStatus | 'all'>('all');
  const [purposeFilter, setPurposeFilter] = useState<MedicalBoardPurpose | 'all'>('all');
  const [kindFilter, setKindFilter] = useState<MedicalBoardKind | 'all'>('all');
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  // the request form: the board, and its first case
  const [kind, setKind] = useState<MedicalBoardKind>('Employer');
  const [facilityId, setFacilityId] = useState(NONE);
  const [firstCase, setFirstCase] = useState<MedicalBoardCaseDraft>(EMPTY_CASE);

  const queryKey = ['hr', 'medical-boards', status, purposeFilter, kindFilter, search];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      medicalBoardService.list({
        status: status === 'all' ? undefined : status,
        purpose: purposeFilter === 'all' ? undefined : purposeFilter,
        kind: kindFilter === 'all' ? undefined : kindFilter,
        search: search || undefined,
        pageSize: 100,
      }),
  });

  const { data: facilities = [] } = useQuery({
    queryKey: ['hr', 'medical-facilities', 'active'],
    queryFn: () => medicalFacilityService.getActiveFacilities(),
    enabled: open,
  });

  const rows = data?.items ?? [];

  const reset = () => {
    setKind('Employer');
    setFacilityId(NONE);
    setFirstCase(EMPTY_CASE);
  };

  const submit = async () => {
    if (!caseDraftComplete(firstCase) || !firstCase.purpose) return;
    setBusy(true);
    try {
      const board = await medicalBoardService.request({
        kind,
        facilityId: facilityId === NONE ? null : facilityId,
        employeeId: firstCase.employeeId,
        purpose: firstCase.purpose,
        reason: firstCase.reason.trim(),
        basedOnExamId: firstCase.examId === NO_EXAM ? null : firstCase.examId,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'medical-boards'] });
      toast({
        title: 'Board requested',
        description: `${board.boardNumber} is awaiting its members. Add anybody else it should hear from its page.`,
      });
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
        description="Panels that hear employees' cases — an absence, an injury at work, fitness for duty or retirement — and decide each one"
        backHref="/hr/medical"
        actions={
          <div className="flex flex-wrap gap-2">
            {/* Lane K-II-b: the injuries incapacity is assessed against (PNDCL 187's schedules). */}
            <Button variant="outline" onClick={() => router.push('/hr/medical/boards/incapacity-schedule')}>
              <Scale className="mr-2 h-4 w-4" /> Compensation schedule
            </Button>
            <Button onClick={() => setOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> Request a board
            </Button>
          </div>
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
            <Label htmlFor="board-purpose-filter">A case about</Label>
            <Select value={purposeFilter} onValueChange={(v) => setPurposeFilter(v as MedicalBoardPurpose | 'all')}>
              <SelectTrigger id="board-purpose-filter" className="w-52">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Anything</SelectItem>
                {PURPOSES.map((p) => (
                  <SelectItem key={p} value={p}>{MEDICAL_BOARD_PURPOSE_LABEL[p]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="board-kind-filter">Convened by</Label>
            <Select value={kindFilter} onValueChange={(v) => setKindFilter(v as MedicalBoardKind | 'all')}>
              <SelectTrigger id="board-kind-filter" className="w-64">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">Anybody</SelectItem>
                {KINDS.map((k) => (
                  <SelectItem key={k} value={k}>{MEDICAL_BOARD_KIND_LABEL[k]}</SelectItem>
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
              description="A board is convened when somebody's health has to be ruled on by a panel — after extended sick leave, an injury at work, a question about their fitness for duty, or before a medical retirement. One board can hear several people."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Board</TableHead>
                  <TableHead>Cases</TableHead>
                  <TableHead>Requested</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Members</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((b) => (
                  <TableRow
                    key={b.id}
                    className="cursor-pointer align-top"
                    onClick={() => router.push(`/hr/medical/boards/${b.id}`)}
                  >
                    <TableCell>
                      <div className="font-medium">{b.boardNumber}</div>
                      {b.kind !== 'Employer' && (
                        <div className="text-xs text-muted-foreground">{MEDICAL_BOARD_KIND_LABEL[b.kind]}</div>
                      )}
                    </TableCell>
                    {/* ⚠ One line per case: a board no longer has "an employee" or "a finding". */}
                    <TableCell>
                      <ul className="space-y-1">
                        {b.cases.map((c) => (
                          <li key={c.id}>
                            <span>{c.employeeName}</span>
                            <span className="ml-2 text-xs text-muted-foreground">
                              {MEDICAL_BOARD_PURPOSE_LABEL[c.purpose] ?? c.purpose}
                              {' · '}
                              {c.outcome
                                ? MEDICAL_BOARD_OUTCOME_LABEL[c.outcome]
                                : MEDICAL_BOARD_CASE_STATUS_LABEL[c.status]}
                            </span>
                            {c.recommendsMedicalRetirement && (
                              <span className="ml-2 text-xs font-medium text-amber-700 dark:text-amber-300">
                                retirement advised
                              </span>
                            )}
                          </li>
                        ))}
                      </ul>
                    </TableCell>
                    <TableCell>{b.requestedOn?.slice(0, 10)}</TableCell>
                    <TableCell><StatusBadge status={boardStatusLabel(b)} /></TableCell>
                    <TableCell>{b.members?.length ?? 0}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) reset(); }}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Request a medical board</DialogTitle>
            <DialogDescription>
              The board is created with its first case and nobody on the panel. Add anybody else it
              should hear, appoint its members, then convene it.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="board-kind">Convened by</Label>
              <Select value={kind} onValueChange={(v) => setKind(v as MedicalBoardKind)}>
                <SelectTrigger id="board-kind"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {KINDS.map((k) => (
                    <SelectItem key={k} value={k}>{MEDICAL_BOARD_KIND_LABEL[k]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                The Workmen&apos;s Compensation Act&apos;s two boards — disfigurement, appointed by the
                chief labour officer; internal organs, appointed by the Minister — are recorded here when
                they sit on an employee&apos;s injury.
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

            <div className="border-t pt-4 text-sm font-medium">The first case</div>
            <MedicalBoardCaseFields value={firstCase} onChange={setFirstCase} enabled={open} idPrefix="board-case" />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={busy}>Cancel</Button>
            <Button onClick={submit} disabled={busy || !caseDraftComplete(firstCase)}>
              Request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
