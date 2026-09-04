'use client';

import { useState } from 'react';
import { format } from 'date-fns';
import { FileText } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import {
  tenderService,
  type TenderRevisionDto,
} from '@/services/tenderService';

type RevisionType =
  | 'Amendment'
  | 'Addendum'
  | 'Corrigendum'
  | 'DeadlineExtension';

const emptyRevision = () => ({
  revisionType: 'Amendment' as RevisionType,
  description: '',
  changes: '',
  newSubmissionDeadline: '',
  requiresRebid: false,
  sendNotifications: true,
});

export function TenderRevisionsPanel({
  tenderId,
  tenderStatus,
  currentSubmissionDeadline,
  currentOpeningDate,
  revisions,
  onChanged,
}: {
  tenderId: string;
  tenderStatus: string;
  currentSubmissionDeadline?: string;
  currentOpeningDate?: string;
  revisions: TenderRevisionDto[];
  onChanged: () => Promise<void> | void;
}) {
  const { hasPermission } = useAuth();
  const canIssue =
    hasPermission('procurement.tender.administer') &&
    tenderStatus === 'Published';
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [draft, setDraft] = useState(emptyRevision);
  const scheduledOpening = currentOpeningDate
    ? new Date(currentOpeningDate)
    : null;
  const openingDateLimit =
    scheduledOpening && !Number.isNaN(scheduledOpening.getTime())
      ? scheduledOpening.toISOString().slice(0, 16)
      : undefined;

  const issue = async () => {
    if (draft.description.trim().length < 10) {
      toast.error('Enter a clear reason for the tender amendment');
      return false;
    }
    if (
      draft.revisionType === 'DeadlineExtension' &&
      !draft.newSubmissionDeadline
    ) {
      toast.error('Select the new submission deadline');
      return false;
    }
    if (draft.newSubmissionDeadline) {
      const proposed = new Date(draft.newSubmissionDeadline);
      const current = currentSubmissionDeadline
        ? new Date(currentSubmissionDeadline)
        : null;
      if (
        Number.isNaN(proposed.getTime()) ||
        (current && proposed <= current)
      ) {
        toast.error(
          'The revised deadline must be valid and later than the current deadline'
        );
        return false;
      }
      if (scheduledOpening && proposed > scheduledOpening) {
        toast.error(
          'The revised deadline cannot be later than the scheduled opening. Choose a deadline at or before the opening schedule.'
        );
        return false;
      }
    }

    try {
      setBusy(true);
      await tenderService.createTenderRevision(tenderId, {
        revisionType: draft.revisionType,
        description: draft.description.trim(),
        changes: draft.changes.trim() || undefined,
        newSubmissionDeadline: draft.newSubmissionDeadline
          ? new Date(draft.newSubmissionDeadline).toISOString()
          : undefined,
        requiresRebid: draft.requiresRebid,
        sendNotifications: draft.sendNotifications,
      });
      toast.success('Tender amendment issued successfully');
      setOpen(false);
      setDraft(emptyRevision());
      await onChanged();
    } catch (error) {
      toast.error(
        getProcurementProblemMessage(error, 'Failed to issue tender amendment')
      );
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div>
            <CardTitle>Tender Amendments and Addenda</CardTitle>
            <CardDescription>
              Immutable history of issued changes, deadline extensions,
              notifications, and rebid instructions.
            </CardDescription>
          </div>
          {canIssue && (
            <Button onClick={() => setOpen(true)}>
              <FileText className="mr-2 h-4 w-4" />
              Issue amendment
            </Button>
          )}
        </CardHeader>
        <CardContent>
          {revisions.length === 0 ? (
            <p className="py-8 text-center text-muted-foreground">
              No tender amendments or addenda have been issued.
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Revision</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>New deadline</TableHead>
                  <TableHead>Rebid</TableHead>
                  <TableHead>Notification</TableHead>
                  <TableHead>Issued</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {revisions.map((revision) => (
                  <TableRow key={revision.id}>
                    <TableCell className="font-mono font-medium">
                      {revision.revisionNumber}
                    </TableCell>
                    <TableCell>{revision.revisionType}</TableCell>
                    <TableCell className="max-w-md">
                      <p>{revision.description}</p>
                      {revision.changes && (
                        <p className="mt-1 text-xs text-muted-foreground">
                          {revision.changes}
                        </p>
                      )}
                    </TableCell>
                    <TableCell>
                      {revision.newSubmissionDeadline
                        ? format(
                            new Date(revision.newSubmissionDeadline),
                            'PPP p'
                          )
                        : '—'}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={revision.requiresRebid ? 'default' : 'outline'}
                      >
                        {revision.requiresRebid ? 'Required' : 'No'}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={
                          revision.notificationSent ? 'default' : 'secondary'
                        }
                      >
                        {revision.notificationSent ? 'Sent' : 'Not sent'}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {format(new Date(revision.revisionDate), 'PPP p')}
                      {revision.revisedByName && (
                        <p className="text-xs text-muted-foreground">
                          {revision.revisedByName}
                        </p>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={open}
        onOpenChange={setOpen}
        title="Issue tender amendment"
        description="Issue an immutable, auditable change to this published tender."
        confirmText="Issue amendment"
        isLoading={busy}
        confirmDisabled={draft.description.trim().length < 10}
        maxWidth="720px"
        onConfirm={issue}
      >
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Change type</Label>
              <Select
                value={draft.revisionType}
                onValueChange={(value) =>
                  setDraft((current) => ({
                    ...current,
                    revisionType: value as RevisionType,
                  }))
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Amendment">Amendment</SelectItem>
                  <SelectItem value="Addendum">Addendum</SelectItem>
                  <SelectItem value="Corrigendum">Corrigendum</SelectItem>
                  <SelectItem value="DeadlineExtension">
                    Deadline extension
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>New submission deadline</Label>
              <Input
                type="datetime-local"
                value={draft.newSubmissionDeadline}
                max={openingDateLimit}
                onChange={(event) =>
                  setDraft((current) => ({
                    ...current,
                    newSubmissionDeadline: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Reason and public summary *</Label>
            <Textarea
              rows={3}
              value={draft.description}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  description: event.target.value,
                }))
              }
            />
          </div>
          <div className="space-y-2">
            <Label>Exact changes</Label>
            <Textarea
              rows={3}
              value={draft.changes}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  changes: event.target.value,
                }))
              }
            />
          </div>
          <label className="flex items-start gap-3 rounded-md border p-3">
            <Checkbox
              checked={draft.requiresRebid}
              onCheckedChange={(checked) =>
                setDraft((current) => ({
                  ...current,
                  requiresRebid: checked === true,
                }))
              }
            />
            <span className="text-sm">
              Require suppliers to revise or resubmit affected bids.
            </span>
          </label>
          <label className="flex items-start gap-3 rounded-md border p-3">
            <Checkbox
              checked={draft.sendNotifications}
              onCheckedChange={(checked) =>
                setDraft((current) => ({
                  ...current,
                  sendNotifications: checked === true,
                }))
              }
            />
            <span className="text-sm">
              Notify invited and participating suppliers.
            </span>
          </label>
        </div>
      </ConfirmationDialog>
    </>
  );
}
