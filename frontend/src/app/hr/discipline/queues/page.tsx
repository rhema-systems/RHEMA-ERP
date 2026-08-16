'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Search, Gavel, Clock, AlertTriangle } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { disciplineProcessService } from '@/services/hr/discipline.service';
import type { DisciplineInvestigation, DisciplineHearing } from '@/types/hr/discipline';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/** The four-week rule from FR-HR-178, mirrored here only to label the tab honestly. */
const INVESTIGATION_WEEKS = 4;

function InvestigationTable({
  rows, emptyTitle, emptyBody, showStarted = true,
}: {
  rows: DisciplineInvestigation[];
  emptyTitle: string;
  emptyBody: string;
  showStarted?: boolean;
}) {
  if (rows.length === 0) return <EmptyState title={emptyTitle} description={emptyBody} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Case</TableHead>
          <TableHead>Investigator</TableHead>
          {showStarted && <TableHead>Started</TableHead>}
          <TableHead>Completed</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((i) => (
          <TableRow key={i.id}>
            <TableCell className="font-medium">
              <Link href={`/hr/discipline/${i.disciplinaryActionId}`} className="hover:underline">
                {i.caseNumber}
              </Link>
            </TableCell>
            <TableCell>{i.investigatorName || '—'}</TableCell>
            {showStarted && <TableCell>{fmtDate(i.investigationStartDate)}</TableCell>}
            <TableCell>{fmtDate(i.investigationEndDate)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

function HearingTable({ rows, emptyTitle, emptyBody }: {
  rows: DisciplineHearing[];
  emptyTitle: string;
  emptyBody: string;
}) {
  if (rows.length === 0) return <EmptyState title={emptyTitle} description={emptyBody} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Case</TableHead>
          <TableHead>Employee</TableHead>
          <TableHead>Hearing</TableHead>
          <TableHead>Venue</TableHead>
          <TableHead>Officer</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((h) => (
          <TableRow key={h.id}>
            <TableCell className="font-medium">
              <Link href={`/hr/discipline/${h.disciplinaryActionId}`} className="hover:underline">
                {h.caseNumber}
              </Link>
            </TableCell>
            <TableCell>{h.employeeName}</TableCell>
            <TableCell>{fmtDate(h.hearingDate)}</TableCell>
            <TableCell>{h.hearingVenue || '—'}</TableCell>
            <TableCell>{h.hearingOfficerName || '—'}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * The investigation and hearing work queues.
 *
 * The overdue tab reads the server's own definition of overdue — FR-HR-178's four weeks — and passes
 * no day count of its own. That is deliberate: the same figure drives the advisory banner on each
 * case, and a client that supplied its own would let this page and that banner disagree about the
 * same investigation.
 */
export default function DisciplineQueuesPage() {
  const openInvestigations = useQuery({
    queryKey: ['hr', 'discipline', 'investigations', 'open'],
    queryFn: () => disciplineProcessService.getOpenInvestigations(),
  });

  const overdueInvestigations = useQuery({
    queryKey: ['hr', 'discipline', 'investigations', 'overdue'],
    queryFn: () => disciplineProcessService.getOverdueInvestigations(),
  });

  const upcomingHearings = useQuery({
    queryKey: ['hr', 'discipline', 'hearings', 'upcoming'],
    queryFn: () => disciplineProcessService.getUpcomingHearings(),
  });

  const awaitingOutcome = useQuery({
    queryKey: ['hr', 'discipline', 'hearings', 'awaiting-outcome'],
    queryFn: () => disciplineProcessService.getHearingsAwaitingOutcome(),
  });

  const spinner = (
    <div className="flex items-center justify-center p-10">
      <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
    </div>
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Investigation &amp; hearing queues"
        description="Where disciplinary cases are waiting on someone, and which have run past their deadline."
        backHref="/hr/discipline"
      />

      <Tabs defaultValue="overdue">
        <TabsList>
          <TabsTrigger value="overdue">
            <AlertTriangle className="mr-2 h-4 w-4" />
            Overdue investigations
            {overdueInvestigations.data?.length ? ` (${overdueInvestigations.data.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="open">
            <Search className="mr-2 h-4 w-4" />
            Open investigations
          </TabsTrigger>
          <TabsTrigger value="upcoming">
            <Clock className="mr-2 h-4 w-4" />
            Upcoming hearings
          </TabsTrigger>
          <TabsTrigger value="outcome">
            <Gavel className="mr-2 h-4 w-4" />
            Awaiting outcome
          </TabsTrigger>
        </TabsList>

        <TabsContent value="overdue" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {overdueInvestigations.isLoading ? spinner : (
                <InvestigationTable
                  rows={overdueInvestigations.data ?? []}
                  emptyTitle="Nothing overdue"
                  emptyBody={`No investigation has been open longer than ${INVESTIGATION_WEEKS} weeks.`}
                />
              )}
            </CardContent>
          </Card>
          <p className="mt-3 text-xs text-muted-foreground">
            FR-HR-178 tracks investigations to completion within {INVESTIGATION_WEEKS} weeks. Being
            listed here does not stop the case being worked on — it records that the deadline has
            passed, which is a fact about what already happened.
          </p>
        </TabsContent>

        <TabsContent value="open" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {openInvestigations.isLoading ? spinner : (
                <InvestigationTable
                  rows={openInvestigations.data ?? []}
                  emptyTitle="No open investigations"
                  emptyBody="Every investigation on record has been completed."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="upcoming" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {upcomingHearings.isLoading ? spinner : (
                <HearingTable
                  rows={upcomingHearings.data ?? []}
                  emptyTitle="No hearings scheduled"
                  emptyBody="Nothing is scheduled in the next fortnight."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="outcome" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {awaitingOutcome.isLoading ? spinner : (
                <HearingTable
                  rows={awaitingOutcome.data ?? []}
                  emptyTitle="Nothing awaiting an outcome"
                  emptyBody="Every hearing that has taken place has its notes recorded."
                />
              )}
            </CardContent>
          </Card>
          <p className="mt-3 text-xs text-muted-foreground">
            These hearings have happened but no notes have been recorded against them.
          </p>
        </TabsContent>
      </Tabs>
    </div>
  );
}
