import { apiService } from '../api.service';
import type {
  OrientationCategory,
  OrientationCategoryLookup,
  OrientationCategoryCreateRequest,
  OrientationCategoryUpdateRequest,
  OrientationNotification,
  OrientationNotificationCreateRequest,
  OrientationDashboard,
} from '@/types/hr/orientation';

/**
 * Orientation categories. Backend route: api/orientation-categories. HR-only — this is catalogue
 * taxonomy, not a participant surface.
 */
class OrientationCategoryService {
  private readonly baseUrl = '/orientation-categories';

  getAll(): Promise<OrientationCategory[]> {
    return apiService.get<OrientationCategory[]>(this.baseUrl);
  }

  getActive(): Promise<OrientationCategory[]> {
    return apiService.get<OrientationCategory[]>(`${this.baseUrl}/active`);
  }

  /** Top-level categories, each with its immediate children. */
  getRoot(): Promise<OrientationCategory[]> {
    return apiService.get<OrientationCategory[]>(`${this.baseUrl}/root`);
  }

  /** Flat id/name list for pickers. */
  getLookup(): Promise<OrientationCategoryLookup[]> {
    return apiService.get<OrientationCategoryLookup[]>(`${this.baseUrl}/lookup`);
  }

  getById(id: string): Promise<OrientationCategory> {
    return apiService.get<OrientationCategory>(`${this.baseUrl}/${id}`);
  }

  getChildren(parentId: string): Promise<OrientationCategory[]> {
    return apiService.get<OrientationCategory[]>(`${this.baseUrl}/${parentId}/children`);
  }

  create(data: OrientationCategoryCreateRequest): Promise<OrientationCategory> {
    return apiService.post<OrientationCategory>(this.baseUrl, data);
  }

  update(id: string, data: OrientationCategoryUpdateRequest): Promise<OrientationCategory> {
    return apiService.put<OrientationCategory>(`${this.baseUrl}/${id}`, data);
  }

  /** Refused with 422 while the category still has programs or sub-categories. */
  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/**
 * Orientation notifications. Backend route: api/orientation-notifications.
 * The `mine` endpoints are the caller's own inbox; anything naming another recipient is HR-only.
 */
class OrientationNotificationService {
  private readonly baseUrl = '/orientation-notifications';

  /** HR-only. For the signed-in user use `getMine()`. */
  getByRecipient(recipientEmployeeId: string, unreadOnly = false): Promise<OrientationNotification[]> {
    return apiService.get<OrientationNotification[]>(
      `${this.baseUrl}/recipient/${recipientEmployeeId}`,
      { unreadOnly },
    );
  }

  getMine(unreadOnly = false): Promise<OrientationNotification[]> {
    return apiService.get<OrientationNotification[]>(`${this.baseUrl}/mine`, { unreadOnly });
  }

  getMyUnreadCount(): Promise<number> {
    return apiService.get<number>(`${this.baseUrl}/mine/unread-count`);
  }

  getByEnrollment(enrollmentId: string): Promise<OrientationNotification[]> {
    return apiService.get<OrientationNotification[]>(`${this.baseUrl}/enrollment/${enrollmentId}`);
  }

  create(data: OrientationNotificationCreateRequest): Promise<OrientationNotification> {
    return apiService.post<OrientationNotification>(this.baseUrl, data);
  }

  /** Own inbox only — an id belonging to someone else comes back as 404, not 403. */
  markAsRead(id: string): Promise<void> {
    return apiService.post<void>(`${this.baseUrl}/${id}/read`);
  }

  markAllAsRead(): Promise<{ message: string; count: number }> {
    return apiService.post<{ message: string; count: number }>(`${this.baseUrl}/mine/read-all`);
  }
}

/** Orientation dashboard roll-ups. Backend route: api/orientation-dashboard. HR-only. */
class OrientationDashboardService {
  getDashboard(): Promise<OrientationDashboard> {
    return apiService.get<OrientationDashboard>('/orientation-dashboard');
  }
}

export const orientationCategoryService = new OrientationCategoryService();
export const orientationNotificationService = new OrientationNotificationService();
export const orientationDashboardService = new OrientationDashboardService();
