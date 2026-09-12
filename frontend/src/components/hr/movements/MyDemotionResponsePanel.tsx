'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Gavel, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { movementSubtypeService } from '@/services/hr/movement-subtype.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The demoted employee's own answer to a demotion notice — accept it, or appeal.
 *
 * ⚠ **This is why HR's pending-appeals queue could never fill.** `POST staff-demotions/{id}/respond`
 * has been implemented and gated correctly the whole time — it carries no permission policy beyond
 * `InternalOnly`, deliberately, and the service refuses anyone but the demoted employee, because an
 * appeal filed in someone's name by someone else is worse than no appeal. What was missing was any
 * screen at all for the one person allowed to use it, so HR's wired queue read an empty table
 * forever.
 *
 * ⚠ **Distinct from accepting the movement itself.** `staff-movements/{id}/respond` records whether
 * the employee accepts the move; this records whether they accept the DEMOTION, and an appeal here
 * is what puts the case in front of HR. A demotion can be accepted as a movement and appealed as a
 * demotion, and the two are not the same statement.
 *
 * ⚠ **One answer only.** The service refuses a second response — an appeal is not something to be
 * revised after the fact — so the panel goes read-only once given.
 */
export function MyDemotionResponsePanel({ movementId }: { movementId: string }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [answering, setAnswering] = useState<'Accepted' | 'Appealed' | null>(null);
  const [comments, setComments] = useState('');

  const queryKey = ['me', 'movements', movementId, 'demotion'];
  const { data: demotion, isLoading } = useQuery({
    queryKey,
    queryFn: () => movementSubtypeService.getDemotionByMovement(movementId),
  });

  const respond = useMutation({
    mutationFn: () => {
      if (!demotion || !answering) throw new Error('Nothing to answer.');
      const note = comments.trim();
      return movementSubtypeService.respondToDemotion(
        demotion.id,
        note ? `${answering}: ${note}` : answering,
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({
        title: answering === 'Appealed' ? 'Your appeal has been filed' : 'Recorded',
        description:
          answering === 'Appealed'
            ? 'HR sees it on their appeals queue.'
            : 'Your acceptance is on the record.',
      });
      setAnswering(null);
      setComments('');
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not record your answer',
        description: e?.response?.data?.message ?? e?.message,
      }),
  });

  // Not a demotion, or its detail has not been recorded yet — nothing to answer.
  if (isLoading || !demotion) return null;

  const answered = !!demotion.employeeResponse;

  return (
    <Card className={answered ? undefined : 'border-amber-300 dark:border-amber-900'}>
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Gavel className="h-4 w-4" />
          Your response to this demotion
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {demotion.reasonName && (
          <p className="text-sm">
            <span className="text-muted-foreground">Reason recorded: </span>
            {demotion.reasonName}
          </p>
        )}

        {answered ? (
          <div className="rounded-md border p-3">
            <Badge variant="secondary">Answered {fmtDate(demotion.employeeResponseDate)}</Badge>
            <p className="mt-2 whitespace-pre-wrap text-sm">{demotion.employeeResponse}</p>
          </div>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">
              You can accept this demotion or appeal against it. Only you can answer — nobody in HR
              can record this for you — and it can only be answered once.
            </p>
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" onClick={() => { setComments(''); setAnswering('Accepted'); }}>
                Accept
              </Button>
              <Button variant="destructive" onClick={() => { setComments(''); setAnswering('Appealed'); }}>
                Appeal
              </Button>
            </div>
          </>
        )}
      </CardContent>

      <Dialog open={answering !== null} onOpenChange={(o) => !o && setAnswering(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {answering === 'Appealed' ? 'Appeal this demotion' : 'Accept this demotion'}
            </DialogTitle>
            <DialogDescription>
              {answering === 'Appealed'
                ? 'Your appeal goes to HR. Say what you disagree with — it is the only account of your side on the record.'
                : 'Recorded against the demotion. You can add anything you want noted.'}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-2">
            <Label htmlFor="dr-comments">
              {answering === 'Appealed' ? 'Grounds for your appeal' : 'Anything to add'}
            </Label>
            <Textarea
              id="dr-comments"
              rows={4}
              maxLength={2000}
              value={comments}
              onChange={(e) => setComments(e.target.value)}
              placeholder={
                answering === 'Appealed'
                  ? 'What you disagree with, and why'
                  : 'Optional'
              }
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAnswering(null)} disabled={respond.isPending}>
              Cancel
            </Button>
            <Button
              variant={answering === 'Appealed' ? 'destructive' : 'default'}
              onClick={() => respond.mutate()}
              disabled={respond.isPending || (answering === 'Appealed' && !comments.trim())}
            >
              {respond.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {answering === 'Appealed' ? 'File the appeal' : 'Accept'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
