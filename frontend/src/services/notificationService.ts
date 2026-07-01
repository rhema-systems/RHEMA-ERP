import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

// Notification interfaces - Aligned with unified backend API
export interface Notification {
  id: string;
  recipientId: string;
  notificationType: 'Email' | 'SMS' | 'Push' | 'InApp';
  title: string;
  message: string;
  priority: 'Low' | 'Normal' | 'High' | 'Critical';
  status: 'Pending' | 'Sent' | 'Failed' | 'Expired' | 'Cancelled';
  entityId: string;
  entityType: string;
  actionUrl?: string;
  isRead: boolean;
  readAt?: string;
  dismissedAt?: string;
  timestamp: string; // Backend sends this as the creation timestamp
  createdAt?: string; // For backwards compatibility
  scheduledFor?: string;
  sentAt?: string;
  expiresAt?: string;
  attemptCount: number;
  lastError?: string;
  deliveryMethods?: string[];
  emailAddress?: string;
  phoneNumber?: string;
  additionalData?: Record<string, any>;
  tenantId: string;
}

export interface CreateNotificationRequest {
  recipientId: string;
  type: 'Email' | 'SMS' | 'Push' | 'InApp';
  title: string;
  message: string;
  priority: 'Low' | 'Normal' | 'High' | 'Critical';
  entityType: string; // Required - e.g., 'JobCard', 'WorkOrder'
  entityId: string; // Required - specific entity instance ID
  actionUrl?: string;
  metadata?: Record<string, any>;
}

export interface NotificationPreference {
  userId: string;
  category: string;
  emailEnabled: boolean;
  inAppEnabled: boolean;
  smsEnabled: boolean;
  pushEnabled: boolean;
  digestFrequency: 'Immediate' | 'Hourly' | 'Daily' | 'Weekly';
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

class NotificationService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    };
  }

  // Notifications CRUD - Uses unified endpoint
  async getNotifications(params: {
    page?: number;
    pageSize?: number;
    status?: string;
    priority?: string;
    notificationType?: string;
    isRead?: boolean;
    fromDate?: string;
    toDate?: string;
    entityType?: string;
  } = {}): Promise<PagedResult<Notification>> {
    const response = await axios.get(`${API_URL}/notifications`, {
      params: {
        pageNumber: params.page || 1,
        pageSize: params.pageSize || 10,
        ...params
      },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getNotificationById(id: string): Promise<Notification> {
    const response = await axios.get(`${API_URL}/notifications/${id}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createNotification(data: CreateNotificationRequest): Promise<Notification> {
    const response = await axios.post(`${API_URL}/notifications`, data, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async markAsRead(notificationId: string): Promise<void> {
    await axios.post(`${API_URL}/notifications/${notificationId}/mark-read`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async markAllAsRead(): Promise<void> {
    await axios.post(`${API_URL}/notifications/mark-all-read`, {}, {
      headers: this.getAuthHeaders()
    });
  }

  async deleteNotification(id: string): Promise<void> {
    await axios.delete(`${API_URL}/notifications/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Unread notifications
  async getUnreadCount(): Promise<number> {
    const response = await axios.get(`${API_URL}/notifications/unread-count`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async getUnreadNotifications(limit: number = 10): Promise<Notification[]> {
    const response = await axios.get(`${API_URL}/notifications/unread`, {
      params: { limit },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Preferences
  async getNotificationPreferences(): Promise<NotificationPreference[]> {
    const response = await axios.get(`${API_URL}/notifications/preferences`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateNotificationPreference(
    category: string, 
    preferences: Partial<NotificationPreference>
  ): Promise<NotificationPreference> {
    const response = await axios.put(`${API_URL}/notifications/preferences/${category}`, preferences, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Generic notification methods using unified API
  // For specific domain workflows, use generic methods with appropriate entityType/entityId
  
  async sendNotification(type: 'Email' | 'SMS' | 'Push' | 'InApp', recipientId: string, title: string, message: string, options?: {
    priority?: 'Low' | 'Normal' | 'High' | 'Critical';
    entityType?: string;
    entityId?: string;
    actionUrl?: string;
    metadata?: Record<string, any>;
  }): Promise<void> {
    await this.createNotification({
      recipientId,
      type,
      title,
      message,
      priority: options?.priority || 'Normal',
      entityType: options?.entityType || 'General',
      entityId: options?.entityId || 'system',
      actionUrl: options?.actionUrl,
      metadata: options?.metadata
    });
  }

  // Real-time notifications are handled by SignalR service
  // See useSignalR hook and signalRService for real-time functionality

  // Utility methods
  getNotificationIcon(priority: Notification['priority']): string {
    const icons = {
      'Low': '📝',
      'Normal': '🔔',
      'High': '⚠️',
      'Critical': '🚨'
    };
    return icons[priority] || '🔔';
  }

  getNotificationColor(priority: Notification['priority']): string {
    const colors = {
      'Low': 'gray',
      'Normal': 'blue',
      'High': 'yellow',
      'Critical': 'red'
    };
    return colors[priority] || 'gray';
  }

  getStatusColor(status: Notification['status']): string {
    const colors = {
      'Pending': 'yellow',
      'Sent': 'green',
      'Failed': 'red',
      'Expired': 'gray',
      'Cancelled': 'gray'
    };
    return colors[status] || 'gray';
  }

  formatNotificationTime(dateString?: string): string {
    try {
      // Handle missing date
      if (!dateString) return 'Unknown';
      
      const date = new Date(dateString);
      if (isNaN(date.getTime())) return 'Unknown';
      
      const now = new Date();
      const diffMs = now.getTime() - date.getTime();
      const diffMins = Math.floor(diffMs / 60000);
      const diffHours = Math.floor(diffMins / 60);
      const diffDays = Math.floor(diffHours / 24);

      if (diffMins < 1) return 'Just now';
      if (diffMins < 60) return `${diffMins}m ago`;
      if (diffHours < 24) return `${diffHours}h ago`;
      if (diffDays < 7) return `${diffDays}d ago`;
      return date.toLocaleDateString();
    } catch (error) {
      console.error('Error formatting notification time:', error);
      return 'Unknown';
    }
  }
}

export const notificationService = new NotificationService();
export default notificationService;
