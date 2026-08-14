// Types for HR Area 10 (SHE) slice 7: fire safety & emergency — the safety-equipment register
// (extinguishers, AEDs, detectors …) with inspections, corrective actions and maintenance, and
// emergency preparedness (plans, assembly points, contacts, drills, response teams).
// Mirrors ErpSystem.Core.DTOs.HR: SafetyPermitPpeEquipmentDTOs (region G) and
// SafetyEmergencyGovernanceDTOs (region M).
// Backend routes: api/safety/equipment, api/safety/emergency.
//
// The slice-13 reminder engine chases these dates automatically (equipment inspection /
// maintenance / certification, emergency-plan reviews, response-team certificates, drills);
// due-for-* / expiring-* remain the QUERIES the screens poll as work views.

import type { AuditFields } from './common';
import type { SheInspectionType } from './safety-inspections';
import type { SheCorrectiveActionStatus } from './safety-incidents';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheSafetyEquipmentType =
  | 'FireExtinguisher'
  | 'FirstAidKit'
  | 'AED'
  | 'EmergencyShower'
  | 'EyeWashStation'
  | 'FireHydrant'
  | 'FireAlarmPanel'
  | 'SmokeDetector'
  | 'HeatDetector'
  | 'SpillKit'
  | 'SafetyShower'
  | 'GasDetector'
  | 'EmergencyLighting'
  | 'FireSuppressionSystem'
  | 'Other';

export const SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS = opts<SheSafetyEquipmentType>([
  ['FireExtinguisher', 'Fire Extinguisher'],
  ['FirstAidKit', 'First Aid Kit'],
  ['AED', 'AED'],
  ['EmergencyShower', 'Emergency Shower'],
  ['EyeWashStation', 'Eye Wash Station'],
  ['FireHydrant', 'Fire Hydrant'],
  ['FireAlarmPanel', 'Fire Alarm Panel'],
  ['SmokeDetector', 'Smoke Detector'],
  ['HeatDetector', 'Heat Detector'],
  ['SpillKit', 'Spill Kit'],
  ['SafetyShower', 'Safety Shower'],
  ['GasDetector', 'Gas Detector'],
  ['EmergencyLighting', 'Emergency Lighting'],
  ['FireSuppressionSystem', 'Fire Suppression System'],
  ['Other', 'Other'],
]);

export type SheSafetyEquipmentStatus =
  | 'Operational'
  | 'RequiresMaintenance'
  | 'UnderMaintenance'
  | 'OutOfService'
  | 'Expired'
  | 'Decommissioned';

export const SHE_SAFETY_EQUIPMENT_STATUS_OPTIONS = opts<SheSafetyEquipmentStatus>([
  ['Operational', 'Operational'],
  ['RequiresMaintenance', 'Requires Maintenance'],
  ['UnderMaintenance', 'Under Maintenance'],
  ['OutOfService', 'Out of Service'],
  ['Expired', 'Expired'],
  ['Decommissioned', 'Decommissioned'],
]);

export type SheInspectionResult = 'Pass' | 'ConditionalPass' | 'Fail' | 'NeedsFollowUp';

export const SHE_INSPECTION_RESULT_OPTIONS = opts<SheInspectionResult>([
  ['Pass', 'Pass'],
  ['ConditionalPass', 'Conditional Pass'],
  ['Fail', 'Fail'],
  ['NeedsFollowUp', 'Needs Follow-Up'],
]);

export type SheEmergencyType =
  | 'Fire'
  | 'MedicalEmergency'
  | 'ChemicalSpill'
  | 'Explosion'
  | 'NaturalDisaster'
  | 'StructuralCollapse'
  | 'PowerFailure'
  | 'SecurityThreat'
  | 'FloodOrWaterIntrusion'
  | 'General';

export const SHE_EMERGENCY_TYPE_OPTIONS = opts<SheEmergencyType>([
  ['Fire', 'Fire'],
  ['MedicalEmergency', 'Medical Emergency'],
  ['ChemicalSpill', 'Chemical Spill'],
  ['Explosion', 'Explosion'],
  ['NaturalDisaster', 'Natural Disaster'],
  ['StructuralCollapse', 'Structural Collapse'],
  ['PowerFailure', 'Power Failure'],
  ['SecurityThreat', 'Security Threat'],
  ['FloodOrWaterIntrusion', 'Flood / Water Intrusion'],
  ['General', 'General'],
]);

