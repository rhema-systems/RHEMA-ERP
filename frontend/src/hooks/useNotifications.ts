import { useEffect, useCallback, useRef } from 'react';
import { useNotificationContext } from '../contexts/NotificationContext';
import signalRService from '../services/signalr.service';
import { HubConnectionState } from '@microsoft/signalr';

export interface UseNotificationsOptions {
  autoFetch?: boolean;
  enableRealTime?: boolean;
}

/**
 * Hook for managing notifications with SignalR real-time updates
 * Integrates with NotificationContext and listens for real-time notifications
 */
export function useNotifications(options: UseNotificationsOptions = {}) {
  const {
    autoFetch = true,
    enableRealTime = true
  } = options;

  const notificationContext = useNotificationContext();
  const unsubscribeRef = useRef<(() => void) | null>(null);
  const connectAttemptedRef = useRef(false);

  // Handle real-time notifications from SignalR
  const handleRealtimeNotification = useCallback((notification: any) => {
    // Map SignalR notification format to our Notification interface
    // The SignalR hub should send unified notification format
    if (notification && notification.id) {
      notificationContext.addNotification({
        id: notification.id,
        recipientId: notification.recipientId || '',
        notificationType: notification.notificationType || 'InApp',
        title: notification.title || '',
        message: notification.message || '',
        priority: notification.priority || 'Normal',
        status: notification.status || 'Sent',
        entityId: notification.entityId || '',
        entityType: notification.entityType || 'General',
        actionUrl: notification.actionUrl,
        isRead: notification.isRead || false,
        timestamp: notification.timestamp || notification.createdAt || new Date().toISOString(),
        readAt: notification.readAt,
        dismissedAt: notification.dismissedAt,
        createdAt: notification.createdAt || new Date().toISOString(),
        scheduledFor: notification.scheduledFor,
        sentAt: notification.sentAt,
        expiresAt: notification.expiresAt,
        attemptCount: notification.attemptCount || 0,
        lastError: notification.lastError,
        deliveryMethods: notification.deliveryMethods,
        emailAddress: notification.emailAddress,
        phoneNumber: notification.phoneNumber,
        additionalData: notification.additionalData,
        tenantId: notification.tenantId || ''
      });
    }
  }, [notificationContext]);

  // Setup real-time listeners
  useEffect(() => {
    if (!enableRealTime) {
      return;
    }

    // Subscribe to SignalR notifications
    unsubscribeRef.current = signalRService.onNotification(handleRealtimeNotification);

    // Ensure SignalR is connected (safe no-op if already connected)
    // If no token is available (logged-out pages), connect() will throw; ignore.
    if (!connectAttemptedRef.current && typeof window !== 'undefined') {
      connectAttemptedRef.current = true;
      signalRService.connect().catch(() => {
        // swallow - connection will succeed once a token exists (e.g., after login)
      });
    }

    // Cleanup
    return () => {
      if (unsubscribeRef.current) {
        unsubscribeRef.current();
        unsubscribeRef.current = null;
      }
    };
  }, [enableRealTime, handleRealtimeNotification]);

  // Auto-fetch notifications on mount (only once)
  useEffect(() => {
    if (autoFetch) {
      notificationContext.fetchNotifications();
    }
  }, [autoFetch]); // Remove notificationContext from dependency to avoid refetching on every context change

  // Helper function to mark notification as read and navigate to entity
  const handleNotificationClick = useCallback(async (notificationId: string, actionUrl?: string) => {
    try {
      // Mark as read
      await notificationContext.markAsRead(notificationId);

      // Navigate if action URL is provided
      if (actionUrl && typeof window !== 'undefined') {
        window.location.href = actionUrl;
      }
    } catch (error) {
      console.error('Error handling notification click:', error);
    }
  }, [notificationContext]);

  // Helper function to dismiss notification (mark as dismissed)
  const dismissNotification = useCallback(async (notificationId: string) => {
    try {
      await notificationContext.deleteNotification(notificationId);
    } catch (error) {
      console.error('Error dismissing notification:', error);
    }
  }, [notificationContext]);

  // Helper function to get unread notifications count
  const getUnreadCount = useCallback(() => {
    return notificationContext.unreadCount;
  }, [notificationContext.unreadCount]);

  // Helper function to filter notifications
  const filterNotifications = useCallback((filter: {
    isRead?: boolean;
    priority?: string;
    entityType?: string;
    status?: string;
  }) => {
    return notificationContext.notifications.filter(notification => {
      if (filter.isRead !== undefined && notification.isRead !== filter.isRead) {
        return false;
      }
      if (filter.priority && notification.priority !== filter.priority) {
        return false;
      }
      if (filter.entityType && notification.entityType !== filter.entityType) {
        return false;
      }
      if (filter.status && notification.status !== filter.status) {
        return false;
      }
      return true;
    });
  }, [notificationContext.notifications]);

  return {
    // State from context
    notifications: notificationContext.notifications,
    unreadCount: notificationContext.unreadCount,
    loading: notificationContext.loading,
    error: notificationContext.error,

    // Actions from context
    fetchNotifications: notificationContext.fetchNotifications,
    markAsRead: notificationContext.markAsRead,
    markAllAsRead: notificationContext.markAllAsRead,
    deleteNotification: notificationContext.deleteNotification,
    clearError: notificationContext.clearError,

    // Custom helpers
    handleNotificationClick,
    dismissNotification,
    getUnreadCount,
    filterNotifications,

    // Real-time status
    isConnected: signalRService.isConnected,
    connectionState: signalRService.connectionState
  };
}

export default useNotifications;
