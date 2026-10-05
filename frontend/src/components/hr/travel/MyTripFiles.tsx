'use client';

import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Download, Paperclip, Trash2, Upload } from 'lucide-react';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest, TravelAttachmentType } from '@/types/hr/travel';
import { TRAVEL_ATTACHMENT_TYPE_LABELS, enumLabel, enumOptions } from './travel-enums';

const TYPES = enumOptions(TRAVEL_ATTACHMENT_TYPE_LABELS);
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtSize = (bytes: number) =>
  bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

/**
 * The files on the traveller's own trip (travel final closure, lane 7, slice 7c2 — E7, T-56).
 *
 * Every file the desk or the traveller attached, from the request's own read — nothing among the attachment types is the
 * desk's alone (P5). The traveller attaches through `/me`, which checks the trip is theirs before anything is stored and
 * sends the file through the controlled gate (scanned, registered in the DMS); not on a cancelled, rejected or closed
 * trip. They remove only a file they uploaded, and only while the trip is still theirs to change — a draft, or returned
 * to them (D-40); after that the desk may be relying on it.
 */
export function MyTripFiles({ request }: { request: StaffTravelRequest }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [attachmentType, setAttachmentType] = useState<TravelAttachmentType>('Other');
  const [description, setDescription] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const closed = ['Cancelled', 'Rejected', 'Closed'].includes(request.status);
  const stillMine = request.status === 'Draft' || request.status === 'ReturnedForRevision';
  const items = (request.attachments ?? []).slice().sort((a, b) => b.uploadedAt.localeCompare(a.uploadedAt));
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['my-travel-request', request.id] });

  const upload = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('No file selected');
      return travelService.uploadMyAttachment(request.id, file, attachmentType, description.trim() || undefined);
    },
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setFile(null);
      setDescription('');
      setAttachmentType('Other');
      if (fileInputRef.current) fileInputRef.current.value = '';
      toast({ title: 'File attached', description: 'The travel desk sees it on your trip.' });
    },
    // The gate answers { code, message } — the message says why it was refused.
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Upload refused', description: e.message || 'The file could not be uploaded.' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => travelService.deleteMyAttachment(id),
    onSuccess: async () => { await refresh(); toast({ title: 'File removed' }); },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not remove the file', description: e.message }),
  });

  const download = async (id: string, name: string) => {
    try {
      await travelService.downloadMyAttachment(id, name);
    } catch (e) {
      toast({ variant: 'destructive', title: 'Could not download the file', description: (e as Error).message });
    }
  };

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div className="space-y-1.5">
            <CardTitle className="text-base">Files</CardTitle>
            <p className="text-sm text-muted-foreground">
              Invitation letters, visa documents, certificates — what you and the travel desk attached to this trip.
              Files are scanned before they are stored.
            </p>
          </div>
          {!closed && (
            <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
              <Upload className="mr-2 h-4 w-4" /> Attach
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {items.length === 0 ? (
            <EmptyState icon={Paperclip} title="No files" description="Nothing has been attached to this trip." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead className="w-40">Type</TableHead>
                  <TableHead className="w-20">Size</TableHead>
                  <TableHead className="w-44">Added by</TableHead>
                  <TableHead className="w-44">When</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((a) => {
                  const mine = !!user?.employeeId && a.uploadedById === user.employeeId;
                  return (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.fileName}</TableCell>
                      <TableCell>{enumLabel(TRAVEL_ATTACHMENT_TYPE_LABELS, a.attachmentType)}</TableCell>
                      <TableCell>{fmtSize(a.fileSizeBytes)}</TableCell>
                      <TableCell>{mine ? 'You' : a.uploadedByName || 'Travel desk'}</TableCell>
                      <TableCell className="whitespace-nowrap">{fmtDateTime(a.uploadedAt)}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <Button variant="ghost" size="icon" aria-label={`Download ${a.fileName}`}
                            onClick={() => download(a.id, a.fileName)}>
                            <Download className="h-4 w-4" />
                          </Button>
                          {/* D-40: yours, while the trip is a draft or returned to you — the server holds the rule. */}
                          {mine && stillMine && (
                            <Button variant="ghost" size="icon" aria-label={`Remove ${a.fileName}`}
                              disabled={remove.isPending} onClick={() => remove.mutate(a.id)}>
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
      {!stillMine && items.some((a) => a.uploadedById === user?.employeeId) && (
        <p className="mt-2 text-xs text-muted-foreground">
          This trip has been sent for approval, so a file you added stays — ask the travel desk if one should go.
        </p>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach a file to your trip</DialogTitle>
            <DialogDescription>
              The file is scanned and stored centrally. A refusal comes back with a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="my-travel-attachment-file">File</Label>
              <Input id="my-travel-attachment-file" type="file" ref={fileInputRef}
                onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="my-travel-attachment-type">Type</Label>
              <Select value={attachmentType}
                onValueChange={(v) => v && setAttachmentType(v as TravelAttachmentType)}>
                <SelectTrigger id="my-travel-attachment-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {TYPES.map((t) => <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="my-travel-attachment-description">Description</Label>
              <Input id="my-travel-attachment-description" value={description}
                onChange={(e) => setDescription(e.target.value)} placeholder="Optional — what it is, for the travel desk" />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => upload.mutate()} disabled={!file || upload.isPending}>
              {upload.isPending ? 'Uploading…' : 'Upload'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
