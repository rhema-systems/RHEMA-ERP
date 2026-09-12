import { apiService } from '@/services/api.service';
import type {
  AcknowledgeProcurementTenderDocumentChangeRequest,
  AttachProcurementTenderDocumentTemplateContentRequest,
  BindProcurementTenderDocumentRequest,
  CloneProcurementTenderDocumentTemplateRequest,
  CreateProcurementTenderDocumentChangeRequest,
  DecideProcurementTenderDocumentChangeRequest,
  IssueProcurementTenderDocumentRegisterRequest,
  ProcurementTenderDocumentChange,
  ProcurementTenderDocumentContentArtifactOption,
  ProcurementTenderDocumentAcknowledgement,
  ProcurementTenderDocumentIssuance,
  ProcurementTenderDocumentLifecycleRequest,
  ProcurementTenderDocumentPolicyOption,
  ProcurementTenderDocumentReadiness,
  ProcurementTenderDocumentRegister,
  ProcurementTenderDocumentSearch,
  ProcurementTenderDocumentSourceType,
  ProcurementTenderDocumentTemplate,
  ProcurementTenderDocumentTemplatePage,
  ProcurementTenderDocumentTemplateSummary,
  ProcurementTenderDocumentWorkflowOption,
  SaveProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';
import { workflowApiService } from '@/services/workflow-api.service';
import { WorkflowStepAction } from '@/types/workflow';

const templateRoot = '/procurement/tender-document-templates';
const registerRoot = '/procurement/tender-document-register';

export const procurementTenderDocumentService = {
  templateSummary: () =>
    apiService.get<ProcurementTenderDocumentTemplateSummary>(
      `${templateRoot}/summary`
    ),
  workflowOptions: () =>
    apiService.get<ProcurementTenderDocumentWorkflowOption[]>(
      `${templateRoot}/workflow-options`
    ),
  policyOptions: () =>
    apiService.get<ProcurementTenderDocumentPolicyOption[]>(
      `${templateRoot}/policy-options`
    ),
  contentArtifactOptions: async (workflowInstanceId?: string) => {
    const instances = await workflowApiService.getWorkflowEvidenceReviewInstances({
      pageSize: 100,
      workflowInstanceId,
    });
    const steps = instances
      .filter(
        (instance) =>
          !workflowInstanceId || instance.id === workflowInstanceId
      )
      .flatMap((instance) =>
        instance.steps
          .filter((step) => step.evidence.total > 0)
          .map((step) => ({ instance, step }))
      );
    const evidenceByStep = await Promise.all(
      steps.map(({ step }) =>
        workflowApiService.getWorkflowStepEvidence(step.stepInstanceId)
      )
    );
    const options = new Map<
      string,
      ProcurementTenderDocumentContentArtifactOption
    >();
    steps.forEach(({ instance, step }, index) => {
      for (const evidence of evidenceByStep[index] ?? []) {
        if (!evidence.isCurrent) continue;
        options.set(evidence.id, {
          id: evidence.id,
          documentName: evidence.documentName,
          documentType: evidence.documentType,
          fileName: evidence.fileName,
          filePath: evidence.filePath,
          sha256: evidence.sha256,
          version: evidence.version,
          isCurrent: evidence.isCurrent,
          verificationStatus: evidence.verificationStatus,
          malwareScanStatus: evidence.malwareScanStatus,
          workflowInstanceId: instance.id,
          workflowName: instance.workflowName,
          entityType: instance.entityType,
          entityId: instance.entityId,
          stepName: step.stepName,
        });
      }
    });
    return [...options.values()].sort((left, right) => {
      const leftApproved =
        left.verificationStatus === 1 && left.malwareScanStatus === 1;
      const rightApproved =
        right.verificationStatus === 1 && right.malwareScanStatus === 1;
      if (leftApproved !== rightApproved) return leftApproved ? -1 : 1;
      return (left.documentName ?? left.fileName).localeCompare(
        right.documentName ?? right.fileName
      );
    });
  },
  templateWorkflowInstance: async (workflowInstanceId: string) => {
    const instances =
      await workflowApiService.getWorkflowEvidenceReviewInstances({
        pageSize: 100,
        workflowInstanceId,
      });
    const instance = instances.find((item) => item.id === workflowInstanceId);
    if (!instance) {
      throw new Error(
        'The exact template approval workflow is not available in the current tenant.'
      );
    }
    return instance;
  },
  uploadTemplateWorkflowContent: (
    stepInstanceId: string,
    file: File
  ) =>
    workflowApiService.uploadStepAttachment(
      stepInstanceId,
      file,
      undefined,
      'Controlled tender-document content',
      'Tender document'
    ),
  completeTemplateContentStep: (stepInstanceId: string) =>
    workflowApiService.processStep(stepInstanceId, {
      action: WorkflowStepAction.Complete,
      comments: 'Controlled tender-document content uploaded for approval.',
    }),
  searchTemplates: (request: ProcurementTenderDocumentSearch) =>
    apiService.get<ProcurementTenderDocumentTemplatePage>(
      templateRoot,
      request as Record<string, unknown>
    ),
  getTemplate: (id: string) =>
    apiService.get<ProcurementTenderDocumentTemplate>(`${templateRoot}/${id}`),
  createTemplate: (request: SaveProcurementTenderDocumentTemplate) =>
    apiService.post<ProcurementTenderDocumentTemplate>(templateRoot, request),
  updateTemplate: (
    id: string,
    request: SaveProcurementTenderDocumentTemplate
  ) =>
    apiService.put<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}`,
      request
    ),
  attachTemplateContent: (
    id: string,
    request: AttachProcurementTenderDocumentTemplateContentRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/content`,
      request
    ),
  submitTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/submit`,
      request
    ),
  publishTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/publish`,
      request
    ),
  rejectTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/reject`,
      request
    ),
  cloneTemplate: (
    id: string,
    request: CloneProcurementTenderDocumentTemplateRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/clone`,
      request
    ),
  retireTemplate: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) =>
    apiService.post<ProcurementTenderDocumentTemplate>(
      `${templateRoot}/${id}/retire`,
      request
    ),
  deleteDraft: (
    id: string,
    request: ProcurementTenderDocumentLifecycleRequest
  ) => apiService.post<void>(`${templateRoot}/${id}/delete-draft`, request),

  readiness: (
    sourceType: ProcurementTenderDocumentSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementTenderDocumentReadiness>(
      `${registerRoot}/readiness`,
      { sourceType, sourceId }
    ),
  getRegister: (
    sourceType: ProcurementTenderDocumentSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementTenderDocumentRegister>(
      registerRoot,
      { sourceType, sourceId }
    ),
  bind: (request: BindProcurementTenderDocumentRequest) =>
    apiService.post<ProcurementTenderDocumentRegister>(
      `${registerRoot}/bind`,
      request
    ),
  issue: (request: IssueProcurementTenderDocumentRegisterRequest) =>
    apiService.post<ProcurementTenderDocumentIssuance>(
      `${registerRoot}/issue`,
      request
    ),
  createChange: (request: CreateProcurementTenderDocumentChangeRequest) =>
    apiService.post<ProcurementTenderDocumentChange>(
      `${registerRoot}/changes`,
      request
    ),
  decideChange: (
    changeId: string,
    request: DecideProcurementTenderDocumentChangeRequest
  ) =>
    apiService.post<ProcurementTenderDocumentChange>(
      `${registerRoot}/changes/${changeId}/decision`,
      request
    ),
  acknowledge: (request: AcknowledgeProcurementTenderDocumentChangeRequest) =>
    apiService.post<ProcurementTenderDocumentAcknowledgement>(
      `${registerRoot}/acknowledgements`,
      request
    ),
};
