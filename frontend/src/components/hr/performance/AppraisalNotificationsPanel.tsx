'use client';

import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BellRing, CheckCheck, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { appraisalNotificationService } from '@/services/hr/appraisal.service';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import type { AppraisalNotification, NotificationUrgency } from '@/types/hr/appraisal';

/**
 * The appraisal notification queue for the signed-in employee.
 *
 * Notifications are addressed to an **employee record**, not to a user account, so an admin
 * login with no linked employee has none at all — the API answers 400 and that is surfaced as
 * an explanation rather than as an empty "all caught up", which would be misleading.
 *
 * `timeAgo` comes back empty from the API by design (the server leaves it to the client), so
 * it is computed here from `createdDate`.
 */
const URGENCY_VARIANT: Record<NotificationUrgency, 'outline' | 'secondary' | 'destructive'> = {
  Normal: 'outline',
  Warning: 'secondary',
  Urgent: 'destructive',
};

function timeAgo(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return '';
  const minutes = Math.round((Date.now() - then) / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  if (days < 30) return `${days}d ago`;
  return new Date(iso).toLocaleDateString();
}

function NotificationRow({
  notification,
  onRead,
}: {
  notification: AppraisalNotification;
  onRead: (id: string) => void;
}) {
  const body = (
    <div className="flex items-start justify-between gap-3 py-3">
      <div className="space-y-0.5">
        <p className={notification.isRead ? 'text-sm' : 'text-sm font-medium'}>
          {notification.title}
        </p>
        <p className="text-sm text-muted-foreground">{notification.message}</p>
        <p className="text-xs text-muted-foreground">
          {humanizeEnum(notification.type)} · {timeAgo(notification.createdDate)}
          {notification.cycleName ? ` · ${notification.cycleName}` : ''}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <Badge variant={URGENCY_VARIANT[notification.urgency]}>
          {humanizeEnum(notification.urgency)}
        </Badge>
        {!notification.isRead && (
          <Button
            size="sm"
            variant="ghost"
            onClick={(e) => {
              // The whole row is a link when the notification carries one; marking it read
              // must not navigate as a side effect.
              e.preventDefault();
              e.stopPropagation();
              onRead(notification.notificationId);
            }}
          >
            Mark read
          </Button>
        )}
      </div>
    </div>
  );

  if (!notification.navigationUrl) {
    return <div className="border-b last:border-0">{body}</div>;
  }

  return (
    <Link
      href={notification.navigationUrl}
      className="block border-b last:border-0 hover:bg-muted/40"
    >
      {body}
    </Link>
  );
}

export function AppraisalNotificationsPanel({ limit = 20 }: { limit?: number }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'appraisal-notifications', 'me', limit],
    queryFn: () => appraisalNotificationService.getMySummary(limit),
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-notifications'] });

  const markOne = useMutation({
    mutationFn: (notificationId: string) =>
      appraisalNotificationService.markAsRead(notificationId),
    onSuccess: invalidate,
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Could not mark the notification as read.',
        variant: 'destructive',
      }),
  });

  const markAll = useMutation({
    mutationFn: () => appraisalNotificationService.markAllMineAsRead(),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Done', description: 'All notifications marked as read.' });
    },
    onError: (e: any) =>
      toast({
        title: 'Error',
        description: e?.message || 'Could not mark the notifications as read.',
        variant: 'destructive',
      }),
  });

  const notifications = data?.recentNotifications ?? [];
  const unread = data?.unreadCount ?? 0;

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-2">
        <div>
          <CardTitle className="flex items-center gap-2 text-base">
            <BellRing className="h-4 w-4" />
            Appraisal notifications
            {unread > 0 && <Badge variant="destructive">{unread}</Badge>}
          </CardTitle>
          <CardDescription>
            Raised when a cycle opens and when HR sends deadline reminders.
          </CardDescription>
        </div>
        {unread > 0 && (
          <Button
            size="sm"
            variant="outline"
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
        )}
      </CardHeader>
      <CardContent className="pt-0">
        {isLoading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : isError ? (
          <EmptyState
            title="No notification queue"
            description={
              (error as any)?.message ||
              'Notifications are addressed to an employee record — this account may not be linked to one.'
            }
          />
        ) : notifications.length === 0 ? (
          <EmptyState
            title="Nothing to read"
            description="Notifications arrive when a cycle opens or a deadline approaches."
          />
        ) : (
          <div>
            {notifications.map((n) => (
              <NotificationRow
                key={n.notificationId}
                notification={n}
                onRead={(notificationId) => markOne.mutate(notificationId)}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
