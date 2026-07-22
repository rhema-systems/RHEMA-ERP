import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';
import { workflowApiService } from './workflow-api.service';
import {
  WorkflowApprovalType,
  WorkflowAssignmentType,
  type WorkflowDocumentRequirementDto,
  WorkflowFieldType,
  type WorkflowQualityCheckDto,
  WorkflowRejectionHandling,
  WorkflowStepType,
} from '@/types/workflow';
import type { CreateWorkflowDefinitionAdminDto } from '@/types/workflow';

export type AcquisitionActionType = 'primary' | 'reject';

export type AcquisitionWorkspaceKind =
  | 'parcel-identification'
  | 'suitability-approval'
  | 'cadastral-survey'
  | 'cadastral-verification'
  | 'ownership-classification'
  | 'ownership-verification'
  | 'agreement-negotiation'
  | 'agreement-approval'
  | 'execution'
  | 'statutory-consent'
  | 'statutory-consent-approval'
  | 'stamp-duty-assessment'
  | 'stamp-duty-approval'
  | 'stamp-duty-payment'
  | 'registration'
  | 'asset-creation';

export interface LandAcquisitionStageDefinition {
  id: number;
  order: number;
  title: string;
  description: string;
  workspaceKind: AcquisitionWorkspaceKind;
  demoUiRoute: string;
  method: 'GET' | 'POST';
  procedureId?: number;
  primaryAction: string;
  rejectAction?: string;
  requiredRole: string;
  estimatedHours: number;
}

export interface LandAcquisitionItem {
  id: string;
  projectReference: string;
  location: string;
  status: string;
  stageOrder: number;
  currentStage: string;
  intendedUse: string;
  estimatedSize: string;
  ownerName?: string;
  acquisitionType?: string;
  documents: number;
  notes: number;
  lastActivity: string;
  valueEstimate?: string;
  riskLevel?: 'Low' | 'Medium' | 'High';
  stageInputsComplete: boolean;
  missingInputs: string[];
}

export interface LandAcquisitionStage extends LandAcquisitionStageDefinition {
  count: number;
  items: LandAcquisitionItem[];
}

export interface LandAcquisitionBoard {
  stages: LandAcquisitionStage[];
}

export interface LandAcquisitionActionPayload {
  acquisitionId: string;
  procedure: number;
  actionType: AcquisitionActionType;
  comments?: string;
}

export interface WorkspaceSavePayload {
  acquisitionId?: string;
  procedureId: number;
  workspaceKind: AcquisitionWorkspaceKind;
  values: Record<string, string | boolean>;
}

export interface WorkspaceData {
  acquisitionId: string;
  procedureId: number;
  values: Record<string, string | boolean>;
  stageInputsComplete: boolean;
  missingInputs: string[];
}

export interface LandAcquisitionStageSummary {
  procedureId: number;
  title: string;
  values: Record<string, string | boolean | number | null>;
}

export interface LandAcquisitionSummary {
  acquisitionId: string;
  stages: LandAcquisitionStageSummary[];
}

export interface LandAcquisitionDocument {
  id: string;
  landAcquisitionId: string;
  fileName: string;
  documentType: string;
  documentName?: string;
  procedureId: number;
  procedure: string;
  uploadedAt: string;
  uploadedBy?: string;
}

export interface LandAcquisitionStageDocumentRequirement {
  id: string;
  requirementKey: string;
  documentName: string;
  documentType?: string;
  isRequired: boolean;
}

export interface EstateManagedAsset {
  id: string;
  assetCode: string;
  name: string;
  description?: string;
  location?: string;
  assetType: number;
  status: number;
  sourceType: number;
  landAcquisitionId?: string;
  areaSquareMeters?: number;
  valuationAmount?: number;
  currency: string;
  notes?: string;
}

interface MaybeApiResponse<T> {
  success?: boolean;
  data?: T;
  stages?: LandAcquisitionStage[];
  item?: LandAcquisitionItem;
  message?: string;
}

interface WorkflowTemplateField {
  name: string;
  label: string;
  fieldType: WorkflowFieldType;
  isRequired?: boolean;
  options?: string[];
}

interface StageWorkflowRequirement {
  documents: Array<{ key: string; name: string; type: string }>;
  checklist: Array<{ name: string; description: string }>;
  fields: WorkflowTemplateField[];
}

const stage = (
  id: number,
  title: string,
  workspaceKind: AcquisitionWorkspaceKind,
  route: string,
  description: string,
  primaryAction: string,
  rejectAction: string,
  requiredRole: string,
  estimatedHours: number,
  method: 'GET' | 'POST' = 'GET',
  procedureId?: number
): LandAcquisitionStageDefinition => ({
  id,
  order: id,
  title,
  description,
  workspaceKind,
  demoUiRoute: route,
  method,
  procedureId,
  primaryAction,
  rejectAction,
  requiredRole,
  estimatedHours,
});

