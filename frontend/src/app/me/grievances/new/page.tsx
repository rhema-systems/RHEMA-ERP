'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { Loader2, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { grievanceService } from '@/services/hr/grievance.service';
import { toast } from 'sonner';
import { GRIEVANCE_LADDER } from '@/types/hr/grievance';

const MIN_STATEMENT = 20;

/**
 * Raise a grievance.
 *
 * There is no employee picker on this form, and that is the point: a grievance is the employee's own
 * act. The server takes the griever from the token and the payload has no field to override it, so
 * HR cannot raise one for somebody — not by design of this screen, but by design of the API.
 *
 * The page states what happens next, because the ladder is the part people do not know: the
 * grievance goes to the first rung, and if the answer does not satisfy them it is THEY who escalate
 * it, not HR.
 *
 * Area 25 slice 9: re-homed from /hr/grievances/new (D3) — filing is the employee's own act, so
 * the form lives where employees live.
 */
export default function NewGrievancePage() {
  const router = useRouter();
  const [subject, setSubject] = useState('');
  const [statement, setStatement] = useState('');

  const fileMutation = useMutation({
    mutationFn: () => grievanceService.file({ subject: subject.trim(), statement: statement.trim() }),
    onSuccess: (created) => {
      toast.success(
        `Grievance ${created.grievanceNumber} raised — it has gone to the first level of the escalation route.`,
      );
      router.push(`/me/grievances/${created.id}`);
    },
    onError: (e: Error) => toast.error(e.message || 'Could not raise it'),
  });

  const canSubmit = subject.trim().length > 0 && statement.trim().length >= MIN_STATEMENT;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Raise a grievance"
        description="A grievance is your own — nobody can raise, escalate or withdraw one on your behalf."
        backHref="/me/grievances"
      />

      <Card>
        <CardHeader><CardTitle>What the grievance is about</CardTitle></CardHeader>
        <CardContent className="space-y-5">
          <div className="space-y-2">
            <Label htmlFor="subject">Subject *</Label>
            <Input
              id="subject"
              maxLength={300}
              placeholder="A short description — for example, allocation of overtime"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="statement">Your statement *</Label>
            <Textarea
              id="statement"
              rows={9}
              maxLength={6000}
              placeholder="Set out what has happened, when, who was involved, and what you have already tried."
              value={statement}
              onChange={(e) => setStatement(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              This is retained word for word at every level of the escalation route, so write it as
              you want it read by whoever ends up considering it.
              {statement.trim().length < MIN_STATEMENT && ' At least 20 characters.'}
            </p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>What happens next</CardTitle></CardHeader>
        <CardContent className="space-y-3 text-sm text-muted-foreground">
          <p>
            Your grievance goes to the first level below. Whoever answers it will record their
            response, and you will see it on the grievance.
          </p>
          <ol className="ml-4 list-decimal space-y-1">
            {GRIEVANCE_LADDER.map((l) => (
              <li key={l.value}>{l.label}</li>
            ))}
          </ol>
          <p>
            If an answer does not settle the matter, <strong>you</strong> are the one who moves it up
            to the next level. Nobody else can do that for you, and nobody can close it on your behalf
            — though you may withdraw it at any point.
          </p>
        </CardContent>
      </Card>

      <div className="flex justify-end gap-3">
        <Button type="button" variant="outline" onClick={() => router.back()}>Cancel</Button>
        <Button onClick={() => fileMutation.mutate()} disabled={!canSubmit || fileMutation.isPending}>
          {fileMutation.isPending
            ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            : <Send className="mr-2 h-4 w-4" />}
          Raise grievance
        </Button>
      </div>
    </div>
  );
}
