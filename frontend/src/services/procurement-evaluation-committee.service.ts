import { apiService } from '@/services/api.service';
import type {
  ActivateProcurementEvaluationCommitteeRequest,
  BindProcurementEvaluationCommitteeRequest,
  ConfirmProcurementEvaluationQuorumRequest,
  CreateProcurementEvaluationMeetingRequest,
  DecideProcurementEvaluationScoreRecallRequest,
  ProcurementEvaluationAppointment,
  ProcurementEvaluationAttendance,
  ProcurementEvaluationCommitteeControl,
  ProcurementEvaluationCommitteeOptions,
  ProcurementEvaluationCommitteeReadiness,
  ProcurementEvaluationMeeting,
  ProcurementEvaluationPhase,
  ProcurementEvaluationScorerEligibility,
  ProcurementEvaluationScoreRecall,
  ProcurementEvaluationSourceType,
  RequestProcurementEvaluationScoreRecallRequest,
  RespondProcurementEvaluationAppointmentRequest,
  SignProcurementEvaluationAttendanceRequest,
  SubmitProcurementEvaluationConflictDeclarationRequest,
} from '@/types/procurement-evaluation-committee';

const root = '/procurement/evaluation-committees';

const sourceQuery = (
  sourceType: ProcurementEvaluationSourceType,
  sourceId: string
) => ({ sourceType, sourceId });

export const procurementEvaluationCommitteeService = {
  readiness: (
    sourceType: ProcurementEvaluationSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementEvaluationCommitteeReadiness>(
      `${root}/readiness`,
      sourceQuery(sourceType, sourceId)
    ),
  options: (sourceType: ProcurementEvaluationSourceType, sourceId: string) =>
    apiService.get<ProcurementEvaluationCommitteeOptions>(
      `${root}/options`,
      sourceQuery(sourceType, sourceId)
    ),
  get: (sourceType: ProcurementEvaluationSourceType, sourceId: string) =>
    apiService.get<ProcurementEvaluationCommitteeControl>(
      root,
      sourceQuery(sourceType, sourceId)
    ),
  bind: (request: BindProcurementEvaluationCommitteeRequest) =>
    apiService.post<ProcurementEvaluationCommitteeControl>(
      `${root}/bind`,
      request
    ),
  activate: (
    committeeControlId: string,
    request: ActivateProcurementEvaluationCommitteeRequest
  ) =>
    apiService.post<ProcurementEvaluationCommitteeControl>(
      `${root}/${committeeControlId}/activate`,
      request
    ),
  respondToAppointment: (
    appointmentId: string,
    request: RespondProcurementEvaluationAppointmentRequest
  ) =>
    apiService.post<ProcurementEvaluationAppointment>(
      `${root}/appointments/${appointmentId}/response`,
      request
    ),
  submitConflictDeclaration: (
    appointmentId: string,
    request: SubmitProcurementEvaluationConflictDeclarationRequest
  ) =>
    apiService.post<ProcurementEvaluationAppointment>(
      `${root}/appointments/${appointmentId}/conflict-declarations`,
      request
    ),
  createMeeting: (
    committeeControlId: string,
    request: CreateProcurementEvaluationMeetingRequest
  ) =>
    apiService.post<ProcurementEvaluationMeeting>(
      `${root}/${committeeControlId}/meetings`,
      request
    ),
  signAttendance: (
    meetingId: string,
    request: SignProcurementEvaluationAttendanceRequest
  ) =>
    apiService.post<ProcurementEvaluationAttendance>(
      `${root}/meetings/${meetingId}/attendance`,
      request
    ),
  confirmQuorum: (
    meetingId: string,
    request: ConfirmProcurementEvaluationQuorumRequest
  ) =>
    apiService.post<ProcurementEvaluationMeeting>(
      `${root}/meetings/${meetingId}/quorum`,
      request
    ),
  scorerEligibility: (
    sourceType: ProcurementEvaluationSourceType,
    sourceId: string,
    phase: ProcurementEvaluationPhase
  ) =>
    apiService.get<ProcurementEvaluationScorerEligibility>(
      `${root}/scorer-eligibility`,
      { ...sourceQuery(sourceType, sourceId), phase }
    ),
  requestRecall: (
    scoreSheetId: string,
    request: RequestProcurementEvaluationScoreRecallRequest
  ) =>
    apiService.post<ProcurementEvaluationScoreRecall>(
      `${root}/score-sheets/${scoreSheetId}/recalls`,
      request
    ),
  decideRecall: (
    recallId: string,
    request: DecideProcurementEvaluationScoreRecallRequest
  ) =>
    apiService.post<ProcurementEvaluationScoreRecall>(
      `${root}/score-recalls/${recallId}/decision`,
      request
    ),
};
