'use client';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatDate } from '@/lib/hr/attendance-format';
import type { ViewSubmittedEvaluation } from '@/types/hr/appraisal-run';

/**
 * A submitted self-evaluation, read-only.
 *
 * Shows the scores as *stored*, not as re-derived here — `achievedGrade` and
 * `achievementPercent` come back resolved by the server against the appraisal's frozen
 * snapshot, and recomputing them client-side would eventually disagree with what the appraisal
 * was actually scored on.
 */
export function SubmittedEvaluationView({ data }: { data: ViewSubmittedEvaluation }) {
  const sections = [...data.sections].sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-wrap items-center gap-x-6 gap-y-2 p-4 text-sm">
          <span className="text-muted-foreground">
            Submitted <span className="font-medium text-foreground">{formatDate(data.submittedDate)}</span>
          </span>
          {data.requirePeerReviews && (
            <span className="text-muted-foreground">
              Peer reviews{' '}
              <span className="font-medium text-foreground">
                {data.peerReviewsInProgress ? 'in progress' : 'complete'}
              </span>
            </span>
          )}
          <span className="text-muted-foreground">
            Manager review{' '}
            <span className="font-medium text-foreground">
              {data.managerReviewComplete ? 'complete' : 'outstanding'}
            </span>
          </span>
          {data.requireHRReview && (
            <span className="text-muted-foreground">
              HR review{' '}
              <span className="font-medium text-foreground">
                {data.hrReviewComplete ? 'complete' : 'outstanding'}
              </span>
            </span>
          )}
        </CardContent>
      </Card>

      {sections.map((section) => (
        <Card key={`${section.displayOrder}-${section.sectionName}`}>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="text-base">{section.sectionName}</CardTitle>
              <Badge variant="outline">Weight {section.sectionWeight}%</Badge>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Criterion</TableHead>
                  <TableHead className="w-20 text-right">Weight</TableHead>
                  <TableHead className="w-32 text-right">Result</TableHead>
                  <TableHead className="w-28">Grade</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {section.items.map((item) => (
                  <TableRow key={item.criterionKey}>
                    <TableCell>
                      <div className="font-medium">{item.itemName}</div>
                      {item.notes && (
                        <div className="mt-1 whitespace-pre-wrap text-xs text-muted-foreground">
                          {item.notes}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{item.itemWeight}%</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {/* Measured by the row's own flag: a goal row carries no KPI id. */}
                      {(item.scoringMethod ? item.scoringMethod === 'Measured' : !!item.kpiDefinitionId) ? (
                        <>
                          {item.actualValue ?? '—'}
                          {item.kpiUnit ? ` ${item.kpiUnit}` : ''}
                          {item.kpiTargetValue != null && (
                            <div className="text-xs text-muted-foreground">
                              of {item.kpiTargetValue}
                              {item.achievementPercent != null &&
                                ` · ${Number(item.achievementPercent).toFixed(1)}%`}
                            </div>
                          )}
                        </>
                      ) : (
                        (item.numericScore ?? '—')
                      )}
                    </TableCell>
                    <TableCell>{item.achievedGrade ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      ))}

      {data.attachments.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Attachments</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            {data.attachments.map((a) => (
              <div key={a.attachmentId} className="flex items-center justify-between gap-4">
                <span>{a.fileName}</span>
                <span className="text-muted-foreground">{formatDate(a.uploadedDate)}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