export const ACQUISITION_STAGES: LandAcquisitionStageDefinition[] = [
  stage(0, 'Parcel Identification', 'parcel-identification', '/LandParcel/ParcelIdentificationView', 'Identify the land parcel, intended use, location, size, and opening notes.', 'Submit for Suitability Approval', 'Return Identification', 'Estate Officer', 8),
  stage(1, 'Suitability Approval', 'suitability-approval', '/LandParcel/SuitabilityApprovalView', 'Review planning fit, access, environmental constraints, and acquisition suitability.', 'Approve Suitability', 'Reject Suitability', 'Estate Manager', 12),
  stage(2, 'Cadastral Survey', 'cadastral-survey', '/LandParcel/CadastralSurvey', 'Capture cadastral survey plan, coordinates, demarcation details, and survey documents.', 'Submit for Survey Verification', 'Return Survey', 'Survey Officer', 24),
  stage(3, 'Cadastral Survey Verification', 'cadastral-verification', '/LandParcel/CadastralSurveyVerifcation', 'Verify cadastral match, boundary consistency, encumbrances, and survey overlap checks.', 'Approve Survey Verification', 'Reject Survey Verification', 'Senior Surveyor', 16),
  stage(4, 'Ownership Classification', 'ownership-classification', '/LandParcel/OwnershipClassification', 'Classify ownership as stool, family, private, state, allodial, or mixed interest.', 'Submit Ownership Classification', 'Return Classification', 'Legal Officer', 8),
  stage(5, 'Ownership Verification', 'ownership-verification', '/LandParcel/OwnershipVerification', 'Verify title documents, identity, searches, authority to sell, and ownership history.', 'Approve Ownership Verification', 'Reject Ownership Verification', 'Legal Manager', 24, 'POST', 5),
  stage(6, 'Agreement Negotiation', 'agreement-negotiation', '/LandParcel/AgreementNegotiation', 'Record offers, counteroffers, negotiated value, conditions, and negotiation notes.', 'Approve Negotiation', 'Return Negotiation', 'Acquisition Committee', 32, 'POST', 6),
  stage(7, 'Agreement Approval', 'agreement-approval', '/LandParcel/AgreementApproval', 'Approve negotiated agreement terms before land instrument execution.', 'Approve Agreement', 'Reject Agreement', 'Executive Approver', 16, 'POST', 7),
  stage(8, 'Land Instrument Execution', 'execution', '/LandParcel/Execution', 'Capture execution details for the conveyance, assignment, lease, or acquisition instrument.', 'Submit Executed Instrument', 'Return Execution', 'Legal Officer', 16, 'GET', 8),
  stage(9, 'Statutory Consent', 'statutory-consent', '/LandParcel/StatutoryConsent', 'Prepare and submit statutory consent application to the appropriate authority.', 'Submit Statutory Consent', 'Return Consent Application', 'Lands Commission Liaison', 24, 'GET', 9),
  stage(10, 'Statutory Consent Approval', 'statutory-consent-approval', '/LandParcel/StatutoryConsentApproval', 'Review statutory consent approval reference, conditions, approval date, and documents.', 'Approve Statutory Consent', 'Reject Statutory Consent', 'Legal Manager', 12, 'GET', 10),
  stage(11, 'Stamp Duty Assessment', 'stamp-duty-assessment', '/LandParcel/StampDutyAssessment', 'Record valuation, assessed value, stamp duty amount, and assessment reference.', 'Submit Stamp Duty Assessment', 'Return Assessment', 'Finance Officer', 12, 'GET', 11),
  stage(12, 'Stamp Duty Approval', 'stamp-duty-approval', '/LandParcel/StampDutyApproval', 'Approve the stamp duty assessment before payment is processed.', 'Approve Stamp Duty Assessment', 'Reject Stamp Duty Assessment', 'Finance Manager', 8, 'GET', 12),
  stage(13, 'Stamp Duty Payment', 'stamp-duty-payment', '/LandParcel/StampDutyPaymentPage', 'Capture payment receipt, payment date, amount paid, and payment evidence.', 'Submit Stamp Duty Payment', 'Return Payment', 'Accounts Payable', 8, 'GET', 13),
  stage(14, 'Registration', 'registration', '/LandParcel/RegistrationStage', 'Capture registry, registration number, volume, folio, instrument date, and archive details.', 'Submit Registration', 'Return Registration', 'Land Registry Officer', 24, 'GET', 14),
  stage(15, 'Asset Creation', 'asset-creation', '/LandParcel/AssetCreation', 'Create the estate asset, assign asset code, GL account, capitalization value, and custodian.', 'Create Estate Asset', 'Return Asset Creation', 'Fixed Asset Officer', 12, 'GET', 15),
];

const WORKFLOW_STEP_IDS: Record<number, string> = {
  0: '11111111-1111-4111-8111-111111111111',
  1: '22222222-2222-4222-8222-222222222222',
  2: '33333333-3333-4333-8333-333333333333',
  3: '44444444-4444-4444-8444-444444444444',
  4: '55555555-5555-4555-8555-555555555555',
  5: '66666666-6666-4666-8666-666666666666',
  6: '77777777-7777-4777-8777-777777777777',
  7: '88888888-8888-4888-8888-888888888888',
  8: '99999999-9999-4999-8999-999999999999',
  9: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  10: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  11: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  12: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
  13: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
  14: 'ffffffff-ffff-4fff-8fff-ffffffffffff',
  15: '12121212-1212-4212-8212-121212121212',
};

const tf = (
  name: string,
  label: string,
  fieldType: WorkflowFieldType = WorkflowFieldType.Text,
  isRequired = true,
  options?: string[]
): WorkflowTemplateField => ({ name, label, fieldType, isRequired, options });

