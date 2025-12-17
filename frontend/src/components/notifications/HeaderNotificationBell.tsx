'use client';

import React, { useState, useRef } from 'react';
import { Bell, Check } from 'lucide-react';
import { Button } from '../ui/button';
import { cn } from '../../lib/utils';
import { Notification, notificationService } from '../../services/notificationService';
import useNotifications from '../../hooks/useNotifications';
import { getEntityNavigationUrl } from '../../utils/notificationNavigation';

interface HeaderNotificationBellProps {
  className?: string;
}

const HeaderNotificationBell: React.FC<HeaderNotificationBellProps> = ({ className }) => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Use the notifications hook for state management and real-time updates
  const {
    notifications,
    unreadCount,
    loading,
    markAsRead,
    markAllAsRead
  } = useNotifications({
    autoFetch: true,
    enableRealTime: true
  });

  // Helper function to strip HTML tags from notification message
  const stripHtml = (html: string) => {
    if (!html) return '';
    const tmp = document.createElement('DIV');
    tmp.innerHTML = html;
    return tmp.textContent || tmp.innerText || '';
  };

  // Show browser notification when new notification arrives
  React.useEffect(() => {
    if (notifications.length > 0) {
      const latestNotification = notifications[0];
      if (!latestNotification.isRead && 'Notification' in window && window.Notification.permission === 'granted') {
        new window.Notification(latestNotification.title, {
          body: latestNotification.message,
          icon: '/favicon.ico'
        });
      }
    }
  }, [notifications]);

  // Handle click outside to close
  React.useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        dropdownRef.current && 
        buttonRef.current &&
        !dropdownRef.current.contains(event.target as Node) &&
        !buttonRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);


  const handleNotificationClick = (notification: Notification) => {
    if (!notification.isRead) {
      markAsRead(notification.id);
    }

    // Prefer backend-provided actionUrl; otherwise derive from entity config
    const url = notification.actionUrl || getEntityNavigationUrl(notification.entityType, notification.entityId);
    if (url) {
      window.open(url, '_blank');
      setIsOpen(false);
    }
  };

  const requestNotificationPermission = async () => {
    if ('Notification' in window && window.Notification.permission === 'default') {
      await window.Notification.requestPermission();
    }
  };

  const getPriorityColor = (priority: Notification['priority']) => {
    const colors = {
      'Low': 'text-green-600',
      'Normal': 'text-blue-600',
      'High': 'text-yellow-600',
      'Critical': 'text-red-600'
    };
    return colors[priority] || 'text-gray-600';
  };

  const getEntityIcon = (entityType: string) => {
    // Using simple icons for header
    const icons = {
      'JobCard': '📋',
      'WorkOrder': '🔧',
      'Quality': '🛡️',
      'Asset': '📦',
      'System': '⚙️'
    };
    return icons[entityType as keyof typeof icons] || '🔔';
  };

  return (
    <div className="relative">
      {/* Notification Bell Button */}
      <Button
        ref={buttonRef}
        variant="ghost"
        size="sm"
        aria-label={unreadCount > 0 ? `${unreadCount} unread notifications` : 'No new notifications'}
        title={unreadCount > 0 ? `${unreadCount} unread notifications` : 'Notifications'}
        onClick={() => {
          setIsOpen(!isOpen);
          requestNotificationPermission();
        }}
        className={cn(
          'relative h-9 w-9 p-0 rounded-full flex items-center justify-center hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-200/60 dark:border-slate-700/60',
          className
        )}
      >
        <Bell className="h-5 w-5 text-slate-700 dark:text-slate-200" />
        {unreadCount > 0 && (
          <span
            className="absolute -top-1 -right-1 min-w-[18px] h-5 px-1.5 bg-red-500 text-white text-[10px] leading-none font-semibold rounded-full flex items-center justify-center shadow-sm ring-2 ring-white dark:ring-slate-900"
          >
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </Button>

      {/* Notification Dropdown */}
      {isOpen && (
        <div 
          ref={dropdownRef}
          className="absolute right-0 top-full mt-2 w-80 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-lg z-50 max-h-96 overflow-hidden"
        >
          {/* Header */}
          <div className="p-3 border-b border-slate-200 dark:border-slate-800">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                Notifications
                {unreadCount > 0 && (
                  <span className="ml-2 text-xs text-slate-500 dark:text-slate-400">
                    ({unreadCount} unread)
                  </span>
                )}
              </h3>
              {unreadCount > 0 && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => markAllAsRead()}
                  className="h-6 px-2 text-xs text-blue-600 hover:text-blue-800 hover:bg-blue-50 dark:hover:bg-blue-900/20"
                >
                  Mark all read
                </Button>
              )}
            </div>
          </div>

          {/* Notifications List */}
          <div className="max-h-80 overflow-y-auto">
            {loading ? (
              <div className="p-4 text-center text-slate-500 dark:text-slate-400">
                Loading notifications...
              </div>
            ) : notifications.length === 0 ? (
              <div className="p-6 text-center text-slate-500 dark:text-slate-400">
                <Bell className="h-8 w-8 mx-auto mb-2 opacity-50" />
                <p className="text-sm">No new notifications</p>
              </div>
            ) : (
              <div>
                {notifications.map((notification) => (
                  <div
                    key={notification.id}
                    className={cn(
                      'px-3 py-3 border-b border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/50 cursor-pointer transition-colors',
                      !notification.isRead && 'bg-blue-50/50 dark:bg-blue-900/10 border-l-2 border-l-blue-500'
                    )}
                    onClick={() => handleNotificationClick(notification)}
                  >
                    <div className="flex items-start space-x-3">
                      <div className="flex-shrink-0 mt-0.5">
                        <span className="text-lg">
                          {getEntityIcon(notification.entityType)}
                        </span>
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <h4 className={cn(
                              'text-sm truncate',
                              !notification.isRead ? 'font-semibold text-slate-900 dark:text-white' : 'font-medium text-slate-700 dark:text-slate-300'
                            )}>
                              {notification.title}
                            </h4>
                            <p className="text-xs text-slate-600 dark:text-slate-400 mt-1 line-clamp-2">
                              {stripHtml(notification.message)}
                            </p>
                            <div className="flex items-center space-x-2 mt-2">
                              <span className={cn('text-xs', getPriorityColor(notification.priority))}>
                                {notification.priority}
                              </span>
                              <span className="text-xs text-slate-400">•</span>
                              <span className="text-xs text-slate-500 dark:text-slate-400">
                                {notification.entityType}
                              </span>
                              <span className="text-xs text-slate-400">•</span>
                              <span className="text-xs text-slate-500 dark:text-slate-400">
                                {notificationService.formatNotificationTime(notification.timestamp || notification.createdAt)}
                              </span>
                            </div>
                          </div>
                          {!notification.isRead && (
                            <div className="flex-shrink-0 ml-2">
                                <button
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    markAsRead(notification.id);
                                  }}
                                  className="p-1 hover:bg-slate-200 dark:hover:bg-slate-700 rounded transition-colors"
                                  title="Mark as read"
                                >
                                  <Check className="h-3 w-3 text-slate-500" />
                                </button>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Footer */}
          {notifications.length > 0 && (
            <div className="p-3 border-t border-slate-200 dark:border-slate-800 text-center">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  window.location.href = '/notifications';
                  setIsOpen(false);
                }}
                className="text-xs text-blue-600 hover:text-blue-800 hover:bg-blue-50 dark:hover:bg-blue-900/20"
              >
                View all notifications
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default HeaderNotificationBell;