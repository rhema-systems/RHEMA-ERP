// Types for HR Area 10 (SHE) slice 10: waste management (types + disposal records) and
// environmental data capture (incidents + monitoring records).
// Mirrors ErpSystem.Core.DTOs.HR.SafetyEnvironmentHealthDTOs (regions J and K).
// Backend routes: api/safety/waste, api/safety/environmental.
//
// ⚠ The disposal-certificate gate is LIVE (pulled in from slice 17): a disposal record whose
// waste type requires a manifest is refused (422) until both the manifest number and the
// certificate document path are recorded — on create and on update.
// Record/incident numbers are server-assigned when left blank (WD-/ENV-/EM-YYYY-NNNN).
// Monitoring exceedance flags are computed server-side from the measured value and limits.
// EPA reporting here is a hand-kept flag — no submission integration exists (slice 15+).

import type { AuditFields } from './common';

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings) ──────────────────────────────────────────────

export type SheWasteClassification =
  | 'NonHazardous'
  | 'Hazardous'
  | 'Recyclable'
  | 'Compostable'
  | 'InertWaste'
  | 'EWaste'
  | 'Medical'
  | 'Radioactive';

export const SHE_WASTE_CLASSIFICATION_OPTIONS = opts<SheWasteClassification>([
  ['NonHazardous', 'Non-hazardous'],
  ['Hazardous', 'Hazardous'],
  ['Recyclable', 'Recyclable'],
  ['Compostable', 'Compostable'],
  ['InertWaste', 'Inert Waste'],
  ['EWaste', 'E-waste'],
  ['Medical', 'Medical'],
  ['Radioactive', 'Radioactive'],
]);

export type SheWasteMeasurementUnit = 'Kilograms' | 'Litres' | 'CubicMetres' | 'Units' | 'Tonnes';

export const SHE_WASTE_UNIT_OPTIONS = opts<SheWasteMeasurementUnit>([
  ['Kilograms', 'Kilograms'],
  ['Litres', 'Litres'],
  ['CubicMetres', 'Cubic Metres'],
  ['Units', 'Units'],
  ['Tonnes', 'Tonnes'],
]);

export type SheWasteDisposalMethod =
  | 'Landfill'
  | 'Incineration'
  | 'Recycling'
  | 'ChemicalTreatment'
  | 'BiologicalTreatment'
  | 'SecureStorage'
  | 'ReturnToSupplier'
  | 'Composting';

export const SHE_WASTE_DISPOSAL_METHOD_OPTIONS = opts<SheWasteDisposalMethod>([
  ['Landfill', 'Landfill'],
  ['Incineration', 'Incineration'],
  ['Recycling', 'Recycling'],
  ['ChemicalTreatment', 'Chemical Treatment'],
  ['BiologicalTreatment', 'Biological Treatment'],
  ['SecureStorage', 'Secure Storage'],
  ['ReturnToSupplier', 'Return to Supplier'],
  ['Composting', 'Composting'],
]);

export type SheEnvironmentalIncidentType =
  | 'ChemicalSpill'
  | 'OilSpill'
  | 'AirEmissionExceedance'
  | 'WaterContamination'
  | 'SoilContamination'
  | 'UncontrolledDumping'
  | 'NoiseExceedance'
  | 'DustExceedance'
  | 'Other';

export const SHE_ENV_INCIDENT_TYPE_OPTIONS = opts<SheEnvironmentalIncidentType>([
  ['ChemicalSpill', 'Chemical Spill'],
  ['OilSpill', 'Oil Spill'],
  ['AirEmissionExceedance', 'Air Emission Exceedance'],
  ['WaterContamination', 'Water Contamination'],
  ['SoilContamination', 'Soil Contamination'],
  ['UncontrolledDumping', 'Uncontrolled Dumping'],
  ['NoiseExceedance', 'Noise Exceedance'],
  ['DustExceedance', 'Dust Exceedance'],
  ['Other', 'Other'],
]);

export type SheEnvironmentalMedia = 'Air' | 'Water' | 'Soil' | 'Groundwater' | 'Marine' | 'Multiple';

export const SHE_ENV_MEDIA_OPTIONS = opts<SheEnvironmentalMedia>([
  ['Air', 'Air'],
  ['Water', 'Water'],
  ['Soil', 'Soil'],
  ['Groundwater', 'Groundwater'],
  ['Marine', 'Marine'],
  ['Multiple', 'Multiple'],
]);

export type SheIncidentSeverity = 'Negligible' | 'Minor' | 'Moderate' | 'Major' | 'Catastrophic';

export const SHE_INCIDENT_SEVERITY_OPTIONS = opts<SheIncidentSeverity>([
  ['Negligible', 'Negligible'],
  ['Minor', 'Minor'],
  ['Moderate', 'Moderate'],
  ['Major', 'Major'],
  ['Catastrophic', 'Catastrophic'],
]);

