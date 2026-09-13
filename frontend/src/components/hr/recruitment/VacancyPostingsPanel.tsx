'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, Link2, Loader2, Megaphone, Paperclip, Plus, Send, TimerOff, Trash2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { AttachmentsPanel } from '@/components/hr/common/AttachmentsPanel';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobPostingService } from '@/services/hr/recruitment.service';
import {
  JOB_POSTING_CHANNELS,
  type JobPostingChannel,
  type JobPostingForm,
  type JobVacancyStatus,
} from '@/types/hr/recruitment';

const blank = (): JobPostingForm => ({
  channel: 'JobBoard',
  title: '',
  description: '',
  postingUrl: '',
  externalPostingId: '',
  publishDate: '',
  expiryDate: '',
});

/**
 * The adverts for one vacancy, one row per channel.
 *
 * ⚠ **Publishing the vacancy already creates the internal and/or external postings** from its
 * audience flags, and taking the vacancy out of Published expires them again. The rows added here
 * are the extra channels — an agency, a newspaper, LinkedIn — and each still refuses to publish
 * unless the vacancy itself is Published, which is what the approval gate in front of publication
 * is for.
 */
export function VacancyPostingsPanel({
  vacancyId,
  vacancyStatus,
  canManage,
}: {
  vacancyId: string;
  vacancyStatus: JobVacancyStatus;
  canManage: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<JobPostingForm>(blank);
  const [attachmentsFor, setAttachmentsFor] = useState<string | null>(null);

  const postings = useQuery({
    queryKey: ['hr', 'vacancy-postings', vacancyId],
    queryFn: () => jobPostingService.getByVacancy(vacancyId),
    enabled: !!vacancyId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'vacancy-postings', vacancyId] });

  const fail = (e: any) =>
    // The server's 422 explains the rule in its own words — the vacancy's state, usually.
    toast({ title: 'Refused', description: e?.message || 'Action failed.', variant: 'destructive' });

  const create = useMutation({
    mutationFn: () =>
      jobPostingService.create(vacancyId, {
        ...form,
        title: form.title.trim(),
        description: form.description.trim(),
        postingUrl: form.postingUrl?.trim() || null,
        externalPostingId: form.externalPostingId?.trim() || null,
        publishDate: form.publishDate || null,
        expiryDate: form.expiryDate || null,
      }),
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setForm(blank());
      toast({ title: 'Advert added', description: 'Saved as a draft — publish it when you are ready.' });
    },
    onError: fail,
  });

  const publish = useMutation({
    mutationFn: (id: string) => jobPostingService.publish(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Advert published' });
    },
    onError: fail,
  });

  const expire = useMutation({
    mutationFn: (id: string) => jobPostingService.expire(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Advert expired' });
    },
    onError: fail,
  });

  const remove = useMutation({
    mutationFn: (id: string) => jobPostingService.remove(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Advert removed' });
    },
    onError: fail,
  });

  const rows = postings.data ?? [];

  /** `/careers/{vacancyId}?posting={postingId}` on this host — the advert's own apply link. */
  const careersLink = (postingId: string) =>
    `${typeof window !== 'undefined' ? window.location.origin : ''}/careers/${vacancyId}?posting=${postingId}`;
  const copyCareersLink = async (postingId: string) => {
    try {
      await navigator.clipboard.writeText(careersLink(postingId));
      toast({ title: 'Link copied', description: 'Paste it on the advert; applications made from it name this advert.' });
    } catch {
      toast({ title: careersLink(postingId) });
    }
  };
  const vacancyIsPublished = vacancyStatus === 'Published';

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Adverts</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            Publishing the vacancy raises the internal and external adverts automatically. Add
            others here.
          </p>
        </div>
        {canManage && (
          <Button size="sm" onClick={() => setOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> Add a channel
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {!vacancyIsPublished && rows.length > 0 && (
          <Alert>
            <AlertDescription className="text-sm">
              This vacancy is {humanizeEnum(vacancyStatus)}, so no advert for it can be published.
              Publish the vacancy first.
            </AlertDescription>
          </Alert>
        )}

        {postings.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            icon={Megaphone}
            title="No adverts yet"
            description="Publishing the vacancy will create the internal and external ones."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Channel</TableHead>
                <TableHead>Title</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Published</TableHead>
                <TableHead>Expires</TableHead>
                <TableHead>Careers link</TableHead>
                <TableHead className="text-right">Applications</TableHead>
                <TableHead className="w-40" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((p) => (
                <TableRow key={p.id}>
                  <TableCell>{humanizeEnum(p.channel)}</TableCell>
                  <TableCell>
                    {p.postingUrl ? (
                      <a
                        href={p.postingUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1 text-primary hover:underline"
                      >
                        {p.title} <ExternalLink className="h-3 w-3" />
                      </a>
                    ) : (
                      p.title
                    )}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={p.status} />
                  </TableCell>
                  <TableCell>{formatDate(p.actualPublishDate ?? p.publishDate)}</TableCell>
                  <TableCell>{formatDate(p.expiryDate)}</TableCell>
                  <TableCell>
                    {/* Round 3, lane A: the link to put on THIS advert. An application made from
                        it carries the posting and takes its source from the channel. */}
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-7 px-2 text-xs"
                      title={careersLink(p.id)}
                      onClick={() => copyCareersLink(p.id)}
                    >
                      <Link2 className="mr-1 h-3.5 w-3.5" /> Copy link
                    </Button>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{p.applicationCount}</TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        title="Attachments"
                        onClick={() => setAttachmentsFor(p.id)}
                      >
                        <Paperclip className="h-4 w-4" />
                      </Button>
                      {canManage && (
                        <>
                        {p.status === 'Draft' && (
                          <Button
                            variant="ghost"
                            size="icon"
                            title={
                              vacancyIsPublished
                                ? 'Publish this advert'
                                : 'The vacancy must be published first'
                            }
                            disabled={publish.isPending}
                            onClick={() => publish.mutate(p.id)}
                          >
                            <Send className="h-4 w-4" />
                          </Button>
                        )}
                        {p.status === 'Published' && (
                          <Button
                            variant="ghost"
                            size="icon"
                            title="Expire this advert"
                            disabled={expire.isPending}
                            onClick={() => expire.mutate(p.id)}
                          >
                            <TimerOff className="h-4 w-4" />
                          </Button>
                        )}
                        {p.status !== 'Published' && (
                          <Button
                            variant="ghost"
                            size="icon"
                            title="Remove"
                            disabled={remove.isPending}
                            onClick={() => remove.mutate(p.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={attachmentsFor !== null} onOpenChange={(o) => !o && setAttachmentsFor(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Advert attachments</DialogTitle>
            <DialogDescription>Creative assets, agency terms, or a signed insertion order.</DialogDescription>
          </DialogHeader>
          {attachmentsFor && (
            <AttachmentsPanel
              title="Attachments"
              queryKey={['hr', 'posting-attachments', attachmentsFor]}
              list={() => jobPostingService.getAttachments(attachmentsFor) as any}
              upload={(file, description) =>
                jobPostingService.uploadAttachment(attachmentsFor, file, description) as any
              }
              download={async (attachment: any) => {
                const blob = await jobPostingService.downloadAttachment(attachmentsFor, attachment.id);
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = attachment.fileName;
                a.click();
                URL.revokeObjectURL(url);
              }}
              remove={canManage ? (attachmentId) => jobPostingService.deleteAttachment(attachmentId) : undefined}
              readOnly={!canManage}
              note="Files are scanned and stored in the document repository; they are never public links."
            />
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add an advert channel</DialogTitle>
            <DialogDescription>
              Saved as a draft. It can be published once the vacancy itself is published.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Channel</Label>
              <Select
                value={form.channel}
                onValueChange={(v) => setForm({ ...form, channel: v as JobPostingChannel })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {JOB_POSTING_CHANNELS.map((c) => (
                    <SelectItem key={c} value={c}>
                      {humanizeEnum(c)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="postingTitle">Advert title</Label>
              <Input
                id="postingTitle"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="postingDescription">Advert body</Label>
              <Textarea
                id="postingDescription"
                rows={4}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="postingUrl">Link</Label>
                <Input
                  id="postingUrl"
                  value={form.postingUrl ?? ''}
                  onChange={(e) => setForm({ ...form, postingUrl: e.target.value })}
                  placeholder="https://…"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="externalPostingId">External reference</Label>
                <Input
                  id="externalPostingId"
                  value={form.externalPostingId ?? ''}
                  onChange={(e) => setForm({ ...form, externalPostingId: e.target.value })}
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="postingPublishDate">Planned publish date</Label>
                <Input
                  id="postingPublishDate"
                  type="date"
                  value={form.publishDate ?? ''}
                  onChange={(e) => setForm({ ...form, publishDate: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="postingExpiryDate">Expiry date</Label>
                <Input
                  id="postingExpiryDate"
                  type="date"
                  value={form.expiryDate ?? ''}
                  onChange={(e) => setForm({ ...form, expiryDate: e.target.value })}
                />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => create.mutate()}
              disabled={!form.title.trim() || !form.description.trim() || create.isPending}
            >
              {create.isPending ? 'Saving…' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
