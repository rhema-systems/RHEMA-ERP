import { apiService } from '../api.service';
import type {
  TrainingWaitlistEntry,
  AddToWaitlistRequest,
  OfferWaitlistPositionRequest,
  RespondToWaitlistOfferRequest,
} from '@/types/hr/training-delivery';

/**
 * The queue for a full schedule. Backend route: api/training-waitlist.
 *
 * ⚠ The lifecycle is three distinct steps, deliberately: HR **offers** a freed slot, the employee
 * **responds**, and HR then **promotes** the accepted entry into a nomination. Acceptance on its own
 * does NOT enrol anybody — it only records that the offer was taken. Promotion is where the seat is
 * re-checked and where the enrolment gets a named HR actor for audit.
 */
class TrainingWaitlistService {
  private readonly baseUrl = '/training-waitlist';

  getById(id: string): Promise<TrainingWaitlistEntry> {
    return apiService.get<TrainingWaitlistEntry>(`${this.baseUrl}/${id}`);
  }

  getBySchedule(scheduleId: string): Promise<TrainingWaitlistEntry[]> {
    return apiService.get<TrainingWaitlistEntry[]>(`${this.baseUrl}/schedule/${scheduleId}`);
  }

  getActiveBySchedule(scheduleId: string): Promise<TrainingWaitlistEntry[]> {
    return apiService.get<TrainingWaitlistEntry[]>(`${this.baseUrl}/schedule/${scheduleId}/active`);
  }

  getByEmployee(employeeId: string): Promise<TrainingWaitlistEntry[]> {
    return apiService.get<TrainingWaitlistEntry[]>(`${this.baseUrl}/employee/${employeeId}`);
  }

  add(data: AddToWaitlistRequest): Promise<TrainingWaitlistEntry> {
    return apiService.post<TrainingWaitlistEntry>(this.baseUrl, data);
  }

  remove(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  offerSlot(id: string, data: Omit<OfferWaitlistPositionRequest, 'waitlistId'>): Promise<TrainingWaitlistEntry> {
    return apiService.post<TrainingWaitlistEntry>(`${this.baseUrl}/${id}/offer-slot`, { waitlistId: id, ...data });
  }

  respond(id: string, data: Omit<RespondToWaitlistOfferRequest, 'waitlistId'>): Promise<TrainingWaitlistEntry> {
    return apiService.post<TrainingWaitlistEntry>(`${this.baseUrl}/${id}/respond`, { waitlistId: id, ...data });
  }

  /**
   * Enrols an accepted entry, creating the nomination and returning the entry with
   * createdNominationNumber resolved. Idempotent — calling it twice returns the same nomination.
   * Refuses with 422 if the entry has not been accepted, or if the schedule filled meanwhile.
   */
  promote(id: string): Promise<TrainingWaitlistEntry> {
    return apiService.post<TrainingWaitlistEntry>(`${this.baseUrl}/${id}/promote`, {});
  }
}

export const trainingWaitlistService = new TrainingWaitlistService();