const STAGE_WORKFLOW_REQUIREMENTS: Record<number, StageWorkflowRequirement> = {
  0: {
    documents: [
      { key: 'planning-evidence', name: 'Planning Evidence', type: 'Planning' },
      { key: 'site-photo', name: 'Site Photograph', type: 'Photo' },
      { key: 'acquisition-request', name: 'Acquisition Request Memo', type: 'Memo' },
    ],
    checklist: [
      { name: 'Parcel reference confirmed', description: 'Project reference, location, intended use, and estimated size are complete.' },
      { name: 'Owner or vendor captured', description: 'Vendor or owner name and acquisition type are recorded.' },
    ],
    fields: [
      tf('projectReference', 'Project Reference'),
      tf('parcelLocation', 'Location'),
      tf('estimatedSize', 'Estimated Size', WorkflowFieldType.Number),
      tf('vendorName', 'Vendor / Owner'),
      tf('acquisitionType', 'Acquisition Type', WorkflowFieldType.Select, true, ['Direct Purchase', 'Assignment', 'Leasehold', 'Conveyance']),
      tf('intendedUse', 'Intended Use', WorkflowFieldType.TextArea),
    ],
  },
  1: {
    documents: [
      { key: 'physical-assessment-report', name: 'Physical Assessment Report', type: 'Assessment' },
      { key: 'zoning-planning-clearance', name: 'Zoning / Planning Clearance', type: 'Planning' },
      { key: 'access-utility-evidence', name: 'Access and Utility Evidence', type: 'Evidence' },
    ],
    checklist: [
      { name: 'Physical inspection completed', description: 'Soil, topography, flooding, access, and utility observations have been recorded.' },
      { name: 'Planning compatibility determined', description: 'Zoning, environmental clearance, and planning compatibility have been reviewed.' },
    ],
    fields: [
      tf('inspectionDate', 'Inspection Date', WorkflowFieldType.Date),
      tf('inspectionOfficer', 'Inspection Officer'),
      tf('soilType', 'Soil Type', WorkflowFieldType.Select, true, ['Laterite', 'Clay', 'Sandy', 'Rocky', 'Mixed']),
      tf('topography', 'Topography', WorkflowFieldType.Select, true, ['Flat', 'Gentle Slope', 'Steep Slope', 'Undulating']),
      tf('zoningClassification', 'Zoning Classification'),
      tf('planningCompatible', 'Planning Compatible', WorkflowFieldType.Boolean),
      tf('accessConfirmed', 'Access Confirmed', WorkflowFieldType.Boolean),
      tf('environmentalClearance', 'Environmental Clearance', WorkflowFieldType.Boolean),
      tf('utilityAvailability', 'Utility Availability', WorkflowFieldType.Boolean),
      tf('isFloodProne', 'Flood Prone', WorkflowFieldType.Boolean, false),
      tf('approvalNotes', 'Assessment Recommendation', WorkflowFieldType.TextArea),
    ],
  },
  2: {
    documents: [
      { key: 'survey-plan', name: 'Survey Plan', type: 'Survey Plan' },
      { key: 'cadastral-site-plan', name: 'Cadastral Site Plan', type: 'Cadastral' },
      { key: 'beacon-demarcation-evidence', name: 'Beacon / Demarcation Evidence', type: 'Evidence' },
    ],
    checklist: [
      { name: 'Beacon coordinates captured', description: 'Beacon index, northing, easting, bearing, and distance in feet have been captured from the survey plan.' },
      { name: 'Survey metadata captured', description: 'Survey plan number, map sheet number, surveyor, and survey date are recorded.' },
    ],
    fields: [
      tf('cadastreDescription', 'Cadastre Description'),
      tf('totalArea', 'Total Area', WorkflowFieldType.Number),
      tf('areaUnit', 'Area Unit', WorkflowFieldType.Select, true, ['sq ft', 'acres', 'hectares', 'sqm']),
      tf('beacon1Index', 'Beacon 1 Index'),
      tf('beacon1NorthingFeet', 'Beacon 1 Northing (Y, ft)', WorkflowFieldType.Number),
      tf('beacon1EastingFeet', 'Beacon 1 Easting (X, ft)', WorkflowFieldType.Number),
      tf('beacon1Bearing', 'Beacon 1 Bearing', WorkflowFieldType.Text, false),
      tf('beacon1DistanceFeet', 'Beacon 1 Distance (ft)', WorkflowFieldType.Number, false),
      tf('beacon2Index', 'Beacon 2 Index'),
      tf('beacon2NorthingFeet', 'Beacon 2 Northing (Y, ft)', WorkflowFieldType.Number),
      tf('beacon2EastingFeet', 'Beacon 2 Easting (X, ft)', WorkflowFieldType.Number),
      tf('beacon2Bearing', 'Beacon 2 Bearing', WorkflowFieldType.Text, false),
      tf('beacon2DistanceFeet', 'Beacon 2 Distance (ft)', WorkflowFieldType.Number, false),
      tf('beacon3Index', 'Beacon 3 Index'),
      tf('beacon3NorthingFeet', 'Beacon 3 Northing (Y, ft)', WorkflowFieldType.Number),
      tf('beacon3EastingFeet', 'Beacon 3 Easting (X, ft)', WorkflowFieldType.Number),
      tf('beacon3Bearing', 'Beacon 3 Bearing', WorkflowFieldType.Text, false),
      tf('beacon3DistanceFeet', 'Beacon 3 Distance (ft)', WorkflowFieldType.Number, false),
      tf('beacon4Index', 'Beacon 4 Index'),
      tf('beacon4NorthingFeet', 'Beacon 4 Northing (Y, ft)', WorkflowFieldType.Number),
      tf('beacon4EastingFeet', 'Beacon 4 Easting (X, ft)', WorkflowFieldType.Number),
      tf('beacon4Bearing', 'Beacon 4 Bearing', WorkflowFieldType.Text, false),
      tf('beacon4DistanceFeet', 'Beacon 4 Distance (ft)', WorkflowFieldType.Number, false),
      tf('boundaryCoordinates', 'Beacon Coordinate JSON', WorkflowFieldType.TextArea, false),
      tf('surveyorName', 'Surveyor Name'),
      tf('surveyDate', 'Survey Date', WorkflowFieldType.Date),
      tf('surveyPlanNumber', 'Survey Plan Number'),
      tf('mapSheetNumber', 'Map Sheet Number'),
    ],
  },
  3: {
    documents: [
      { key: 'survey-verification-report', name: 'Survey Verification Report', type: 'Verification' },
      { key: 'overlap-clearance', name: 'Overlap Clearance Evidence', type: 'Clearance' },
    ],
    checklist: [
      { name: 'Cadastral match confirmed', description: 'Submitted survey matches the parcel and acquisition record.' },
      { name: 'Boundary and overlap cleared', description: 'Boundary consistency and overlap checks have been completed.' },
    ],
    fields: [
      tf('cadastralMatch', 'Cadastral Match', WorkflowFieldType.Boolean),
      tf('overlapCleared', 'Overlap Cleared', WorkflowFieldType.Boolean),
      tf('boundaryConfirmed', 'Boundary Confirmed', WorkflowFieldType.Boolean),
      tf('verificationReference', 'Verification Reference'),
      tf('verificationNotes', 'Verification Notes', WorkflowFieldType.TextArea),
    ],
  },
  4: {
    documents: [
      { key: 'ownership-classification-memo', name: 'Ownership Classification Memo', type: 'Memo' },
      { key: 'owner-identification', name: 'Owner Identification', type: 'Identity' },
      { key: 'witness-oath', name: 'Witness Oath / Statutory Declaration', type: 'Declaration' },
    ],
    checklist: [
      { name: 'Ownership type classified', description: 'Ownership type, tenure, acquisition method, and interest held are complete.' },
      { name: 'Witness details captured', description: 'Witness names, contacts, relationship, address, and oath details are recorded where applicable.' },
    ],
    fields: [
      tf('ownershipType', 'Ownership Type', WorkflowFieldType.Select, true, ['Allodial', 'Stool', 'Family', 'Private', 'State', 'Mixed Interest']),
      tf('ownerName', 'Owner Name'),
      tf('contactNumber', 'Contact Number'),
      tf('acquisitionMethod', 'Acquisition Method', WorkflowFieldType.Select, true, ['Purchase', 'Gift', 'Inheritance', 'Court Order', 'Leasehold', 'Assignment', 'Conveyance']),
      tf('tenureType', 'Tenure Type'),
      tf('identificationNumber', 'Identification Number'),
      tf('classificationRisk', 'Classification Risk', WorkflowFieldType.Select, true, ['Low', 'Medium', 'High']),
    ],
  },
  5: {
    documents: [
      { key: 'title-search-report', name: 'Title Search Report', type: 'Search' },
      { key: 'authority-to-sell', name: 'Authority to Sell', type: 'Authority' },
      { key: 'property-file', name: 'Property File', type: 'Property File' },
    ],
    checklist: [
      { name: 'Title search completed', description: 'Title search is complete and search reference is recorded.' },
      { name: 'Identity and authority verified', description: 'Owner identity and authority to sell have been verified.' },
    ],
    fields: [
      tf('titleSearchCompleted', 'Title Search Completed', WorkflowFieldType.Boolean),
      tf('ownerIdentityVerified', 'Owner Identity Verified', WorkflowFieldType.Boolean),
      tf('authorityToSellVerified', 'Authority To Sell Verified', WorkflowFieldType.Boolean),
      tf('searchReference', 'Search Reference'),
      tf('verificationNotes', 'Verification Notes', WorkflowFieldType.TextArea),
    ],
  },
  6: {
    documents: [
      { key: 'seller-quote', name: 'Seller Quote', type: 'Quote' },
      { key: 'negotiation-minutes', name: 'Negotiation Minutes', type: 'Minutes' },
      { key: 'draft-heads-of-terms', name: 'Draft Heads of Terms', type: 'Draft' },
    ],
    checklist: [
      { name: 'Commercial terms captured', description: 'Seller quote, offer, counter offer, negotiated value, and payment type are recorded.' },
      { name: 'Agreement draft generated', description: 'Agreement date parts and generated agreement status are complete.' },
    ],
    fields: [
      tf('sellerQuote', 'Seller Quote', WorkflowFieldType.Number),
      tf('offerAmount', 'Offer Amount', WorkflowFieldType.Number),
      tf('counterOffer', 'Counter Offer', WorkflowFieldType.Number, false),
      tf('negotiatedValue', 'Negotiated Value', WorkflowFieldType.Number),
      tf('paymentType', 'Payment Type', WorkflowFieldType.Select, true, ['Cash', 'Cheque', 'Mobile Money', 'Bank Transfer', 'One-Off', 'Installment', 'Part Payment', 'Swap', 'Other']),
      tf('offerTerms', 'Offer Terms'),
      tf('agreementDay', 'Agreement Day'),
      tf('agreementMonth', 'Agreement Month'),
      tf('agreementYear', 'Agreement Year'),
      tf('isAccepted', 'Offer Accepted', WorkflowFieldType.Boolean),
      tf('agreementGenerated', 'Agreement Generated', WorkflowFieldType.Boolean),
      tf('negotiationNotes', 'Negotiation Notes', WorkflowFieldType.TextArea),
    ],
  },
  7: {
    documents: [
      { key: 'approved-agreement-draft', name: 'Approved Agreement Draft', type: 'Agreement' },
      { key: 'finance-review-memo', name: 'Finance Review Memo', type: 'Finance' },
      { key: 'board-approval', name: 'Board / Management Approval', type: 'Approval' },
    ],
    checklist: [
      { name: 'Legal review complete', description: 'Legal review has been completed and root of title is recorded.' },
      { name: 'Finance and approval references complete', description: 'Finance review, payment schedule, and approval reference are recorded.' },
    ],
    fields: [
      tf('agreementDate', 'Agreement Date', WorkflowFieldType.Date),
      tf('isFamilyLand', 'Family Land', WorkflowFieldType.Boolean),
      tf('isStoolLand', 'Stool Land', WorkflowFieldType.Boolean),
      tf('rootOfTitle', 'Root Of Title', WorkflowFieldType.TextArea),
      tf('specialConditions', 'Special Conditions', WorkflowFieldType.TextArea),
      tf('grantorName', 'Grantor / Seller Name'),
      tf('grantorAddress', 'Grantor / Seller Address', WorkflowFieldType.TextArea),
      tf('grantorPhone', 'Grantor / Seller Phone'),
      tf('granteeName', 'Grantee / Buyer Name'),
      tf('granteeAddress', 'Grantee / Buyer Address', WorkflowFieldType.TextArea),
      tf('granteePhone', 'Grantee / Buyer Phone'),
      tf('agreementPaymentType', 'Agreement Payment Type', WorkflowFieldType.Select, true, ['Full Payment', 'Deposit', 'Installment', 'Balance Payment', 'Other']),
      tf('agreementPaymentAmount', 'Agreement Payment Amount', WorkflowFieldType.Number),
      tf('agreementPaymentDueDate', 'Payment Due Date', WorkflowFieldType.Date),
      tf('agreementPaymentMethod', 'Payment Method', WorkflowFieldType.Select, true, ['Cash', 'Cheque', 'Mobile Money', 'Bank Transfer', 'Other']),
      tf('agreementWitness1Name', 'Agreement Witness 1 Name'),
      tf('agreementWitness1Address', 'Agreement Witness 1 Address', WorkflowFieldType.TextArea),
      tf('agreementWitness2Name', 'Agreement Witness 2 Name'),
      tf('agreementWitness2Address', 'Agreement Witness 2 Address', WorkflowFieldType.TextArea),
      tf('legalReviewComplete', 'Legal Review Complete', WorkflowFieldType.Boolean),
      tf('financeReviewComplete', 'Finance Review Complete', WorkflowFieldType.Boolean),
      tf('boardApprovalReference', 'Board Approval Reference'),
      tf('approvalConditions', 'Approval Conditions', WorkflowFieldType.TextArea),
    ],
  },
  8: {
    documents: [
      { key: 'executed-instrument', name: 'Executed Instrument', type: 'Instrument' },
      { key: 'witness-page', name: 'Witness Page', type: 'Witness' },
      { key: 'counterparty-id', name: 'Counterparty Identification', type: 'Identity' },
    ],
    checklist: [
      { name: 'Instrument executed', description: 'Instrument type, number, execution date, and signatories are recorded.' },
      { name: 'Witness details complete', description: 'Witness details are captured before moving to statutory consent.' },
    ],
    fields: [
      tf('instrumentType', 'Instrument Type', WorkflowFieldType.Select, true, ['Conveyance', 'Assignment', 'Lease', 'Vesting Assent']),
      tf('instrumentNumber', 'Instrument Number'),
      tf('executionDate', 'Execution Date', WorkflowFieldType.Date),
      tf('executedBy', 'Executed By'),
      tf('counterpartySignatory', 'Counterparty Signatory'),
      tf('isExecuted', 'Instrument Executed', WorkflowFieldType.Boolean),
    ],
  },
  9: {
    documents: [
      { key: 'consent-application', name: 'Consent Application', type: 'Application' },
      { key: 'supporting-land-documents', name: 'Supporting Land Documents', type: 'Supporting Document' },
      { key: 'application-receipt', name: 'Application Receipt', type: 'Receipt' },
    ],
    checklist: [
      { name: 'Consent application submitted', description: 'Consent authority, application number, and submission date are recorded.' },
      { name: 'Supporting documents attached', description: 'All required land documents for consent submission are attached.' },
    ],
    fields: [
      tf('consentAuthority', 'Consent Authority'),
      tf('applicationNumber', 'Application Number'),
      tf('submissionDate', 'Submission Date', WorkflowFieldType.Date),
      tf('consentNotes', 'Consent Notes', WorkflowFieldType.TextArea),
    ],
  },
  10: {
    documents: [
      { key: 'consent-approval-letter', name: 'Consent Approval Letter', type: 'Approval' },
      { key: 'conditions-schedule', name: 'Consent Conditions Schedule', type: 'Conditions' },
    ],
    checklist: [
      { name: 'Consent approval received', description: 'Approval reference and approval date are recorded.' },
      { name: 'Consent conditions reviewed', description: 'Any conditions attached to the consent have been recorded.' },
    ],
    fields: [
      tf('approvalReference', 'Approval Reference'),
      tf('approvalDate', 'Approval Date', WorkflowFieldType.Date),
      tf('consentConditions', 'Consent Conditions', WorkflowFieldType.TextArea),
    ],
  },
  11: {
    documents: [
      { key: 'valuation-report', name: 'Valuation Report', type: 'Valuation' },
      { key: 'stamp-duty-assessment-notice', name: 'Stamp Duty Assessment Notice', type: 'Assessment' },
    ],
    checklist: [
      { name: 'Assessment values captured', description: 'Property value, duty amount, authority, reference, and date are recorded.' },
      { name: 'Assessment notice attached', description: 'Stamp duty assessment notice has been attached.' },
    ],
    fields: [
      tf('propertyValue', 'Property Value', WorkflowFieldType.Number),
      tf('stampDutyAmount', 'Stamp Duty Amount', WorkflowFieldType.Number),
      tf('assessmentAuthority', 'Assessment Authority'),
      tf('assessmentReference', 'Assessment Reference'),
      tf('assessmentDate', 'Assessment Date', WorkflowFieldType.Date),
    ],
  },
  12: {
    documents: [
      { key: 'finance-approval-memo', name: 'Finance Approval Memo', type: 'Finance' },
      { key: 'approved-assessment-notice', name: 'Approved Assessment Notice', type: 'Assessment' },
    ],
    checklist: [
      { name: 'Finance approval complete', description: 'Finance approval reference, approved amount, and approver are recorded.' },
      { name: 'Approved assessment attached', description: 'The approved assessment notice is attached before payment.' },
    ],
    fields: [
      tf('financeApprovalReference', 'Finance Approval Reference'),
      tf('approvedDutyAmount', 'Approved Duty Amount', WorkflowFieldType.Number),
      tf('approverName', 'Approver Name'),
      tf('approvalNotes', 'Approval Notes', WorkflowFieldType.TextArea),
    ],
  },
  13: {
    documents: [
      { key: 'payment-receipt', name: 'Payment Receipt', type: 'Receipt' },
      { key: 'bank-payment-evidence', name: 'Bank Payment Evidence', type: 'Payment Evidence' },
      { key: 'stamped-instrument-copy', name: 'Stamped Instrument Copy', type: 'Instrument' },
    ],
    checklist: [
      { name: 'Payment captured', description: 'Receipt number, payment reference, date, amount, and method are recorded.' },
      { name: 'Payment evidence attached', description: 'Receipt and payment evidence are attached before registration.' },
    ],
    fields: [
      tf('receiptNumber', 'Receipt Number'),
      tf('paymentReference', 'Payment Reference'),
      tf('paymentDate', 'Payment Date', WorkflowFieldType.Date),
      tf('amountPaid', 'Amount Paid', WorkflowFieldType.Number),
      tf('paymentMethod', 'Payment Method', WorkflowFieldType.Select, true, ['Bank Transfer', 'Cheque', 'Cash', 'Mobile Money']),
    ],
  },
  14: {
    documents: [
      { key: 'registration-certificate', name: 'Lands Commission Registration Certificate', type: 'Certificate' },
      { key: 'registered-instrument', name: 'Registered Instrument', type: 'Instrument' },
      { key: 'registry-extract', name: 'Registry Extract', type: 'Registry' },
    ],
    checklist: [
      { name: 'Registration details captured', description: 'Registry office, number, volume, folio, and date are recorded.' },
      { name: 'Registered instrument attached', description: 'Registered instrument and registry extract are attached.' },
    ],
    fields: [
      tf('registryOffice', 'Registry Office'),
      tf('registrationNumber', 'Registration Number'),
      tf('volume', 'Volume'),
      tf('folio', 'Folio'),
      tf('registrationDate', 'Registration Date', WorkflowFieldType.Date),
    ],
  },
  15: {
    documents: [
      { key: 'asset-creation-memo', name: 'Asset Creation Memo', type: 'Memo' },
      { key: 'capitalization-approval', name: 'GL / Capitalization Approval', type: 'Finance' },
      { key: 'project-handoff-pack', name: 'Project Handoff Pack', type: 'Handoff' },
    ],
    checklist: [
      { name: 'Land asset record complete', description: 'Asset code, parcel identifier, owner, size, purpose, and zoning are recorded.' },
      { name: 'Ready for project handoff', description: 'Capitalization details and project handoff pack are complete.' },
    ],
    fields: [
      tf('assetCode', 'Asset Code'),
      tf('parcelIdentifier', 'Parcel Identifier'),
      tf('assetLocation', 'Asset Location'),
      tf('size', 'Size', WorkflowFieldType.Number),
      tf('sizeUnit', 'Size Unit', WorkflowFieldType.Select, true, ['Acres', 'Hectares', 'sqm']),
      tf('purpose', 'Purpose'),
      tf('capitalizationValue', 'Capitalization Value', WorkflowFieldType.Number),
      tf('custodian', 'Custodian'),
    ],
  },
};

