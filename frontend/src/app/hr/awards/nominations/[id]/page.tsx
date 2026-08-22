'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  Award,
  Gavel,
  Info,
  ListChecks,
  Loader2,
  Plus,
  Trophy,
  Users,
} from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * One nomination, from the awards desk.
 *
 * ⚠ **Conferring is the only path from a decision to an award**, and it is not merely a status
 * change: it reserves the money against the year's budget. A nomination that is still a draft,
 * already rejected, withdrawn, or has already produced an award is refused — so the button appears
 * only when the record can actually take it.
 *
 * ⚠ **A team nomination cannot be conferred.** It has no single nominee, and the underlying write
 * would produce a foreign-key violation surfacing as a 500. The screen says so instead of offering
 * a button that breaks.
 *
 * ⚠ **Assigning a committee is what makes scoring possible at all.** Committee membership is the
 * gate on who may score; a nomination assigned to nobody can be scored by nobody.
 */
export default function DeskNominationPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();

  const [committeeId, setCommitteeId] = useState('');
  const [awardDate, setAwardDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [monetaryAmount, setMonetaryAmount] = useState('');
  const [levelId, setLevelId] = useState('');
  const [citation, setCitation] = useState('');
  const [contribution, setContribution] = useState('');
  const [teamMember, setTeamMember] = useState<null | {
    employeeId: string; role: string; contributionSummary: string; rewardPercentage: string;
  }>(null);

  const { data: nomination, isLoading } = useQuery({
    queryKey: ['nomination', id],
    queryFn: () => awardsService.getNomination(id),
  });

  const awardTypeId = nomination?.awardTypeId;

  const { data: awardType } = useQuery({
    queryKey: ['award-type', awardTypeId],
    queryFn: () => awardsService.getType(awardTypeId as string),
    enabled: Boolean(awardTypeId),
  });

  const { data: levels } = useQuery({
    queryKey: ['award-levels', awardTypeId],
    queryFn: () => awardsService.getLevels(awardTypeId as string),
    enabled: Boolean(awardTypeId) && Boolean(awardType?.hasLevels),
  });

  const { data: committees } = useQuery({
    queryKey: ['award-committees'],
    queryFn: () => awardsService.getCommittees(),
  });

  const { data: reviews } = useQuery({
    queryKey: ['nomination-reviews', id],
    queryFn: () => awardsService.getNominationReviews(id),
  });

  const { data: contributions } = useQuery({
    queryKey: ['nomination-contributions', id],
    queryFn: () => awardsService.getContributions(id),
  });

  const { data: teamNominees } = useQuery({
    queryKey: ['nomination-team', id],
    queryFn: () => awardsService.getTeamNominees(id),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['nomination', id] });
    queryClient.invalidateQueries({ queryKey: ['award-nominations'] });
  };

  const assign = useMutation({
    mutationFn: () => awardsService.assignToCommittee(id, committeeId),
    onSuccess: () => { toast.success('Assigned. Its members can now score it.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The assignment was refused.'),
  });

  const addContribution = useMutation({
    mutationFn: () => awardsService.addContribution(id, contribution.trim()),
    onSuccess: () => {
      toast.success('Recorded.');
      setContribution('');
      queryClient.invalidateQueries({ queryKey: ['nomination-contributions', id] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The contribution was refused.'),
  });

  const addTeamMember = useMutation({
    mutationFn: () => {
      if (!teamMember) throw new Error('No member to add.');
      return awardsService.addTeamNominee(id, {
        employeeId: teamMember.employeeId,
        role: teamMember.role.trim() || null,
        contributionSummary: teamMember.contributionSummary.trim() || null,
        rewardPercentage:
          teamMember.rewardPercentage === '' ? null : Number(teamMember.rewardPercentage),
      });
    },
    onSuccess: () => {
      toast.success('Added to the team.');
      setTeamMember(null);
      queryClient.invalidateQueries({ queryKey: ['nomination-team', id] });
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The member was refused.'),
  });

  const confer = useMutation({
    mutationFn: () =>
      awardsService.conferFromNomination(id, {
        awardDate: new Date(awardDate).toISOString().slice(0, 19),
        monetaryAmount: monetaryAmount === '' ? null : Number(monetaryAmount),
        awardLevelId: levelId || null,
        additionalCitation: citation.trim() || null,
      }),
    onSuccess: (award) => {
      toast.success(`Award ${award.awardNumber} conferred.`);
      invalidate();
      router.push(`/hr/awards/${award.id}`);
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The conferral was refused.'),
  });

  if (isLoading || !nomination) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const isTeam = Boolean(nomination.teamName) && !nomination.nomineeId;
  const alreadyAwarded = Boolean(nomination.awardId);
  const conferrable =
    !isTeam &&
    !alreadyAwarded &&
    ['Submitted', 'UnderReview'].includes(nomination.status);

  const levelRequired = Boolean(awardType?.hasLevels);
  const conferReady = conferrable && awardDate !== '' && (!levelRequired || levelId !== '');
  const scoredReviews = reviews ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={nomination.nominationNumber}
        description={`${nomination.awardTypeName}${nomination.awardCycleName ? ` — ${nomination.awardCycleName}` : ''}`}
        backHref="/hr/awards"
        actions={<Badge variant="secondary">{nomination.statusName}</Badge>}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">The nomination</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-xs text-muted-foreground">Nominee</p>
            <p className="text-sm">
              {nomination.nomineeName || nomination.teamName || '—'}
              {nomination.nomineeEmployeeNumber && (
                <span className="ml-2 text-xs text-muted-foreground">
                  {nomination.nomineeEmployeeNumber}
                </span>
              )}
            </p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Nominated by</p>
            <p className="text-sm">{nomination.nominatedByName}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Raised</p>
            <p className="text-sm">{fmtDate(nomination.nominationDate)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">Committee</p>
            <p className="text-sm">{nomination.committeeName ?? 'Not assigned'}</p>
          </div>
          <div className="sm:col-span-2">
            <p className="text-xs text-muted-foreground">Justification</p>
            <p className="whitespace-pre-wrap text-sm">{nomination.justification}</p>
          </div>
        </CardContent>
      </Card>

      {/* ── committee ──────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Users className="h-4 w-4" />
            Committee
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {nomination.committeeName ? (
            <p className="text-sm">
              With <span className="font-medium">{nomination.committeeName}</span>. Its active
              members can score it from their own screen.
            </p>
          ) : (
            <>
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  Nobody can score this nomination until it is in front of a committee — membership
                  is the entitlement.
                </AlertDescription>
              </Alert>
              <div className="flex items-end gap-3">
                <div className="flex-1 space-y-2">
                  <Label>Assign to</Label>
                  <Select value={committeeId} onValueChange={setCommitteeId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Choose a committee" />
                    </SelectTrigger>
                    <SelectContent>
                      {(committees ?? [])
                        .filter((c) => c.isActive)
                        .map((c) => (
                          <SelectItem key={c.id} value={c.id}>
                            {c.name} ({c.memberCount} members)
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
                <Button disabled={!committeeId || assign.isPending} onClick={() => assign.mutate()}>
                  {assign.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Users className="mr-2 h-4 w-4" />
                  )}
                  Assign
                </Button>
              </div>
            </>
          )}

          {scoredReviews.length > 0 && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reviewer</TableHead>
                  <TableHead className="text-right">Score</TableHead>
                  <TableHead>Given</TableHead>
                  <TableHead>Comments</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {scoredReviews.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.reviewerName}</TableCell>
                    <TableCell className="text-right">{r.score}</TableCell>
                    <TableCell>{fmtDate(r.reviewDate)}</TableCell>
                    <TableCell className="max-w-md truncate">{r.comments ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── the team, when it is one ────────────────────────────────────────── */}
      {isTeam && (
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle className="flex items-center gap-2 text-base">
              <Users className="h-4 w-4" />
              Team members
            </CardTitle>
            <Button
              size="sm"
              variant="outline"
              onClick={() =>
                setTeamMember({ employeeId: '', role: '', contributionSummary: '', rewardPercentage: '' })
              }
            >
              <Plus className="mr-2 h-4 w-4" />
              Add member
            </Button>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* ⚠ The nominate form tells employees that team members are added here afterwards by
                the desk. Until this panel existed that was a promise the product could not keep. */}
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                A team nomination names the team; who was in it is recorded here. This is where the
                nominate form says the desk fills in the members.
              </AlertDescription>
            </Alert>

            {(teamNominees ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">Nobody has been named yet.</p>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Member</TableHead>
                    <TableHead>Role</TableHead>
                    <TableHead>Contribution</TableHead>
                    <TableHead className="text-right">Share</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(teamNominees ?? []).map((m) => (
                    <TableRow key={m.id}>
                      <TableCell>{m.employeeName}</TableCell>
                      <TableCell>{m.role ?? '—'}</TableCell>
                      <TableCell className="max-w-md truncate">{m.contributionSummary ?? '—'}</TableCell>
                      <TableCell className="text-right">
                        {m.rewardPercentage === null ? '—' : `${m.rewardPercentage}%`}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      )}

      {/* ── what they actually did ──────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ListChecks className="h-4 w-4" />
            Contributions
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {(contributions ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nothing recorded beyond the justification. Contributions are the specific things a
              committee weighs — useful where one paragraph is not enough.
            </p>
          ) : (
            <ul className="space-y-2">
              {(contributions ?? []).map((c) => (
                <li key={c.id} className="rounded-md border p-3 text-sm">
                  {c.description}
                </li>
              ))}
            </ul>
          )}

          <div className="flex items-end gap-3">
            <div className="flex-1 space-y-2">
              <Label htmlFor="contribution">Add a contribution</Label>
              <Textarea
                id="contribution"
                rows={2}
                value={contribution}
                onChange={(e) => setContribution(e.target.value)}
                placeholder="One specific thing this nominee did"
              />
            </div>
            <Button
              disabled={contribution.trim().length === 0 || addContribution.isPending}
              onClick={() => addContribution.mutate()}
            >
              {addContribution.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Plus className="mr-2 h-4 w-4" />
              )}
              Add
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* ── conferral ──────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Trophy className="h-4 w-4" />
            Confer the award
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {alreadyAwarded ? (
            <Alert>
              <Award className="h-4 w-4" />
              <AlertDescription>
                This nomination has already produced award {nomination.awardNumber}.
              </AlertDescription>
            </Alert>
          ) : isTeam ? (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                This is a team nomination, so there is no single employee to confer the award to.
                Team awards are recorded against their members rather than conferred from here.
              </AlertDescription>
            </Alert>
          ) : !conferrable ? (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                A nomination can only be conferred once it has reached the desk. This one is{' '}
                {nomination.statusName.toLowerCase()}.
              </AlertDescription>
            </Alert>
          ) : (
            <>
              <Alert>
                <Gavel className="h-4 w-4" />
                <AlertDescription>
                  Conferring commits the amount against this year&apos;s budget. Recording the
                  payment later releases that commitment and books what was actually paid — the two
                  can differ.
                </AlertDescription>
              </Alert>

              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="awardDate">Award date</Label>
                  <Input
                    id="awardDate"
                    type="date"
                    value={awardDate}
                    onChange={(e) => setAwardDate(e.target.value)}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="amount">Amount</Label>
                  <Input
                    id="amount"
                    type="number"
                    min={0}
                    value={monetaryAmount}
                    onChange={(e) => setMonetaryAmount(e.target.value)}
                    placeholder="Leave blank for none"
                  />
                </div>
                {levelRequired && (
                  <div className="space-y-2">
                    <Label>Level</Label>
                    <Select value={levelId} onValueChange={setLevelId}>
                      <SelectTrigger>
                        <SelectValue placeholder="Which level?" />
                      </SelectTrigger>
                      <SelectContent>
                        {(levels ?? [])
                          .filter((l) => l.isActive)
                          .map((l) => (
                            <SelectItem key={l.id} value={l.id}>
                              {l.name}
                            </SelectItem>
                          ))}
                      </SelectContent>
                    </Select>
                    <p className="text-xs text-muted-foreground">
                      This award has levels, so it cannot be conferred without naming one.
                    </p>
                  </div>
                )}
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="citation">Citation (optional)</Label>
                  <Textarea
                    id="citation"
                    rows={3}
                    value={citation}
                    onChange={(e) => setCitation(e.target.value)}
                  />
                </div>
              </div>

              <div className="flex justify-end">
                <Button disabled={!conferReady || confer.isPending} onClick={() => confer.mutate()}>
                  {confer.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Trophy className="mr-2 h-4 w-4" />
                  )}
                  Confer
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={Boolean(teamMember)} onOpenChange={(o) => !o && setTeamMember(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a team member</DialogTitle>
            <DialogDescription>Who was in the team, and what they did.</DialogDescription>
          </DialogHeader>
          {teamMember && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Employee</Label>
                <EmployeePicker
                  value={teamMember.employeeId || null}
                  onChange={(v) => setTeamMember({ ...teamMember, employeeId: v ?? '' })}
                />
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="tmRole">Role</Label>
                  <Input
                    id="tmRole"
                    value={teamMember.role}
                    onChange={(e) => setTeamMember({ ...teamMember, role: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="tmShare">Share of the reward (%)</Label>
                  <Input
                    id="tmShare"
                    type="number"
                    min={0}
                    max={100}
                    value={teamMember.rewardPercentage}
                    onChange={(e) => setTeamMember({ ...teamMember, rewardPercentage: e.target.value })}
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="tmSummary">What they contributed</Label>
                <Textarea
                  id="tmSummary"
                  rows={3}
                  value={teamMember.contributionSummary}
                  onChange={(e) =>
                    setTeamMember({ ...teamMember, contributionSummary: e.target.value })
                  }
                />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setTeamMember(null)}>Cancel</Button>
                <Button
                  disabled={!teamMember.employeeId || addTeamMember.isPending}
                  onClick={() => addTeamMember.mutate()}
                >
                  {addTeamMember.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  Add
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
