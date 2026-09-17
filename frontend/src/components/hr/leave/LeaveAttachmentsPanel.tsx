'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Paperclip, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { leaveService } from '@/services/hr/leave.service';
import {
  LEAVE_EVIDENCE_KIND_LABEL,
  type LeaveEvidenceKind,
} from '@/types/hr/leave-request';

const sizeLabel = (bytes?: number | null) => {
  if (!bytes) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
};

/**
 * Leave request attachments. Uploads go through the controlled gate (virus scan + central
 * DMS registration) and downloads stream from the authorized endpoint — the stored
 * `filePath` is never used as a link.
 */
export function LeaveAttachmentsPanel({
  leaveRequestId,
  canUpload = true,
}: {
  leaveRequestId: string;
  canUpload?: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const queryKey = ['hr', 'leave-requests', leaveRequestId, 'attachments'];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => leaveService.getAttachments(leaveRequestId),
    enabled: !!leaveRequestId,
  });

  const removeMutation = useMutation({
    mutationFn: (attachmentId: string) => leaveService.removeAttachment(attachmentId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Removed', description: 'Attachment removed.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Failed to remove the attachment.',
        variant: 'destructive',
      }),
  });

  const download = async (attachmentId: string, fileName: string) => {
    try {
      await leaveService.downloadAttachment(attachmentId, fileName);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to download the attachment.',
        variant: 'destructive',
      });
    }
  };

  const rows = data ?? [];
  // Defaults to a plain supporting document — the same thing every attachment uploaded before
  // this control existed genuinely was.
  const [kind, setKind] = useState<LeaveEvidenceKind>('Other');

  return (
    <div className="space-y-4">
      {canUpload && (
        <Card>
          <CardContent className="space-y-4 pt-6">
            {/*
              ⚠ The kind is chosen BEFORE the file, and it has to be: the evidence gate asks whether a
              document of the right kind is attached, and a file name cannot answer that — `scan.pdf`
              is a medical certificate or a holiday photograph with equal probability. Without this
              control the gate would be a rule nobody could satisfy from the screen, which is the
              shape travel's T-23 finding records (a required acknowledgement unreachable by anyone).
            */}
            <div className="space-y-2">
              <Label htmlFor="evidence-kind">What is this document?</Label>
              <Select value={kind} onValueChange={(v) => setKind(v as LeaveEvidenceKind)}>
                <SelectTrigger id="evidence-kind" className="sm:max-w-sm">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(LEAVE_EVIDENCE_KIND_LABEL) as LeaveEvidenceKind[]).map((k) => (
                    <SelectItem key={k} value={k}>
                      {LEAVE_EVIDENCE_KIND_LABEL[k]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Leave types that require excuse duty will not accept a submission until a document
                of that kind is attached. Anything else is a supporting document.
              </p>
            </div>

            <DocumentUploadField
              label="Attach a document"
              endpoint={`/Leaves/${leaveRequestId}/attachments?evidenceKind=${kind}`}
              accept=".pdf,.png,.jpg,.jpeg,.doc,.docx"
              helpText="Scanned on upload; max 10 MB."
              onUploaded={() => queryClient.invalidateQueries({ queryKey })}
            />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Paperclip}
              title="No attachments"
              description="Supporting documents appear here."
            />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>File</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Size</TableHead>
                    <TableHead>Kind</TableHead>
                    <TableHead>Uploaded</TableHead>
                    <TableHead>By</TableHead>
                    <TableHead className="w-[110px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.fileName}</TableCell>
                      <TableCell className="text-muted-foreground">{a.contentType || '—'}</TableCell>
                      <TableCell>{sizeLabel(a.fileSizeBytes)}</TableCell>
                      <TableCell>
                        {LEAVE_EVIDENCE_KIND_LABEL[a.evidenceKind] ?? a.evidenceKind}
                      </TableCell>
                      <TableCell>{a.uploadedDate?.slice(0, 10)}</TableCell>
                      <TableCell className="text-muted-foreground">{a.uploadedByName}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8"
                            onClick={() => download(a.id, a.fileName)}
                          >
                            <Download className="h-4 w-4" />
                            <span className="sr-only">Download</span>
                          </Button>
                          {canUpload && (
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-red-600"
                              disabled={removeMutation.isPending}
                              onClick={() => removeMutation.mutate(a.id)}
                            >
                              <Trash2 className="h-4 w-4" />
                              <span className="sr-only">Remove</span>
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