export type SheEnvironmentalIncidentStatus =
  | 'Reported'
  | 'ResponseInProgress'
  | 'Contained'
  | 'Remediated'
  | 'PendingRegulatoryClosure'
  | 'Closed';

/** 'Closed' is deliberately absent — closing goes through the close endpoint, never the edit form. */
export const SHE_ENV_INCIDENT_EDIT_STATUS_OPTIONS = opts<SheEnvironmentalIncidentStatus>([
  ['Reported', 'Reported'],
  ['ResponseInProgress', 'Response in Progress'],
  ['Contained', 'Contained'],
  ['Remediated', 'Remediated'],
  ['PendingRegulatoryClosure', 'Pending Regulatory Closure'],
]);

export type SheEnvironmentalMonitoringType =
  | 'Noise'
  | 'AmbientDust'
  | 'AirQuality'
  | 'WaterQuality'
  | 'Vibration'
  | 'Emissions'
  | 'SoilQuality'
  | 'Lighting';

export const SHE_ENV_MONITORING_TYPE_OPTIONS = opts<SheEnvironmentalMonitoringType>([
  ['Noise', 'Noise'],
  ['AmbientDust', 'Ambient Dust'],
  ['AirQuality', 'Air Quality'],
  ['WaterQuality', 'Water Quality'],
  ['Vibration', 'Vibration'],
  ['Emissions', 'Emissions'],
  ['SoilQuality', 'Soil Quality'],
  ['Lighting', 'Lighting'],
]);

// ── Waste types ───────────────────────────────────────────────────────────────

export interface SheWasteType extends AuditFields {
  tenantId: string;
  code: string;
  name: string;
  classification: SheWasteClassification;
  classificationName: string;
  disposalRequirements?: string | null;
  regulatoryReference?: string | null;
  /** When true, disposal records for this type must carry a manifest number and certificate. */
  requiresManifest: boolean;
  isActive: boolean;
}

export interface SheWasteTypeCreateRequest {
  /** Unique per tenant — a duplicate is refused (422). Immutable after creation. */
  code: string;
  name: string;
  classification: SheWasteClassification;
  disposalRequirements?: string | null;
  regulatoryReference?: string | null;
  requiresManifest: boolean;
  isActive: boolean;
}

export interface SheWasteTypeUpdateRequest {
  id: string;
  name: string;
  classification: SheWasteClassification;
  disposalRequirements?: string | null;
  regulatoryReference?: string | null;
  requiresManifest: boolean;
  isActive: boolean;
}

// ── Disposal records ──────────────────────────────────────────────────────────

export interface SheWasteDisposalRecordSummary {
  id: string;
  recordNumber: string;
  wasteTypeName: string;
  disposalDate: string;
  quantity: number;
  unit: SheWasteMeasurementUnit;
  disposalMethod: SheWasteDisposalMethod;
  wasteContractorName?: string | null;
}

export interface SheWasteDisposalRecord extends AuditFields {
  tenantId: string;
  recordNumber: string;
  wasteTypeId: string;
  wasteTypeName: string;
  wasteClassification: SheWasteClassification;
  locationId?: string | null;
  locationName?: string | null;
  generationArea?: string | null;
  disposalDate: string;
  quantity: number;
  unit: SheWasteMeasurementUnit;
  disposalMethod: SheWasteDisposalMethod;
  wasteContractorId?: string | null;
  wasteContractorName?: string | null;
  manifestNumber?: string | null;
  disposalSite?: string | null;
  notes?: string | null;
  recordedById: string;
  recordedByName: string;
  /** The disposal certificate — required when the waste type requires a manifest. */
  documentPath?: string | null;
}

export interface SheWasteDisposalRecordCreateRequest {
  /** Leave blank — the server assigns WD-YYYY-NNNN. */
  recordNumber?: string | null;
  wasteTypeId: string;
  locationId?: string | null;
  generationArea?: string | null;
  disposalDate: string;
  quantity: number;
  unit: SheWasteMeasurementUnit;
  disposalMethod: SheWasteDisposalMethod;
  wasteContractorId?: string | null;
  manifestNumber?: string | null;
  disposalSite?: string | null;
  notes?: string | null;
  recordedById: string;
  documentPath?: string | null;
}

export interface SheWasteDisposalRecordUpdateRequest {
  id: string;
  wasteTypeId: string;
  locationId?: string | null;
  generationArea?: string | null;
  disposalDate: string;
  quantity: number;
  unit: SheWasteMeasurementUnit;
  disposalMethod: SheWasteDisposalMethod;
  wasteContractorId?: string | null;
  manifestNumber?: string | null;
  disposalSite?: string | null;
  notes?: string | null;
  documentPath?: string | null;
}

// ── Environmental incidents ───────────────────────────────────────────────────

