'use client';

import { useQuery } from '@tanstack/react-query';
import { CalendarClock, Download, Megaphone, Pin } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { hrDocumentService } from '@/services/hr/hr-document.service';
import {
  myAnnouncementsService,
  type HrAnnouncementCategory,
  type MyAnnouncement,
} from '@/services/hr/announcements.service';
import { cn } from '@/lib/utils';

/**
 * Announcements (area 25 slice 12c) — what the organisation has told this employee.
 *
 * ⚠ `body` is PLAIN TEXT and is rendered with `whitespace-pre-line`, never as markup. An
 * announcement reaches everyone, so treating HR-authored text as HTML would be a stored-XSS
 * vector aimed at the entire staff.
 *
 * There is no "mark as read" here, and that is deliberate: a notice board is not an obligation.
 * Where the organisation must prove somebody was told, that is a policy acknowledgement.
 */

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' }) : '';

const CATEGORY_STYLE: Record<HrAnnouncementCategory, string> = {
  General: 'bg-muted text-muted-foreground',
  Policy: 'bg-blue-100 text-blue-900 dark:bg-blue-950/50 dark:text-blue-200',
  Benefits: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  Safety: 'bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
  Event: 'bg-violet-100 text-violet-900 dark:bg-violet-950/50 dark:text-violet-200',
  Urgent: 'bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-200',
};

export default function MyAnnouncementsPage() {
  const { toast } = useToast();

  const { data: announcements = [], isLoading, isError } = useQuery({
    queryKey: ['me', 'announcements'],
    queryFn: () => myAnnouncementsService.getMine(),
  });

  const download = async (a: MyAnnouncement) => {
    try {
      await hrDocumentService.download(
        myAnnouncementsService.attachmentEndpoint(a.id),
        a.fileName ?? 'attachment',
      );
    } catch {
      toast({
        title: 'Could not download the attachment',
        description: 'Please try again in a moment.',
        variant: 'destructive',
      });
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Announcements"
        description="Notices from the organisation that apply to you."
        backHref="/me"
      />

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Announcements could not be loaded right now. Try again in a moment.
        </p>
      ) : announcements.length === 0 ? (
        <EmptyState
          icon={Megaphone}
          title="Nothing to report"
          description="When HR publishes something that applies to you, it appears here."
        />
      ) : (
        <div className="space-y-3">
          {announcements.map((a) => (
            <Card key={a.id} className={cn(a.isPinned && 'border-primary/40')}>
              <CardContent className="space-y-3 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  {a.isPinned && <Pin className="h-3.5 w-3.5 text-primary" aria-label="Pinned" />}
                  <span className="font-medium">{a.title}</span>
                  <Badge className={cn('border-0', CATEGORY_STYLE[a.category])}>
                    {a.categoryName}
                  </Badge>
                  <span className="ml-auto text-xs text-muted-foreground">
                    {fmtDate(a.publishedAt)}
                    {a.publishedByName ? ` · ${a.publishedByName}` : ''}
                  </span>
                </div>

                {/* Plain text — see the file's note. Never dangerouslySetInnerHTML. */}
                <p className="whitespace-pre-line text-sm leading-relaxed">{a.body}</p>

                <div className="flex flex-wrap items-center gap-3">
                  {a.hasAttachment && (
                    <Button variant="outline" size="sm" onClick={() => download(a)}>
                      <Download className="mr-1 h-3.5 w-3.5" />
                      {a.fileName ?? 'Attachment'}
                    </Button>
                  )}
                  {a.expiresOn && (
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      <CalendarClock className="h-3.5 w-3.5" />
                      until {fmtDate(a.expiresOn)}
                    </span>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
