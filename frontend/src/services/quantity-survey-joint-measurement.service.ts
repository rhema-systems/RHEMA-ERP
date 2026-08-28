import { apiService } from '@/services/api.service';

const internalRoot = '/quantity-survey/joint-measurements';
const externalRoot = (projectId: string) =>
  `/projects/external/my-projects/${projectId}/joint-measurements`;

export type JointMeasurementEvidenceType =
  | 'RequestEvidence'
  | 'SitePhoto'
  | 'AttendanceRecord'
  | 'SignedMeasurementRecord'
  | 'SupportingDocument';

export interface JointMeasurementLookup {
  id: string;
  label: string;
  group?: string | null;
  description?: string | null;
}

export interface JointMeasurementLookups {
  approvedBoqLines: JointMeasurementLookup[];
  contractorPartners: JointMeasurementLookup[];
  consultantPartners: JointMeasurementLookup[];
  recordedMeasurements: JointMeasurementLookup[];
}

export interface JointMeasurementParticipant {
  id: string;
  participantType: 'Contractor' | 'Consultant' | 'InternalRole';
  businessPartnerId?: string | null;
  businessPartnerName?: string | null;
  requiredRoleId?: string | null;
  requiredRoleName?: string | null;
  isRequired: boolean;
  attendanceStatus: string;
  attendedByName?: string | null;
  attendedAt?: string | null;
  attendanceNotes?: string | null;
}

export interface JointMeasurementEvidence {
  id: string;
  evidenceType: JointMeasurementEvidenceType | number;
  title: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  checksumSha256: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  uploadedByName: string;
  uploadedAt: string;
}

export interface JointMeasurementEndorsement {
  id: string;
  signerType: string;
  businessPartnerId?: string | null;
  businessPartnerName?: string | null;
  signedByName: string;
  signatureMethod: number | string;
  attestation: string;
  certificateThumbprint?: string | null;
  externalSignatureReference?: string | null;
  notes?: string | null;
  signedAt: string;
}

export interface JointMeasurement {
  id: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectBoqVersionId: string;
  projectBoqVersionNumber: number;
  projectBoqVersionLineId: string;
  boqLineKey: string;
  boqLineLabel: string;
  unitOfMeasure?: string | null;
  contractorBusinessPartnerId: string;
  contractorName: string;
  consultantBusinessPartnerId?: string | null;
  consultantName?: string | null;
  requestNumber: string;
  title: string;
  reason: string;
  previousQuantity: number;
  contractorProposedQuantity?: number | null;
  recordedQuantity?: number | null;
  requestedSiteLocation?: string | null;
  preferredStartAt?: string | null;
  preferredEndAt?: string | null;
  status: string;
  approvalStatus: string;
  scheduledStartAt?: string | null;
  scheduledEndAt?: string | null;
  scheduledSiteLocation?: string | null;
  measurementSheetId?: string | null;
  measurementSheetReference?: string | null;
  workflowInstanceId?: string | null;
  remeasurementVersionId?: string | null;
  remeasurementVersionNumber?: number | null;
  contractorSignatureRequired: boolean;
  consultantSignatureRequired: boolean;
  evidenceRequired: boolean;
  endorsementAttestation: string;
  requestedByName: string;
  requestedAt: string;
  submittedAt?: string | null;
  approvedAt?: string | null;
  appliedAt?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
  participants: JointMeasurementParticipant[];
  evidence: JointMeasurementEvidence[];
  endorsements: JointMeasurementEndorsement[];
}

export interface JointMeasurementPage {
  items: JointMeasurement[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface JointMeasurementRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson: string;
  createdAt: string;
}

export interface JointMeasurementCreateRequest {
  clientRequestId: string;
  projectId: string;
  projectBoqVersionLineId: string;
  contractorBusinessPartnerId?: string;
  title: string;
  reason: string;
  contractorProposedQuantity?: number;
  requestedSiteLocation?: string;
  preferredStartAt?: string;
  preferredEndAt?: string;
}

const root = (projectId: string, external: boolean) =>
  external ? externalRoot(projectId) : internalRoot;

export const quantitySurveyJointMeasurementService = {
  lookups: (projectId: string, external = false) =>
    apiService.get<JointMeasurementLookups>(
      external ? `${externalRoot(projectId)}/lookups` : `${internalRoot}/lookups`,
      external ? undefined : { projectId }
    ),
  list: (projectId: string, external = false) =>
    apiService.get<JointMeasurementPage>(
      root(projectId, external),
      external ? undefined : { projectId, page: 1, pageSize: 100 }
    ),
  get: (projectId: string, id: string, external = false) =>
    apiService.get<JointMeasurement>(`${root(projectId, external)}/${id}`),
  create: (projectId: string, request: JointMeasurementCreateRequest, external = false) =>
    apiService.post<JointMeasurement>(root(projectId, external), request),
  submit: (projectId: string, id: string, request: object, external = false) =>
    apiService.post<JointMeasurement>(`${root(projectId, external)}/${id}/submit`, request),
  schedule: (id: string, request: object) =>
    apiService.post<JointMeasurement>(`${internalRoot}/${id}/schedule`, request),
  linkMeasurement: (id: string, request: object) =>
    apiService.post<JointMeasurement>(`${internalRoot}/${id}/measurement`, request),
  attend: (projectId: string, id: string, participantId: string, request: object, external = false) =>
    apiService.post<JointMeasurement>(
      `${root(projectId, external)}/${id}/participants/${participantId}/attendance`,
      request
    ),
  endorse: (projectId: string, id: string, participantId: string, request: object) =>
    apiService.post<JointMeasurement>(
      `${externalRoot(projectId)}/${id}/participants/${participantId}/endorse`,
      request
    ),
  review: (id: string, request: object) =>
    apiService.post<JointMeasurement>(`${internalRoot}/${id}/review`, request),
  approve: (id: string, request: object) =>
    apiService.post<JointMeasurement>(`${internalRoot}/${id}/approve`, request),
  reject: (id: string, request: object) =>
    apiService.post<JointMeasurement>(`${internalRoot}/${id}/reject`, request),
  addEvidence: (
    projectId: string,
    id: string,
    request: { clientRequestId: string; evidenceType: JointMeasurementEvidenceType; title: string; file: File },
    external = false
  ) => {
    const form = new FormData();
    form.append('clientRequestId', request.clientRequestId);
    form.append('evidenceType', request.evidenceType);
    form.append('title', request.title);
    form.append('file', request.file, request.file.name);
    return apiService.post<JointMeasurementEvidence>(
      `${root(projectId, external)}/${id}/evidence`,
      form
    );
  },
  evidenceContent: (projectId: string, id: string, evidenceId: string, external = false) =>
    apiService.downloadBlob(`${root(projectId, external)}/${id}/evidence/${evidenceId}/content`),
  history: (id: string) =>
    apiService.get<JointMeasurementRevision[]>(`${internalRoot}/${id}/history`),
};
