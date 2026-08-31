import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { getStoredToken } from './api.service';

export interface DashboardData {
  metrics: DashboardMetrics;
  recentActivities: RecentActivity[];
  notifications: Notification[];
  onlineUsers: OnlineUser[];
  systemStatus: SystemStatus;
  lastUpdated: string;
}

export interface DashboardMetrics {
  totalUsers: number;
  activeUsers: number;
  onlineUsers: number;
  totalSessions: number;
  activeSessions: number;
  systemUptime: number;
  usersByRole: Record<string, number>;
  userLoginTrend: ChartDataPoint[];
  sessionActivity: ChartDataPoint[];
}

export interface RecentActivity {
  id: string;
  type: string;
  description: string;
  userId: string;
  userName: string;
  timestamp: string;
  additionalData?: string;
}

export interface Notification {
  id: string;
  type: string;
  title: string;
  message: string;
  severity: 'info' | 'warning' | 'error' | 'success';
  timestamp: string;
  isRead: boolean;
  actionUrl?: string;
  metadata?: Record<string, any>;
}

export interface OnlineUser {
  userId: string;
  userName: string;
  email: string;
  role: string;
  lastActivity: string;
  status: string;
  location?: string;
}

export interface SystemStatus {
  isHealthy: boolean;
  status: string;
  cpuUsage: number;
  memoryUsage: number;
  diskUsage: number;
  databaseConnections: number;
  services: ServiceStatus[];
  lastCheck: string;
}

export interface ServiceStatus {
  name: string;
  isHealthy: boolean;
  status: string;
  responseTime: number;
  errorMessage?: string;
}

export interface ChartDataPoint {
  label: string;
  value: number;
  timestamp: string;
}

export interface UserSessionUpdate {
  type: string;
  userId: string;
  userName: string;
  sessionId: string;
  timestamp: string;
  additionalInfo?: string;
}

class SignalRService {
  private connection: HubConnection | null = null;
  private connectPromise: Promise<void> | null = null;
  private readonly hubUrl: string;
  private reconnectInterval: NodeJS.Timeout | null = null;
  private isManualDisconnect = false;
  private rateLimitBackoffUntil = 0;
  private readonly enableSignalRDebugLogging =
    process.env.NEXT_PUBLIC_DEBUG_SIGNALR === 'true';

  constructor() {
    const baseUrl = (process.env.NEXT_PUBLIC_API_URL || '/api').replace(
      /\/api\/?$/,
      ''
    );
    this.hubUrl = `${baseUrl}/api/hubs/dashboard`;
    if (this.enableSignalRDebugLogging) {
      console.log('SignalR Service initialized:', {
        NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
        baseUrl,
        hubUrl: this.hubUrl,
      });
    }
  }

  // Event handlers
  private dashboardUpdateHandlers: Set<(data: DashboardData) => void> =
    new Set();
  private notificationHandlers: Set<(notification: Notification) => void> =
    new Set();
  private userSessionUpdateHandlers: Set<(update: UserSessionUpdate) => void> =
    new Set();
  private connectionStateChangeHandlers: Set<
    (state: HubConnectionState) => void
  > = new Set();

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    if (this.connectPromise) {
      return this.connectPromise;
    }

    const now = Date.now();
    if (this.rateLimitBackoffUntil > now) {
      const secondsRemaining = Math.ceil(
        (this.rateLimitBackoffUntil - now) / 1000
      );
      throw new Error(
        `SignalR connection is paused after rate limiting. Retry in ${secondsRemaining} seconds.`
      );
    }

