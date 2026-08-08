import { apiService } from '../api.service';
import type {
  AppraisalConversation,
  CompleteConversation,
  ConversationType,
  CreateAppraisalConversation,
  UpdateAppraisalConversation,
} from '@/types/hr/conversations';

/**
 * api/AppraisalConversations — the scheduled meetings that punctuate an appraisal cycle.
 *
 * `getMine` and `getMyDiary` take the employee from the token; nothing on this screen needs to
 * know its own employee id, and passing one is how the earlier version let anybody read anybody's
 * diary. Everything keyed on an id is 403 unless the caller is HR, the appraisee, the appraisee's
 * manager, or whoever scheduled or is holding the meeting.
 *
 * ⚠ `getMyDiary` includes **overdue** conversations — ones whose date has passed and which were
 * never held. Those are the rows a manager most needs, so do not filter them out on the client.
 */
class AppraisalConversationService {
  private readonly baseUrl = '/AppraisalConversations';

  getById(id: string): Promise<AppraisalConversation> {
    return apiService.get<AppraisalConversation>(`${this.baseUrl}/${id}`);
  }

  getByAppraisal(appraisalId: string): Promise<AppraisalConversation[]> {
    return apiService.get<AppraisalConversation[]>(`${this.baseUrl}/by-appraisal/${appraisalId}`);
  }

  getByType(appraisalId: string, type: ConversationType): Promise<AppraisalConversation[]> {
    return apiService.get<AppraisalConversation[]>(
      `${this.baseUrl}/by-appraisal/${appraisalId}/type/${type}`,
    );
  }

  /** Conversations about the signed-in employee. `[]` when the account has no employee link. */
  getMine(): Promise<AppraisalConversation[]> {
    return apiService.get<AppraisalConversation[]>(`${this.baseUrl}/mine`);
  }

  /** Everything the signed-in manager scheduled or is down to hold and has not yet held. */
  getMyDiary(): Promise<AppraisalConversation[]> {
    return apiService.get<AppraisalConversation[]>(`${this.baseUrl}/my-diary`);
  }

  create(data: CreateAppraisalConversation): Promise<AppraisalConversation> {
    return apiService.post<AppraisalConversation>(this.baseUrl, data);
  }

  /** 422 once the conversation has been completed — the notes are then the record. */
  update(id: string, data: UpdateAppraisalConversation): Promise<AppraisalConversation> {
    return apiService.put<AppraisalConversation>(`${this.baseUrl}/${id}`, data);
  }

  delete(id: string): Promise<void> {
    return apiService.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * The only way a conversation closes. Stamps the held date and notifies the employee that the
   * notes are up; an ordinary update cannot do either.
   */
  complete(id: string, data: CompleteConversation): Promise<AppraisalConversation> {
    return apiService.post<AppraisalConversation>(`${this.baseUrl}/${id}/complete`, data);
  }
}

export const appraisalConversationService = new AppraisalConversationService();
