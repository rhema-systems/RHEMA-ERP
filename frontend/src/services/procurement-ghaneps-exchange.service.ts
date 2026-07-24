import { apiService } from '@/services/api.service';
import type {
  PrepareProcurementGhanepsExportRequest,
  ProcurementGhanepsExchangeEvent,
  ProcurementGhanepsExchangeHistoryItem,
  ProcurementGhanepsExchangeOptions,
  ProcurementGhanepsExchangeOverview,
  ProcurementGhanepsSourceType,
  ReconcileProcurementGhanepsExchangeRequest,
  RecordProcurementGhanepsAcknowledgementRequest,
  RecordProcurementGhanepsAttemptRequest,
  RecordProcurementGhanepsImportRequest,
  RetryProcurementGhanepsExchangeRequest,
} from '@/types/procurement-ghaneps-exchange';

const root = '/procurement/ghaneps-exchanges';

const sourceRoot = (
  sourceType: ProcurementGhanepsSourceType,
  sourceId: string
) => `${root}/${sourceType}/${sourceId}`;

export const procurementGhanepsExchangeService = {
  getOverview: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementGhanepsExchangeOverview>(
      sourceRoot(sourceType, sourceId)
    ),

  getOptions: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementGhanepsExchangeOptions>(
      `${sourceRoot(sourceType, sourceId)}/options`
    ),

  getStatus: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementGhanepsExchangeOverview>(
      `${sourceRoot(sourceType, sourceId)}/status`
    ),

  getHistory: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string
  ) =>
    apiService.get<ProcurementGhanepsExchangeHistoryItem[]>(
      `${sourceRoot(sourceType, sourceId)}/history`
    ),

  getEvent: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    exchangeEventId: string
  ) =>
    apiService.get<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/events/${exchangeEventId}`
    ),

  prepareExport: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    request: PrepareProcurementGhanepsExportRequest
  ) =>
    apiService.post<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/exports`,
      request
    ),

  recordImport: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    request: RecordProcurementGhanepsImportRequest
  ) =>
    apiService.post<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/imports`,
      request
    ),

  recordAttempt: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    exchangeEventId: string,
    request: RecordProcurementGhanepsAttemptRequest
  ) =>
    apiService.post<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/events/${exchangeEventId}/attempts`,
      request
    ),

  retry: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    exchangeEventId: string,
    request: RetryProcurementGhanepsExchangeRequest
  ) =>
    apiService.post<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/events/${exchangeEventId}/retry`,
      request
    ),

  recordAcknowledgement: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    exchangeEventId: string,
    request: RecordProcurementGhanepsAcknowledgementRequest
  ) =>
    apiService.post<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/events/${exchangeEventId}/acknowledgements`,
      request
    ),

  reconcile: (
    sourceType: ProcurementGhanepsSourceType,
    sourceId: string,
    exchangeEventId: string,
    request: ReconcileProcurementGhanepsExchangeRequest
  ) =>
    apiService.post<ProcurementGhanepsExchangeEvent>(
      `${sourceRoot(sourceType, sourceId)}/events/${exchangeEventId}/reconciliations`,
      request
    ),
};
