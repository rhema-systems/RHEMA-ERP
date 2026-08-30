export type CivilEngineeringQualityTestCategoryOption = { value: number; label: string };
export type CivilEngineeringQualityTestLookupOption = { id: string; label: string };
export type CivilEngineeringQualityTestDocument = {
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  documentReference: string;
  title: string;
  versionNumber: string;
};

export type CivilEngineeringQualityTestLookups = {
  testCategories: CivilEngineeringQualityTestCategoryOption[];
  sourceTypes: string[];
  resultStatuses: string[];
  reviewers: CivilEngineeringQualityTestLookupOption[];
  sourcePartners: CivilEngineeringQualityTestLookupOption[];
  projectPackages: CivilEngineeringQualityTestLookupOption[];
  paymentCertificates: CivilEngineeringQualityTestLookupOption[];
  documents: CivilEngineeringQualityTestDocument[];
  requiresEndorsementEvidence: boolean;
};

export type CivilEngineeringQualityTestReport = {
  id: string;
  projectId: string;
  projectPackageId?: string | null;
  projectPackageName?: string | null;
  projectPaymentCertificateId?: string | null;
  paymentCertificateNumber?: string | null;
  testCategory: number;
  testCategoryLabel: string;
  sourceType: string;
  sourceBusinessPartnerId?: string | null;
  sourceBusinessPartnerName?: string | null;
  reportReference: string;
  testedAt: string;
  resultStatus: string;
  resultSummary: string;
  reviewerUserId: string;
  reviewerName: string;
  status: string;
  approvalStatus: string;
  acceptanceBlocked: boolean;
  rejectionReason?: string | null;
  workflowInstanceId?: string | null;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  endorsementDocumentRecordId?: string | null;
  endorsementDocumentVersionId?: string | null;
  rowVersion: string;
};

export type CreateCivilEngineeringQualityTestReportRequest = {
  clientRequestId: string;
  reportReference: string;
  testCategory: number;
  sourceType: string;
  sourceBusinessPartnerId?: string | null;
  testedAt: string;
  resultStatus: string;
  resultSummary: string;
  reviewerUserId: string;
  projectPhaseId?: string | null;
  projectPackageId?: string | null;
  projectPaymentCertificateId?: string | null;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
};

export type ProcessCivilEngineeringQualityTestReportRequest = {
  clientRequestId: string;
  rowVersion: string;
  approve: boolean;
  reason: string;
  endorsementDocumentRecordId?: string | null;
  endorsementDocumentVersionId?: string | null;
};
