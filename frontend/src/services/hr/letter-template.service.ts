import { apiService } from '../api.service';
import type {
  HrLetterTemplate,
  HrLetterTemplatePreview,
  HrLetterTemplateSummary,
  HrLetterTemplateTestSendResult,
} from '@/types/hr/letter-templates';

/**
 * HR letter & email templates (round 4, lane N). Backend route: api/hr/letter-templates.
 * Reading and previewing need HR.Company.Read; saving, resetting and a test send need HR.Company.Write.
 */
class LetterTemplateService {
  private readonly baseUrl = '/hr/letter-templates';

  private path(module: string, eventKey: string) {
    return `${this.baseUrl}/${encodeURIComponent(module)}/${encodeURIComponent(eventKey)}`;
  }

  list(): Promise<HrLetterTemplateSummary[]> {
    return apiService.get<HrLetterTemplateSummary[]>(this.baseUrl);
  }

  get(module: string, eventKey: string): Promise<HrLetterTemplate> {
    return apiService.get<HrLetterTemplate>(this.path(module, eventKey));
  }

  save(module: string, eventKey: string, subject: string, htmlBody: string): Promise<HrLetterTemplate> {
    return apiService.put<HrLetterTemplate>(this.path(module, eventKey), { subject, htmlBody });
  }

  /** Unsaved wording rendered with sample values, and what a save would be refused for. Changes nothing. */
  preview(module: string, eventKey: string, subject: string, htmlBody: string): Promise<HrLetterTemplatePreview> {
    return apiService.post<HrLetterTemplatePreview>(`${this.path(module, eventKey)}/preview`, { subject, htmlBody });
  }

  /** Emails a sample to the signed-in officer themselves. */
  testSend(module: string, eventKey: string, subject: string, htmlBody: string): Promise<HrLetterTemplateTestSendResult> {
    return apiService.post<HrLetterTemplateTestSendResult>(`${this.path(module, eventKey)}/test-send`, { subject, htmlBody });
  }

  reset(module: string, eventKey: string): Promise<HrLetterTemplate> {
    return apiService.post<HrLetterTemplate>(`${this.path(module, eventKey)}/reset`, {});
  }
}

export const letterTemplateService = new LetterTemplateService();