// ── Safety equipment ──────────────────────────────────────────────────────────

export interface SafetyEquipmentSummary {
  id: string;
  equipmentNumber: string;
  name: string;
  type: SheSafetyEquipmentType;
  typeName: string;
  locationName: string;
  status: SheSafetyEquipmentStatus;
  statusName: string;
  nextInspectionDueDate?: string | null;
  certificationExpiryDate?: string | null;
}

export interface SafetyEquipment extends AuditFields {
  tenantId: string;
  equipmentNumber: string;
  name: string;
  description?: string | null;
  type: SheSafetyEquipmentType;
  typeName: string;
  locationId: string;
  locationName: string;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  purchaseDate?: string | null;
  installationDate?: string | null;
  requiresRegularInspection: boolean;
  inspectionFrequencyDays: number;
  lastInspectionDate?: string | null;
  nextInspectionDueDate?: string | null;
  requiresCertification: boolean;
  certificationExpiryDate?: string | null;
  certificationDocumentPath?: string | null;
  status: SheSafetyEquipmentStatus;
  statusName: string;
  outOfServiceDate?: string | null;
  outOfServiceReason?: string | null;
  expiryDate?: string | null;
  lastMaintenanceDate?: string | null;
  nextMaintenanceDueDate?: string | null;
  responsiblePersonId?: string | null;
  responsiblePersonName?: string | null;
  notes?: string | null;
  inspections: SafetyEquipmentInspection[];
  maintenanceRecords: SafetyEquipmentMaintenance[];
}

export interface SafetyEquipmentCreateRequest {
  name: string;
  description?: string | null;
  type: SheSafetyEquipmentType;
  locationId: string;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  purchaseDate?: string | null;
  installationDate?: string | null;
  requiresRegularInspection: boolean;
  inspectionFrequencyDays: number;
  requiresCertification: boolean;
  certificationExpiryDate?: string | null;
  expiryDate?: string | null;
  responsiblePersonId?: string | null;
  notes?: string | null;
}

export interface SafetyEquipmentUpdateRequest {
  id: string;
  name: string;
  description?: string | null;
  type: SheSafetyEquipmentType;
  locationId: string;
  specificArea?: string | null;
  organizationUnitId?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  purchaseDate?: string | null;
  installationDate?: string | null;
  requiresRegularInspection: boolean;
  inspectionFrequencyDays: number;
  lastInspectionDate?: string | null;
  nextInspectionDueDate?: string | null;
  requiresCertification: boolean;
  certificationExpiryDate?: string | null;
  certificationDocumentPath?: string | null;
  status: SheSafetyEquipmentStatus;
  outOfServiceDate?: string | null;
  outOfServiceReason?: string | null;
  expiryDate?: string | null;
  lastMaintenanceDate?: string | null;
  nextMaintenanceDueDate?: string | null;
  responsiblePersonId?: string | null;
  notes?: string | null;
}

export interface SafetyEquipmentInspection extends AuditFields {
  equipmentId: string;
  inspectionDate: string;
  inspectionType: SheInspectionType;
  inspectionTypeName: string;
  inspectedById: string;
  inspectedByName: string;
  result: SheInspectionResult;
  resultName: string;
  findings?: string | null;
  deficienciesNoted?: string | null;
  nextInspectionDate?: string | null;
  inspectionActions: SafetyEquipmentInspectionAction[];
}

export interface SafetyEquipmentInspectionCreateRequest {
  equipmentId: string;
  inspectionDate: string;
  inspectionType: SheInspectionType;
  inspectedById: string;
  result: SheInspectionResult;
  findings?: string | null;
  deficienciesNoted?: string | null;
  nextInspectionDate?: string | null;
}

export interface SafetyEquipmentInspectionUpdateRequest {
  id: string;
  inspectionDate: string;
  inspectionType: SheInspectionType;
  result: SheInspectionResult;
  findings?: string | null;
  deficienciesNoted?: string | null;
  nextInspectionDate?: string | null;
}

