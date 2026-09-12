'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bell, BellOff, CheckCheck, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import {
  mePortalService,
  type PortalNotificationItem,
  type PortalNotificationSource,
} from '@/services/hr/me-portal.service';
import { cn } from '@/lib/utils';

/**
 * My Notifications (area 25 slice 11) — one feed over four stores.
 *
 * The general platform store is keyed by USER, the appraisal and orientation stores by
 * EMPLOYEE, and the movement rows are computed at request time from your movements. The
 * server merges them into one shape; read state stays with whichever store owns it, so
 * mark-read dispatches BY SOURCE (`mePortalService.markNotificationRead`) — this screen
 * invents no fourth store.
 *
 * Movement rows carry no read state at all (their ids are regenerated every request), which
 * is why they show a "Live" badge instead of a mark-read control and never appear under
 * Unread. Saying so on the row is more honest than a control that would silently do nothing.
 */

const SOURCE_LABEL: Record<PortalNotificationSource, string> = {
  General: 'System',
  Appraisal: 'Appraisal',
  Orientation: 'Orientation',
  Movement: 'Movement',
};

/** "SelfEvaluationDue" → "Self Evaluation Due" — display only. */
const humanize = (v: string) => v.replace(/([a-z0-9])([A-Z])/g, '$1 $2');

const timeAgo = (iso: string) => {
  const then = new Date(iso).getTime();
  const mins = Math.max(0, Math.round((Date.now() - then) / 60000));
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const dys = Math.round(hrs / 24);
  if (dys < 30) return `${dys}d ago`;
  return new Date(iso).toLocaleDateString();
};

export default function MyNotificationsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [tab, setTab] = useState<'all' | 'unread'>('all');

  const { data, isLoading, isError } = useQuery({
    queryKey: ['me', 'notifications'],
    queryFn: () => mePortalService.getMyNotifications(),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['me', 'notifications'] });
    // The nav badge and the landing chips read the counts endpoint.
    queryClient.invalidateQueries({ queryKey: ['me', 'inbox-counts'] });
  };

  const markOne = useMutation({
    mutationFn: ({ source, id }: { source: PortalNotificationSource; id: string }) =>
      mePortalService.markNotificationRead(source, id),
    onSuccess: invalidate,
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Could not mark the notification as read.',
        variant: 'destructive',
      }),
  });

  const markAll = useMutation({
    mutationFn: () => mePortalService.markAllNotificationsRead(),
    onSuccess: async () => {
      invalidate();
      toast({ title: 'Done', description: 'All notifications marked as read.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Could not mark the notifications as read.',
        variant: 'destructive',
      }),
  });

  const items = data?.items ?? [];
  const unreadCount = data?.unreadCount ?? 0;

  const visible = useMemo(
    () => (tab === 'unread' ? items.filter((n) => n.isRead === false) : items),
    [items, tab],
  );

  const open = (n: PortalNotificationItem) => {
    if (n.canMarkRead && n.isRead === false) {
      markOne.mutate({ source: n.source, id: n.id });
    }
    if (n.actionUrl) router.push(n.actionUrl);
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notifications"
        description="Everything the organisation has told you — appraisals, orientation, your movements and system messages, in one place."
        backHref="/me"
        actions={
          unreadCount > 0 ? (
            <Button
              variant="outline"
              size="sm"
              onClick={() => markAll.mutate()}
              disabled={markAll.isPending}
            >
              {markAll.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <CheckCheck className="mr-2 h-4 w-4" />
              )}
              Mark all read
            </Button>
          ) : undefined
        }
      />

      <Tabs value={tab} onValueChange={(v) => setTab(v as 'all' | 'unread')}>
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="unread">
            Unread
            {unreadCount > 0 && (
              <Badge variant="destructive" className="ml-2">
                {unreadCount}
              </Badge>
            )}
          </TabsTrigger>
        </TabsList>
      </Tabs>

      {isLoading ? (
        <div className="space-y-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-16" />
          ))}
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Your notifications could not be loaded right now. Try again in a moment.
        </p>
      ) : visible.length === 0 ? (
        <EmptyState
          icon={tab === 'unread' ? BellOff : Bell}
          title={tab === 'unread' ? 'Nothing unread' : 'No notifications yet'}
          description={
            tab === 'unread'
              ? 'You have read everything. New notifications will appear here as they arrive.'
              : 'When your appraisal cycle opens, your orientation reminds you, or a movement of yours moves, you will hear about it here.'
          }
        />
      ) : (
        <Card>
          <CardContent className="p-0">
            {visible.map((n) => {
              const unread = n.isRead === false;
              const clickable = !!n.actionUrl || (n.canMarkRead && unread);
              return (
                <div
                  key={`${n.source}-${n.id}`}
                  role={clickable ? 'button' : undefined}
                  tabIndex={clickable ? 0 : undefined}
                  onClick={clickable ? () => open(n) : undefined}
                  onKeyDown={
                    clickable
                      ? (e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            open(n);
                          }
                        }
                      : undefined
                  }
                  className={cn(
                    'flex items-start gap-3 border-b px-4 py-3 last:border-b-0',
                    clickable && 'cursor-pointer hover:bg-muted/40',
                    unread && 'bg-primary/[0.03]',
                  )}
                >
                  <span
                    className={cn(
                      'mt-2 h-2 w-2 shrink-0 rounded-full',
                      unread ? 'bg-primary' : 'bg-transparent',
                    )}
                    aria-hidden
                  />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={cn('truncate', unread && 'font-semibold')}>{n.title}</span>
                      <Badge variant="outline">{SOURCE_LABEL[n.source] ?? n.source}</Badge>
                      {n.isActionRequired && <Badge variant="destructive">Action needed</Badge>}
                      {!n.canMarkRead && (
                        <Badge
                          variant="secondary"
                          title="Reflects the current state of your record — there is nothing to mark read"
                        >
                          Live
                        </Badge>
                      )}
                    </div>
                    {n.message && (
                      <p className="mt-0.5 text-sm text-muted-foreground">{n.message}</p>
                    )}
                    <p className="mt-1 text-xs text-muted-foreground">
                      {humanize(n.category)} · {timeAgo(n.createdAt)}
                    </p>
                  </div>
                  {n.canMarkRead && unread && (
                    <Button
                      variant="ghost"
                      size="sm"
                      title="Mark as read"
                      disabled={markOne.isPending}
                      onClick={(e) => {
                        e.stopPropagation();
                        markOne.mutate({ source: n.source, id: n.id });
                      }}
                    >
                      <CheckCheck className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              );
            })}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
