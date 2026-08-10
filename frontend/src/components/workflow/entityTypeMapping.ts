import type { WorkflowEntityTypeInfo } from '@/types/workflow';

const normalizeKey = (value?: string | null) =>
  (value || '').replace(/[^a-z0-9]/gi, '').toLowerCase();

const isMatch = (candidate: WorkflowEntityTypeInfo, key: string) => {
  const normalized = normalizeKey(key);
  return (
    normalizeKey(candidate.name) === normalized ||
    normalizeKey(candidate.code) === normalized
  );
};

export const moduleEntityTypeMap: Record<string, string[]> = {
  // Maintenance module includes Fleet (vehicles/trips/inspections/defects) as well as core work management.
  maintenance: ['WorkOrder', 'JobCard', 'Asset', 'FleetTrip', 'FleetVehicle', 'FleetTripInspection', 'FleetDefect'],
  procurement: ['ProcurementPlan', 'PurchaseOrder', 'PurchaseRequisition', 'Tender', 'Vendor'],
  inventory: ['Inventory', 'Asset'],
  hr: [
    'Employee',
    'PayrollRun',
    'PayrollSalaryAdvance',
    'PayrollBonusSetup',
    'PayrollBackpaySetup',
    'LeaveRequest',
    'LeavePlan',
    'LeaveEncashment',
    'TrainingNomination',
    'StaffAttendanceRegularization',
    'StaffOvertimeRequest',
    'RemoteWorkRequest',
    'ConsultantTimesheet',
    'AppraisalTemplate',
    'SalaryReviewProposal',
    'EmploymentActionProposal',
    'PerformanceImprovementPlan',
    'StaffRequisition',
  ],
  helpdesk: ['EhcTicket', 'ServiceRequest'],
  projects: ['Project'],
  // Estate/DMS integration: Planning, Estate, and Legal entries make existing Workflow filters aware of our procedure cases.
  planning: [
    'PlanningLandAllocationVetting',
    'PlanningChangeOfUseReview',
    'PlanningSchemeLayoutPreparation',
    'PlanningSiteReport',
    'PlanningSitePlanPreparation',
    'PlanningOfficialSearchData',
    'PlanningDevelopmentPermitConformity',
    'PlanningRegularization',
    'PlanningLayoutReviewCorrection',
    'PlanningComplianceInspection',
    'PlanningDisputeComplaint',
    'PlanningAssemblySpatialCommittee'
  ],
  // Estate/DMS integration: Estate procedure and land-acquisition cases use Workflow approvals instead of a separate approval engine.
  estate: [
    'LandAcquisition',
    'EstateFacilityPropertySite',
    'EstateFacilityLease',
    'EstateFacilityMaintenance',
    'EstateFacilityComplaint',
    'EstateFacilityServiceProvider',
    'EstateFacilityStaffCleaner',
    'EstateFacilityAssetRegister',
    'EstateFacilityDocument',
    'EstateRegistrySecretariat',
    'EstateRecordsManagement',
    'EstateInspection',
    'EstateSearchApplication',
    'EstateRecordAmendment',
    'EstateCertifiedTrueCopy',
    'EstateJointOwnership',
    'EstateTransfer',
    'EstateAssignment',
    'EstateLeasePreparation',
    'EstateLeaseRenewal',
    'EstateServicedPlotAllocation',
    'EstateLandsPartiallyServiced',
    'EstateHousingHomeOwnership',
    'EstateTraditionalLands',
    'EstateTenancyRegularisation',
    'EstateReportingControls'
  ],
  // Estate/DMS integration: Legal procedures are exposed to Workflow so Estate handoffs can still route through Legal-owned review.
  legal: [
    'LegalProcedure',
    'LegalMortgage',
    'LegalMortgageInPrinciple',
    'LegalCourtProcess',
    'LegalOtherCourtProcess',
    'LegalTerminationRecognition',
    'LegalAssignmentSubleaseVesting',
    'LegalLeaseVariationRenewalSublease',
    'LegalTransfer'
  ],
  sales: ['Customer', 'SalesOrder', 'SalesAgreement', 'SalesAllocation', 'PlotAllocation', 'Refund', 'CreditNote'],
  quality: ['Quality']
};

const toEntityCode = (name: string) => {
  if (!name.trim()) {
    return 'ENTITY';
  }
  return name
    .replace(/([a-z])([A-Z])/g, '$1_$2')
    .replace(/[^a-zA-Z0-9]+/g, '_')
    .replace(/_+/g, '_')
    .replace(/^_+|_+$/g, '')
    .toUpperCase() || 'ENTITY';
};

export const buildFallbackEntityTypes = (): WorkflowEntityTypeInfo[] => {
  const names = new Set<string>();
  Object.values(moduleEntityTypeMap).forEach((items) => {
    items.forEach((item) => names.add(item));
  });

  return Array.from(names)
    .sort((a, b) => a.localeCompare(b))
    .map((name, index) => ({
      id: name,
      code: toEntityCode(name),
      name,
      description: undefined,
      displayOrder: index,
      icon: undefined,
      colorCode: undefined,
      isActive: true
    }));
};

export const filterEntityTypesByModule = (
  entityTypes: WorkflowEntityTypeInfo[],
  moduleId?: string
) => {
  if (!moduleId || moduleId === 'all' || !moduleEntityTypeMap[moduleId]?.length)
  {
    return { items: entityTypes, fallback: false };
  }

  const keys = moduleEntityTypeMap[moduleId].map(normalizeKey);
  const filtered = entityTypes.filter((entityType) =>
    keys.some((key) => isMatch(entityType, key))
  );

  if (filtered.length === 0)
  {
    return { items: entityTypes, fallback: true };
  }

  return { items: filtered, fallback: false };
};

export const isEntityTypeInList = (
  entityType: string,
  entityTypes: WorkflowEntityTypeInfo[]
) =>
  entityTypes.some((item) =>
    normalizeKey(item.name) === normalizeKey(entityType) ||
    normalizeKey(item.code) === normalizeKey(entityType)
  );
