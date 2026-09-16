import { apiService } from '@/services/api.service';

export type ArCollectionTaskStatus =
  | 'Unassigned' | 'Pending' | 'InProgress' | 'PromiseToPay' | 'Escalated' | 'Resolved' | 'WrittenOff';

export interface ArCollectionWorkItem {
  settlementBalanceId: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  customerEmail?: string;
  customerPhone?: string;
  customerAddress?: string;
  customerCity?: string;
  customerState?: string;
  customerCountry?: string;
  customerPostalCode?: string;
  isPartnerResolved: boolean;
  partnerResolutionMessage?: string;
  invoiceId: string;
  invoiceNumber: string;
  transactionDate: string;
  dueDate: string;
  currencyCode: string;
  outstandingAmount: number;
  daysOverdue: number;
  agingBucket: string;
  taskId?: string;
  taskReference: string;
  taskStatus: ArCollectionTaskStatus;
  priority: number;
  assignedToId?: string;
  assignedToName?: string;
  followUpDate?: string;
  promisedAmount: number;
  promisedPayDate?: string;
  outcome?: string;
  notes?: string;
  lastActivityAt?: string;
  isFollowUpOverdue: boolean;
  isPromiseBreached: boolean;
  rowVersion?: string;
}

export interface ArCollectionWorkQueue {
  asOfDate: string;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: ArCollectionWorkItem[];
}

export interface ArCollectionSummary {
  asOfDate: string;
  overdueExposureCount: number;
  unassignedExposureCount: number;
  overdueFollowUpCount: number;
  promiseToPayCount: number;
  breachedPromiseCount: number;
  nativeCurrencyTotals: ArCollectionCurrencyTotal[];
  functionalCurrencyCode?: string;
  functionalOutstandingTotal?: number;
  functionalPromisedTotal?: number;
  functionalTotalBasis: string;
  functionalTotalUnavailableReason?: string;
}

export interface ArCollectionCurrencyTotal {
  currencyCode: string;
  outstandingAmount: number;
  promisedAmount: number;
}

export interface ArCollectionAssignee {
  userId: string;
  displayName: string;
}

export interface ArCollectionHistoryItem {
  id: string;
  activityType: string;
  subject: string;
  collectionStatus: string;
  outcome?: string;
  reminderChannel?: string;
  reminderRecipient?: string;
  description?: string;
  notes?: string;
  activityDate: string;
  actorName?: string;
}

export interface GenerateArCollectionTasksResult {
  eligibleCount: number;
  createdCount: number;
  existingCount: number;
  refreshedCount: number;
  autoResolvedCount: number;
  reactivatedCount: number;
  skippedUnresolvedPartnerCount: number;
}

class ArCollectionDataService {
  getWorkQueue = (params: {
    asOfDate?: string;
    minimumDaysOverdue?: number;
    status?: string;
    assignedToId?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  } = {}) => apiService.get<ArCollectionWorkQueue>('/finance/ar/collections/work-queue', params);

  // Use the shared query serializer instead of composing nested URL templates. Besides
  // consistently encoding optional values, this keeps the route visible to the repository's
  // frontend/backend contract test so a future endpoint rename cannot silently break the page.
  getSummary = (asOfDate?: string) => apiService.get<ArCollectionSummary>(
    '/finance/ar/collections/summary', { asOfDate });

  getAssignees = () => apiService.get<ArCollectionAssignee[]>('/finance/ar/collections/assignees');

  generateTasks = (request: {
    asOfDate?: string;
    minimumDaysOverdue: number;
    assignedToId?: string;
    followUpInDays: number;
    priority: number;
  }) => apiService.post<GenerateArCollectionTasksResult>('/finance/ar/collections/tasks/generate', request);

  createTask = (request: {
    settlementBalanceId: string;
    assignedToId?: string;
    followUpDate?: string;
    priority: number;
    notes?: string;
  }) => apiService.post<ArCollectionWorkItem>('/finance/ar/collections/tasks', request);

  updateTask = (taskId: string, request: {
    collectionStatus: string;
    assignedToId?: string;
    followUpDate?: string;
    priority: number;
    promisedAmount: number;
    promisedPayDate?: string;
    outcome?: string;
    notes?: string;
    rowVersion: string;
  }) => apiService.put<ArCollectionWorkItem>(`/finance/ar/collections/tasks/${taskId}`, request);

  recordReminder = (taskId: string, request: {
    channel: string;
    recipient?: string;
    message: string;
    nextFollowUpDate?: string;
    confirmedDispatched: boolean;
    rowVersion: string;
  }) => apiService.post<ArCollectionHistoryItem>(`/finance/ar/collections/tasks/${taskId}/reminders`, request);

  getHistory = (taskId: string) =>
    apiService.get<ArCollectionHistoryItem[]>(`/finance/ar/collections/tasks/${taskId}/history`);
}

export const arCollectionDataService = new ArCollectionDataService();
