'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { jobPostingService } from '@/services/hr/recruitment.service';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import { APPLICATION_SOURCES, type ApplicationSource, type JobApplication } from '@/types/hr/recruitment-pipeline';

const NONE = '__none__';

/**
 * HR corrects how an application arrived (round 3, lane A; register row R-4): the source and the
 * advert it came through. There was no update door before, so a source recorded wrongly — and
 * every self-service application used to say "Company website" whatever the advert — stayed wrong.
 */
export function ApplicationSourceDialog({
  open,
  onOpenChange,
  application,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  application: Pick<JobApplication, 'id' | 'jobVacancyId' | 'source' | 'jobPostingId'>;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [source, setSource] = useState<ApplicationSource>(application.source);
  const [postingId, setPostingId] = useState(application.jobPostingId ?? '');

  useEffect(() => {
    if (open) {
      setSource(application.source);
      setPostingId(application.jobPostingId ?? '');
    }
  }, [open, application.source, application.jobPostingId]);

  const postings = useQuery({
    queryKey: ['hr', 'postings', 'vacancy', application.jobVacancyId],
    queryFn: () => jobPostingService.getByVacancy(application.jobVacancyId),
    enabled: open,
  });

  const save = useMutation({
    mutationFn: () => jobApplicationService.updateSource(application.id, { source, jobPostingId: postingId || null }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'application', application.id] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'applications'] });
      toast({ title: 'Source corrected' });
      onOpenChange(false);
    },
    onError: (e: any) =>
      toast({ title: 'Could not correct it', description: e?.data?.message ?? e?.message, variant: 'destructive' }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Correct the source</DialogTitle>
          <DialogDescription>
            How this application arrived, and the advert it came through. Only this vacancy&apos;s
            adverts are offered.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="source-source">Source</Label>
            <Select value={source} onValueChange={(v) => setSource(v as ApplicationSource)}>
              <SelectTrigger id="source-source">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {APPLICATION_SOURCES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {humanizeEnum(s)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="source-posting">Advert</Label>
            <Select value={postingId || NONE} onValueChange={(v) => setPostingId(v === NONE ? '' : v)}>
              <SelectTrigger id="source-posting">
                <SelectValue placeholder="None" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None</SelectItem>
                {(postings.data ?? []).map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {humanizeEnum(p.channel)} · {p.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => save.mutate()} disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