    this.connectPromise = this.startConnection();
    try {
      await this.connectPromise;
    } finally {
      this.connectPromise = null;
    }
  }

  private async startConnection(): Promise<void> {
    if (!getStoredToken()) {
      console.warn('No authentication token available for SignalR connection');
      throw new Error('No authentication token available');
    }

    // Clean up existing connection if any
    if (this.connection) {
      try {
        if (this.connection.state !== HubConnectionState.Disconnected) {
          await this.connection.stop();
        }
      } catch (error) {
        console.warn('Error stopping previous SignalR connection:', error);
      }
      this.connection = null;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => getStoredToken() || '',
        withCredentials: false,
        transport: undefined,
        skipNegotiation: false,
        timeout: 30000, // 30 seconds
      })
      .configureLogging(
        this.enableSignalRDebugLogging ? LogLevel.Information : LogLevel.Warning
      )
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          if (retryContext.previousRetryCount === 0) return 2000; // 2 seconds
          if (retryContext.previousRetryCount === 1) return 5000; // 5 seconds
          if (retryContext.previousRetryCount === 2) return 10000; // 10 seconds
          return Math.min(
            15000 + retryContext.previousRetryCount * 5000,
            30000
          ); // Cap at 30s
        },
      })
      .build();

    // The API emits server keep-alives every 30 seconds. Leave enough headroom
    // for a delayed heartbeat before treating an otherwise healthy hub as dead.
    this.connection.serverTimeoutInMilliseconds = 120000;
    this.connection.keepAliveIntervalInMilliseconds = 15000;

    // Set up event handlers
    this.setupEventHandlers();

    try {
      this.isManualDisconnect = false;
      if (this.enableSignalRDebugLogging) {
        console.log(`Attempting to connect to SignalR hub: ${this.hubUrl}`);
      }

      await this.connection.start();
      this.rateLimitBackoffUntil = 0;

      if (this.enableSignalRDebugLogging) {
        console.log(
          `SignalR connection established successfully. ConnectionId: ${this.connection.connectionId}`
        );
      }

      this.notifyConnectionStateChange(this.connection.state);

      // Clear any reconnection intervals since auto-reconnect is handled by SignalR
      if (this.reconnectInterval) {
        clearInterval(this.reconnectInterval);
        this.reconnectInterval = null;
      }
    } catch (error) {
      console.error('SignalR connection failed:', error);
      if (this.isRateLimitError(error)) {
        this.rateLimitBackoffUntil = Date.now() + 60000;
      }

      // Log additional debugging information
      if (error instanceof Error) {
        console.error('Error details:', {
          name: error.name,
          message: error.message,
          stack: error.stack,
        });
      }

      // Clean up failed connection
      if (this.connection) {
        try {
          if (this.connection.state !== HubConnectionState.Disconnected) {
            await this.connection.stop();
          }
        } catch (stopError) {
          console.warn('Error stopping failed connection:', stopError);
        }
        this.connection = null;
      }

      throw error;
    }
  }

  private isRateLimitError(error: unknown): boolean {
    const message = error instanceof Error ? error.message : String(error);
    return (
      message.includes('429') ||
      message.toLowerCase().includes('too many requests')
    );
  }

  async disconnect(): Promise<void> {
    this.isManualDisconnect = true;

    if (this.reconnectInterval) {
      clearInterval(this.reconnectInterval);
      this.reconnectInterval = null;
    }

    if (this.connection) {
      try {
        await this.connection.stop();
        if (this.enableSignalRDebugLogging) {
          console.log('SignalR connection stopped');
        }
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      } finally {
        this.connection = null;
      }
    }
  }

  private setupEventHandlers(): void {
    if (!this.connection) return;

    // Dashboard update events
    this.connection.on('DashboardUpdate', (data: DashboardData) => {
      if (this.enableSignalRDebugLogging) {
        console.log('Received dashboard update:', data);
      }
      this.dashboardUpdateHandlers.forEach((handler) => {
        try {
          handler(data);
        } catch (error) {
          console.error('Error in dashboard update handler:', error);
        }
      });
    });

    // Notification events
    this.connection.on('NewNotification', (notification: Notification) => {
      if (this.enableSignalRDebugLogging) {
        console.log('Received new notification:', notification);
      }
      this.notificationHandlers.forEach((handler) => {
        try {
          handler(notification);
        } catch (error) {
          console.error('Error in notification handler:', error);
        }
      });
    });

    // User session update events
    this.connection.on('UserSessionUpdate', (update: UserSessionUpdate) => {
      if (this.enableSignalRDebugLogging) {
        console.log('Received user session update:', update);
      }
      this.userSessionUpdateHandlers.forEach((handler) => {
        try {
          handler(update);
        } catch (error) {
          console.error('Error in user session update handler:', error);
        }
      });
    });

    // Connection state change events
    this.connection.onclose(async (error) => {
      if (this.enableSignalRDebugLogging) {
        console.log('SignalR connection closed:', error);
      }
      this.notifyConnectionStateChange(HubConnectionState.Disconnected);

      if (!this.isManualDisconnect) {
        if (this.enableSignalRDebugLogging) {
          console.log('Attempting to reconnect...');
        }
      }
    });

    this.connection.onreconnecting((error) => {
      if (this.enableSignalRDebugLogging) {
        console.log('SignalR reconnecting:', error);
      }
      this.notifyConnectionStateChange(HubConnectionState.Reconnecting);
    });

    this.connection.onreconnected((connectionId) => {
      if (this.enableSignalRDebugLogging) {
        console.log('SignalR reconnected with connection ID:', connectionId);
      }
      this.notifyConnectionStateChange(HubConnectionState.Connected);
    });
  }

  private notifyConnectionStateChange(state: HubConnectionState): void {
    this.connectionStateChangeHandlers.forEach((handler) => {
      try {
        handler(state);
      } catch (error) {
        console.error('Error in connection state change handler:', error);
      }
    });
  }

  // Event subscription methods
  onDashboardUpdate(handler: (data: DashboardData) => void): () => void {
    this.dashboardUpdateHandlers.add(handler);
    return () => this.dashboardUpdateHandlers.delete(handler);
  }

  onNotification(handler: (notification: Notification) => void): () => void {
    this.notificationHandlers.add(handler);
    return () => this.notificationHandlers.delete(handler);
  }

  onUserSessionUpdate(
    handler: (update: UserSessionUpdate) => void
  ): () => void {
    this.userSessionUpdateHandlers.add(handler);
    return () => this.userSessionUpdateHandlers.delete(handler);
  }

  onConnectionStateChange(
    handler: (state: HubConnectionState) => void
  ): () => void {
    this.connectionStateChangeHandlers.add(handler);
    return () => this.connectionStateChangeHandlers.delete(handler);
  }

  // Hub methods
  async joinGroup(groupName: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('JoinGroup', groupName);
    }
  }

  async leaveGroup(groupName: string): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('LeaveGroup', groupName);
    }
  }

  // Utility methods
  get connectionState(): HubConnectionState {
    return this.connection?.state ?? HubConnectionState.Disconnected;
  }

  get isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  get isConnecting(): boolean {
    return (
      this.connectPromise != null ||
      this.connection?.state === HubConnectionState.Connecting ||
      this.connection?.state === HubConnectionState.Reconnecting
    );
  }

  get connectionId(): string | null {
    return this.connection?.connectionId ?? null;
  }
}

// Export singleton instance
export const signalRService = new SignalRService();
export default signalRService;