export interface SheEnvironmentalIncidentSummary {
  id: string;
  incidentNumber: string;
  type: SheEnvironmentalIncidentType;
  typeName: string;
  affectedMedia: SheEnvironmentalMedia;
  affectedMediaName: string;
  severity: SheIncidentSeverity;
  severityName: string;
  incidentDate: string;
  locationName?: string | null;
  reportedToEpa: boolean;
  status: SheEnvironmentalIncidentStatus;
  statusName: string;
}

export interface SheEnvironmentalIncident extends AuditFields {
  tenantId: string;
  incidentNumber: string;
  safetyIncidentId?: string | null;
  safetyIncidentNumber?: string | null;
  type: SheEnvironmentalIncidentType;
  affectedMedia: SheEnvironmentalMedia;
  severity: SheIncidentSeverity;
  incidentDate: string;
  locationId?: string | null;
  locationName?: string | null;
  specificArea?: string | null;
  description: string;
  spillVolume?: string | null;
  substanceInvolved?: string | null;
  immediateResponseAction?: string | null;
  reportedToEpa: boolean;
  epaNotificationDate?: string | null;
  epaReferenceNumber?: string | null;
  status: SheEnvironmentalIncidentStatus;
  statusName: string;
  reportedById: string;
  reportedByName: string;
  reportedDate: string;
  investigationFindings?: string | null;
  correctiveActions?: string | null;
  closedDate?: string | null;
  closedById?: string | null;
  closedByName?: string | null;
}

export interface SheEnvironmentalIncidentCreateRequest {
  /** Leave blank — the server assigns ENV-YYYY-NNNN. */
  incidentNumber?: string | null;
  safetyIncidentId?: string | null;
  type: SheEnvironmentalIncidentType;
  affectedMedia: SheEnvironmentalMedia;
  severity: SheIncidentSeverity;
  incidentDate: string;
  locationId?: string | null;
  specificArea?: string | null;
  description: string;
  spillVolume?: string | null;
  substanceInvolved?: string | null;
  immediateResponseAction?: string | null;
  reportedById: string;
  reportedDate?: string;
}

/** Closed incidents refuse edits; setting status to Closed here is refused — use close. */
export interface SheEnvironmentalIncidentUpdateRequest {
  id: string;
  safetyIncidentId?: string | null;
  type: SheEnvironmentalIncidentType;
  affectedMedia: SheEnvironmentalMedia;
  severity: SheIncidentSeverity;
  incidentDate: string;
  locationId?: string | null;
  specificArea?: string | null;
  description: string;
  spillVolume?: string | null;
  substanceInvolved?: string | null;
  immediateResponseAction?: string | null;
  reportedToEpa: boolean;
  epaNotificationDate?: string | null;
  epaReferenceNumber?: string | null;
  status: SheEnvironmentalIncidentStatus;
  investigationFindings?: string | null;
  correctiveActions?: string | null;
}

export interface SheEnvironmentalIncidentCloseRequest {
  incidentId: string;
  closedById: string;
  closedDate: string;
  correctiveActions?: string | null;
}

// ── Monitoring records ────────────────────────────────────────────────────────

export interface SheEnvironmentalMonitoringRecord extends AuditFields {
  tenantId: string;
  recordNumber: string;
  monitoringType: SheEnvironmentalMonitoringType;
  monitoringTypeName: string;
  locationId?: string | null;
  locationName?: string | null;
  monitoringPoint?: string | null;
  measurementDate: string;
  measuredValue: number;
  unit: string;
  regulatoryLimit?: number | null;
  actionLevel?: number | null;
  /** Computed server-side from the measured value and limits — never client-supplied. */
  exceedsLimit: boolean;
  exceedsActionLevel: boolean;
  instrumentUsed?: string | null;
  weatherConditions?: string | null;
  measuredById: string;
  measuredByName: string;
  comments?: string | null;
  documentPath?: string | null;
}

export interface SheEnvironmentalMonitoringRecordCreateRequest {
  /** Leave blank — the server assigns EM-YYYY-NNNN. */
  recordNumber?: string | null;
  monitoringType: SheEnvironmentalMonitoringType;
  locationId?: string | null;
  monitoringPoint?: string | null;
  measurementDate: string;
  measuredValue: number;
  unit: string;
  regulatoryLimit?: number | null;
  actionLevel?: number | null;
  instrumentUsed?: string | null;
  weatherConditions?: string | null;
  measuredById: string;
  comments?: string | null;
  documentPath?: string | null;
}

/** The measurer is fixed once recorded. */
export interface SheEnvironmentalMonitoringRecordUpdateRequest {
  id: string;
  monitoringType: SheEnvironmentalMonitoringType;
  locationId?: string | null;
  monitoringPoint?: string | null;
  measurementDate: string;
  measuredValue: number;
  unit: string;
  regulatoryLimit?: number | null;
  actionLevel?: number | null;
  instrumentUsed?: string | null;
  weatherConditions?: string | null;
  comments?: string | null;
  documentPath?: string | null;
}
