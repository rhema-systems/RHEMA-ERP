// HR letter & email templates (round 4, lane N). Backend: api/hr/letter-templates — every email the HR
// modules send, listed from their catalogues; a tenant's own wording where HR has chosen one.

export interface HrLetterTemplateToken {
  token: string;
  description: string;
  sampleValue: string;
  /** Ready-made HTML the system builds itself — may be placed with {{{Token}}}. Everything else is escaped. */
  mayBeRaw: boolean;
}

export interface HrLetterTemplateSummary {
  module: string;
  eventKey: string;
  name: string;
  category: string;
  description: string;
  /** 'Default' — the shipped wording goes out; 'Edited' — the tenant's own. */
  state: 'Default' | 'Edited' | string;
  /** Edited, but still word for word the shipped default. */
  matchesDefault: boolean;
  editedAt?: string | null;
  editedBy?: string | null;
  tokenCount: number;
}

export interface HrLetterTemplate extends HrLetterTemplateSummary {
  subject: string;
  htmlBody: string;
  defaultSubject: string;
  defaultHtmlBody: string;
  tokens: HrLetterTemplateToken[];
  /** Tokens every template may use: the company name, the portal link. */
  commonTokens: HrLetterTemplateToken[];
}

export interface HrLetterTemplatePreview {
  subject: string;
  htmlBody: string;
  /** What a save would be refused for. */
  problems: string[];
}

export interface HrLetterTemplateTestSendResult {
  outcome: 'Sent' | 'NoMailServer' | 'NoAddress' | 'Failed' | 'TimedOut' | string;
  sentTo?: string | null;
  subject: string;
}
