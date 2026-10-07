'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { useAuth } from '@/hooks/use-auth';
import { companyMilestoneService } from '@/services/hr/company-schedule.service';
import type { CompanyMilestone } from '@/types/hr/company-schedule';

const size = (bytes?: number | null) =>
  !bytes ? '—' : bytes < 1024 * 1024 ? `${Math.ceil(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;

/**
 * A milestone's files (company schedule lane 4a, D-3): the certificate, the licence, the opening photograph — uploaded
 * through the gate (scanned, stored, registered), downloaded, and removed on `HR.Company.Write` (the user's ruling, as
 * event files). Before this a milestone had a text box called "Related documents" and no files at all.
 */
export function MilestoneFilesDialog({ milestone, onClose }: { milestone: CompanyMilestone | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canWrite = hasPermission('HR.Company.Write');
  const [description, setDescription] = useState('');
  const [removing, setRemoving] = useState<string | null>(null);
  const key = ['hr', 'company-schedule', 'milestones', milestone?.id, 'documents'];

  const { data, isLoading } = useQuery({
    queryKey: key,
    queryFn: () => companyMilestoneService.getDocuments(milestone?.id ?? ''),
    enabled: !!milestone,
  });

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: key }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'milestones'] }),
    ]);

  const remove = async (id: string) => {
    setRemoving(id);
    try {
      await companyMilestoneService.removeDocument(id);
      await refresh();
      toast({ title: 'File removed' });
    } catch (error: any) {
      toast({
        title: 'Could not remove the file',
        description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setRemoving(null);
    }
  };

  const files = data ?? [];

  return (
    <Dialog open={!!milestone} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Files — {milestone?.title}</DialogTitle>
          <DialogDescription>
            What evidences the milestone: a certificate, a licence, a photograph. Each file is scanned before it is stored.
          </DialogDescription>
        </DialogHeader>

        {canWrite && milestone && (
          <div className="space-y-3 rounded-md border p-4">
            <div className="space-y-1">
              <Label htmlFor="milestoneFileDescription">About it (optional)</Label>
              <Input
                id="milestoneFileDescription"
                value={description}
                maxLength={1000}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>
            <DocumentUploadField
              label="File"
              endpoint={companyMilestoneService.documentUploadEndpoint(milestone.id)}
              fields={{ description: description.trim() || undefined }}
              maxSizeMb={25}
              onUploaded={async () => {
                await refresh();
                setDescription('');
                toast({ title: 'File added' });
              }}
            />
          </div>
        )}

        {isLoading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading its files…
          </div>
        ) : files.length === 0 ? (
          <p className="text-sm text-muted-foreground">No files yet.</p>
        ) : (
          <ul className="divide-y text-sm">
            {files.map((f) => (
              <li key={f.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <div className="min-w-0">
                  <p className="truncate font-medium">{f.fileName}</p>
                  <p className="text-xs text-muted-foreground">
                    {[f.description, size(f.fileSizeBytes), f.uploadDate.slice(0, 10), f.uploadedByName].filter(Boolean).join(' · ')}
                  </p>
                </div>
                <div className="flex gap-1">
                  <Button variant="ghost" size="sm" onClick={() => companyMilestoneService.downloadDocument(f)}>
                    <Download className="mr-1 h-4 w-4" /> Download
                  </Button>
                  {canWrite && (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-destructive"
                      disabled={removing === f.id}
                      onClick={() => remove(f.id)}
                    >
                      {removing === f.id ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Trash2 className="mr-1 h-4 w-4" />}
                      Remove
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </DialogContent>
    </Dialog>
  );
}
