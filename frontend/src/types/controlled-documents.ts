export interface ControlledDocumentIssueSummary {
  originalIssued: boolean;
  originalIssuedAtUtc?: string;
  originalIssuedByName?: string;
  replacementCount: number;
  totalIssued: number;
  lastIssuedAtUtc?: string;
  lastIssuedByName?: string;
  issues: ControlledDocumentIssueItem[];
}

export interface ControlledDocumentIssueItem {
  id: string;
  documentType: string;
  copyNumber: number;
  copyType: ControlledDocumentCopyType;
  replacementReason?: string;
  issuedAtUtc: string;
  issuedByName: string;
  contentSha256: string;
  fileName: string;
  retained: boolean;
  retainUntilUtc?: string;
}

export type ControlledDocumentCopyType = 'Original' | 'Replacement';
