export type ProcedureWorkspaceType =
  | 'Case Workflow'
  | 'Register'
  | 'Operational Queue'
  | 'Operational Board'
  | 'Event Workflow'
  | 'Legal Matter'
  | 'Dashboard / Report';

interface ProcedureWorkspaceTerminology {
  title: string;
  collectionLabel: string;
  emptyMessage: string;
  createHeading: string;
  createLabel: string;
  selectMessage: string;
}

const terminologyByType: Record<
  ProcedureWorkspaceType,
  ProcedureWorkspaceTerminology
> = {
  'Case Workflow': {
    title: 'Live Case Workspace',
    collectionLabel: 'Cases',
    emptyMessage: 'No cases opened for this procedure yet.',
    createHeading: 'Open Case',
    createLabel: 'Create case',
    selectMessage: 'Select or create a case to start work.',
  },
  Register: {
    title: 'Register Workspace',
    collectionLabel: 'Records',
    emptyMessage: 'No records have been added to this register yet.',
    createHeading: 'Add Record',
    createLabel: 'Create record',
    selectMessage: 'Select or create a record to start work.',
  },
  'Operational Queue': {
    title: 'Operational Queue',
    collectionLabel: 'Queue Items',
    emptyMessage: 'There are no items in this queue yet.',
    createHeading: 'Add Queue Item',
    createLabel: 'Create queue item',
    selectMessage: 'Select or create a queue item to start work.',
  },
  'Operational Board': {
    title: 'Operational Board',
    collectionLabel: 'Status Entries',
    emptyMessage: 'There are no status entries on this board yet.',
    createHeading: 'Add Status Entry',
    createLabel: 'Create status entry',
    selectMessage: 'Select or create a status entry to start work.',
  },
  'Event Workflow': {
    title: 'Event Workflow',
    collectionLabel: 'Events',
    emptyMessage: 'No events have been recorded yet.',
    createHeading: 'Record Event',
    createLabel: 'Create event',
    selectMessage: 'Select or create an event to start work.',
  },
  'Legal Matter': {
    title: 'Legal matters',
    collectionLabel: 'Legal Matters',
    emptyMessage: 'No legal matters have been opened for this procedure yet.',
    createHeading: 'Open Legal Matter',
    createLabel: 'Create legal matter',
    selectMessage: 'Select or create a legal matter to start work.',
  },
  'Dashboard / Report': {
    title: 'Dashboard / Report',
    collectionLabel: 'Reports',
    emptyMessage: 'No reports have been prepared yet.',
    createHeading: 'Prepare Report',
    createLabel: 'Create report',
    selectMessage: 'Select or create a report to start work.',
  },
};

const workspaceTypeByEntityType: Record<string, ProcedureWorkspaceType> = {
  EstateRegistrySecretariat: 'Operational Queue',
  EstateRecordsManagement: 'Register',
  EstateInspection: 'Event Workflow',
  EstateReminderRateRevision: 'Operational Queue',
  EstateReportingControls: 'Dashboard / Report',
  EstatePropertyManagementPropertyUnit: 'Register',
  EstatePropertyManagementLease: 'Register',
  EstatePropertyManagementTenantOccupant: 'Register',
  EstatePropertyManagementBillingServiceCharge: 'Operational Queue',
  EstatePropertyManagementListingApplication: 'Operational Queue',
  EstatePropertyManagementOccupancyAvailability: 'Operational Board',
  EstatePropertyManagementMoveInMoveOutHandover: 'Event Workflow',
  EstatePropertyManagementDocumentRecordIndex: 'Register',
};

export const resolveProcedureWorkspaceType = (
  entityType: string,
  workspaceType?: ProcedureWorkspaceType
): ProcedureWorkspaceType =>
  workspaceTypeByEntityType[entityType] ??
  workspaceType ??
  'Case Workflow';

export const getProcedureWorkspaceTerminology = (
  workspaceType?: string
): ProcedureWorkspaceTerminology =>
  terminologyByType[workspaceType as ProcedureWorkspaceType] ??
  terminologyByType['Case Workflow'];

export const getProcedureWorkspaceActionLabel = (
  workspaceType?: string
): string => {
  switch (workspaceType) {
    case 'Register':
      return 'Open register';
    case 'Operational Queue':
      return 'Open queue';
    case 'Operational Board':
      return 'Open board';
    case 'Event Workflow':
      return 'Open event workflow';
    case 'Legal Matter':
      return 'Open legal matter';
    case 'Dashboard / Report':
      return 'Open reporting';
    default:
      return 'Open case workflow';
  }
};

export const getProcedureStageLabel = (
  workspaceType: string | undefined,
  stageCount: number
): string => {
  if (workspaceType === 'Register') {
    return `${stageCount} record stages`;
  }

  if (workspaceType === 'Operational Board') {
    return `${stageCount} control stages`;
  }

  if (workspaceType === 'Dashboard / Report') {
    return `${stageCount} reporting stages`;
  }

  if (workspaceType === 'Legal Matter') {
    return `${stageCount} legal stages`;
  }

  return `${stageCount} stages`;
};
