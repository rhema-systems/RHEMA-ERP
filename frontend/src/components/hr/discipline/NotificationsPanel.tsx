'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BellRing, Loader2, Send } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { disciplineNotificationService } from '@/services/hr/discipline.service';
import {
  NOTIFICATION_TYPE_OPTIONS,
  type DisciplinaryNotificationType,
  type DisciplineNotification,
} from '@/types/hr/discipline';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const labelFor = (v?: DisciplinaryNotificationType | null) =>
  NOTIFICATION_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? (v ?? '—');

/**
 * Formal notices issued to the subject of the case.
 *
 * ⚠ **There is no acknowledge button here, and that is deliberate.** `AcknowledgeAsync` refuses
 * anyone but the employee the notice was issued to — "Only the employee a notification was issued
 * to can acknowledge it" — so an acknowledge control on an HR screen would be shown exclusively to
 * the people guaranteed to be refused by it. The acknowledged state is displayed; setting it
 * belongs to the employee's own surface.
 *
 * ⚠ **A notice cannot be edited or deleted.** There is no update route and no delete route: once
 * issued, a notice is a fact about what the employee was told. Chasing an unacknowledged one is a
 * follow-up — a second act recorded against the first — not a correction.
 *
 * ⚠ **`sentById` is server-stamped** and absent from the create payload. Unusually for this area
 * that was already true before the case file was built out; the note author and the legal-review
 * referrer both had to be fixed (D-05, D-08).
 */
export function NotificationsPanel({
  caseId,
  canWrite,
  onChanged,
}: {
  caseId: string;
  canWrite: boolean;
  onChanged?: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const queryKey = ['hr', 'discipline', caseId, 'notifications'];
  const { data: notices, isLoading } = useQuery({
    queryKey,
    queryFn: () => disciplineNotificationService.getForCase(caseId),
    enabled: !!caseId,
  });

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    notificationType: 'ShowCause' as DisciplinaryNotificationType,
    sentDate: new Date().toISOString().slice(0, 10),
    content: '',
  });
  const [followup, setFollowup] = useState<DisciplineNotification | null>(null);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    onChanged?.();
  };

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      description: e?.response?.data?.detail ?? e?.response?.data?.message ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const send = useMutation({
    mutationFn: () =>
      disciplineNotificationService.send(caseId, {
        notificationType: form.notificationType,
        sentDate: form.sentDate,
        content: form.content.trim(),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Notice recorded' });
      setOpen(false);
      setForm({ notificationType: 'ShowCause', sentDate: new Date().toISOString().slice(0, 10), content: '' });
    },
    onError: fail('Could not record the notice'),
  });

  const sendFollowup = useMutation({
    mutationFn: (id: string) => disciplineNotificationService.sendFollowup(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Follow-up recorded' });
      setFollowup(null);
    },
    onError: fail('Could not record the follow-up'),
  });

  const rows = notices ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>Notices issued</CardTitle>
        {canWrite && (
          <Button size="sm" onClick={() => setOpen(true)}>
            <Send className="mr-2 h-4 w-4" />
            Issue a notice
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {isLoading ? (
          <div className="flex justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 pb-6">
            <EmptyState
              icon={BellRing}
              title="No notices"
              description="Nothing has been formally put to the employee on this case."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Notice</TableHead>
                <TableHead>Sent</TableHead>
                <TableHead>By</TableHead>
                <TableHead>Acknowledged</TableHead>
                {canWrite && <TableHead className="text-right">Actions</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((n) => (
                <TableRow key={n.id}>
                  <TableCell>
                    <div>{n.notificationTypeName ?? labelFor(n.notificationType)}</div>
                    <div className="max-w-md whitespace-pre-wrap text-xs text-muted-foreground">
                      {n.content}
                    </div>
                  </TableCell>
                  <TableCell>{fmtDate(n.sentDate)}</TableCell>
                  <TableCell>{n.sentByName || '—'}</TableCell>
                  <TableCell>
                    {/* Displayed, never settable from here — see the note on this component. */}
                    {n.isAcknowledged ? (
                      <div className="flex flex-col gap-1">
                        <Badge variant="outline">Acknowledged</Badge>
                        <span className="text-xs text-muted-foreground">{fmtDate(n.acknowledgedDate)}</span>
                      </div>
                    ) : (
                      <div className="flex flex-col gap-1">
                        <Badge variant="secondary">Awaiting the employee</Badge>
                        {n.isFollowupSent && (
                          <span className="text-xs text-muted-foreground">
                            Chased {fmtDate(n.followupDate)}
                          </span>
                        )}
                      </div>
                    )}
                  </TableCell>
                  {canWrite && (
                    <TableCell className="text-right">
                      {!n.isAcknowledged && (
                        <Button variant="ghost" size="sm" onClick={() => setFollowup(n)}>
                          <BellRing className="mr-1 h-4 w-4" />
                          {n.isFollowupSent ? 'Chase again' : 'Chase'}
                        </Button>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-[620px]">
          <DialogHeader>
            <DialogTitle>Issue a notice</DialogTitle>
            <DialogDescription>
              Records what was formally put to the employee. You are recorded as its sender, and a
              notice cannot be edited or withdrawn afterwards.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="notice-type">Type</Label>
                <Select
                  value={form.notificationType}
                  onValueChange={(v) => setForm({ ...form, notificationType: v as DisciplinaryNotificationType })}
                >
                  <SelectTrigger id="notice-type"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {NOTIFICATION_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="notice-date">Sent on</Label>
                <Input
                  id="notice-date"
                  type="date"
                  value={form.sentDate}
                  onChange={(e) => setForm({ ...form, sentDate: e.target.value })}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="notice-content">
                What the notice says<span className="ml-0.5 text-red-500">*</span>
              </Label>
              <Textarea
                id="notice-content"
                rows={7}
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
                placeholder="The wording put to the employee. They will be able to read this when acknowledging it."
              />
              <p className="text-xs text-muted-foreground">
                The employee sees these words on their own copy of the case — a notice they cannot
                read is not one they can meaningfully acknowledge.
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={send.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => send.mutate()}
              disabled={send.isPending || !form.content.trim() || !form.sentDate}
            >
              {send.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Issue notice
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={followup !== null}
        onOpenChange={(o) => !o && setFollowup(null)}
        title="Record a follow-up?"
        description="This records that the employee was chased for an acknowledgement, dated today. It does not send anything by itself."
        confirmText="Record follow-up"
        isLoading={sendFollowup.isPending}
        onConfirm={async () => { if (followup) await sendFollowup.mutateAsync(followup.id); }}
      />
    </Card>
  );
}
