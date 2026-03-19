'use client';

import React, { useEffect, useRef } from 'react';
import { X, AlertCircle, CheckCircle, InfoIcon, AlertTriangle } from 'lucide-react';
import { useNotifications } from '../../hooks/useNotifications';

interface NotificationToastProps {
  maxToasts?: number;
  autoDismissDelay?: number;
  position?: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
}

interface ToastItem {
  id: string;
  notification: any;
  show: boolean;
  timeoutId?: NodeJS.Timeout;
}

export function NotificationToast({
  maxToasts = 3,
  autoDismissDelay = 5000,
  position = 'top-right'
}: NotificationToastProps) {
  const { notifications } = useNotifications({ autoFetch: false, enableRealTime: true });
  const [toasts, setToasts] = React.useState<ToastItem[]>([]);
  const lastNotificationIdRef = useRef<string | null>(null);

  // Add new notifications to toast queue
  useEffect(() => {
    if (notifications.length > 0) {
      const latestNotification = notifications[0];

      // Only show toast for new notifications (avoid duplicates)
      if (lastNotificationIdRef.current !== latestNotification.id) {
        lastNotificationIdRef.current = latestNotification.id;

        const toastId = `toast-${latestNotification.id}`;
        
        // Only show toast if it's recent (just received)
        if (!latestNotification.createdAt) {
          return;
        }
        const createdTime = new Date(latestNotification.createdAt).getTime();
        const now = Date.now();
        if (now - createdTime < 2000) { // Show only if created in last 2 seconds
          setToasts(prev => {
            const updated = [
              { id: toastId, notification: latestNotification, show: true },
              ...prev
            ].slice(0, maxToasts);
            return updated;
          });

          // Auto-dismiss after delay
          const timeoutId = setTimeout(() => {
            setToasts(prev =>
              prev.map(t =>
                t.id === toastId ? { ...t, show: false } : t
              )
            );

            // Remove from DOM after animation
            setTimeout(() => {
              setToasts(prev => prev.filter(t => t.id !== toastId));
            }, 300);
          }, autoDismissDelay);

          return () => clearTimeout(timeoutId);
        }
      }
    }
  }, [notifications, maxToasts, autoDismissDelay]);

  const dismissToast = (toastId: string) => {
    setToasts(prev =>
      prev.map(t =>
        t.id === toastId ? { ...t, show: false } : t
      )
    );

    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== toastId));
    }, 300);
  };

  const getIcon = (priority: string) => {
    switch (priority) {
      case 'Critical':
        return <AlertCircle className="h-5 w-5 text-red-600" />;
      case 'High':
        return <AlertTriangle className="h-5 w-5 text-yellow-600" />;
      case 'Normal':
        return <InfoIcon className="h-5 w-5 text-blue-600" />;
      case 'Low':
        return <CheckCircle className="h-5 w-5 text-green-600" />;
      default:
        return <InfoIcon className="h-5 w-5 text-blue-600" />;
    }
  };

  const getToastColor = (priority: string) => {
    switch (priority) {
      case 'Critical':
        return 'bg-red-50 border-red-200 dark:bg-red-950/20 dark:border-red-800';
      case 'High':
        return 'bg-yellow-50 border-yellow-200 dark:bg-yellow-950/20 dark:border-yellow-800';
      case 'Normal':
        return 'bg-blue-50 border-blue-200 dark:bg-blue-950/20 dark:border-blue-800';
      case 'Low':
        return 'bg-green-50 border-green-200 dark:bg-green-950/20 dark:border-green-800';
      default:
        return 'bg-blue-50 border-blue-200 dark:bg-blue-950/20 dark:border-blue-800';
    }
  };

  const getPositionClasses = () => {
    const baseClasses = 'fixed z-50 pointer-events-none';
    switch (position) {
      case 'top-left':
        return `${baseClasses} top-4 left-4 flex flex-col gap-2`;
      case 'top-right':
        return `${baseClasses} top-4 right-4 flex flex-col gap-2`;
      case 'bottom-left':
        return `${baseClasses} bottom-4 left-4 flex flex-col gap-2`;
      case 'bottom-right':
        return `${baseClasses} bottom-4 right-4 flex flex-col gap-2`;
      default:
        return `${baseClasses} top-4 right-4 flex flex-col gap-2`;
    }
  };

  return (
    <div className={getPositionClasses()}>
      {toasts.map(toast => (
        <div
          key={toast.id}
          className={`
            pointer-events-auto transform transition-all duration-300 ease-in-out
            ${toast.show ? 'translate-x-0 opacity-100' : 'translate-x-full opacity-0'}
            max-w-sm w-full
          `}
        >
          <div
            className={`
              rounded-lg border shadow-lg p-4 flex items-start gap-3
              ${getToastColor(toast.notification.priority)}
            `}
          >
            {/* Icon */}
            <div className="flex-shrink-0 pt-0.5">
              {getIcon(toast.notification.priority)}
            </div>

            {/* Content */}
            <div className="flex-1 min-w-0">
              <h3 className="font-semibold text-sm mb-1">
                {toast.notification.title}
              </h3>
              <p className="text-xs text-muted-foreground line-clamp-2">
                {toast.notification.message}
              </p>
              {toast.notification.entityType && (
                <p className="text-xs text-muted-foreground mt-1">
                  {toast.notification.entityType}
                  {toast.notification.entityId && ` #${toast.notification.entityId.substring(0, 8)}`}
                </p>
              )}
            </div>

            {/* Close Button */}
            <button
              onClick={() => dismissToast(toast.id)}
              className="
                flex-shrink-0 text-muted-foreground hover:text-foreground
                transition-colors p-1
              "
              aria-label="Dismiss notification"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

export default NotificationToast;