export interface SafetyEquipmentInspectionAction extends AuditFields {
  safetyEquipmentInspectionId: string;
  correctiveActionTemplateId: string;
  correctiveActionTemplateTitle: string;
  status: SheCorrectiveActionStatus;
  statusName: string;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
  assignedToId?: string | null;
  assignedToName?: string | null;
}

export interface SafetyEquipmentInspectionActionCreateRequest {
  safetyEquipmentInspectionId: string;
  correctiveActionTemplateId: string;
  status: SheCorrectiveActionStatus;
  dueDate?: string | null;
  assignedToId?: string | null;
}

export interface SafetyEquipmentInspectionActionUpdateRequest {
  id: string;
  status: SheCorrectiveActionStatus;
  dueDate?: string | null;
  completionDate?: string | null;
  completionNotes?: string | null;
  assignedToId?: string | null;
}

export interface SafetyEquipmentMaintenance extends AuditFields {
  equipmentId: string;
  maintenanceRecordId?: string | null;
  maintenanceDate: string;
  maintenanceType?: string | null;
  description?: string | null;
  equipmentTakenOutOfService: boolean;
  outOfServiceStart?: string | null;
  outOfServiceEnd?: string | null;
  cost?: number | null;
  performedBy?: string | null;
  notes?: string | null;
}

export interface SafetyEquipmentMaintenanceCreateRequest {
  equipmentId: string;
  maintenanceRecordId?: string | null;
  maintenanceDate: string;
  maintenanceType?: string | null;
  description?: string | null;
  equipmentTakenOutOfService: boolean;
  outOfServiceStart?: string | null;
  outOfServiceEnd?: string | null;
  cost?: number | null;
  performedBy?: string | null;
  notes?: string | null;
}

export interface SafetyEquipmentMaintenanceUpdateRequest {
  id: string;
  maintenanceDate: string;
  maintenanceType?: string | null;
  description?: string | null;
  equipmentTakenOutOfService: boolean;
  outOfServiceStart?: string | null;
  outOfServiceEnd?: string | null;
  cost?: number | null;
  performedBy?: string | null;
  notes?: string | null;
}

// ── Emergency plans ───────────────────────────────────────────────────────────

export interface EmergencyPlanSummary {
  id: string;
  planNumber: string;
  planName: string;
  type: SheEmergencyType;
  typeName: string;
  locationName?: string | null;
  planOwnerName: string;
  nextReviewDate: string;
  isActive: boolean;
}

export interface EmergencyPlan extends AuditFields {
  tenantId: string;
  planNumber: string;
  planName: string;
  type: SheEmergencyType;
  typeName: string;
  description: string;
  procedures: string;
  evacuationRouteDocumentPath?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  lastReviewed: string;
  nextReviewDate: string;
  planOwnerId: string;
  planOwnerName: string;
  documentPath?: string | null;
  isActive: boolean;
  assemblyPoints: SheAssemblyPoint[];
  emergencyContacts: EmergencyContact[];
  drills: EmergencyDrill[];
  teamMembers: EmergencyResponseTeamMember[];
}

export interface EmergencyPlanCreateRequest {
  planNumber: string;
  planName: string;
  type: SheEmergencyType;
  description?: string;
  procedures?: string;
  evacuationRouteDocumentPath?: string | null;
  locationId?: string | null;
  lastReviewed: string;
  nextReviewDate: string;
  planOwnerId: string;
  documentPath?: string | null;
  isActive: boolean;
}

export interface EmergencyPlanUpdateRequest {
  id: string;
  planName: string;
  type: SheEmergencyType;
  description?: string;
  procedures?: string;
  evacuationRouteDocumentPath?: string | null;
  locationId?: string | null;
  lastReviewed: string;
  nextReviewDate: string;
  planOwnerId: string;
  documentPath?: string | null;
  isActive: boolean;
}

export interface SheAssemblyPoint extends AuditFields {
  emergencyPlanId: string;
  name: string;
  description: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  gpsLatitude?: number | null;
  gpsLongitude?: number | null;
  capacity?: number | null;
  isActive: boolean;
}

