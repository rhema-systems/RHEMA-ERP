import { apiService } from '../api.service';
import type { LocationContact, UpsertLocationContact } from '@/types/hr/location';

/**
 * Contacts at a location.
 *
 * ⚠ The update takes the row's `id` in the BODY as well as the route, and refuses with a bare
 * "ID mismatch" when they differ — the id is inherited from `UpdateDtoBase`, so it does not appear
 * in the DTO's own field list and a form written from those fields alone fails with no clue why.
 * Established by `probe-lane2-groupA.mjs`.
 */
class LocationContactService {
  private readonly baseUrl = '/LocationContact';

  getForLocation(locationId: string) {
    return apiService.get<LocationContact[]>(`${this.baseUrl}/location/${locationId}`);
  }

  getPrimary(locationId: string) {
    return apiService.get<LocationContact | null>(`${this.baseUrl}/location/${locationId}/primary`);
  }

  create(payload: UpsertLocationContact) {
    return apiService.post<LocationContact>(this.baseUrl, payload);
  }

  update(id: string, payload: UpsertLocationContact & { id: string }) {
    return apiService.put<LocationContact>(`${this.baseUrl}/${id}`, payload);
  }

  remove(id: string) {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * ⚠ A promotion, not a flag: the server clears whoever held primary first, so the caller must
   * refetch the whole list rather than patching one row in place.
   */
  setPrimary(id: string) {
    return apiService.patch<void>(`${this.baseUrl}/${id}/set-primary`, {});
  }
}

export const locationContactService = new LocationContactService();
