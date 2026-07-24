import { apiService } from '@/services/api.service';
import type {
  AcknowledgeProcurementBidderDispatchRequest,
  ActOnProcurementTenderSecurityRequest,
  ApproveProcurementBidderLetterRequest,
  DispatchProcurementBidderLetterRequest,
  FileProcurementBidderAppealRequest,
  InitializeProcurementBidderCommunicationRequest,
  ProcurementBidderAcknowledgement,
  ProcurementBidderAppeal,
  ProcurementBidderCommunicationOverview,
  ProcurementBidderCommunicationSourceType,
  ProcurementBidderDelivery,
  ProcurementBidderLetterDispatch,
  ProcurementBidderLetterVersion,
  ProcurementTenderSecurityAction,
  ProcurementTenderSecurityInstrument,
  RecordProcurementBidderDeliveryRequest,
  RegisterProcurementTenderSecurityRequest,
  ResolveProcurementBidderAppealRequest,
} from '@/types/procurement-bidder-communication';

const root = '/procurement/bidder-communications';

const sourceRoot = (
  sourceType: ProcurementBidderCommunicationSourceType,
  sourceId: string
) => `${root}/${sourceType}/${sourceId}`;

export const procurementBidderCommunicationService = {
  getOverview: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementBidderCommunicationOverview>(
      sourceRoot(sourceType, sourceId)
    ),

  getExternalStatus: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementBidderCommunicationOverview>(
      `${root}/external/${sourceType}/${sourceId}`
    ),

  initialize: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    request: InitializeProcurementBidderCommunicationRequest
  ) =>
    apiService.post<ProcurementBidderCommunicationOverview>(
      `${sourceRoot(sourceType, sourceId)}/initialize`,
      request
    ),

  approveLetter: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    recipientId: string,
    request: ApproveProcurementBidderLetterRequest
  ) =>
    apiService.post<ProcurementBidderLetterVersion>(
      `${sourceRoot(sourceType, sourceId)}/recipients/${recipientId}/letters/approve`,
      request
    ),

  dispatchLetter: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    letterVersionId: string,
    request: DispatchProcurementBidderLetterRequest
  ) =>
    apiService.post<ProcurementBidderLetterDispatch>(
      `${sourceRoot(sourceType, sourceId)}/letters/${letterVersionId}/dispatch`,
      request
    ),

  recordDelivery: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    dispatchId: string,
    request: RecordProcurementBidderDeliveryRequest
  ) =>
    apiService.post<ProcurementBidderDelivery>(
      `${sourceRoot(sourceType, sourceId)}/dispatches/${dispatchId}/delivery`,
      request
    ),

  acknowledge: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    dispatchId: string,
    request: AcknowledgeProcurementBidderDispatchRequest,
    external = false
  ) =>
    apiService.post<ProcurementBidderAcknowledgement>(
      external
        ? `${root}/external/${sourceType}/${sourceId}/dispatches/${dispatchId}/acknowledgement`
        : `${sourceRoot(sourceType, sourceId)}/dispatches/${dispatchId}/acknowledgement`,
      request
    ),

  fileAppeal: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    recipientId: string,
    request: FileProcurementBidderAppealRequest,
    external = false
  ) =>
    apiService.post<ProcurementBidderAppeal>(
      external
        ? `${root}/external/${sourceType}/${sourceId}/appeals`
        : `${sourceRoot(sourceType, sourceId)}/recipients/${recipientId}/appeals`,
      request
    ),

  resolveAppeal: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    appealId: string,
    request: ResolveProcurementBidderAppealRequest
  ) =>
    apiService.post<ProcurementBidderAppeal>(
      `${sourceRoot(sourceType, sourceId)}/appeals/${appealId}/resolve`,
      request
    ),

  registerSecurity: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    recipientId: string,
    request: RegisterProcurementTenderSecurityRequest
  ) =>
    apiService.post<ProcurementTenderSecurityInstrument>(
      `${sourceRoot(sourceType, sourceId)}/recipients/${recipientId}/securities`,
      request
    ),

  actOnSecurity: (
    sourceType: ProcurementBidderCommunicationSourceType,
    sourceId: string,
    securityId: string,
    request: ActOnProcurementTenderSecurityRequest
  ) =>
    apiService.post<ProcurementTenderSecurityAction>(
      `${sourceRoot(sourceType, sourceId)}/securities/${securityId}/actions`,
      request
    ),
};
