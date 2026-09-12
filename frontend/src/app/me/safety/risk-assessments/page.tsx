'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { toast } from 'sonner';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import type { MyRiskAcknowledgement } from '@/types/hr/safety-hazards';

/**
 * My Risk Assessments (area 25 slice 8, spec #34) — built from nothing on the new
 * `for-acknowledgement/mine` self arm; the census found only the SheReadPolicy-gated per-employee
 * read plus a self-defaulting acknowledge POST.
 *
 * Every approved/active assessment appears, split into what still needs the caller's signature
 * and what they have already signed. Signing is self-service: the server records the TOKEN's
 * employee regardless of the body, refuses a repeat signature (422) and refuses anything not
 * approved/active. The assessment DETAIL stays desk-side (SheReadPolicy) — what the employee
 * acknowledges is the assessment as briefed by their supervisor; the title, type, location and
 * validity shown here identify it.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyRiskAssessmentsPage() {
  const queryClient = useQueryClient();
  const [signing, setSigning] = useState<MyRiskAcknowledgement | null>(null);
  const [comments, setComments] = useState('');

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['me', 'safety', 'risk-acknowledgements'],
    queryFn: () => safetyRiskAssessmentService.getMineForAcknowledgement(),
  });

  const awaiting = rows.filter((r) => !r.acknowledgedByMe);
  const done = rows.filter((r) => r.acknowledgedByMe);

  const acknowledge = useMutation({
    mutationFn: (row: MyRiskAcknowledgement) =>
      safetyRiskAssessmentService.addAcknowledgement(row.riskAssessmentId, {
        riskAssessmentId: row.riskAssessmentId,
        // Ignored for non-desk callers — the server always signs as the token's employee.
        employeeId: '00000000-0000-0000-0000-000000000000',
        comments: comments.trim() || null,
      }),
    onSuccess: () => {
      toast.success('Acknowledged — your signature is on record.');
      setSigning(null);
      setComments('');
      queryClient.invalidateQueries({ queryKey: ['me', 'safety', 'risk-acknowledgements'] });
    },
    onError: (error) =>
      toast.error(error instanceof Error ? error.message : 'Could not record the acknowledgement'),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Risk Assessments"
        description="The approved risk assessments that apply across the business. Acknowledging one records that you have read and understood it — ask your supervisor to take you through any you have not been briefed on."
        backHref="/me/safety"
      />

      {isLoading ? null : rows.length === 0 ? (
        <EmptyState
          icon={ClipboardCheck}
          title="No active risk assessments"
          description="When the SHE team approves assessments that need your acknowledgement, they appear here."
        />
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">
                Awaiting your acknowledgement{awaiting.length > 0 ? ` (${awaiting.length})` : ''}
              </CardTitle>
            </CardHeader>
            <CardContent className="p-0">
              {awaiting.length === 0 ? (
                <p className="text-muted-foreground px-6 pb-6 text-sm">
                  Nothing outstanding — you have signed every active assessment.
                </p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead className="max-w-sm">Title</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Location</TableHead>
                      <TableHead>Valid until</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {awaiting.map((r) => (
                      <TableRow key={r.riskAssessmentId}>
                        <TableCell className="font-mono text-sm">{r.assessmentNumber}</TableCell>
                        <TableCell className="max-w-sm font-medium">{r.title}</TableCell>
                        <TableCell>{r.typeName}</TableCell>
                        <TableCell>{r.locationName ?? '—'}</TableCell>
                        <TableCell>{fmtDate(r.validUntil)}</TableCell>
                        <TableCell className="text-right">
                          <Button size="sm" onClick={() => setSigning(r)}>
                            Acknowledge
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {done.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Acknowledged ({done.length})</CardTitle>
              </CardHeader>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead className="max-w-sm">Title</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Signed</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {done.map((r) => (
                      <TableRow key={r.riskAssessmentId}>
                        <TableCell className="font-mono text-sm">{r.assessmentNumber}</TableCell>
                        <TableCell className="max-w-sm">{r.title}</TableCell>
                        <TableCell>{r.typeName}</TableCell>
                        <TableCell>
                          <Badge variant="secondary">{fmtDate(r.acknowledgedDate)}</Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </>
      )}

      <Dialog open={!!signing} onOpenChange={(open) => !open && setSigning(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Acknowledge {signing?.assessmentNumber}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-sm">
              You are confirming you have read and understood{' '}
              <span className="font-medium">&ldquo;{signing?.title}&rdquo;</span> and will follow
              its controls. Your signature is recorded with today&rsquo;s date and cannot be
              repeated.
            </p>
            <div className="space-y-2">
              <Label htmlFor="ack-comments">Comments (optional)</Label>
              <Textarea
                id="ack-comments"
                rows={2}
                value={comments}
                onChange={(e) => setComments(e.target.value)}
                placeholder="Anything unclear, or conditions you want noted."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setSigning(null)}>
              Cancel
            </Button>
            <Button
              disabled={acknowledge.isPending}
              onClick={() => signing && acknowledge.mutate(signing)}
            >
              Sign acknowledgement
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
