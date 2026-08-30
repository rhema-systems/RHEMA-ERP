export type CivilEngineeringComplaintResolution = {
  helpdeskTicketId: string;
  complaintTicketNumber: string;
  complaintSubject?: string | null;
  helpdeskStatus: string;
  complaintLoggedAt: string;
  intakeId: string;
  intakeNumber: string;
  intakeStatus: string;
  assessmentId?: string | null;
  assessmentStage?: string | null;
  costingHandoffId?: string | null;
  costingStage?: string | null;
  executionLinkId?: string | null;
  executionStage?: string | null;
  workOrderNumber?: string | null;
  workOrderStatus?: string | null;
  completionControlId?: string | null;
  completionStage?: string | null;
  inspectionStatus?: string | null;
  paymentDirectionStatus?: string | null;
  resolutionStage: string;
  resolutionLabel: string;
  readyForHelpdeskResolution: boolean;
  lastActivityAt: string;
};

export type CivilEngineeringComplaintResolutionTimelineEntry = {
  occurredAt: string;
  source: string;
  action: string;
  stage?: string | null;
  actorName?: string | null;
};
