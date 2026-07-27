import type { SupplierEvidenceReadiness } from './procurement-supplier-evidence-pack';

export type SupplierApplicantChannel = 'Email' | 'Sms';
export type SupplierRegistrationCategory = 'Goods' | 'Works' | 'Services';

export interface SupplierApplicantIssueResult {
  registrationId: string;
  registrationNumber: string;
  tokenId: string;
  tokenReference: string;
  applicationToken: string;
  feeMode: 'Free' | 'Paid' | number;
  tokenStatus: 'AwaitingPayment' | 'Active' | 'Expired' | number;
  paymentStatus: string | number;
  totalAmount: number;
  currencyCode: string;
  deliveryStatus: string;
  message: string;
}

export interface SupplierApplicantSessionResult {
  sessionToken: string;
  expiresAtUtc: string;
  paymentOnly: boolean;
  registrationId: string;
}

export interface SupplierApplicantDocument {
  id: string;
  documentType: string;
  documentName: string;
  fileSize: number;
  mimeType?: string;
  evidenceRequirementCode?: string;
  classificationCode?: string;
  issuedAtUtc?: string;
  expiresAtUtc?: string;
  isVerified: boolean;
  isRejected: boolean;
  rejectionReason?: string;
  uploadedAt: string;
}

export interface SupplierApplicantStatusHistory {
  id: string;
  fromStatus?: string;
  toStatus: string;
  changedAt: string;
  notes?: string;
}

export interface SupplierApplicantPortal {
  registrationId: string;
  tokenId: string;
  registrationNumber: string;
  companyName: string;
  email?: string;
  phone?: string;
  partnerType: string;
  registrationCategory?: SupplierRegistrationCategory;
  status: string;
  registrationData?: string;
  rejectionReason?: string;
  tokenStatus: string | number;
  paymentStatus: string | number;
  feeMode: string | number;
  totalAmount: number;
  currencyCode: string;
  tokenRowVersion: string;
  canEdit: boolean;
  canSubmit: boolean;
  paymentOnly: boolean;
  documents: SupplierApplicantDocument[];
  statusHistory: SupplierApplicantStatusHistory[];
  evidenceReadiness?: SupplierEvidenceReadiness;
}

export interface SupplierApplicantPaymentMethod {
  id: string;
  code: string;
  name: string;
  requiresReference: boolean;
  isPostingReady: boolean;
}

export interface SupplierApplicantAccessSummary {
  totalApplications: number;
  applicationInProgress: number;
  pendingCredentialDelivery: number;
  credentialDelivered: number;
  activated: number;
  rejected: number;
  activationFailed: number;
}

export interface SupplierApplicantAccessHistory {
  id: string;
  registrationId: string;
  registrationNumber: string;
  companyName: string;
  verifiedChannel: string;
  verifiedContactMasked: string;
  status: string | number;
  loginIdentifier?: string;
  verifiedAtUtc: string;
  temporaryCredentialExpiresAtUtc?: string;
  credentialActivatedAtUtc?: string;
  notificationAttemptCount: number;
  lastNotificationStatus?: string;
}