function requirementsFor(stageDefinition: LandAcquisitionStageDefinition): StageWorkflowRequirement {
  return STAGE_WORKFLOW_REQUIREMENTS[stageDefinition.order] || {
    documents: [{ key: `${stageDefinition.workspaceKind}-document`, name: `${stageDefinition.title} Document`, type: stageDefinition.title }],
    checklist: [{ name: `${stageDefinition.title} reviewed`, description: stageDefinition.description }],
    fields: [],
  };
}

function normalizeStage(raw: Partial<LandAcquisitionStage>, definition: LandAcquisitionStageDefinition): LandAcquisitionStage {
  const items = raw.items || [];
  return {
    ...definition,
    ...raw,
    id: definition.id,
    order: definition.order,
    workspaceKind: definition.workspaceKind,
    demoUiRoute: definition.demoUiRoute,
    method: definition.method,
    procedureId: definition.procedureId,
    primaryAction: raw.primaryAction || definition.primaryAction,
    rejectAction: raw.rejectAction || definition.rejectAction,
    count: raw.count ?? items.length,
    items,
  };
}

function buildFallbackBoard(): LandAcquisitionBoard {
  return {
    stages: ACQUISITION_STAGES.map((definition) => normalizeStage({ items: [], count: 0 }, definition)),
  };
}

