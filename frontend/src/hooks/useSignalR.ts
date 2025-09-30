import { useEffect, useState, useCallback, useRef } from 'react';
import { HubConnectionState } from '@microsoft/signalr';
import signalRService, { 
  DashboardData, 
  Notification, 
  UserSessionUpdate 
} from '../services/signalr.service';

export interface UseSignalROptions {
  autoConnect?: boolean;
  onDashboardUpdate?: (data: DashboardData) => void;
  onNotification?: (notification: Notification) => void;
  onUserSessionUpdate?: (update: UserSessionUpdate) => void;
}

export function useSignalR(options: UseSignalROptions = {}) {
  const {
    autoConnect = true,
    onDashboardUpdate,
    onNotification,
    onUserSessionUpdate
  } = options;

  const [connectionState, setConnectionState] = useState<HubConnectionState>(
    signalRService.connectionState
  );
  const [error, setError] = useState<string | null>(null);
  const [isConnecting, setIsConnecting] = useState(false);
  
  // Use refs to store the latest callback references
  const dashboardUpdateRef = useRef(onDashboardUpdate);
  const notificationRef = useRef(onNotification);
  const userSessionUpdateRef = useRef(onUserSessionUpdate);

  // Update refs when callbacks change
  useEffect(() => {
    dashboardUpdateRef.current = onDashboardUpdate;
  }, [onDashboardUpdate]);

  useEffect(() => {
    notificationRef.current = onNotification;
  }, [onNotification]);

  useEffect(() => {
    userSessionUpdateRef.current = onUserSessionUpdate;
  }, [onUserSessionUpdate]);

  const connect = useCallback(async () => {
    if (isConnecting || signalRService.isConnected) {
      return;
    }

    setIsConnecting(true);
    setError(null);

    try {
      await signalRService.connect();
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to connect to SignalR';
      setError(errorMessage);
      console.error('SignalR connection error:', err);
    } finally {
      setIsConnecting(false);
    }
  }, [isConnecting]);

  const disconnect = useCallback(async () => {
    try {
      await signalRService.disconnect();
      setError(null);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to disconnect from SignalR';
      setError(errorMessage);
      console.error('SignalR disconnection error:', err);
    }
  }, []);

  const joinGroup = useCallback(async (groupName: string) => {
    try {
      await signalRService.joinGroup(groupName);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to join group';
      setError(errorMessage);
      console.error('SignalR join group error:', err);
    }
  }, []);

  const leaveGroup = useCallback(async (groupName: string) => {
    try {
      await signalRService.leaveGroup(groupName);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to leave group';
      setError(errorMessage);
      console.error('SignalR leave group error:', err);
    }
  }, []);

  useEffect(() => {
    // Subscribe to connection state changes
    const unsubscribeStateChange = signalRService.onConnectionStateChange((state) => {
      setConnectionState(state);
      
      // Clear error when successfully connected
      if (state === HubConnectionState.Connected) {
        setError(null);
      }
    });

    // Subscribe to dashboard updates
    let unsubscribeDashboard: (() => void) | undefined;
    if (dashboardUpdateRef.current) {
      unsubscribeDashboard = signalRService.onDashboardUpdate((data) => {
        dashboardUpdateRef.current?.(data);
      });
    }

    // Subscribe to notifications
    let unsubscribeNotifications: (() => void) | undefined;
    if (notificationRef.current) {
      unsubscribeNotifications = signalRService.onNotification((notification) => {
        notificationRef.current?.(notification);
      });
    }

    // Subscribe to user session updates
    let unsubscribeUserSessions: (() => void) | undefined;
    if (userSessionUpdateRef.current) {
      unsubscribeUserSessions = signalRService.onUserSessionUpdate((update) => {
        userSessionUpdateRef.current?.(update);
      });
    }

    // Auto-connect if enabled
    if (autoConnect && !signalRService.isConnected && !isConnecting) {
      connect();
    }

    // Cleanup function
    return () => {
      unsubscribeStateChange();
      unsubscribeDashboard?.();
      unsubscribeNotifications?.();
      unsubscribeUserSessions?.();
    };
  }, [autoConnect, connect, isConnecting]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      // Don't automatically disconnect on unmount as other components might be using SignalR
      // The service manages its own lifecycle
    };
  }, []);

  return {
    connectionState,
    isConnected: connectionState === HubConnectionState.Connected,
    isConnecting: isConnecting || connectionState === HubConnectionState.Connecting,
    isReconnecting: connectionState === HubConnectionState.Reconnecting,
    isDisconnected: connectionState === HubConnectionState.Disconnected,
    error,
    connectionId: signalRService.connectionId,
    connect,
    disconnect,
    joinGroup,
    leaveGroup
  };
}

export function useDashboardSignalR() {
  const [dashboardData, setDashboardData] = useState<DashboardData | null>(null);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [userSessionUpdates, setUserSessionUpdates] = useState<UserSessionUpdate[]>([]);

  const handleDashboardUpdate = useCallback((data: DashboardData) => {
    setDashboardData(data);
  }, []);

  const handleNotification = useCallback((notification: Notification) => {
    setNotifications(prev => [notification, ...prev].slice(0, 50)); // Keep last 50 notifications
  }, []);

  const handleUserSessionUpdate = useCallback((update: UserSessionUpdate) => {
    setUserSessionUpdates(prev => [update, ...prev].slice(0, 100)); // Keep last 100 updates
  }, []);

  const signalR = useSignalR({
    autoConnect: true,
    onDashboardUpdate: handleDashboardUpdate,
    onNotification: handleNotification,
    onUserSessionUpdate: handleUserSessionUpdate
  });

  const clearNotifications = useCallback(() => {
    setNotifications([]);
  }, []);

  const markNotificationAsRead = useCallback((notificationId: string) => {
    setNotifications(prev => prev.map(notification => 
      notification.id === notificationId 
        ? { ...notification, isRead: true }
        : notification
    ));
  }, []);

  const removeNotification = useCallback((notificationId: string) => {
    setNotifications(prev => prev.filter(notification => notification.id !== notificationId));
  }, []);

  const clearUserSessionUpdates = useCallback(() => {
    setUserSessionUpdates([]);
  }, []);

  return {
    ...signalR,
    dashboardData,
    notifications,
    userSessionUpdates,
    unreadNotificationsCount: notifications.filter(n => !n.isRead).length,
    clearNotifications,
    markNotificationAsRead,
    removeNotification,
    clearUserSessionUpdates
  };
}

export default useSignalR;