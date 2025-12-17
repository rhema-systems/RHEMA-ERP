'use client';

import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Bell, CheckCircle, AlertCircle, Info, Trash2 } from 'lucide-react';
import { useNotifications } from '@/hooks/useNotifications';
import { formatDistanceToNow } from 'date-fns';
import { useRouter } from 'next/navigation';

export default function NotificationsPage() {
  const router = useRouter();
  const {
    notifications,
    unreadCount,
    loading,
    markAsRead,
    markAllAsRead,
  } = useNotifications({
    autoFetch: true,
    enableRealTime: true,
  });

  // Helper function to strip HTML tags from notification message
  const stripHtml = (html: string) => {
    if (!html) return '';
    const tmp = document.createElement('DIV');
    tmp.innerHTML = html;
    return tmp.textContent || tmp.innerText || '';
  };

  const getNotificationIcon = (priority: string) => {
    switch (priority) {
      case 'High':
      case 'Critical':
        return { icon: AlertCircle, iconColor: 'text-red-600', bgColor: 'bg-red-50' };
      case 'Normal':
        return { icon: Info, iconColor: 'text-blue-600', bgColor: 'bg-blue-50' };
      case 'Low':
        return { icon: CheckCircle, iconColor: 'text-green-600', bgColor: 'bg-green-50' };
      default:
        return { icon: Info, iconColor: 'text-blue-600', bgColor: 'bg-blue-50' };
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center">
            <Bell className="mr-3 h-8 w-8" />
            Notifications
          </h1>
          <p className="text-gray-600 mt-2">
            {unreadCount > 0 ? `You have ${unreadCount} unread notification${unreadCount > 1 ? 's' : ''}` : 'All caught up!'}
          </p>
        </div>
        <div className="flex space-x-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => markAllAsRead()}
            disabled={unreadCount === 0 || loading}
          >
            Mark All as Read
          </Button>
        </div>
      </div>

      {/* Notifications List */}
      {loading ? (
        <div className="text-center py-12 text-gray-500">
          Loading notifications...
        </div>
      ) : notifications.length === 0 ? (
        <Card>
          <CardContent className="p-12 text-center">
            <Bell className="h-12 w-12 text-gray-400 mx-auto mb-4" />
            <h3 className="text-lg font-semibold mb-2">No Notifications</h3>
            <p className="text-gray-600">
              You're all caught up! Check back later for updates.
            </p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          {notifications.map((notification) => {
            const { icon: Icon, iconColor, bgColor } = getNotificationIcon(notification.priority);

            // Safely parse the date - use timestamp or createdAt
            const dateString = notification.timestamp || notification.createdAt;
            const createdDate = dateString ? new Date(dateString) : null;
            const isValidDate = createdDate && !isNaN(createdDate.getTime());

            return (
              <Card
                key={notification.id}
                className={!notification.isRead ? 'border-l-4 border-l-blue-500' : ''}
              >
                <CardContent className="p-6">
                  <div className="flex items-start space-x-4">
                    <div className={`${bgColor} p-3 rounded-lg`}>
                      <Icon className={`h-6 w-6 ${iconColor}`} />
                    </div>
                    <div className="flex-1">
                      <div className="flex items-start justify-between">
                        <div
                          className="flex-1 cursor-pointer"
                          onClick={() => {
                            if (!notification.isRead) {
                              markAsRead(notification.id);
                            }
                            if (notification.actionUrl) {
                              router.push(notification.actionUrl);
                            }
                          }}
                        >
                          <div className="flex items-center space-x-2 mb-1">
                            <h3 className="font-semibold">{notification.title}</h3>
                            {!notification.isRead && (
                              <Badge variant="default" className="bg-blue-600">
                                New
                              </Badge>
                            )}
                          </div>
                          <p className="text-gray-600 mb-2">{stripHtml(notification.message)}</p>
                          <p className="text-sm text-gray-400">
                            {isValidDate
                              ? formatDistanceToNow(createdDate, { addSuffix: true })
                              : 'Just now'
                            }
                          </p>
                        </div>
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