function unwrapBoard(response: MaybeApiResponse<LandAcquisitionBoard | LandAcquisitionStage[]>): LandAcquisitionBoard | null {
  const source = response.data ?? response;
  if (Array.isArray(source)) return { stages: source };
  if (source && 'stages' in source && Array.isArray(source.stages)) return { stages: source.stages };
  if (Array.isArray(response.stages)) return { stages: response.stages };
  return null;
}

export class EstateAcquisitionService {
  async getAcquisitionWorkflowBoard(): Promise<LandAcquisitionBoard> {
    const response = await apiService.get<MaybeApiResponse<LandAcquisitionBoard | LandAcquisitionStage[]>>('/estate/land-acquisitions/workflow-board');
    const board = unwrapBoard(response);
    if (!board) throw new Error('Unable to read the role-filtered acquisition workflow board.');

    return {
      stages: board.stages.flatMap((apiStage) => {
        const definition = ACQUISITION_STAGES.find(
          (candidate) => candidate.order === apiStage.order || candidate.id === apiStage.id
        );
        return definition ? [normalizeStage(apiStage, definition)] : [];
      }),
    };
  }

  async runWorkflowAction(payload: LandAcquisitionActionPayload): Promise<{ success: boolean; message?: string; item?: LandAcquisitionItem }> {
    const response = await apiService.post<MaybeApiResponse<{ success: boolean; message?: string; item?: LandAcquisitionItem }>>(
      '/estate/land-acquisitions/workflow-action',
      payload
    );
    const source = response.data ?? response;
    return {
      success: response.success !== false,
      message: response.message || (payload.actionType === 'primary' ? 'Workflow action completed.' : 'Acquisition returned.'),
      item: source.item,
    };
  }

