'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Award,
  ClipboardCheck,
  Loader2,
  Medal,
  Trophy,
  UserPlus,
  Vote,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const fmtMoney = (v?: number | null) =>
  v === null || v === undefined
    ? '—'
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * The employee's own awards surface — `api/awards/me`.
 *
 * ⚠ **Nothing on this page is behind an HR permission**, and that is deliberate rather than an
 * oversight. Nominating a colleague, casting a vote and scoring as a committee member are acts every
 * employee performs; entitlement is read off the record, not granted. Gating this screen on
 * `HR.Awards.Read` would lock the entire workforce out of the feature the area exists for — the
 * area-15b trap, where a permission gate was used for an actor defined by the record.
 *
 * ⚠ **"My awards" did not exist until slice 11.** Every route on this surface was about taking part
 * in the process; there was no way to see what you had actually won. The gap was found by the UI
 * payload probe, not by anyone using the system.
 *
 * Area 25 slice 9: re-homed from /hr/awards/me into the portal shell (D3 — moved, not redirected),
 * subpages with it.
 */
export default function MyAwardsPage() {
  const { data: myAwards, isLoading: loadingAwards } = useQuery({
    queryKey: ['me', 'awards', 'received'],
    queryFn: () => awardsService.getMyAwards(),
  });

  const { data: myLongService } = useQuery({
    queryKey: ['me', 'awards', 'long-service'],
    queryFn: () => awardsService.getMyLongServiceAwards(),
  });

  const { data: myNominations, isLoading: loadingNominations } = useQuery({
    queryKey: ['me', 'awards', 'nominations'],
    queryFn: () => awardsService.getMyNominations(),
  });

  const { data: openCycles } = useQuery({
    queryKey: ['me', 'awards', 'open-cycles'],
    queryFn: () => awardsService.getMyOpenCycles(),
  });

  const { data: votingCycles } = useQuery({
    queryKey: ['me', 'awards', 'voting-cycles'],
    queryFn: () => awardsService.getMyVotingCycles(),
  });

  const { data: pendingReviews } = useQuery({
    queryKey: ['me', 'awards', 'pending-reviews'],
    queryFn: () => awardsService.getMyPendingReviews(),
  });

  const awards = myAwards ?? [];
  const longService = myLongService ?? [];
  const nominations = myNominations ?? [];
  const openForNomination = openCycles ?? [];
  const openForVoting = votingCycles ?? [];
  const owed = pendingReviews ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Awards"
        description="What you have been awarded, who you have put forward, and what is open to you now."
        backHref="/me"
        actions={
          <div className="flex gap-2">
            {openForVoting.length > 0 && (
              <Button variant="outline" asChild>
                <Link href="/me/awards/vote">
                  <Vote className="mr-2 h-4 w-4" />
                  Vote
                </Link>
              </Button>
            )}
            {owed.length > 0 && (
              <Button variant="outline" asChild>
                <Link href="/me/awards/reviews">
                  <ClipboardCheck className="mr-2 h-4 w-4" />
                  Score ({owed.length})
                </Link>
              </Button>
            )}
            <Button asChild disabled={openForNomination.length === 0}>
              <Link href="/me/awards/nominate">
                <UserPlus className="mr-2 h-4 w-4" />
                Nominate a colleague
              </Link>
            </Button>
          </div>
        }
      />

      {/* ── things the employee can act on right now ────────────────────────── */}
      {(openForNomination.length > 0 || openForVoting.length > 0 || owed.length > 0) && (
        <div className="grid gap-4 md:grid-cols-3">
          {openForNomination.length > 0 && (
            <Card className="transition hover:border-primary">
              <Link href="/me/awards/nominate">
              <CardContent className="flex items-center gap-3 p-4">
                <UserPlus className="h-5 w-5 text-sky-600" />
                <div className="min-w-0">
                  <p className="text-sm font-medium">
                    {openForNomination.length} award
                    {openForNomination.length === 1 ? '' : 's'} open for nomination
                  </p>
                  <p className="truncate text-xs text-muted-foreground">
                    {openForNomination.map((c) => c.awardTypeName).join(', ')}
                  </p>
                </div>
              </CardContent>
              </Link>
            </Card>
          )}

          {openForVoting.length > 0 && (
            <Card className="transition hover:border-primary">
              <Link href="/me/awards/vote">
              <CardContent className="flex items-center gap-3 p-4">
                <Vote className="h-5 w-5 text-violet-600" />
                <div className="min-w-0">
                  <p className="text-sm font-medium">
                    {openForVoting.length} ballot{openForVoting.length === 1 ? '' : 's'} open
                  </p>
                  <p className="truncate text-xs text-muted-foreground">
                    {openForVoting.map((c) => c.name).join(', ')}
                  </p>
                </div>
              </CardContent>
              </Link>
            </Card>
          )}

          {/* ⚠ Committee members hold no HR permission. For most of them this is the only awards
              screen they will ever open. */}
          {owed.length > 0 && (
            <Card className="transition hover:border-primary">
              <Link href="/me/awards/reviews">
              <CardContent className="flex items-center gap-3 p-4">
                <ClipboardCheck className="h-5 w-5 text-amber-600" />
                <div className="min-w-0">
                  <p className="text-sm font-medium">
                    {owed.length} nomination{owed.length === 1 ? '' : 's'} awaiting your score
                  </p>
                  <p className="truncate text-xs text-muted-foreground">
                    As a committee member
                  </p>
                </div>
              </CardContent>
              </Link>
            </Card>
          )}
        </div>
      )}

      {/* ── what you have won ───────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Trophy className="h-4 w-4" />
            Awards you have received
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {loadingAwards ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : awards.length === 0 ? (
            <EmptyState
              icon={Trophy}
              title="No awards yet"
              description="Awards you receive appear here, with the date and any amount attached."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Award</TableHead>
                  <TableHead>Level</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead className="text-right">Value</TableHead>
                  <TableHead>Presented</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {awards.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.awardNumber}</TableCell>
                    <TableCell>{a.awardTypeName}</TableCell>
                    <TableCell>{a.awardLevelName ?? '—'}</TableCell>
                    <TableCell>{fmtDate(a.awardDate)}</TableCell>
                    <TableCell className="text-right">{fmtMoney(a.monetaryAmount)}</TableCell>
                    <TableCell>{fmtDate(a.presentationDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── long service, kept separate ─────────────────────────────────────── */}
      {longService.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Medal className="h-4 w-4" />
              Long-service milestones
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  {/* ⚠ The MILESTONE reached, not the years served. Somebody swept at twenty-two
                      years receives the twenty-year award, and that is what a certificate prints. */}
                  <TableHead>Milestone</TableHead>
                  <TableHead>Reached</TableHead>
                  <TableHead className="text-right">Value</TableHead>
                  <TableHead>Presented</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {longService.map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="font-medium">{l.yearsOfService} years</TableCell>
                    <TableCell>{fmtDate(l.milestoneDate)}</TableCell>
                    <TableCell className="text-right">{fmtMoney(l.monetaryAmount)}</TableCell>
                    <TableCell>{fmtDate(l.presentationDate)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* ── who you put forward ─────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <UserPlus className="h-4 w-4" />
            Nominations you have raised
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {loadingNominations ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : nominations.length === 0 ? (
            <EmptyState
              icon={Award}
              title="You have not nominated anyone"
              description={
                openForNomination.length > 0
                  ? 'There are awards open for nomination right now.'
                  : 'Nothing is open for nomination at the moment.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Nominee</TableHead>
                  <TableHead>Award</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {nominations.map((n) => (
                  <TableRow key={n.id}>
                    <TableCell className="font-medium">
                      <Link className="underline" href={`/me/awards/nominations/${n.id}`}>
                        {n.nominationNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{n.nomineeName || n.teamName || '—'}</TableCell>
                    <TableCell>{n.awardTypeName}</TableCell>
                    <TableCell>{fmtDate(n.nominationDate)}</TableCell>
                    <TableCell>
                      <Badge variant="secondary">{n.statusName}</Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* The desk register link stays: it is permission-gated there, and an HR user arriving
          through the portal switcher is exactly who follows it. */}
      <p className="text-sm text-muted-foreground">
        Managing the awards themselves?{' '}
        <Link className="underline" href="/hr/awards">
          Go to the awards register
        </Link>
        .
      </p>
    </div>
  );
}
