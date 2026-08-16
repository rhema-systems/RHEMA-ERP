'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Check, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { disciplineService } from '@/services/hr/discipline.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Disciplinary decisions awaiting the caller's confirmation.
 *
 * This page is open to any authenticated user, and that is the point: the officer who confirms a
 * sanction is a head of department or the MD, neither of whom is in HR, and the case register
 * answers 403 for them. Without this they would have no way to see the work at all.
 *
 * It is not an open door. The list is token-derived with no id parameter, and its contents come from
 * the workflow engine, which answers per case and per step and knows about delegation — so a caller
 * sees exactly the cases they are assigned to and nothing else. The confirm and refuse calls are
 * checked the same way server-side; the buttons here are a convenience, not the gate.
 */
export default function DisciplineApprovalsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [decisionOn, setDecisionOn] = useState<{ id: string; caseNumber: string; approve: boolean } | null>(null);
  const [comments, setComments] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'discipline', 'awaiting-my-approval'],
    queryFn: () => disciplineService.getAwaitingMyApproval(),
  });

  const act = useMutation({
    mutationFn: () => {
      if (!decisionOn) throw new Error('Nothing selected.');
      return decisionOn.approve
        ? disciplineService.approveDecision(decisionOn.id, comments || null)
        : disciplineService.rejectDecision(decisionOn.id, comments || null);
    },
    onSuccess: () => {
      toast({
        title: decisionOn?.approve ? 'Decision confirmed' : 'Decision refused',
        description: decisionOn?.approve
          ? undefined
          : 'The case has returned for reconsideration. The allegation still stands.',
      });
      setDecisionOn(null);
      setComments('');
      queryClient.invalidateQueries({ queryKey: ['hr', 'discipline'] });
    },
    onError: (e: Error) =>
      toast({ title: 'That is not possible right now', description: e.message, variant: 'destructive' }),
  });

  const items = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Decisions awaiting my confirmation"
        description="Disciplinary sanctions proposed by an officer and routed to you to confirm or refuse."
        backHref="/hr"
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="Nothing awaiting you"
              description="No disciplinary decision is routed to you for confirmation at the moment."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Case</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Offence</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Incident</TableHead>
                  <TableHead className="text-right">Decision</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/discipline/${c.id}`} className="hover:underline">
                        {c.caseNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div>{c.employeeName}</div>
                      {c.employeeNumber && (
                        <div className="text-xs text-muted-foreground">{c.employeeNumber}</div>
                      )}
                    </TableCell>
                    <TableCell>{c.offenseName}</TableCell>
                    <TableCell><StatusBadge status={c.severityName} /></TableCell>
                    <TableCell>{fmtDate(c.incidentDate)}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => { setDecisionOn({ id: c.id, caseNumber: c.caseNumber, approve: false }); setComments(''); }}
                        >
                          <X className="mr-1 h-4 w-4" /> Refuse
                        </Button>
                        <Button
                          size="sm"
                          onClick={() => { setDecisionOn({ id: c.id, caseNumber: c.caseNumber, approve: true }); setComments(''); }}
                        >
                          <Check className="mr-1 h-4 w-4" /> Confirm
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Open a case to read the allegation, the investigation and the hearing before deciding. This
        list deliberately shows only what is needed to find the right case.
      </p>

      <Dialog open={decisionOn !== null} onOpenChange={(open) => !open && setDecisionOn(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {decisionOn?.approve ? 'Confirm this decision' : 'Refuse this decision'}
              {decisionOn ? ` — ${decisionOn.caseNumber}` : ''}
            </DialogTitle>
            <DialogDescription>
              {decisionOn?.approve
                ? 'The sanction becomes final and the case moves to decision made.'
                : 'The case returns for reconsideration with the proposed sanction cleared. Refusing a sanction does not dismiss the allegation.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>Comments{decisionOn?.approve ? '' : ' — say why, so the officer can decide again'}</Label>
            <Textarea rows={4} value={comments} onChange={(e) => setComments(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDecisionOn(null)}>Cancel</Button>
            <Button
              variant={decisionOn?.approve ? 'default' : 'destructive'}
              onClick={() => act.mutate()}
              disabled={act.isPending}
            >
              {act.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {decisionOn?.approve ? 'Confirm' : 'Refuse'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
