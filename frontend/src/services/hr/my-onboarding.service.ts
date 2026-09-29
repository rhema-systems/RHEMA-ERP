import { apiService } from '../api.service';
import type {
  CompleteMyOnboardingTaskRequest,
  MyOnboarding,
  MyOnboardingTask,
} from '@/types/hr/onboarding';

/**
 * Onboarding as the signed-in employee sees it (round 4, lane K-b2). Backend route:
 * api/employee-portal/onboarding — always the caller's own, from the token; no id to get wrong.
 */
class MyOnboardingService {
  private readonly baseUrl = '/employee-portal/onboarding';

  /** Their own plan as a new hire, the tasks given to them, and the new hires they are buddy to. */
  getMine(): Promise<MyOnboarding> {
    return apiService.get<MyOnboarding>(this.baseUrl);
  }

  /** Marks a task given to them done. One that needs signing off waits for its coordinator. */
  markDone(taskId: string, data: CompleteMyOnboardingTaskRequest): Promise<MyOnboardingTask> {
    return apiService.post<MyOnboardingTask>(`${this.baseUrl}/tasks/${taskId}/complete`, data);
  }
}

export const myOnboardingService = new MyOnboardingService();