  async saveWorkspace(payload: WorkspaceSavePayload): Promise<{ success: boolean; message?: string; acquisitionId: string; stageInputsComplete: boolean; missingInputs: string[] }> {
    const response = await apiService.post<MaybeApiResponse<WorkspaceData>>(
      '/estate/land-acquisitions/workspace',
      payload
    );
    const source = response.data ?? (response as unknown as WorkspaceData);
    return {
      success: response.success !== false,
      message: response.message || 'Workspace saved.',
      acquisitionId: source.acquisitionId,
      stageInputsComplete: source.stageInputsComplete === true,
      missingInputs: source.missingInputs || [],
    };
  }

  async getWorkspace(acquisitionId: string, procedureId: number): Promise<WorkspaceData> {
    const response = await apiService.get<MaybeApiResponse<WorkspaceData>>(
      `/estate/land-acquisitions/${acquisitionId}/workspace/${procedureId}`
    );
    const data = response.data ?? (response as unknown as WorkspaceData);
    return {
      acquisitionId: data.acquisitionId,
      procedureId: data.procedureId,
      values: data.values || {},
      stageInputsComplete: data.stageInputsComplete === true,
      missingInputs: data.missingInputs || [],
    };
  }

  async getSummary(acquisitionId: string): Promise<LandAcquisitionSummary> {
    const response = await apiService.get<MaybeApiResponse<LandAcquisitionSummary>>(
      `/estate/land-acquisitions/${acquisitionId}/summary`
    );
    const data = response.data ?? (response as unknown as LandAcquisitionSummary);
    return {
      acquisitionId: data.acquisitionId,
      stages: data.stages || [],
    };
  }

