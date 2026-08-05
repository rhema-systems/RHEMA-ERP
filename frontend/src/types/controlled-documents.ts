export interface ControlledDocumentIssueSummary {
  originalIssued: boolean;
  originalIssuedAtUtc?: string;
  originalIssuedByName?: string;
  replacementCount: number;
  totalIssued: number;
  lastIssuedAtUtc?: string;
  lastIssuedByName?: string;
}

export type ControlledDocumentCopyType = 'Original' | 'Replacement';
