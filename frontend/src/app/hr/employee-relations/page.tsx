'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Clock, Search, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { employeeRelationsService } from '@/services/hr/employee-relations.service';
import {
  ER_CASE_TYPE_OPTIONS, ER_OPENABLE_CASE_TYPES, GRIEVANCE_LADDER, GRIEVANCE_STATUS_OPTIONS,
  type EmployeeRelationsCaseType, type GrievanceEscalationLevel, type GrievanceStatus,
  type OpenErCaseRequest,
} from '@/types/hr/employee-relations';

const PAGE_SIZE = 25;

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;
const typeLabel = (v: string) => ER_CASE_TYPE_OPTIONS.find((t) => t.value === v)?.label ?? v;

type OpenableCaseType = Exclude<EmployeeRelationsCaseType, 'Grievance'>;

/**
 * HR's employee-relations register.
 *
 * ⚠ **This replaces `/hr/grievances`, which linked every row into `/me/grievances/[id]`** — the
 * portal screen. That happened to work, because the portal detail carries the assign and respond
 * mutations too, but the desk and the portal were sharing one screen by accident rather than by
 * design, and the portal shell is the wrong frame for HR's own work. Rows here open the desk's own
 * case file.
 *
 * ⚠ **Paged, not filtered in the browser.** The old register read every case and filtered the array;
 * on this tenant that is already 442 rows and every one of them drags its steps and parties across
 * the wire. The server filters, counts and pages — and its page size is capped at 200, so nothing
 * here may treat one page as the whole set.
 */
export default function EmployeeRelationsRegisterPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [caseType, setCaseType] = useState<EmployeeRelationsCaseType | 'all'>('all');
  const [status, setStatus] = useState<GrievanceStatus | 'all'>('all');
  const [level, setLevel] = useState<GrievanceEscalationLevel | 'all'>('all');
  const [awaitingOnly, setAwaitingOnly] = useState(false);

  const [openCaseOpen, setOpenCaseOpen] = useState(false);
  const [newType, setNewType] = useState<OpenableCaseType>('ConflictMediation');
  const [newEmployeeId, setNewEmployeeId] = useState<string | null>(null);
  const [newSubject, setNewSubject] = useState('');
  const [newStatement, setNewStatement] = useState('');

  const query = {
    page,
    pageSize: PAGE_SIZE,
    ...(caseType !== 'all' ? { caseType } : {}),
    ...(status !== 'all' ? { status } : {}),
    ...(level !== 'all' ? { level } : {}),
    ...(awaitingOnly ? { awaitingResponseOnly: true } : {}),
    ...(searchTerm ? { search: searchTerm } : {}),
  };

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'employee-relations', 'register', query],
    queryFn: () => employeeRelationsService.getRegister(query),
  });

  // Any filter change resets to page 1: staying on page 7 of a result set that now has two pages
  // shows an empty table and reads as "no cases", which is a different and alarming answer.
  const applyFilter = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setPage(1); };

  const openCase = useMutation({
    mutationFn: (payload: OpenErCaseRequest) => employeeRelationsService.openCase(payload),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'employee-relations'] });
      setOpenCaseOpen(false);
      setNewEmployeeId(null);
      setNewSubject('');
      setNewStatement('');
      router.push(`/hr/employee-relations/${created.id}`);
    },
    // The server's own refusal, not a generic failure: it explains rules this form cannot enforce.
    onError: (e: any) =>
      toast({ title: 'Case not opened', description: e?.message, variant: 'destructive' }),
  });

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee relations"
        description="Grievances and their progress up the escalation route — supervisor, head of department, HR, GM Finance & Administration, Managing Director, Board — alongside mediations, welfare matters and union consultations."
        backHref="/hr"
        actions={
          <div className="flex gap-2">
            <Button onClick={() => setOpenCaseOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              Open a case
            </Button>
          </div>
        }
      />

      {/*
        ⚠ There is deliberately no "raise a grievance" button here. A grievance is the employee's
        own complaint and the server refuses to open one on this route at all — the button would
        be an affordance for something that cannot happen. Staff raise their own from /me.
      */}

      <div className="flex flex-wrap items-end gap-2">
        <div className="flex-1 min-w-[240px]">
          <Label htmlFor="er-search" className="text-xs text-muted-foreground">Search</Label>
          <form
            onSubmit={(e) => { e.preventDefault(); setSearchTerm(search.trim()); setPage(1); }}
            className="flex gap-2"
          >
            <Input
              id="er-search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Reference, subject or employee"
            />
            <Button type="submit" variant="outline" size="icon" aria-label="Search">
              <Search className="h-4 w-4" />
            </Button>
          </form>
        </div>

        <div>
          <Label className="text-xs text-muted-foreground">Case type</Label>
          <Select value={caseType} onValueChange={applyFilter((v) => setCaseType(v as EmployeeRelationsCaseType | 'all'))}>
            <SelectTrigger className="w-48"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All case types</SelectItem>
              {ER_CASE_TYPE_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div>
          <Label className="text-xs text-muted-foreground">Status</Label>
          <Select value={status} onValueChange={applyFilter((v) => setStatus(v as GrievanceStatus | 'all'))}>
            <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {GRIEVANCE_STATUS_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div>
          <Label className="text-xs text-muted-foreground">Currently with</Label>
          <Select value={level} onValueChange={applyFilter((v) => setLevel(v as GrievanceEscalationLevel | 'all'))}>
            <SelectTrigger className="w-56"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Any rung</SelectItem>
              {GRIEVANCE_LADDER.map((o) => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <Button
          variant={awaitingOnly ? 'default' : 'outline'}
          onClick={() => { setAwaitingOnly((v) => !v); setPage(1); }}
        >
          <Clock className="mr-2 h-4 w-4" />
          Awaiting a response
        </Button>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="Nothing here"
              description={
                awaitingOnly
                  ? 'Every open case has had a response at the rung it is sitting at.'
                  : 'No case matches these filters.'
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Reference</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Primary party</TableHead>
                    <TableHead>Subject</TableHead>
                    <TableHead>Filed</TableHead>
                    <TableHead>Currently with</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((g) => (
                    <TableRow key={g.id}>
                      <TableCell className="font-medium">
                        {/* The desk's own case file — not /me/. See the note at the top. */}
                        <Link href={`/hr/employee-relations/${g.id}`} className="hover:underline">
                          {g.grievanceNumber}
                        </Link>
                      </TableCell>
                      <TableCell>{typeLabel(g.caseType)}</TableCell>
                      <TableCell>
                        <div>{g.employeeName}</div>
                        {g.activePartyCount > 0 && (
                          <div className="flex items-center gap-1 text-xs text-muted-foreground">
                            <Users className="h-3 w-3" />
                            {g.activePartyCount} other {g.activePartyCount === 1 ? 'party' : 'parties'}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="max-w-md truncate">{g.subject}</TableCell>
                      <TableCell>{fmtDate(g.filedDate)}</TableCell>
                      <TableCell>
                        <div>{levelLabel(g.currentLevel)}</div>
                        {g.awaitingResponse && (
                          <div className="text-xs text-muted-foreground">awaiting a response</div>
                        )}
                      </TableCell>
                      <TableCell><StatusBadge status={g.statusName} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {data && data.totalCount > 0 && (
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Page {data.page} of {totalPages} · {data.totalCount} case{data.totalCount === 1 ? '' : 's'}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!data.hasPrevious} onClick={() => setPage((p) => p - 1)}>
              Previous
            </Button>
            <Button variant="outline" size="sm" disabled={!data.hasNext} onClick={() => setPage((p) => p + 1)}>
              Next
            </Button>
          </div>
        </div>
      )}

      <Dialog open={openCaseOpen} onOpenChange={setOpenCaseOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Open an employee-relations case</DialogTitle>
            <DialogDescription>
              A mediation, welfare matter or union consultation opened by the desk about somebody.
              A grievance cannot be opened here — staff raise their own.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-1">
              <Label>Case type</Label>
              <Select value={newType} onValueChange={(v) => setNewType(v as OpenableCaseType)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ER_OPENABLE_CASE_TYPES.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1">
              <Label>Primary party</Label>
              <EmployeePicker
                value={newEmployeeId}
                onChange={(id) => setNewEmployeeId(id)}
                placeholder="Whom is this case about?"
              />
            </div>

            <div className="space-y-1">
              <Label htmlFor="er-subject">Subject</Label>
              <Input
                id="er-subject"
                value={newSubject}
                onChange={(e) => setNewSubject(e.target.value)}
                maxLength={300}
              />
            </div>

            <div className="space-y-1">
              <Label htmlFor="er-statement">What the case is about</Label>
              <Textarea
                id="er-statement"
                value={newStatement}
                onChange={(e) => setNewStatement(e.target.value)}
                rows={5}
                maxLength={6000}
              />
              <p className="text-xs text-muted-foreground">
                At least 20 characters — the server refuses anything shorter.
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenCaseOpen(false)}>Cancel</Button>
            <Button
              disabled={
                !newEmployeeId || !newSubject.trim() || newStatement.trim().length < 20 || openCase.isPending
              }
              onClick={() => {
                if (!newEmployeeId) return;
                openCase.mutate({
                  caseType: newType,
                  employeeId: newEmployeeId,
                  subject: newSubject.trim(),
                  statement: newStatement.trim(),
                });
              }}
            >
              {openCase.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Open case
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
