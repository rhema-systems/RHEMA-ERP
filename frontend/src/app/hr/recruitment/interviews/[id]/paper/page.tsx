'use client';

import { useEffect, useMemo, useState } from 'react';
import { useParams, useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { FileWarning, Printer } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import type { InterviewPaperVariant } from '@/types/hr/interviews';

const PRINT_BODY_CLASS = 'printing-hr-interview-paper';

const VARIANTS: { value: InterviewPaperVariant; label: string; blurb: string }[] = [
  {
    value: 'ScoreSheet',
    label: 'Scoring sheets',
    blurb: 'One sheet per candidate per panelist, with empty score boxes. The thing that gets signed.',
  },
  {
    value: 'Questions',
    label: 'Question list',
    blurb: 'The drawn questions alone, with no score boxes — for the panel to read beforehand.',
  },
  {
    value: 'Pack',
    label: 'Full pack',
    blurb: 'A cover page naming the whole panel and the day’s timetable, then every scoring sheet.',
  },
];

function isVariant(value: string | null): value is InterviewPaperVariant {
  return value === 'ScoreSheet' || value === 'Questions' || value === 'Pack';
}

/**
 * The printed interview paper (round 4, lane F).
 *
 * ⚠ **This renders server-produced HTML**, and that is safe for the same reason the issued-letter
 * page is: the document is composed entirely by the backend from an HR-authored template plus
 * tokens the server supplies, and every token value is HTML-encoded on the way in. No
 * candidate-supplied field reaches the page as markup.
 *
 * Printing follows the HR **letter** family — `printing-hr-interview-paper` +
 * `.hr-interview-paper-print-root` — rather than the payslip's fixed-A4 box. A payslip is one page
 * by construction; a paper is many by construction (one sheet per candidate per panelist), so
 * pinning it to 210×297mm would clip every sheet after the first.
 *
 * There is deliberately **no auto-print**. The version this was ported from fired `window.print()`
 * the moment the page loaded, which is defensible when the print is one sheet for one panelist —
 * but this page can produce a dozen, and the filters below decide how many. The sheet count is
 * stated before the dialog opens, not discovered at the printer.
 */
export default function InterviewPaperPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const interviewId = String(params?.id ?? '');

  const variantParam = searchParams.get('variant');
  const [variant, setVariant] = useState<InterviewPaperVariant>(
    isVariant(variantParam) ? variantParam : 'ScoreSheet',
  );
  // The panelist row id, not an employee id — /me/panel links here with its own slot id, which is
  // exactly that row. Empty string means "everybody", because a Select cannot hold a null value.
  const [panelistId, setPanelistId] = useState<string>(searchParams.get('panelistId') ?? '');
  // Repeated keys, so `?intervieweeIds=a&intervieweeIds=b` works — the same shape the API binds.
  // The scorecard screen links here with exactly one, to reprint the sheet being typed in.
  const [intervieweeIds, setIntervieweeIds] = useState<string[]>(() =>
    searchParams.getAll('intervieweeIds'),
  );

  const details = useQuery({
    queryKey: ['hr', 'interviews', interviewId, 'details'],
    queryFn: () => jobInterviewService.getDetails(interviewId),
    enabled: interviewId !== '',
  });

  const paper = useQuery({
    queryKey: ['hr', 'interviews', interviewId, 'paper', variant, panelistId, intervieweeIds],
    queryFn: () =>
      jobInterviewService.getPaper(interviewId, {
        variant,
        panelistId: panelistId || null,
        intervieweeIds,
      }),
    enabled: interviewId !== '',
  });

  const panel = useMemo(() => {
    const internal = (details.data?.panelists ?? []).map((p) => ({
      id: p.id,
      name: p.employeeName,
      role: humanizeEnum(p.role),
    }));
    // Externals carry their organisation, because "Ama Darko" tells a recruiter choosing whose
    // sheets to print rather less than "Ama Darko — Technical Assessor · ICAG".
    const external = (details.data?.externalPanelists ?? []).map((p) => ({
      id: p.id,
      name: p.associateName,
      role: p.associateOrganization
        ? `${humanizeEnum(p.role)} · ${p.associateOrganization}`
        : humanizeEnum(p.role),
    }));
    return [...internal, ...external];
  }, [details.data]);

  const attendees = details.data?.interviewees ?? [];

  // Filters that do not apply to the chosen variant must not silently narrow it. The question list
  // has no candidate and no panelist on it at all, so both are cleared rather than left dangling in
  // the query key where they would look as if they were still in force.
  const showPanelistFilter = variant === 'ScoreSheet';
  const showCandidateFilter = variant !== 'Questions';

  useEffect(() => {
    if (!showPanelistFilter && panelistId) setPanelistId('');
    if (!showCandidateFilter && intervieweeIds.length > 0) setIntervieweeIds([]);
  }, [showPanelistFilter, showCandidateFilter, panelistId, intervieweeIds.length]);

  const print = () => {
    const done = () => {
      window.removeEventListener('afterprint', done);
      document.body.classList.remove(PRINT_BODY_CLASS);
    };
    window.addEventListener('afterprint', done);
    document.body.classList.add(PRINT_BODY_CLASS);
    window.print();
  };

  // Leaving the page mid-print must not strand the class on <body> — everything else on the site
  // would stay invisible.
  useEffect(() => () => document.body.classList.remove(PRINT_BODY_CLASS), []);

  const toggleCandidate = (id: string, checked: boolean) =>
    setIntervieweeIds((current) =>
      checked ? [...current, id] : current.filter((value) => value !== id),
    );

  if (details.isLoading && paper.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-32" />
        <Skeleton className="h-96" />
      </div>
    );
  }

  if (paper.isError) {
    return (
      <div className="space-y-6">
        <PageHeader title="Interview paper" backHref={`/hr/recruitment/interviews/${interviewId}`} />
        <Alert variant="destructive">
          <FileWarning className="h-4 w-4" />
          <AlertDescription>
            This paper could not be produced. Interviews are readable by HR, or by a panelist sitting
            on this interview — if you are neither, you will not see it here.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const sheetCount = paper.data?.sheetCount ?? 0;
  const activeVariant = VARIANTS.find((v) => v.value === variant);

  return (
    <div className="space-y-6">
      <div className="hr-interview-paper-no-print space-y-6">
        <PageHeader
          title="Interview paper"
          description={
            paper.data
              ? `${paper.data.interviewNumber} · ${paper.data.jobTitle}`
              : details.data
                ? `${details.data.interviewNumber} · ${details.data.jobTitle}`
                : undefined
          }
          backHref={`/hr/recruitment/interviews/${interviewId}`}
          actions={
            <Button onClick={print} disabled={!paper.data || sheetCount === 0}>
              <Printer className="mr-1 h-4 w-4" />
              Print {sheetCount > 0 ? `${sheetCount} ${sheetCount === 1 ? 'sheet' : 'sheets'}` : ''}
            </Button>
          }
        />

        <Card>
          <CardHeader>
            <CardTitle className="text-base">What to print</CardTitle>
            <CardDescription>{activeVariant?.blurb}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="paperVariant">Paper</Label>
              <Select value={variant} onValueChange={(value) => setVariant(value as InterviewPaperVariant)}>
                <SelectTrigger id="paperVariant">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {VARIANTS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {showPanelistFilter && (
              <div className="space-y-2">
                <Label htmlFor="paperPanelist">Panelist</Label>
                <Select value={panelistId || 'all'} onValueChange={(v) => setPanelistId(v === 'all' ? '' : v)}>
                  <SelectTrigger id="paperPanelist">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Everyone on the panel</SelectItem>
                    {panel.map((member) => (
                      <SelectItem key={member.id} value={member.id}>
                        {member.name}
                        {member.role ? ` — ${member.role}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  One sheet per candidate for each panelist chosen. The whole panel is the default —
                  the version this replaced could only ever print the person currently signed in.
                </p>
              </div>
            )}

            {showCandidateFilter && (
              <div className="space-y-2">
                <Label>Candidates</Label>
                {attendees.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Nobody is booked in yet. A blank sheet still prints, so the panel can prepare.
                  </p>
                ) : (
                  <div className="max-h-40 space-y-2 overflow-y-auto rounded-md border p-3">
                    {attendees.map((attendee) => (
                      <div key={attendee.id} className="flex items-start gap-2">
                        <Checkbox
                          id={`candidate-${attendee.id}`}
                          checked={intervieweeIds.includes(attendee.id)}
                          onCheckedChange={(checked) => toggleCandidate(attendee.id, checked === true)}
                        />
                        <Label htmlFor={`candidate-${attendee.id}`} className="text-sm font-normal">
                          {attendee.candidateName}
                          {attendee.slotStartTime ? (
                            <span className="ml-1 text-muted-foreground">
                              · {attendee.slotStartTime.slice(0, 5)}
                            </span>
                          ) : null}
                        </Label>
                      </div>
                    ))}
                  </div>
                )}
                <p className="text-xs text-muted-foreground">
                  {intervieweeIds.length === 0
                    ? 'Everyone booked in, in slot order — so the sheets come off the printer in the order the panel will see people.'
                    : `${intervieweeIds.length} selected.`}
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        {variant === 'Questions' && (
          <Alert>
            <FileWarning className="h-4 w-4" />
            <AlertDescription>
              This paper lists the questions that were drawn. A blank scoring sheet gives nothing
              away; this one does — it is for the panel, not for the candidate.
            </AlertDescription>
          </Alert>
        )}

        {paper.data && sheetCount > 0 && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Badge variant="secondary">
              {sheetCount} {sheetCount === 1 ? 'sheet' : 'sheets'}
            </Badge>
            <span>Each sheet starts a new page.</span>
          </div>
        )}
      </div>

      {paper.isFetching && !paper.data ? (
        <Skeleton className="h-96" />
      ) : (
        <div className="hr-interview-paper-print-root rounded-lg border bg-white p-2 shadow-sm dark:bg-white">
          {/* Rendered on a light surface whatever the theme: this is a piece of paper, and a
              dark-mode letterhead is not what comes out of the printer. */}
          <div
            className="text-black [&_a]:text-black"
            dangerouslySetInnerHTML={{ __html: paper.data?.htmlBody ?? '' }}
          />
        </div>
      )}
    </div>
  );
}
