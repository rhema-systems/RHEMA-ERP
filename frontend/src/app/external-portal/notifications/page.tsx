'use client';

import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Bell, CheckCircle, AlertCircle, Info, Trash2 } from 'lucide-react';

export default function NotificationsPage() {
  const notifications = [
    {
      id: 1,
      type: 'success',
      title: 'Registration Approved',
      message: 'Your business partner registration for ABC Trading Company has been approved.',
      time: '2 hours ago',
      unread: true,
      icon: CheckCircle,
      iconColor: 'text-green-600',
      bgColor: 'bg-green-50',
    },
    {
      id: 2,
      type: 'warning',
      title: 'Document Required',
      message: 'Please upload your tax clearance certificate to complete your registration.',
      time: '1 day ago',
      unread: true,
      icon: AlertCircle,
      iconColor: 'text-yellow-600',
      bgColor: 'bg-yellow-50',
    },
    {
      id: 3,
      type: 'info',
      title: 'Welcome to External Portal',
      message: 'Welcome! You can now submit applications and track their status.',
      time: '3 days ago',
      unread: false,
      icon: Info,
      iconColor: 'text-blue-600',
      bgColor: 'bg-blue-50',
    },
  ];

  const unreadCount = notifications.filter((n) => n.unread).length;

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
          <Button variant="outline" size="sm">
            Mark All as Read
          </Button>
          <Button variant="outline" size="sm">
            <Trash2 className="mr-2 h-4 w-4" />
            Clear All
          </Button>
        </div>
      </div>

      {/* Notifications List */}
      <div className="space-y-4">
        {notifications.map((notification) => {
          const Icon = notification.icon;
          return (
            <Card
              key={notification.id}
              className={notification.unread ? 'border-l-4 border-l-blue-500' : ''}
            >
              <CardContent className="p-6">
                <div className="flex items-start space-x-4">
                  <div className={`${notification.bgColor} p-3 rounded-lg`}>
                    <Icon className={`h-6 w-6 ${notification.iconColor}`} />
                  </div>
                  <div className="flex-1">
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center space-x-2 mb-1">
                          <h3 className="font-semibold">{notification.title}</h3>
                          {notification.unread && (
                            <Badge variant="default" className="bg-blue-600">
                              New
                            </Badge>
                          )}
                        </div>
                        <p className="text-gray-600 mb-2">{notification.message}</p>
                        <p className="text-sm text-gray-400">{notification.time}</p>
                      </div>
                      <Button variant="ghost" size="icon">
                        <Trash2 className="h-4 w-4 text-gray-400" />
                      </Button>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>

      {/* Empty State (if no notifications) */}
      {notifications.length === 0 && (
        <Card>
          <CardContent className="p-12 text-center">
            <Bell className="h-12 w-12 text-gray-400 mx-auto mb-4" />
            <h3 className="text-lg font-semibold mb-2">No Notifications</h3>
            <p className="text-gray-600">
              You're all caught up! Check back later for updates.
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

