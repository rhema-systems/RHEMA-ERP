'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Info, Loader2, Trash2, Vote } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

/**
 * Cast a vote (AWD-04, AWD-05, AWD-06, AWD-10).
 *
 * ⚠ **Not every award goes to a vote.** Only cycles whose award is decided by `StaffVote` appear
 * here, and only while the voting window is open. Both facts are computed server-side on the ballot;
 * the screen must not re-derive "is it open" from the dates, or a clock skew becomes a wrong answer.
 *
 * ⚠ **The electorate is a real gate.** An award can be voted on by a section of staff or by
 * everyone — an award with no electorate target admits everyone, which is the "or all of them" half
 * of TDC's note. The ballot says whether the caller is in it; a screen that hid the ballot instead
 * would leave them wondering why an award they can see offers them nothing.
 *
 * ⚠ **The tally is withheld until the window closes.** A missing result is not "nobody voted".
 */
export default function VotePage() {
  const queryClient = useQueryClient();
  const [cycleId, setCycleId] = useState('');
  const [choice, setChoice] = useState('');
  const [justification, setJustification] = useState('');

  const { data: cycles, isLoading } = useQuery({
    queryKey: ['my-voting-cycles'],
    queryFn: () => awardsService.getMyVotingCycles(),
  });

  const openCycles = cycles ?? [];

  const { data: ballot, isFetching: loadingBallot } = useQuery({
    queryKey: ['award-ballot', cycleId],
    queryFn: () => awardsService.getBallot(cycleId),
    enabled: Boolean(cycleId),
  });

  const cast = useMutation({
    mutationFn: () =>
      awardsService.castVote(cycleId, {
        awardNominationId: choice,
        justification: justification.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Your vote has been recorded.');
      setJustification('');
      queryClient.invalidateQueries({ queryKey: ['award-ballot', cycleId] });
    },
    onError: (e: any) =>
      toast.error(e?.body?.detail || e?.body?.message || e?.message || 'The vote was refused.'),
  });

  const withdraw = useMutation({
    mutationFn: () => awardsService.withdrawVote(cycleId),
    onSuccess: () => {
      toast.success('Your vote has been withdrawn.');
      setChoice('');
      queryClient.invalidateQueries({ queryKey: ['award-ballot', cycleId] });
    },
    onError: (e: any) =>
      toast.error(e?.body?.detail || e?.message || 'The withdrawal was refused.'),
  });

  const alreadyVoted = Boolean(ballot?.myVoteNominationId);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Vote"
        description="Awards decided by a staff vote, while their voting window is open."
        backHref="/hr/awards/me"
      />

      {isLoading ? (
        <div className="flex items-center justify-center p-12">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : openCycles.length === 0 ? (
        <EmptyState
          icon={Vote}
          title="No ballots are open"
          description="Awards appear here while their voting window is open. Not every award goes to a vote — some are decided by a committee or chosen outright by management."
        />
      ) : (
        <>
          <Card>
            <CardContent className="flex flex-wrap items-center gap-3 p-4">
              <Vote className="h-4 w-4 text-muted-foreground" />
              <span className="text-sm text-muted-foreground">Award</span>
              <Select
                value={cycleId}
                onValueChange={(v) => {
                  setCycleId(v);
                  setChoice('');
                }}
              >
                <SelectTrigger className="w-96">
                  <SelectValue placeholder="Choose a ballot" />
                </SelectTrigger>
                <SelectContent>
                  {openCycles.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.awardTypeName} — {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </CardContent>
          </Card>

          {loadingBallot ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : !ballot ? null : !ballot.isInElectorate ? (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                This award is voted on by a section of staff that does not include you. You can see
                the ballot, but you cannot cast a vote in it.
              </AlertDescription>
            </Alert>
          ) : !ballot.isVotingOpen ? (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertDescription>
                Voting is not open for this award. It opens {fmtDate(ballot.votingOpensOn)} and
                closes {fmtDate(ballot.votingClosesOn)}.
              </AlertDescription>
            </Alert>
          ) : ballot.options.length === 0 ? (
            <EmptyState
              icon={Vote}
              title="Nobody is on this ballot"
              description="No nomination reached the vote for this award."
            />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">{ballot.cycleName}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {alreadyVoted && (
                  <Alert>
                    <CheckCircle2 className="h-4 w-4" />
                    <AlertDescription>
                      You have already voted in this ballot. Voting again replaces your earlier
                      vote — it does not add a second one.
                    </AlertDescription>
                  </Alert>
                )}

                <div className="space-y-2">
                  {ballot.options.map((o) => {
                    const isMine = o.nominationId === ballot.myVoteNominationId;
                    const selected = choice ? choice === o.nominationId : isMine;
                    return (
                      <button
                        type="button"
                        key={o.nominationId}
                        onClick={() => setChoice(o.nominationId)}
                        className={`w-full rounded-md border p-3 text-left transition hover:bg-muted ${
                          selected ? 'border-primary bg-muted' : ''
                        }`}
                      >
                        <div className="flex items-center justify-between gap-3">
                          <span className="font-medium">{o.nomineeName || o.teamName}</span>
                          {isMine && (
                            <span className="text-xs text-emerald-700 dark:text-emerald-300">
                              your current vote
                            </span>
                          )}
                        </div>
                        <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">
                          {o.justification}
                        </p>
                      </button>
                    );
                  })}
                </div>

                {/* AWD-10 — a voter may state a reason too. Optional, because the note says "may". */}
                <div className="space-y-2">
                  <Label htmlFor="voteReason">Your reason (optional)</Label>
                  <Textarea
                    id="voteReason"
                    rows={3}
                    value={justification}
                    onChange={(e) => setJustification(e.target.value)}
                    placeholder="Why this nominee?"
                  />
                </div>

                <div className="flex items-center justify-between">
                  <p className="text-xs text-muted-foreground">
                    Voting closes {fmtDate(ballot.votingClosesOn)}. Results are not shown until then.
                  </p>
                  {/* ⚠ Withdrawing is not the same as changing. Casting again REPLACES a vote;
                      this removes it entirely, so the tally has one fewer voter rather than one
                      moved. Both are legitimate and the screen should not collapse them. */}
                  {alreadyVoted && (
                    <Button
                      variant="outline"
                      className="mr-2"
                      disabled={withdraw.isPending}
                      onClick={() => withdraw.mutate()}
                    >
                      {withdraw.isPending ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Trash2 className="mr-2 h-4 w-4" />
                      )}
                      Withdraw my vote
                    </Button>
                  )}
                  <Button
                    disabled={!choice || choice === ballot.myVoteNominationId || cast.isPending}
                    onClick={() => cast.mutate()}
                  >
                    {cast.isPending ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Vote className="mr-2 h-4 w-4" />
                    )}
                    {alreadyVoted ? 'Change my vote' : 'Cast my vote'}
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
