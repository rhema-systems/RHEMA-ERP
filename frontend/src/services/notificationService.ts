import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Notification interfaces
export interface Notification {
  id: string;
  userId: string;
  title: string;
  message: string;
  type: 'Info' | 'Warning' | 'Error' | 'Success';
  category: 'JobCard' | 'WorkOrder' | 'Quality' | 'Asset' | 'System';
  entityId?: string;
  entityType?: string;
  actionUrl?: string;
  isRead: boolean;
  readAt?: string;
  createdAt: string;
  expiresAt?: string;
  metadata?: Record<string, any>;
}

export interface CreateNotificationRequest {
  userIds?: string[];
  roles?: string[];
  title: string;
  message: string;
  type: 'Info' | 'Warning' | 'Error' | 'Success';
  category: 'JobCard' | 'WorkOrder' | 'Quality' | 'Asset' | 'System';
  entityId?: string;
  entityType?: string;
  actionUrl?: string;
  expiresAt?: string;
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

  // Notifications CRUD
  async getNotifications(params: {
    page?: number;
    pageSize?: number;
    category?: string;
    type?: string;
    isRead?: boolean;
    fromDate?: string;
    toDate?: string;
  } = {}): Promise<PagedResult<Notification>> {
    const response = await axios.get(`${API_URL}/notifications`, {
      params,
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

  // Workflow-specific notification methods
  async notifyJobCardSubmission(jobCardId: string, jobCardNumber: string): Promise<void> {
    await this.createNotification({
      roles: ['MaintenanceManager', 'MaintenanceSupervisor'],
      title: 'New Job Card Submitted',
      message: `Job Card ${jobCardNumber} has been submitted and requires approval.`,
      type: 'Info',
      category: 'JobCard',
      entityId: jobCardId,
      entityType: 'JobCard',
      actionUrl: `/maintenance/job-cards?id=${jobCardId}`
    });
  }

  async notifyJobCardApproval(jobCardId: string, jobCardNumber: string, requesterId: string, approved: boolean): Promise<void> {
    await this.createNotification({
      userIds: [requesterId],
      title: `Job Card ${approved ? 'Approved' : 'Rejected'}`,
      message: `Your Job Card ${jobCardNumber} has been ${approved ? 'approved' : 'rejected'}.`,
      type: approved ? 'Success' : 'Warning',
      category: 'JobCard',
      entityId: jobCardId,
      entityType: 'JobCard',
      actionUrl: `/maintenance/job-cards?id=${jobCardId}`
    });
  }

  async notifyWorkOrderGenerated(workOrderId: string, workOrderNumber: string, jobCardNumber: string, technicianId?: string): Promise<void> {
    const recipients: string[] = [];
    if (technicianId) recipients.push(technicianId);

    await this.createNotification({
      userIds: recipients,
      roles: ['MaintenanceTechnician'],
      title: 'New Work Order Generated',
      message: `Work Order ${workOrderNumber} has been generated from Job Card ${jobCardNumber}.`,
      type: 'Info',
      category: 'WorkOrder',
      entityId: workOrderId,
      entityType: 'WorkOrder',
      actionUrl: `/maintenance/work-orders?id=${workOrderId}`
    });
  }

  async notifyAssetAdmission(admissionId: string, admissionNumber: string, assetName: string, technicianId?: string): Promise<void> {
    const recipients: string[] = [];
    if (technicianId) recipients.push(technicianId);

    await this.createNotification({
      userIds: recipients,
      roles: ['MaintenanceTechnician', 'MaintenanceManager'],
      title: 'Asset Admitted for Maintenance',
      message: `${assetName} has been admitted for maintenance (${admissionNumber}).`,
      type: 'Info',
      category: 'Asset',
      entityId: admissionId,
      entityType: 'AssetAdmission',
      actionUrl: `/maintenance/asset-admission?id=${admissionId}`
    });
  }

  async notifyQualityControlRequired(workOrderId: string, workOrderNumber: string, assetName: string): Promise<void> {
    await this.createNotification({
      roles: ['QualityInspector', 'MaintenanceManager'],
      title: 'Quality Control Required',
      message: `Work Order ${workOrderNumber} for ${assetName} requires quality control inspection.`,
      type: 'Warning',
      category: 'Quality',
      entityId: workOrderId,
      entityType: 'WorkOrder',
      actionUrl: `/maintenance/quality-control?workOrderId=${workOrderId}`
    });
  }

  async notifyWorkOrderCompletion(workOrderId: string, workOrderNumber: string, assetName: string, requesterId?: string): Promise<void> {
    const recipients: string[] = [];
    if (requesterId) recipients.push(requesterId);

    await this.createNotification({
      userIds: recipients,
      roles: ['MaintenanceManager'],
      title: 'Work Order Completed',
      message: `Work Order ${workOrderNumber} for ${assetName} has been completed successfully.`,
      type: 'Success',
      category: 'WorkOrder',
      entityId: workOrderId,
      entityType: 'WorkOrder',
      actionUrl: `/maintenance/work-orders?id=${workOrderId}`
    });
  }

  async notifyAssetDischarge(dischargeId: string, dischargeNumber: string, assetName: string, requesterId?: string): Promise<void> {
    const recipients: string[] = [];
    if (requesterId) recipients.push(requesterId);

    await this.createNotification({
      userIds: recipients,
      roles: ['MaintenanceManager'],
      title: 'Asset Discharged',
      message: `${assetName} has been discharged from maintenance (${dischargeNumber}).`,
      type: 'Success',
      category: 'Asset',
      entityId: dischargeId,
      entityType: 'AssetDischarge',
      actionUrl: `/maintenance/asset-admission?dischargeId=${dischargeId}`
    });
  }

  async notifyOverdueWorkOrder(workOrderId: string, workOrderNumber: string, assetName: string, technicianId?: string): Promise<void> {
    const recipients: string[] = [];
    if (technicianId) recipients.push(technicianId);

    await this.createNotification({
      userIds: recipients,
      roles: ['MaintenanceManager', 'MaintenanceSupervisor'],
      title: 'Work Order Overdue',
      message: `Work Order ${workOrderNumber} for ${assetName} is overdue and requires attention.`,
      type: 'Error',
      category: 'WorkOrder',
      entityId: workOrderId,
      entityType: 'WorkOrder',
      actionUrl: `/maintenance/work-orders?id=${workOrderId}`
    });
  }

  // Real-time notifications (if WebSocket is available)
  private socket: WebSocket | null = null;
  private notificationHandlers: Array<(notification: Notification) => void> = [];

  connectToRealTimeNotifications(): void {
    const token = localStorage.getItem('authToken');
    if (!token) return;

    try {
      const wsUrl = `ws://localhost:5000/notifications?token=${token}`;
      this.socket = new WebSocket(wsUrl);

      this.socket.onopen = () => {
        console.log('Connected to notification service');
      };

      this.socket.onmessage = (event) => {
        try {
          const notification: Notification = JSON.parse(event.data);
          this.notificationHandlers.forEach(handler => handler(notification));
        } catch (error) {
          console.error('Error parsing notification:', error);
        }
      };

      this.socket.onclose = () => {
        console.log('Disconnected from notification service');
        // Attempt to reconnect after 5 seconds
        setTimeout(() => this.connectToRealTimeNotifications(), 5000);
      };

      this.socket.onerror = (error) => {
        console.error('WebSocket error:', error);
      };
    } catch (error) {
      console.error('Failed to connect to notification service:', error);
    }
  }

  disconnectFromRealTimeNotifications(): void {
    if (this.socket) {
      this.socket.close();
      this.socket = null;
    }
  }

  onNotification(handler: (notification: Notification) => void): () => void {
    this.notificationHandlers.push(handler);
    
    // Return unsubscribe function
    return () => {
      const index = this.notificationHandlers.indexOf(handler);
      if (index > -1) {
        this.notificationHandlers.splice(index, 1);
      }
    };
  }

  // Utility methods
  getNotificationIcon(type: Notification['type']): string {
    const icons = {
      'Info': '🔔',
      'Warning': '⚠️',
      'Error': '🚨',
      'Success': '✅'
    };
    return icons[type] || '🔔';
  }

  getNotificationColor(type: Notification['type']): string {
    const colors = {
      'Info': 'blue',
      'Warning': 'yellow',
      'Error': 'red',
      'Success': 'green'
    };
    return colors[type] || 'gray';
  }

  formatNotificationTime(createdAt: string): string {
    const date = new Date(createdAt);
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
  }
}

export const notificationService = new NotificationService();
export default notificationService;