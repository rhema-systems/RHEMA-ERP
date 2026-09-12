'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import {
  TextField,
  NumberField,
  DateField,
  TimeField,
  TextareaField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { consultantTimesheetService } from '@/services/hr/consultant.service';
import { formatDate, formatDateTime, formatTime, formatHours } from '@/lib/hr/attendance-format';
import type { ConsultantTimesheetEntry } from '@/types/hr/consultant';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

const entrySchema = z
  .object({
    workDate: z.string().min(1, 'Required'),
    startTime: z.string().min(1, 'Required'),
    endTime: z.string().min(1, 'Required'),
    breakMinutes: z.coerce.number().min(0).max(480),
    activitySummary: z.string().min(1, 'Describe the work done').max(2000),
    location: z.string().max(500).optional(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => v.endTime !== v.startTime, {
    message: 'End time cannot equal start time',
    path: ['endTime'],
  });

type EntryForm = z.input<typeof entrySchema>;

const emptyEntry: EntryForm = {
  workDate: '',
  startTime: '09:00:00',
  endTime: '17:00:00',
  breakMinutes: 60,
  activitySummary: '',
  location: '',
  notes: '',
};

/**
 * One consultant timesheet: its day entries, internal approval, and the client-confirmation
 * round-trip.
 *
 * Three gates in sequence, each owned by someone different:
 *  1. **Submit → approve/reject** — the generic workflow engine, internal.
 *  2. **Send for confirmation** — a tokenised link emailed to a client contact, who is
 *     external and has no ERP account. Not a workflow step, which is why it has its own
 *     button rather than appearing in the approval actions.
 *  3. **Invoice** — only once the client has confirmed.
 */
export default function ConsultantTimesheetDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [sending, setSending] = useState(false);
  const [busy, setBusy] = useState(false);
  const [contactEmail, setContactEmail] = useState('');
  const [contactName, setContactName] = useState('');

  const { data: t, isLoading, isError } = useQuery({
    queryKey: ['hr', 'consultant-timesheets', id],
    queryFn: () => consultantTimesheetService.getById(id),
    enabled: !!id,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'consultant-timesheets'] });
  };

  const workflow = useWorkflowRecord({
    entityType: 'ConsultantTimesheet',
    entityId: id,
    entityLabel: 'Consultant Timesheet',
    entityNumber: t?.timesheetNumber,
    status: t?.status ?? 'Draft',
    // Unlike the attendance requests, a timesheet does have a Draft state, so submitting is
    // a real user action. Rejected sheets can be corrected and resubmitted.
    canSubmit: t?.status === 'Draft' || t?.status === 'Rejected',
    canApproveReject: t?.status === 'Submitted',
    enabled: !!t,
    commands: {
      submit: () => consultantTimesheetService.submit(id),
      approve: (ctx) => consultantTimesheetService.approve(id, ctx.comments || null),
      reject: (ctx) => consultantTimesheetService.reject(id, ctx.comments || 'Rejected'),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const openSend = () => {
    setContactEmail('');
    setContactName('');
    setSending(true);
  };

  const sendForConfirmation = async () => {
    if (!contactEmail.trim()) {
      toast({ title: 'An email address is required', variant: 'destructive' });
      return false;
    }
    setBusy(true);
    try {
      const alreadySent = (t?.confirmations.length ?? 0) > 0;
      const payload = {
        clientContactEmail: contactEmail.trim(),
        clientContactName: contactName.trim() || null,
        tokenExpiryDate: null,
      };
      const result = alreadySent
        ? await consultantTimesheetService.resendConfirmation(id, payload)
        : await consultantTimesheetService.sendConfirmation(id, payload);
      await refresh();
      await workflow.refresh();
      toast({
        title: result.emailSent ? 'Sent' : 'Link created',
        description: result.emailSent
          ? `A confirmation link was emailed to ${contactEmail.trim()}.`
          : 'The confirmation link was created but the email could not be sent — check mail settings.',
        variant: result.emailSent ? undefined : 'destructive',
      });
      setSending(false);
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to send the confirmation.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !t) {
    return (
      <div className="p-6">
        <EmptyState title="Timesheet not found" description="It may have been removed." />
      </div>
    );
  }

  const isDraft = t.status === 'Draft';
  const canSendToClient = t.status === 'Approved' || t.status === 'SentToClient';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={t.timesheetNumber}
        description={`${t.consultantName} · ${t.clientName} · ${formatDate(t.periodStartDate)} – ${formatDate(t.periodEndDate)}`}
        backHref="/hr/consulting/timesheets"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={t.status} />
            <WorkflowApprovalActions {...workflow.actionProps} />
            {canSendToClient && (
              <Button variant="outline" onClick={openSend}>
                <Send className="mr-2 h-4 w-4" />
                {t.confirmations.length > 0 ? 'Resend to client' : 'Send to client'}
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="entries">
        <TabsList>
          <TabsTrigger value="entries">Entries ({t.entries.length})</TabsTrigger>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="confirmations">
            Client confirmation ({t.confirmations.length})
          </TabsTrigger>
          <WorkflowTabTrigger value="workflow" {...workflow.tabProps} />
        </TabsList>

        <TabsContent value="entries" className="pt-4">
          <ResourceCollectionTab<ConsultantTimesheetEntry, EntryForm>
            parentId={id}
            title="entries"
            singular="entry"
            queryKey={['hr', 'consultant-timesheets', id, 'entries']}
            invalidateKeys={[['hr', 'consultant-timesheets', id]]}
            dialogHint="Total hours are calculated from the times minus the break."
            emptyDescription="Log the days worked in this billing period."
            // Once submitted the sheet is under review, so entries are frozen.
            readOnly={!isDraft}
            list={(timesheetId) => consultantTimesheetService.getEntries(timesheetId)}
            create={(timesheetId, values) =>
              consultantTimesheetService.addEntry(timesheetId, {
                ...entrySchema.parse(values),
                timesheetId,
                location: values.location || null,
                notes: values.notes || null,
              })
            }
            update={(_timesheetId, entryId, values) =>
              consultantTimesheetService.updateEntry(entryId, {
                ...entrySchema.parse(values),
                id: entryId,
                location: values.location || null,
                notes: values.notes || null,
              })
            }
            remove={(_timesheetId, entryId) => consultantTimesheetService.removeEntry(entryId)}
            getId={(e) => e.id}
            columns={[
              { header: 'Date', cell: (e) => <span className="font-medium">{formatDate(e.workDate)}</span> },
              { header: 'From', cell: (e) => formatTime(e.startTime) },
              { header: 'To', cell: (e) => formatTime(e.endTime) },
              { header: 'Break', cell: (e) => `${e.breakMinutes}m`, className: 'text-right' },
              { header: 'Hours', cell: (e) => formatHours(e.totalHours), className: 'text-right' },
              { header: 'Activity', cell: (e) => e.activitySummary },
              { header: 'Location', cell: (e) => e.location || '—' },
            ]}
            schema={entrySchema as any}
            emptyForm={{ ...emptyEntry, workDate: t.periodStartDate }}
            toForm={(e) => ({
              workDate: e.workDate,
              startTime: e.startTime,
              endTime: e.endTime,
              breakMinutes: e.breakMinutes,
              activitySummary: e.activitySummary,
              location: e.location ?? '',
              notes: e.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <DateField form={form} name="workDate" label="Work date" required />
                <FieldRow>
                  <TimeField form={form} name="startTime" label="Start time" required />
                  <TimeField form={form} name="endTime" label="End time" required />
                </FieldRow>
                <NumberField form={form} name="breakMinutes" label="Break (minutes)" />
                <TextareaField
                  form={form}
                  name="activitySummary"
                  label="Activity summary"
                  rows={3}
                />
                <TextField form={form} name="location" label="Location" />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="overview" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Timesheet</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow
                label="Consultant"
                value={`${t.consultantName} (${t.consultantNumber})`}
              />
              <InfoRow label="Client" value={`${t.clientName} (${t.clientCode})`} />
              <InfoRow
                label="Engagement"
                value={t.engagementCode ? `${t.engagementCode} · ${t.engagementTitle}` : '—'}
              />
              <InfoRow label="Period start" value={formatDate(t.periodStartDate)} />
              <InfoRow label="Period end" value={formatDate(t.periodEndDate)} />
              <InfoRow label="Total hours" value={formatHours(t.totalHours)} />
              <InfoRow label="Submitted" value={formatDateTime(t.submittedDate)} />
            </CardContent>
          </Card>

          {t.notes && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Notes</CardTitle>
              </CardHeader>
              <CardContent className="text-sm">
                <p className="whitespace-pre-wrap">{t.notes}</p>
              </CardContent>
            </Card>
          )}

          {t.engagementId && (
            <Button
              variant="outline"
              onClick={() => router.push(`/hr/consulting/engagements/${t.engagementId}`)}
            >
              Open the engagement
            </Button>
          )}
        </TabsContent>

        <TabsContent value="confirmations" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {t.confirmations.length === 0 ? (
                <EmptyState
                  title="Not sent to the client yet"
                  description="Once approved internally, send the timesheet to a client contact to confirm."
                  action={
                    canSendToClient ? (
                      <Button size="sm" onClick={openSend}>
                        <Send className="mr-2 h-4 w-4" /> Send to client
                      </Button>
                    ) : undefined
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Contact</TableHead>
                      <TableHead>Sent</TableHead>
                      <TableHead>Expires</TableHead>
                      <TableHead>Viewed</TableHead>
                      <TableHead>Responded</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Client notes</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {t.confirmations.map((c) => (
                      <TableRow key={c.id}>
                        <TableCell>
                          <div className="font-medium">{c.clientContactEmail}</div>
                          {c.clientContactName && (
                            <div className="text-xs text-muted-foreground">
                              {c.clientContactName}
                            </div>
                          )}
                        </TableCell>
                        <TableCell>{formatDateTime(c.sentDate)}</TableCell>
                        <TableCell>{formatDateTime(c.tokenExpiryDate)}</TableCell>
                        <TableCell>{formatDateTime(c.viewedDate)}</TableCell>
                        <TableCell>
                          {formatDateTime(c.confirmedDate ?? c.rejectedDate)}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={c.status} />
                        </TableCell>
                        <TableCell className="max-w-[240px] text-muted-foreground">
                          {c.clientNotes || '—'}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <WorkflowTabContent
          {...workflow.tabProps}
          value="workflow"
          entityType="ConsultantTimesheet"
          entityId={id}
          entityLabel="Consultant Timesheet"
          entityNumber={t.timesheetNumber}
          status={t.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />
      </Tabs>

      <ConfirmationDialog
        open={sending}
        onOpenChange={setSending}
        title={t.confirmations.length > 0 ? 'Resend to the client' : 'Send to the client'}
        description="Emails a one-time link the contact can use to confirm or reject these hours. Resending invalidates any earlier link."
        confirmText="Send"
        isLoading={busy}
        onConfirm={sendForConfirmation}
      >
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="contactEmail">Client contact email</Label>
            <Input
              id="contactEmail"
              type="email"
              value={contactEmail}
              onChange={(e) => setContactEmail(e.target.value)}
              placeholder="name@client.com"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="contactName">Contact name</Label>
            <Input
              id="contactName"
              value={contactName}
              onChange={(e) => setContactName(e.target.value)}
              placeholder="Optional"
            />
          </div>
        </div>
      </ConfirmationDialog>
    </div>
  );
}