  async getDocuments(acquisitionId: string): Promise<LandAcquisitionDocument[]> {
    const response = await apiService.get<MaybeApiResponse<LandAcquisitionDocument[]>>(
      `/estate/land-acquisitions/${acquisitionId}/documents`
    );
    return response.data || [];
  }

  async getActiveWorkflowDocumentRequirements(): Promise<Record<number, LandAcquisitionStageDocumentRequirement[]>> {
    const catalogRequirements = ACQUISITION_STAGES.reduce<Record<number, LandAcquisitionStageDocumentRequirement[]>>((acc, stage) => {
      const requirements = requirementsFor(stage).documents.map((item, index) => ({
        id: `${stage.workspaceKind}-${item.key || index + 1}`,
        requirementKey: item.key || `${stage.workspaceKind}-document-${index + 1}`,
        documentName: item.name,
        documentType: item.type,
        isRequired: true,
      }));

      if (requirements.length > 0) {
        acc[stage.id] = requirements;
      }

      return acc;
    }, {});

    try {
      const definitions = await workflowApiService.getWorkflowDefinitions({
        page: 1,
        pageSize: 5,
        sortBy: 'CreatedAt',
        sortDescending: true,
        entityType: 'LandAcquisition',
        isActive: true,
      });

      const definition =
        definitions.data.find((item) => item.isActive && item.entityType === 'LandAcquisition') ||
        definitions.data[0];

      if (!definition) return catalogRequirements;

      const detail = await workflowApiService.getWorkflowDefinition(definition.id);
      const workflowRequirements = (detail.steps || []).reduce<Record<number, LandAcquisitionStageDocumentRequirement[]>>(
        (acc, step) => {
          const taskRequirements = (step.configuration?.taskConfig?.documentRequirements || [])
            .filter((item: WorkflowDocumentRequirementDto) => item.documentName?.trim() || item.requirementKey?.trim())
            .map((item: WorkflowDocumentRequirementDto, index: number) => ({
              id: item.id || `${step.id}-document-${index + 1}`,
              requirementKey: item.requirementKey?.trim() || `${step.id}-document-${index + 1}`,
              documentName: item.documentName?.trim() || `Document ${index + 1}`,
              documentType: item.documentType?.trim() || undefined,
              isRequired: item.isRequired !== false,
            }));
          const qualityRequirements = (step.configuration?.qualityConfig?.qualityChecks || [])
            .filter((item: WorkflowQualityCheckDto) => item.requiresDocument && (item.documentName?.trim() || item.name?.trim()))
            .map((item: WorkflowQualityCheckDto, index: number) => ({
              id: item.id || `${step.id}-check-document-${index + 1}`,
              requirementKey: item.id || `${step.id}-check-document-${index + 1}`,
              documentName: item.documentName?.trim() || item.name.trim(),
              documentType: item.documentType?.trim() || undefined,
              isRequired: item.isRequired !== false,
            }));
          const requirements = [...taskRequirements, ...qualityRequirements]
            .filter((item, index, all) =>
              all.findIndex((candidate) =>
                candidate.documentName.toLowerCase() === item.documentName.toLowerCase()
              ) === index
            );

          if (requirements.length > 0) {
            acc[step.order - 1] = requirements;
            acc[step.order] = requirements;
          }

          return acc;
        },
        {}
      );

      return {
        ...catalogRequirements,
        ...workflowRequirements,
      };
    } catch {
      return catalogRequirements;
    }
  }

