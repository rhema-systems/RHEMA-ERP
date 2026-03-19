import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
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
  private readonly hubUrl: string;
  private reconnectInterval: NodeJS.Timeout | null = null;
  private isManualDisconnect = false;

  constructor() {
    // Construct hub URL with better debugging
    const baseUrl = process.env.NEXT_PUBLIC_API_URL?.replace('/api', '') || 'http://localhost:5000';
    this.hubUrl = `${baseUrl}/api/hubs/dashboard`;
    console.log('SignalR Service initialized:', {
      NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
      baseUrl,
      hubUrl: this.hubUrl
    });
  }

  // Event handlers
  private dashboardUpdateHandlers: Set<(data: DashboardData) => void> = new Set();
  private notificationHandlers: Set<(notification: Notification) => void> = new Set();
  private userSessionUpdateHandlers: Set<(update: UserSessionUpdate) => void> = new Set();
  private connectionStateChangeHandlers: Set<(state: HubConnectionState) => void> = new Set();

  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    const token = getStoredToken();
    if (!token) {
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
        accessTokenFactory: () => token,
        withCredentials: false, // Changed from true to false
        // Enable transport fallback: WebSockets -> ServerSentEvents -> LongPolling
        transport: undefined, // Let SignalR choose the best transport
        skipNegotiation: false, // Keep negotiation to determine best transport
        // Add timeout configurations
        timeout: 30000, // 30 seconds
        headers: {
          'Authorization': `Bearer ${token}` // Add explicit authorization header
        }
      })
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // More conservative retry strategy
          if (retryContext.previousRetryCount === 0) return 2000; // 2 seconds
          if (retryContext.previousRetryCount === 1) return 5000; // 5 seconds
          if (retryContext.previousRetryCount === 2) return 10000; // 10 seconds
          return Math.min(15000 + (retryContext.previousRetryCount * 5000), 30000); // Cap at 30s
        }
      })
      .build();

    // Set up event handlers
    this.setupEventHandlers();

    try {
      this.isManualDisconnect = false;
      console.log(`Attempting to connect to SignalR hub: ${this.hubUrl}`);
      
      await this.connection.start();
      
      console.log(`SignalR connection established successfully. ConnectionId: ${this.connection.connectionId}`);
      
      this.notifyConnectionStateChange(this.connection.state);
      
      // Clear any reconnection intervals since auto-reconnect is handled by SignalR
      if (this.reconnectInterval) {
        clearInterval(this.reconnectInterval);
        this.reconnectInterval = null;
      }
    } catch (error) {
      console.error('SignalR connection failed:', error);
      
      // Log additional debugging information
      if (error instanceof Error) {
        console.error('Error details:', {
          name: error.name,
          message: error.message,
          stack: error.stack
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

  async disconnect(): Promise<void> {
    this.isManualDisconnect = true;
    
    if (this.reconnectInterval) {
      clearInterval(this.reconnectInterval);
      this.reconnectInterval = null;
    }

    if (this.connection) {
      try {
        await this.connection.stop();
        console.log('SignalR connection stopped');
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
      console.log('Received dashboard update:', data);
      this.dashboardUpdateHandlers.forEach(handler => {
        try {
          handler(data);
        } catch (error) {
          console.error('Error in dashboard update handler:', error);
        }
      });
    });

    // Notification events
    this.connection.on('NewNotification', (notification: Notification) => {
      console.log('Received new notification:', notification);
      this.notificationHandlers.forEach(handler => {
        try {
          handler(notification);
        } catch (error) {
          console.error('Error in notification handler:', error);
        }
      });
    });

    // User session update events
    this.connection.on('UserSessionUpdate', (update: UserSessionUpdate) => {
      console.log('Received user session update:', update);
      this.userSessionUpdateHandlers.forEach(handler => {
        try {
          handler(update);
        } catch (error) {
          console.error('Error in user session update handler:', error);
        }
      });
    });

    // Connection state change events
    this.connection.onclose(async (error) => {
      console.log('SignalR connection closed:', error);
      this.notifyConnectionStateChange(HubConnectionState.Disconnected);
      
      if (!this.isManualDisconnect) {
        console.log('Attempting to reconnect...');
        // Auto-reconnect is handled by SignalR, but we can add custom logic if needed
      }
    });

    this.connection.onreconnecting((error) => {
      console.log('SignalR reconnecting:', error);
      this.notifyConnectionStateChange(HubConnectionState.Reconnecting);
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected with connection ID:', connectionId);
      this.notifyConnectionStateChange(HubConnectionState.Connected);
    });
  }

  private notifyConnectionStateChange(state: HubConnectionState): void {
    this.connectionStateChangeHandlers.forEach(handler => {
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

  onUserSessionUpdate(handler: (update: UserSessionUpdate) => void): () => void {
    this.userSessionUpdateHandlers.add(handler);
    return () => this.userSessionUpdateHandlers.delete(handler);
  }

  onConnectionStateChange(handler: (state: HubConnectionState) => void): () => void {
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

  get connectionId(): string | null {
    return this.connection?.connectionId ?? null;
  }
}

// Export singleton instance
export const signalRService = new SignalRService();
export default signalRService;
