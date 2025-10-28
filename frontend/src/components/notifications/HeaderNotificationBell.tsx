'use client';

import React, { useState, useEffect, useRef } from 'react';
import { Bell, Check, X, MoreVertical } from 'lucide-react';
import { Button } from '../ui/button';
import { cn } from '../../lib/utils';
import { 
  Notification, 
  notificationService 
} from '../../services/notificationService';

interface HeaderNotificationBellProps {
  className?: string;
}

const HeaderNotificationBell: React.FC<HeaderNotificationBellProps> = ({ className }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(false);
  
  const dropdownRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Load notifications and setup real-time updates
  useEffect(() => {
    loadUnreadNotifications();
    loadUnreadCount();
    
    // Connect to real-time notifications
    notificationService.connectToRealTimeNotifications();
    
    const unsubscribe = notificationService.onNotification((notification) => {
      setNotifications(prev => [notification, ...prev.slice(0, 9)]); // Keep max 10
      setUnreadCount(prev => prev + 1);
      
      // Show browser notification if permission granted
      if ('Notification' in window && window.Notification.permission === 'granted') {
        new window.Notification(notification.title, {
          body: notification.message,
          icon: '/favicon.ico'
        });
      }
    });

    return () => {
      unsubscribe();
      notificationService.disconnectFromRealTimeNotifications();
    };
  }, []);

  // Handle click outside to close
  useEffect(() => {
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

  const loadUnreadNotifications = async () => {
    setLoading(true);
    try {
      const result = await notificationService.getUnreadNotifications(10);
      setNotifications(result);
    } catch (error) {
      console.error('Failed to load notifications:', error);
    } finally {
      setLoading(false);
    }
  };

  const loadUnreadCount = async () => {
    try {
      const count = await notificationService.getUnreadCount();
      setUnreadCount(count);
    } catch (error) {
      console.error('Failed to load unread count:', error);
    }
  };

  const handleMarkAsRead = async (notificationId: string) => {
    try {
      await notificationService.markAsRead(notificationId);
      setNotifications(prev =>
        prev.map(n => n.id === notificationId ? { ...n, isRead: true } : n)
      );
      setUnreadCount(prev => Math.max(0, prev - 1));
    } catch (error) {
      console.error('Failed to mark as read:', error);
    }
  };

  const handleMarkAllAsRead = async () => {
    try {
      await notificationService.markAllAsRead();
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
    } catch (error) {
      console.error('Failed to mark all as read:', error);
    }
  };

  const handleNotificationClick = (notification: Notification) => {
    if (!notification.isRead) {
      handleMarkAsRead(notification.id);
    }
    
    if (notification.actionUrl) {
      window.location.href = notification.actionUrl;
      setIsOpen(false);
    }
  };

  const requestNotificationPermission = async () => {
    if ('Notification' in window && window.Notification.permission === 'default') {
      await window.Notification.requestPermission();
    }
  };

  const getTypeColor = (type: Notification['type']) => {
    const colors = {
      'Info': 'text-blue-600',
      'Warning': 'text-yellow-600',
      'Error': 'text-red-600',
      'Success': 'text-green-600'
    };
    return colors[type] || 'text-gray-600';
  };

  const getCategoryIcon = (category: string) => {
    // Using simple icons for header
    const icons = {
      'JobCard': '📋',
      'WorkOrder': '🔧',
      'Quality': '🛡️',
      'Asset': '📦',
      'System': '⚙️'
    };
    return icons[category as keyof typeof icons] || '🔔';
  };

  return (
    <div className="relative">
      {/* Notification Bell Button */}
      <Button
        ref={buttonRef}
        variant="ghost"
        size="sm"
        onClick={() => {
          setIsOpen(!isOpen);
          requestNotificationPermission();
        }}
        className={cn(
          'relative h-9 w-9 p-0 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800',
          className
        )}
      >
        <Bell className="h-4 w-4 text-slate-600 dark:text-slate-400" />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -right-1 h-4 w-4 bg-red-500 text-white text-xs font-medium rounded-full flex items-center justify-center border-2 border-white dark:border-slate-900">
            {unreadCount > 9 ? '9+' : unreadCount}
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
                  onClick={handleMarkAllAsRead}
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
                          {getCategoryIcon(notification.category)}
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
                              {notification.message}
                            </p>
                            <div className="flex items-center space-x-2 mt-2">
                              <span className={cn('text-xs', getTypeColor(notification.type))}>
                                {notification.type}
                              </span>
                              <span className="text-xs text-slate-400">•</span>
                              <span className="text-xs text-slate-500 dark:text-slate-400">
                                {notification.category}
                              </span>
                              <span className="text-xs text-slate-400">•</span>
                              <span className="text-xs text-slate-500 dark:text-slate-400">
                                {notificationService.formatNotificationTime(notification.createdAt)}
                              </span>
                            </div>
                          </div>
                          {!notification.isRead && (
                            <div className="flex-shrink-0 ml-2">
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  handleMarkAsRead(notification.id);
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