  async uploadDocument(acquisitionId: string, procedureId: number, file: File, documentType: string, documentName?: string): Promise<LandAcquisitionDocument> {
    const form = new FormData();
    form.append('file', file);
    form.append('procedureId', `${procedureId}`);
    form.append('documentType', documentType);
    if (documentName) form.append('documentName', documentName);

    const response = await rawApiService.request<MaybeApiResponse<LandAcquisitionDocument>>(
      `/estate/land-acquisitions/${acquisitionId}/documents`,
      { method: 'POST', body: form }
    );

    if (!response.data) throw new Error(response.message || 'Unable to upload acquisition document.');
    return response.data;
  }

  async downloadDocument(acquisitionId: string, documentId: string): Promise<Blob> {
    return rawApiService.downloadBlob(`/estate/land-acquisitions/${acquisitionId}/documents/${documentId}/download`);
  }

  async viewDocumentPdf(acquisitionId: string, documentId: string): Promise<Blob> {
    return rawApiService.downloadBlob(`/estate/land-acquisitions/${acquisitionId}/documents/${documentId}/viewer-pdf`);
  }

  async markReadyForProjectManagement(acquisitionId: string): Promise<{ success: boolean; message?: string; asset?: EstateManagedAsset }> {
    const response = await apiService.post<MaybeApiResponse<EstateManagedAsset>>(
      `/estate/land-acquisitions/${acquisitionId}/ready-for-project-management`,
      {}
    );

    return {
      success: response.success !== false,
      message: response.message || 'Demarcated land is ready for project management.',
      asset: response.data,
    };
  }

  async createWorkflowTemplate(): Promise<void> {
    const workflowStages = ACQUISITION_STAGES;
    const steps = workflowStages.map((item) => {
      const requirements = requirementsFor(item);

      return {
        id: WORKFLOW_STEP_IDS[item.order],
        name: item.title,
        description: item.description,
        stepType: WorkflowStepType.Approval,
        order: item.order,
        isRequired: true,
        requiredRole: item.requiredRole,
        estimatedHours: item.estimatedHours,
        configuration: {
          approvalConfig: {
            approvalType: WorkflowApprovalType.Single,
            approverRules: [
              {
                assignmentType: WorkflowAssignmentType.Role,
                role: item.requiredRole,
                priority: 1,
              },
            ],
            minApprovalsRequired: 1,
            rejectionHandling: WorkflowRejectionHandling.ReturnToPreviousStep,
            preventInitiatorApproval: false,
            requireDistinctApprovers: false,
            conflictRules: [],
          },
          taskConfig: {
            taskActionType: 'stage-documents',
            requiresDocument: requirements.documents.length > 0,
            documentRequirementKey: `${item.workspaceKind}-documents`,
            documentName: `${item.title} Documents`,
            instructions: `Attach all required documents for ${item.title}: ${requirements.documents.map((doc) => doc.name).join(', ')}.`,
          },
          qualityConfig: {
            qualityChecks: [
              ...requirements.documents.map((doc) => ({
                id: `${item.workspaceKind}-${doc.key}`,
                name: `Attach ${doc.name}`,
                description: `Upload ${doc.name} before ${item.title} can be approved.`,
                isRequired: true,
                requiresDocument: true,
                documentType: doc.type,
                documentName: doc.name,
              })),
              ...requirements.checklist.map((check, index) => ({
                id: `${item.workspaceKind}-check-${index + 1}`,
                name: check.name,
                description: check.description,
                isRequired: true,
                requiresDocument: false,
              })),
            ],
          },
          formFields: requirements.fields.map((field) => ({
            name: field.name,
            label: field.label,
            fieldType: field.fieldType,
            isRequired: field.isRequired !== false,
            options: field.options,
          })),
        },
      };
    });

    const transitions = steps.slice(0, -1).map((item, index) => ({
      fromStepId: item.id,
      toStepId: steps[index + 1].id,
      name: `${item.name} to ${steps[index + 1].name}`,
      description: `Move land acquisition from ${item.name} to ${steps[index + 1].name}.`,
      isDefault: true,
      priority: index + 1,
    }));

    const payload: CreateWorkflowDefinitionAdminDto = {
      name: 'Land Acquisition Procedure - Documents and Checklists',
      description: 'Estate land acquisition procedure routed through RHEMA workflow roles, assignments, stage documents, and checklist evidence.',
      entityType: 'LandAcquisition',
      isActive: true,
      configuration: JSON.stringify({
        source: 'Estate land acquisition',
        routing: 'RHEMA workflow roles and assignments',
        documentMode: 'multiple-documents-per-stage',
        stages: ACQUISITION_STAGES,
        stageRequirements: STAGE_WORKFLOW_REQUIREMENTS,
      }),
      steps,
      transitions,
    };

    await workflowApiService.createWorkflowDefinition(payload);
  }
}

export const estateAcquisitionService = new EstateAcquisitionService();
