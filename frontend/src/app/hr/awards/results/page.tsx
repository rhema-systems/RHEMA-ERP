'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Info, Loader2, Star, Trophy, Vote } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { EmptyState } from '@/components/hr/common/EmptyState';
import { awardsService } from '@/services/hr/awards.service';

/**
 * Who won, and by what margin.
 *
 * ⚠ **A tie is reported, never broken.** Neither the vote tally nor the committee average picks a
 * winner when two nominees are level — that is a decision for people, and a system that quietly
 * chose one would be making it for them without saying so.
 *
 * ⚠ **The tally is withheld until the voting window closes.** An empty result while voting is open
 * is the rule working, not "nobody voted", and this screen says which.
 *
 * ⚠ **A committee score only counts once enough reviewers have given one.** A nominee scored by one
 * member of a five-member committee is not leading — they are unjudged, and the screen marks them
 * so rather than sorting them to the top.
 */
export default function AwardResultsPage() {
  const [awardTypeId, setAwardTypeId] = useState('');
  const [cycleId, setCycleId] = useState('');

  const { data: types } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  const activeTypes = (types ?? []).filter((t) => t.isActive);
  const chosenType = activeTypes.find((t) => t.id === awardTypeId);
  const byVote = chosenType?.winnerDecision === 'StaffVote';
  const byCommittee = chosenType?.winnerDecision === 'CommitteeScore';

  const { data: cycles } = useQuery({
    queryKey: ['award-cycles', awardTypeId],
    queryFn: () => awardsService.getCycles(awardTypeId),
    enabled: Boolean(awardTypeId),
  });

  const cycle = (cycles ?? []).find((c) => c.id === cycleId);

  const { data: voteResult, isFetching: loadingVotes } = useQuery({
    queryKey: ['award-vote-results', cycleId],
    queryFn: () => awardsService.getVoteResults(cycleId),
    enabled: Boolean(cycleId) && byVote,
    retry: false,
  });

  const { data: committeeResult, isFetching: loadingScores } = useQuery({
    queryKey: ['award-committee-results', cycleId],
    queryFn: () => awardsService.getCommitteeResults(cycleId),
    enabled: Boolean(cycleId) && byCommittee,
    retry: false,
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Results"
        description="Who won a cycle — the vote tally, or the committee's average scores."
        backHref="/hr/awards"
      />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <Trophy className="h-4 w-4 text-muted-foreground" />
          <Select
            value={awardTypeId}
            onValueChange={(v) => { setAwardTypeId(v); setCycleId(''); }}
          >
            <SelectTrigger className="w-80">
              <SelectValue placeholder="Choose an award" />
            </SelectTrigger>
            <SelectContent>
              {activeTypes.map((t) => (
                <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>

          {awardTypeId && (
            <Select value={cycleId} onValueChange={setCycleId}>
              <SelectTrigger className="w-80">
                <SelectValue placeholder="Choose a cycle" />
              </SelectTrigger>
              <SelectContent>
                {(cycles ?? []).map((c) => (
                  <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          {chosenType && <Badge variant="secondary">{chosenType.winnerDecisionName}</Badge>}
        </CardContent>
      </Card>

      {!cycleId ? (
        <EmptyState
          icon={Trophy}
          title="Choose a cycle"
          description="Results belong to a run of an award, not to the award itself."
        />
      ) : chosenType?.winnerDecision === 'ManagementDecision' ? (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            This award is decided outright by management, so there is no tally or score to show. Its
            winners appear in the conferred-awards register.
          </AlertDescription>
        </Alert>
      ) : byVote ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Vote className="h-4 w-4" /> Vote tally
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {loadingVotes ? (
              <div className="flex items-center justify-center p-8">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : !voteResult ? (
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  No tally yet. Results are withheld until the voting window closes — that is the
                  rule working, not an absence of votes.
                </AlertDescription>
              </Alert>
            ) : voteResult.isVotingOpen ? (
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  Voting is still open, so the tally is withheld. Showing it now would let late
                  voters see which way it is going.
                </AlertDescription>
              </Alert>
            ) : (
              <>
                {voteResult.isTied ? (
                  <Alert>
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>
                      <strong>Tied.</strong> The system reports this rather than breaking it — who
                      wins is a decision for people to make.
                    </AlertDescription>
                  </Alert>
                ) : voteResult.winnerName ? (
                  <Alert>
                    <Trophy className="h-4 w-4" />
                    <AlertDescription>
                      <strong>{voteResult.winnerName}</strong> won, from {voteResult.totalVotes}{' '}
                      vote{voteResult.totalVotes === 1 ? '' : 's'} cast.
                    </AlertDescription>
                  </Alert>
                ) : null}

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Nominee</TableHead>
                      <TableHead className="text-right">Votes</TableHead>
                      <TableHead className="text-right">Share</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {voteResult.tally.map((t) => (
                      <TableRow key={t.nominationId}>
                        <TableCell className={t.nominationId === voteResult.winningNominationId ? 'font-medium' : ''}>
                          {t.nomineeName}
                        </TableCell>
                        {/* Nominees with zero votes are listed too — a ballot that showed only
                            those who scored would hide who stood and lost. */}
                        <TableCell className="text-right">{t.votes}</TableCell>
                        <TableCell className="text-right text-muted-foreground">
                          {voteResult.totalVotes > 0
                            ? `${Math.round((t.votes / voteResult.totalVotes) * 100)}%`
                            : '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </>
            )}
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Star className="h-4 w-4" /> Committee scores
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {loadingScores ? (
              <div className="flex items-center justify-center p-8">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : !committeeResult ? (
              <Alert>
                <Info className="h-4 w-4" />
                <AlertDescription>
                  Nothing has been scored in this cycle yet.
                </AlertDescription>
              </Alert>
            ) : (
              <>
                {committeeResult.isTied ? (
                  <Alert>
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>
                      <strong>Tied on average score.</strong> Reported rather than broken.
                    </AlertDescription>
                  </Alert>
                ) : committeeResult.winnerName ? (
                  <Alert>
                    <Trophy className="h-4 w-4" />
                    <AlertDescription>
                      <strong>{committeeResult.winnerName}</strong> has the highest average score.
                    </AlertDescription>
                  </Alert>
                ) : (
                  <Alert>
                    <Info className="h-4 w-4" />
                    <AlertDescription>
                      No nominee has been scored by enough reviewers yet
                      {committeeResult.minRequiredReviewers
                        ? ` — this award needs ${committeeResult.minRequiredReviewers}.`
                        : '.'}
                    </AlertDescription>
                  </Alert>
                )}

                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Nominee</TableHead>
                      <TableHead className="text-right">Average</TableHead>
                      <TableHead className="text-right">Reviewers</TableHead>
                      <TableHead>Counts?</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {committeeResult.scores.map((sc) => (
                      <TableRow key={sc.nominationId}>
                        <TableCell className={sc.nominationId === committeeResult.winningNominationId ? 'font-medium' : ''}>
                          {sc.nomineeName}
                        </TableCell>
                        <TableCell className="text-right">{sc.averageScore.toFixed(1)}</TableCell>
                        <TableCell className="text-right">{sc.reviewerCount}</TableCell>
                        <TableCell>
                          {/* ⚠ A high average from one reviewer is not a lead. Marking it is the
                              difference between a ranking and a misleading one. */}
                          {sc.meetsMinimumReviewers ? (
                            <Badge variant="secondary">Counts</Badge>
                          ) : (
                            <span className="text-xs text-muted-foreground">
                              too few reviewers
                            </span>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </>
            )}
          </CardContent>
        </Card>
      )}

      {cycle && (
        <p className="text-sm text-muted-foreground">
          {cycle.name} — {cycle.nominationCount} nomination
          {cycle.nominationCount === 1 ? '' : 's'} raised.
        </p>
      )}
    </div>
  );
}
