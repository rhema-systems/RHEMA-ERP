'use client';

import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import { travelService } from '@/services/hr/travel.service';
import type { TravelAttachmentType } from '@/types/hr/travel';

const TYPES: { value: TravelAttachmentType; label: string }[] = [
  { value: 'Invitation', label: 'Invitation' },
  { value: 'Agenda', label: 'Agenda' },
  { value: 'Quotation', label: 'Quotation' },
  { value: 'Approval', label: 'Approval' },
  { value: 'VisaSupport', label: 'Visa support letter' },
  { value: 'Other', label: 'Other' },
];

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const fmtSize = (bytes: number) =>
  bytes >= 1_048_576
    ? `${(bytes / 1_048_576).toFixed(1)} MB`
    : `${Math.max(1, Math.round(bytes / 1024))} KB`;

/**
 * Documents attached to a travel request.
 *
 * Purpose-built rather than reusing `AttachmentsPanel` for one reason: **on travel the attachment
 * type is the point.** A visa support letter and a quotation are different documents with different
 * consequences, so the type is chosen on upload and shown in the list; the shared panel has a
 * free-text description and no type.
 *
 * Everything goes through the controlled gate — the file is scanned, registered in the DMS and
 * stored outside the web root. `fileUrl` comes back empty by design, so download is a token-bearing
 * fetch, never an `href`.
 */
export function TravelAttachmentsPanel({
  requestId,
  canUpload = true,
  canDelete = false,
}: {
  requestId: string;
  /** `HR.Travel.Write`. */
  canUpload?: boolean;
  /** `HR.Travel.Admin` — deleting a scanned passport page is not a desk-clerk action. */
  canDelete?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const queryKey = ['travel-request-attachments', requestId];
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [attachmentType, setAttachmentType] = useState<TravelAttachmentType>('Other');
  const [description, setDescription] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => travelService.getAttachments(requestId),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey });

  const upload = useMutation({
    mutationFn: () => {
      if (!file) throw new Error('No file selected');
      return travelService.uploadAttachment(
        requestId, file, attachmentType, description.trim() || undefined);
    },
    onSuccess: () => {
      refresh();
      setOpen(false);
      setFile(null);
      setDescription('');
      setAttachmentType('Other');
      if (fileInputRef.current) fileInputRef.current.value = '';
      toast({ title: 'File attached' });
    },
    // The gate answers { code, message } — the message says why it was refused.
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'Upload refused',
        description: e.message || 'The file could not be uploaded.',
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => travelService.deleteAttachment(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Attachment removed' });
    },
    onError: (e: Error) =>
      toast({
        variant: 'destructive',
        title: 'Could not remove the attachment',
        description: e.message,
      }),
  });

  const items = data ?? [];

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4">
          <div className="space-y-1.5">
            <CardTitle className="text-base">Attachments</CardTitle>
            <p className="text-sm text-muted-foreground">
              Invitations, quotations and visa support letters. Files are scanned before they are
              stored.
            </p>
          </div>
          {canUpload && (
            <Button variant="outline" size="sm" onClick={() => setOpen(true)}>
              <Upload className="mr-2 h-4 w-4" />
              Attach
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="space-y-2 p-4">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={Paperclip}
              title="No files"
              description="Nothing has been attached to this request."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead className="w-44">Type</TableHead>
                  <TableHead className="w-24">Size</TableHead>
                  <TableHead className="w-44">Uploaded by</TableHead>
                  <TableHead className="w-44">When</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.fileName}</TableCell>
                    <TableCell>{a.attachmentTypeName}</TableCell>
                    <TableCell>{fmtSize(a.fileSizeBytes)}</TableCell>
                    <TableCell>{a.uploadedByName || '—'}</TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDateTime(a.uploadedAt)}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Download ${a.fileName}`}
                          onClick={() =>
                            hrDocumentService.download(
                              travelService.downloadAttachmentUrl(a.id), a.fileName)
                          }
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                        {canDelete && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove ${a.fileName}`}
                            onClick={() => remove.mutate(a.id)}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach a file</DialogTitle>
            <DialogDescription>
              The file is scanned and stored centrally. A refusal comes back with a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="travel-attachment-file">File</Label>
              <Input
                id="travel-attachment-file"
                type="file"
                ref={fileInputRef}
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="travel-attachment-type">Type</Label>
              <Select
                value={attachmentType}
                onValueChange={(v) => setAttachmentType(v as TravelAttachmentType)}
              >
                <SelectTrigger id="travel-attachment-type">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TYPES.map((t) => (
                    <SelectItem key={t.value} value={t.value}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="travel-attachment-description">Description</Label>
              <Input
                id="travel-attachment-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Optional — kept with the stored document"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => upload.mutate()} disabled={!file || upload.isPending}>
              {upload.isPending ? 'Uploading…' : 'Upload'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