export interface SheAssemblyPointCreateRequest {
  emergencyPlanId: string;
  name: string;
  description?: string;
  locationId?: string | null;
  specificArea?: string | null;
  gpsLatitude?: number | null;
  gpsLongitude?: number | null;
  capacity?: number | null;
  isActive: boolean;
}

export interface SheAssemblyPointUpdateRequest {
  id: string;
  name: string;
  description?: string;
  locationId?: string | null;
  specificArea?: string | null;
  gpsLatitude?: number | null;
  gpsLongitude?: number | null;
  capacity?: number | null;
  isActive: boolean;
}

export interface EmergencyContact extends AuditFields {
  emergencyPlanId: string;
  name: string;
  role: string;
  primaryPhone: string;
  alternatePhone?: string | null;
  email?: string | null;
  isExternal: boolean;
  displayOrder: number;
  isActive: boolean;
}

export interface EmergencyContactCreateRequest {
  emergencyPlanId: string;
  name: string;
  role: string;
  primaryPhone: string;
  alternatePhone?: string | null;
  email?: string | null;
  isExternal: boolean;
  displayOrder: number;
  isActive: boolean;
}

export interface EmergencyContactUpdateRequest {
  id: string;
  name: string;
  role: string;
  primaryPhone: string;
  alternatePhone?: string | null;
  email?: string | null;
  isExternal: boolean;
  displayOrder: number;
  isActive: boolean;
}

export interface EmergencyDrill extends AuditFields {
  emergencyPlanId: string;
  drillNumber: string;
  drillName: string;
  drillDate: string;
  /** TimeSpan — "HH:mm:ss". */
  drillTime?: string | null;
  scenario?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  wasAnnounced: boolean;
  participantsCount?: number | null;
  /** TimeSpan — "HH:mm:ss". */
  evacuationTime?: string | null;
  observations?: string | null;
  strengthsIdentified?: string | null;
  areasForImprovement?: string | null;
  correctiveActions?: string | null;
  objectivesMet: boolean;
  coordinatorId: string;
  coordinatorName: string;
  nextDrillScheduledDate?: string | null;
  drillReportDocumentPath?: string | null;
}

export interface EmergencyDrillCreateRequest {
  emergencyPlanId: string;
  drillNumber: string;
  drillName: string;
  drillDate: string;
  drillTime?: string | null;
  scenario?: string | null;
  locationId?: string | null;
  departmentId?: string | null;
  wasAnnounced: boolean;
  coordinatorId: string;
  nextDrillScheduledDate?: string | null;
}

export interface EmergencyDrillUpdateRequest {
  id: string;
  drillName: string;
  drillDate: string;
  drillTime?: string | null;
  scenario?: string | null;
  locationId?: string | null;
  departmentId?: string | null;
  wasAnnounced: boolean;
  participantsCount?: number | null;
  evacuationTime?: string | null;
  observations?: string | null;
  strengthsIdentified?: string | null;
  areasForImprovement?: string | null;
  correctiveActions?: string | null;
  objectivesMet: boolean;
  coordinatorId: string;
  nextDrillScheduledDate?: string | null;
  drillReportDocumentPath?: string | null;
}

export interface EmergencyResponseTeamMember extends AuditFields {
  emergencyPlanId: string;
  planName?: string | null;
  employeeId: string;
  employeeName: string;
  role: string;
  responsibilities?: string | null;
  certificateExpiryDate?: string | null;
  certificateDocumentPath?: string | null;
  isActive: boolean;
}

export interface EmergencyResponseTeamMemberCreateRequest {
  emergencyPlanId: string;
  employeeId: string;
  role: string;
  responsibilities?: string | null;
  certificateExpiryDate?: string | null;
  certificateDocumentPath?: string | null;
  isActive: boolean;
}

export interface EmergencyResponseTeamMemberUpdateRequest {
  id: string;
  role: string;
  responsibilities?: string | null;
  certificateExpiryDate?: string | null;
  certificateDocumentPath?: string | null;
  isActive: boolean;
}
