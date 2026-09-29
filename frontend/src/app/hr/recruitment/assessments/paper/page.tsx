'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { FileWarning, KeyRound, Printer, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { recruitmentTestService as tests } from '@/services/hr/recruitment-test.service';
import type { RecruitmentTestPaperVariant } from '@/types/hr/recruitment-tests';

/**
 * ⚠ Lane F's print family, reused on purpose. The body class and the `.interview-paper-sheet`
 * sections are what `globals.css` hangs the page breaks and keep-together rules on; a second copy
 * of those rules under another name is how two print stylesheets drift apart.
 */
const PRINT_BODY_CLASS = 'printing-hr-interview-paper';

function isVariant(value: string | null): value is RecruitmentTestPaperVariant {
  return value === 'QuestionPaper' || value === 'MarkingKey';
}

/**
 * The printed recruitment test (round 4, lane E6): the question paper a candidate writes on, or the
 * marker's key.
 *
 * ⚠ **This renders server-produced HTML**, safe for the same reason the interview paper is: the
 * document is composed on the server from an HR-authored template, and every value that goes into
 * it is encoded there. No candidate-supplied text reaches this page as markup.
 *
 * ⚠ **No auto-print.** A named run prints one paper per candidate the assignment reaches — the
 * count is stated on the button before the dialog opens, not discovered at the printer.
 */
export default function RecruitmentTestPaperPage() {
  const searchParams = useSearchParams();
  const testId = searchParams.get('testId') ?? '';
  const assignmentId = searchParams.get('assignmentId') ?? '';
  const applicationIds = searchParams.getAll('applicationIds');

  const variantParam = searchParams.get('variant');
  const [variant, setVariant] = useState<RecruitmentTestPaperVariant>(
    isVariant(variantParam) ? variantParam : 'QuestionPaper',
  );

  const paper = useQuery({
    queryKey: ['hr', 'recruitment-test-paper', testId, variant, assignmentId, applicationIds],
    queryFn: () =>
      tests.getPaper(testId, {
        variant,
        assignmentId: assignmentId || null,
        applicationIds,
      }),
    enabled: testId !== '',
    retry: false,
  });

  const sheetCount = paper.data?.sheetCount ?? 0;
  const isKey = variant === 'MarkingKey';

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

  const noun = isKey ? 'key' : sheetCount === 1 ? 'paper' : 'papers';

  return (
    <div className="space-y-6 p-6">
      <div className="hr-interview-paper-no-print space-y-6">
        <PageHeader
          title={isKey ? 'Marking key' : 'Test paper'}
          description={paper.data ? `${paper.data.testCode} · ${paper.data.testName}` : undefined}
          backHref="/hr/recruitment/assessments"
          actions={
            <Button onClick={print} disabled={!paper.data || sheetCount === 0}>
              <Printer className="mr-2 h-4 w-4" />
              Print {sheetCount > 0 ? `${sheetCount} ${noun}` : ''}
            </Button>
          }
        />

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">What to print</CardTitle>
            <CardDescription>
              {assignmentId && !isKey
                ? 'One named paper per candidate this assignment reaches, in surname order for the sign-in desk.'
                : isKey
                  ? 'The marker’s copy — the correct answers and the marking notes.'
                  : 'A single blank paper, with lines for the candidate to write their name.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-3">
            <Label className="text-sm">Document</Label>
            <Select value={variant} onValueChange={(value) => setVariant(value as RecruitmentTestPaperVariant)}>
              <SelectTrigger className="w-[220px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="QuestionPaper">Question paper</SelectItem>
                <SelectItem value="MarkingKey">Marking key</SelectItem>
              </SelectContent>
            </Select>
            {paper.data && (
              <Badge variant="secondary">
                {sheetCount} {sheetCount === 1 ? 'sheet' : 'sheets'}
              </Badge>
            )}
          </CardContent>
        </Card>

        {isKey && (
          <Alert variant="destructive">
            <ShieldAlert className="h-4 w-4" />
            <AlertTitle>This page shows every answer</AlertTitle>
            <AlertDescription>
              Keep the printed key apart from the scripts, and never leave it in the examination room.
            </AlertDescription>
          </Alert>
        )}

        {!isKey && (
          <Alert>
            <KeyRound className="h-4 w-4" />
            <AlertTitle>Printed in the order it was written</AlertTitle>
            <AlertDescription>
              A printed paper is never shuffled, even when the online paper is — one marking key has
              to fit every script in the pile.
            </AlertDescription>
          </Alert>
        )}

        {paper.isError && (
          <Alert variant="destructive">
            <FileWarning className="h-4 w-4" />
            <AlertTitle>Nothing to print</AlertTitle>
            <AlertDescription>{(paper.error as Error)?.message ?? 'The paper could not be built.'}</AlertDescription>
          </Alert>
        )}
      </div>

      {paper.isLoading ? (
        <Skeleton className="h-[600px] w-full" />
      ) : paper.data ? (
        <div className="hr-interview-paper-print-root rounded-lg border bg-white p-2 shadow-sm dark:bg-white">
          <div
            className="text-black"
            // Composed on the server from an HR template; every value encoded there. See the header.
            dangerouslySetInnerHTML={{ __html: paper.data.htmlBody }}
          />
        </div>
      ) : null}
    </div>
  );
}
