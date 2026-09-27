'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { SupplierInvoiceWorkspaceButton } from '@/components/procurement/SupplierInvoiceWorkspaceButton';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  ArrowRight,
  BadgeCheck,
  Ban,
  BookOpen,
  Building2,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  Download,
  Eye,
  ExternalLink,
  FileArchive,
  FileClock,
  FileCheck2,
  FileSignature,
  FileText,
  FolderOpen,
  Landmark,
  Layers3,
  Loader2,
  MapPin,
  Plus,
  Receipt,
  RefreshCw,
  Save,
  Scale,
  Search,
  Send,
  ShieldCheck,
  Stamp,
  Trash2,
  Upload,
  WalletCards,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
import { useAuth } from '@/hooks/use-auth';
import { usePaginatedItems } from '@/hooks/use-paginated-items';
import type { WorkflowTaskAttachmentDto } from '@/types/workflow';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  documentManagementService,
  type CentralDocumentRecord,
  type CentralDocumentGenerationTemplate,
  type GeneratedCentralDocumentResult,
} from '@/services/document-management.service';
import {
  ACQUISITION_STAGES,
  estateAcquisitionService,
  type AcquisitionWorkspaceKind,
  type LandAcquisitionDocument,
  type LandAcquisitionItem,
  type LandAcquisitionSummary,
  type LandAcquisitionStage,
  type LandAcquisitionStageDocumentRequirement,
  type HrLocationLookup,
} from '@/services/estate-acquisition.service';

const CadastralMapPanel = dynamic(() => import('./CadastralMapPanel'), {
  ssr: false,
});

const ProcedurePdfViewer = dynamic(
  () => import('@/components/procedures/ProcedurePdfViewer'),
  { ssr: false }
);

const ACQUISITION_APPROVAL_STAGE_IDS = new Set([1, 3, 5, 7, 11, 13]);
const LAND_ACQUISITION_AGREEMENT_TEMPLATE_CODE = 'EST-LAND-ACQ-AGREEMENT';

type FieldType =
  | 'text'
  | 'date'
  | 'textarea'
  | 'select'
  | 'check'
  | 'region'
  | 'district';

interface WorkspaceField {
  key: string;
  label: string;
  type: FieldType;
  options?: string[];
  span?: 1 | 2;
  readOnly?: boolean;
}

interface WorkspaceSection {
  title: string;
  description: string;
  keys: string[];
}

type PendingAcquisitionDocument = {
  file: File;
  documentType: string;
  documentName: string;
  requirementId?: string;
};

type WorkspaceValues = Record<string, string | boolean>;

type OtherAcquisitionServiceCost = {
  id: string;
  serviceName: string;
  payeeName: string;
  amount: string;
  dueDate: string;
  notes: string;
};

type BeaconComparisonRow = {
  label: string;
  cadastralNorthing?: number;
  cadastralEasting?: number;
  classificationNorthing?: number;
  classificationEasting?: number;
  deviationFeet?: number;
};

const stageIcons: Record<
  AcquisitionWorkspaceKind,
  React.ComponentType<{ className?: string }>
> = {
  'parcel-identification': MapPin,
  'suitability-approval': ClipboardCheck,
  'cadastral-survey': Layers3,
  'cadastral-verification': ShieldCheck,
  'ownership-classification': Scale,
  'ownership-verification': FileCheck2,
  'agreement-negotiation': WalletCards,
  'agreement-approval': BadgeCheck,
  'vendor-payment': WalletCards,
  execution: FileSignature,
  'statutory-consent': Landmark,
  'statutory-consent-approval': CheckCircle2,
  'stamp-duty-assessment': Stamp,
  'stamp-duty-approval': Receipt,
  'stamp-duty-payment': WalletCards,
  registration: BookOpen,
  'asset-creation': Building2,
};

const riskClasses = {
  Low: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/70 dark:bg-emerald-950/40 dark:text-emerald-300',
  Medium:
    'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/70 dark:bg-amber-950/40 dark:text-amber-300',
  High: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900/70 dark:bg-rose-950/40 dark:text-rose-300',
};

const CADASTRAL_EXTERNAL_SURVEYOR_FIELDS = new Set([
  'surveyorBusinessPartnerId',
  'surveyorFeeAmount',
  'surveyorFeeDueDate',
  'accountsPayableInvoiceNumber',
  'accountsPayableInvoiceStatus',
  'accountsPayablePaymentNumber',
  'accountsPayablePaymentStatus',
  'receiptNumber',
  'paymentReference',
  'paymentDate',
  'amountPaid',
  'paymentMethod',
  'isPaid',
  'paymentNotes',
]);

const PAYABLE_BUSINESS_PARTNER_TYPES = new Set([
  'supplier',
  'contractor',
  'vendor',
  'both',
  'consultant',
]);

const createServiceCostId = () =>
  typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `service-${Date.now()}-${Math.random().toString(16).slice(2)}`;

const emptyOtherAcquisitionService = (): OtherAcquisitionServiceCost => ({
  id: createServiceCostId(),
  serviceName: '',
  payeeName: '',
  amount: '',
  dueDate: '',
  notes: '',
});

const parseOtherAcquisitionServices = (
  raw: string | boolean | undefined
): OtherAcquisitionServiceCost[] => {
  if (typeof raw !== 'string' || !raw.trim()) return [];
  try {
    const parsed = JSON.parse(raw) as Partial<OtherAcquisitionServiceCost>[];
    if (!Array.isArray(parsed)) return [];
    return parsed.map((item) => ({
      id: item.id || createServiceCostId(),
      serviceName: item.serviceName || '',
      payeeName: item.payeeName || '',
      amount: item.amount ? `${item.amount}` : '',
      dueDate: item.dueDate || '',
      notes: item.notes || '',
    }));
  } catch {
    return [];
  }
};

const serializeOtherAcquisitionServices = (
  services: OtherAcquisitionServiceCost[]
) => JSON.stringify(services);

const sumOtherAcquisitionServices = (
  services: OtherAcquisitionServiceCost[]
) =>
  services.reduce((total, service) => {
    const amount = Number(service.amount);
    return total + (Number.isFinite(amount) && amount > 0 ? amount : 0);
  }, 0);

function isActiveApprovedBusinessPartner(partner: BusinessPartnerDto) {
  const status = partner.status?.toLowerCase();
  const isActive = status === 'active' || status === 'approved';
  const approvalStatus = partner.approvalStatus?.toLowerCase();
  const isApproved = !approvalStatus || approvalStatus === 'approved';
  return isActive && isApproved && !partner.isBlacklisted;
}

function isPayableBusinessPartner(partner: BusinessPartnerDto) {
  return PAYABLE_BUSINESS_PARTNER_TYPES.has(
    `${partner.partnerType || ''}`.toLowerCase()
  );
}

const field = (
  key: string,
  label: string,
  type: FieldType = 'text',
  options?: string[],
  span: 1 | 2 = 1,
  readOnly = false
): WorkspaceField => ({
  key,
  label,
  type,
  options,
  span,
  readOnly,
});

const SUITABILITY_CAPTURE_FIELDS: WorkspaceField[] = [
  field('inspectionDate', 'Inspection Date', 'date'),
  field('inspectionOfficer', 'Inspection Officer'),
  field('soilType', 'Soil Type', 'select', [
    'Sandy',
    'Clay',
    'Loamy',
    'Rocky',
    'Mixed',
  ]),
  field('topography', 'Topography', 'select', [
    'Flat',
    'Gentle Slope',
    'Steep Slope',
    'Hilly',
  ]),
  field('hasAccessRoad', 'Has Access Road', 'check'),
  field('hasUtilities', 'Has Utilities', 'check'),
  field('siteAccessRoute', 'Site Access Route'),
  field('drainageCondition', 'Drainage Condition', 'select', [
    'Good',
    'Fair',
    'Poor',
    'Requires Drainage Works',
  ]),
  field(
    'existingDevelopment',
    'Existing Development / Occupation',
    'textarea',
    undefined,
    2
  ),
  field('zoningClassification', 'Zoning Classification'),
  field('planningSchemeReference', 'Planning Scheme Reference'),
  field('isFloodProne', 'Flood Prone', 'check'),
  field('planningCompatible', 'Planning Compatible', 'check'),
  field('accessConfirmed', 'Access Confirmed', 'check'),
  field('environmentalClearance', 'Environmental Clearance', 'check'),
  field('utilityAvailability', 'Utility Availability', 'check'),
  field('encumbranceObserved', 'Encumbrance Observed On Site', 'check'),
];

const SUITABILITY_DECISION_FIELDS: WorkspaceField[] = [
  field('assessmentRecommendation', 'Assessment Recommendation', 'select', [
    'Proceed to Cadastral Survey',
    'Return for More Information',
    'Reject Site',
  ]),
  field('approvalNotes', 'Physical Assessment Notes', 'textarea', undefined, 2),
];

const WORKSPACE_FIELDS: Record<AcquisitionWorkspaceKind, WorkspaceField[]> = {
  'parcel-identification': [
    field('projectReference', 'Project Reference'),
    field('parcelLocation', 'Location'),
    field('estimatedSize', 'Estimated Size'),
    field('coordinates', 'Site / Locality Reference'),
    field('vendorName', 'Vendor / Owner'),
    field('acquisitionType', 'Acquisition Type', 'select', [
      'Direct Purchase',
      'Assignment',
      'Leasehold',
      'Conveyance',
    ]),
    field('intendedUse', 'Intended Use', 'select', [
      'Residential',
      'Commercial',
      'Industrial',
      'Agricultural',
      'Government',
    ]),
    field('openingNotes', 'Opening Notes', 'textarea', undefined, 2),
    ...SUITABILITY_CAPTURE_FIELDS,
  ],
  'suitability-approval': [...SUITABILITY_DECISION_FIELDS],
  'cadastral-survey': [
    field('cadastreDescription', 'Cadastre Description'),
    field('regionId', 'Region', 'region'),
    field('districtId', 'District', 'district'),
    field('townId', 'Town'),
    field('totalArea', 'Total Area'),
    field('areaUnit', 'Area Unit', 'select', [
      'sq ft',
      'acres',
      'hectares',
      'sqm',
    ]),
    field('beacon1Index', 'Beacon 1 Index'),
    field('beacon1NorthingFeet', 'Beacon 1 Northing (Y, ft)'),
    field('beacon1EastingFeet', 'Beacon 1 Easting (X, ft)'),
    field('beacon1Bearing', 'Beacon 1 Bearing'),
    field('beacon1DistanceFeet', 'Beacon 1 Distance (ft)'),
    field('beacon2Index', 'Beacon 2 Index'),
    field('beacon2NorthingFeet', 'Beacon 2 Northing (Y, ft)'),
    field('beacon2EastingFeet', 'Beacon 2 Easting (X, ft)'),
    field('beacon2Bearing', 'Beacon 2 Bearing'),
    field('beacon2DistanceFeet', 'Beacon 2 Distance (ft)'),
    field('beacon3Index', 'Beacon 3 Index'),
    field('beacon3NorthingFeet', 'Beacon 3 Northing (Y, ft)'),
    field('beacon3EastingFeet', 'Beacon 3 Easting (X, ft)'),
    field('beacon3Bearing', 'Beacon 3 Bearing'),
    field('beacon3DistanceFeet', 'Beacon 3 Distance (ft)'),
    field('beacon4Index', 'Beacon 4 Index'),
    field('beacon4NorthingFeet', 'Beacon 4 Northing (Y, ft)'),
    field('beacon4EastingFeet', 'Beacon 4 Easting (X, ft)'),
    field('beacon4Bearing', 'Beacon 4 Bearing'),
    field('beacon4DistanceFeet', 'Beacon 4 Distance (ft)'),
    field(
      'boundaryCoordinates',
      'Beacon Coordinate JSON',
      'textarea',
      undefined,
      2
    ),
    field('surveyorSource', 'Surveyor Source', 'select', [
      'Internal',
      'External',
    ]),
    field('surveyorBusinessPartnerId', 'External Surveyor / Vendor'),
    field('surveyorFeeAmount', 'External Surveyor Fee'),
    field('surveyorFeeDueDate', 'Surveyor Fee Due Date', 'date'),
    field('surveyorName', 'Surveyor Name'),
    field('licensedSurveyor', 'Surveyor Licence Number'),
    field('surveyDate', 'Survey Date', 'date'),
    field('surveyorSignedDate', 'Surveyor Signed Date', 'date'),
    field('surveyPlanNumber', 'Survey Plan Number'),
    field('mapSheetNumber', 'Map Sheet Number'),
    field('surveyStatus', 'Survey Status', 'select', [
      'Not Initiated',
      'In Progress',
      'Submitted',
      'Certified',
    ]),
    field('isCertified', 'Certified Survey', 'check'),
    field('beaconCount', 'Beacon Count'),
    field('regionalSurveyorName', 'Regional Surveyor Name'),
    field(
      'regionalSurveyorSignedDate',
      'Regional Surveyor Signed Date',
      'date'
    ),
    field('mainPortion', 'Main Portion', 'check'),
    field('coordinateReference', 'Coordinate Reference'),
    field('surveyNotes', 'Survey Notes', 'textarea', undefined, 2),
    field(
      'accountsPayableInvoiceNumber',
      'AP Invoice',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayableInvoiceStatus',
      'Invoice Status',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayablePaymentNumber',
      'AP Payment',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayablePaymentStatus',
      'Payment Status',
      'text',
      undefined,
      1,
      true
    ),
    field('receiptNumber', 'Receipt Number', 'text', undefined, 1, true),
    field('paymentReference', 'Payment Reference', 'text', undefined, 1, true),
    field('paymentDate', 'Payment Date', 'date', undefined, 1, true),
    field('amountPaid', 'Amount Paid', 'text', undefined, 1, true),
    field('paymentMethod', 'Payment Method', 'text', undefined, 1, true),
    field('isPaid', 'Paid', 'check', undefined, 1, true),
    field('paymentNotes', 'Payment Notes', 'textarea', undefined, 2, true),
  ],
  'cadastral-verification': [],
  'ownership-classification': [
    field('ownershipType', 'Ownership Type', 'select', [
      'Allodial',
      'Stool',
      'Family',
      'Private',
      'State',
      'Mixed Interest',
    ]),
    field('ownerName', 'Owner Name'),
    field('contactNumber', 'Contact Number'),
    field('address', 'Owner Address', 'textarea'),
    field('acquisitionMethod', 'Acquisition Method', 'select', [
      'Purchase',
      'Gift',
      'Inheritance',
      'Court Order',
      'Leasehold',
      'Assignment',
      'Conveyance',
    ]),
    field('tenureType', 'Tenure Type', 'select', [
      'Freehold',
      'Leasehold',
      'Customary',
      'Vested',
      'Unknown',
    ]),
    field('ownershipStartDate', 'Ownership Start Date', 'date'),
    field('ownershipEndDate', 'Ownership End Date', 'date'),
    field('isCurrentOwner', 'Current Owner', 'check'),
    field('identificationType', 'Identification Type', 'select', [
      'Ghana Card',
      'Passport',
      'Voter ID',
      'Driver License',
      'Other',
    ]),
    field('identificationNumber', 'Identification Number'),
    field('classificationRisk', 'Classification Risk', 'select', [
      'Low',
      'Medium',
      'High',
    ]),
    field('dateGapReason', 'Date Gap Reason', 'textarea', undefined, 2),
    field('ownerRegionId', 'Ownership Region', 'region'),
    field('ownerDistrictId', 'Ownership District', 'district'),
    field('ownerTownId', 'Ownership Town'),
    field('ownerBeacon1NorthingFeet', 'Owner Beacon 1 Northing (Y, ft)'),
    field('ownerBeacon1EastingFeet', 'Owner Beacon 1 Easting (X, ft)'),
    field('ownerBeacon2NorthingFeet', 'Owner Beacon 2 Northing (Y, ft)'),
    field('ownerBeacon2EastingFeet', 'Owner Beacon 2 Easting (X, ft)'),
    field('ownerBeacon3NorthingFeet', 'Owner Beacon 3 Northing (Y, ft)'),
    field('ownerBeacon3EastingFeet', 'Owner Beacon 3 Easting (X, ft)'),
    field('ownerBeacon4NorthingFeet', 'Owner Beacon 4 Northing (Y, ft)'),
    field('ownerBeacon4EastingFeet', 'Owner Beacon 4 Easting (X, ft)'),
    field('witnessName1', 'Witness 1 Name'),
    field('witnessContact1', 'Witness 1 Contact'),
    field('witnessRelation1', 'Witness 1 Relationship'),
    field('witnessAddress1', 'Witness 1 Address', 'textarea'),
    field('witnessSwornOath1', 'Witness 1 Sworn Oath', 'check'),
    field('witnessOathSwornBefore1', 'Witness 1 Oath Sworn Before'),
    field('witnessOathSwornDate1', 'Witness 1 Oath Date', 'date'),
    field('witnessName2', 'Witness 2 Name'),
    field('witnessContact2', 'Witness 2 Contact'),
    field('witnessRelation2', 'Witness 2 Relationship'),
    field('witnessAddress2', 'Witness 2 Address', 'textarea'),
    field('witnessSwornOath2', 'Witness 2 Sworn Oath', 'check'),
    field('witnessOathSwornBefore2', 'Witness 2 Oath Sworn Before'),
    field('witnessOathSwornDate2', 'Witness 2 Oath Date', 'date'),
    field(
      'classificationNotes',
      'Classification Notes',
      'textarea',
      undefined,
      2
    ),
  ],
  'ownership-verification': [
    field('dueDiligenceStatus', 'Due Diligence Status', 'select', [
      'Pending',
      'Approved',
      'Failed',
    ]),
    field('titleSearchCompleted', 'Title Search Completed', 'check'),
    field('ownerIdentityVerified', 'Owner Identity Verified', 'check'),
    field('authorityToSellVerified', 'Authority To Sell Verified', 'check'),
    field('overlapCleared', 'Ownership / Title Overlap Cleared', 'check'),
    field('encumbrancesFound', 'Encumbrances Found', 'check'),
    field('litigationFound', 'Litigation Found', 'check'),
    field(
      'landsCommissionSearchReference',
      'Lands Commission Search Reference'
    ),
    field('searchReference', 'Search Reference'),
    field('ownershipVerified', 'Ownership Verified', 'check'),
    field('verificationNotes', 'Verification Notes', 'textarea', undefined, 2),
  ],
  'agreement-negotiation': [
    field('sellerQuote', 'Seller Quote'),
    field('offerAmount', 'Offer Amount'),
    field('counterOffer', 'Counter Offer'),
    field('negotiatedValue', 'Negotiated Value'),
    field('paymentType', 'Payment Type', 'select', [
      'Cash',
      'Cheque',
      'Mobile Money',
      'Bank Transfer',
      'One-Off',
      'Installment',
      'Part Payment',
      'Swap',
      'Other',
    ]),
    field('offerTerms', 'Offer Terms'),
    field('agreementDay', 'Agreement Day'),
    field('agreementMonth', 'Agreement Month'),
    field('agreementYear', 'Agreement Year'),
    field('isAccepted', 'Offer Accepted', 'check'),
    field('agreementGenerated', 'Agreement Generated', 'check'),
    field('negotiationNotes', 'Negotiation Notes', 'textarea', undefined, 2),
    field('agreementDate', 'Agreement Date', 'date'),
    field('rootOfTitle', 'Root Of Title', 'textarea', undefined, 2),
    field('specialConditions', 'Special Conditions', 'textarea', undefined, 2),
    field('partyDetails', 'Party Details', 'textarea'),
    field('paymentSchedule', 'Payment Schedule', 'textarea'),
    field('agreementWitnessDetails', 'Agreement Witness Details', 'textarea'),
    field('grantorName', 'Grantor / Seller Name'),
    field('grantorAddress', 'Grantor / Seller Address', 'textarea'),
    field('grantorPhone', 'Grantor / Seller Phone'),
    field('granteeName', 'Grantee / Buyer Name'),
    field('granteeAddress', 'Grantee / Buyer Address', 'textarea'),
    field('granteePhone', 'Grantee / Buyer Phone'),
    field('agreementPaymentType', 'Agreement Payment Type', 'select', [
      'Full Payment',
      'Deposit',
      'Installment',
      'Balance Payment',
      'Other',
    ]),
    field('agreementPaymentAmount', 'Agreement Payment Amount'),
    field('agreementPaymentDueDate', 'Payment Due Date', 'date'),
    field('agreementPaymentMethod', 'Payment Method', 'select', [
      'Cash',
      'Cheque',
      'Mobile Money',
      'Bank Transfer',
      'Other',
    ]),
    field('agreementWitness1Name', 'Agreement Witness 1 Name'),
    field(
      'agreementWitness1Address',
      'Agreement Witness 1 Address',
      'textarea'
    ),
    field('agreementWitness2Name', 'Agreement Witness 2 Name'),
    field(
      'agreementWitness2Address',
      'Agreement Witness 2 Address',
      'textarea'
    ),
  ],
  'agreement-approval': [
    field('legalReviewComplete', 'Legal Review Complete', 'check'),
    field('financeReviewComplete', 'Finance Review Complete', 'check'),
    field('boardApprovalReference', 'Board Approval Reference'),
    field(
      'approvalConditions',
      'Approval Conditions',
      'textarea',
      undefined,
      2
    ),
  ],
  'vendor-payment': [
    field('vendorName', 'Vendor / Seller', 'text', undefined, 1, true),
    field('paymentPurpose', 'Payment Purpose', 'text', undefined, 1, true),
    field('agreedAmount', 'Agreed Amount', 'text', undefined, 1, true),
    field(
      'vendorPaymentDueDate',
      'Payment Due Date',
      'date',
      undefined,
      1,
      true
    ),
    field(
      'vendorPaymentMethod',
      'Agreement Payment Method',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'boardApprovalReference',
      'Approval Reference',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayableInvoiceNumber',
      'AP Invoice',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayableInvoiceStatus',
      'Invoice Status',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayablePaymentNumber',
      'AP Payment',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayablePaymentStatus',
      'Payment Status',
      'text',
      undefined,
      1,
      true
    ),
    field('receiptNumber', 'Receipt Number', 'text', undefined, 1, true),
    field('paymentReference', 'Payment Reference', 'text', undefined, 1, true),
    field('paymentDate', 'Payment Date', 'date', undefined, 1, true),
    field('amountPaid', 'Amount Paid', 'text', undefined, 1, true),
    field(
      'paymentMethod',
      'Processed Payment Method',
      'text',
      undefined,
      1,
      true
    ),
    field('isPaid', 'Paid', 'check', undefined, 1, true),
    field('paymentNotes', 'Payment Notes', 'textarea', undefined, 2, true),
  ],
  execution: [
    field('instrumentType', 'Instrument Type', 'select', [
      'Conveyance',
      'Assignment',
      'Lease',
      'Vesting Assent',
    ]),
    field('instrumentNumber', 'Instrument Number'),
    field('documentName', 'Instrument Document Name'),
    field('documentType', 'Instrument Document Type', 'select', [
      'Indenture',
      'Lease Agreement',
      'Deed Of Assignment',
      'Allocation Letter',
      'Survey Plan',
      'Court Order',
      'Customary Grant',
      'Other',
    ]),
    field('executionDate', 'Execution Date', 'date'),
    field('executedBy', 'Executed By'),
    field('counterpartySignatory', 'Counterparty Signatory'),
    field('isExecuted', 'Instrument Executed', 'check'),
    field('witnessDetails', 'Witness Details', 'textarea'),
    field('executionNotes', 'Execution Notes', 'textarea'),
  ],
  'statutory-consent': [
    field('consentAuthority', 'Consent Authority'),
    field('consentDate', 'Consent Date', 'date'),
    field('applicationNumber', 'Application Number'),
    field('submissionDate', 'Submission Date', 'date'),
    field('documentName', 'Consent Document Name'),
    field('documentType', 'Consent Document Type', 'select', [
      'Government Approval',
      'Statutory Consent',
      'Land Use Approval',
      'Other',
    ]),
    field('isApproved', 'Consent Approved', 'check'),
    field('consentNotes', 'Consent Notes', 'textarea', undefined, 2),
  ],
  'statutory-consent-approval': [
    field('approvalReference', 'Approval Reference'),
    field('approvalDate', 'Approval Date', 'date'),
    field('consentConditions', 'Consent Conditions', 'textarea'),
    field('approvalNotes', 'Approval Notes', 'textarea'),
  ],
  'stamp-duty-assessment': [
    field('propertyValue', 'Property Value'),
    field('stampDutyAmount', 'Stamp Duty Amount'),
    field('assessmentAuthority', 'Assessment Authority'),
    field('assessmentReference', 'Assessment Reference'),
    field('assessmentDate', 'Assessment Date', 'date'),
    field('isApproved', 'Assessment Approved', 'check'),
    field('assessmentNotes', 'Assessment Notes', 'textarea', undefined, 2),
  ],
  'stamp-duty-approval': [
    field('financeApprovalReference', 'Finance Approval Reference'),
    field('approvedDutyAmount', 'Approved Duty Amount'),
    field('approverName', 'Approver Name'),
    field('approvalNotes', 'Approval Notes', 'textarea', undefined, 2),
  ],
  'stamp-duty-payment': [
    field(
      'accountsPayableInvoiceNumber',
      'AP Invoice',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayableInvoiceStatus',
      'Invoice Status',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayablePaymentNumber',
      'AP Payment',
      'text',
      undefined,
      1,
      true
    ),
    field(
      'accountsPayablePaymentStatus',
      'Payment Status',
      'text',
      undefined,
      1,
      true
    ),
    field('receiptNumber', 'Receipt Number', 'text', undefined, 1, true),
    field('paymentReference', 'Payment Reference', 'text', undefined, 1, true),
    field('paymentDate', 'Payment Date', 'date', undefined, 1, true),
    field('amountPaid', 'Amount Paid', 'text', undefined, 1, true),
    field('paymentMethod', 'Payment Method', 'text', undefined, 1, true),
    field('isPaid', 'Paid', 'check', undefined, 1, true),
    field('paymentNotes', 'Payment Notes', 'textarea', undefined, 2, true),
  ],
  registration: [
    field('registryOffice', 'Registry Office'),
    field('publicationDate', 'Publication Date', 'date'),
    field('landTitleReference', 'Land Title Reference'),
    field('landTitleCapturedDate', 'Land Title Captured Date', 'date'),
    field('registrationNumber', 'Registration Number'),
    field('volume', 'Volume'),
    field('folio', 'Folio'),
    field('registrationDate', 'Registration Date', 'date'),
    field('isRegistered', 'Registered', 'check'),
    field('documentName', 'Registration Document Name'),
    field('registrationNotes', 'Registration Notes', 'textarea', undefined, 2),
  ],
  'asset-creation': [
    field('assetCode', 'Land Code', 'text', undefined, 1, true),
    field('assetNumber', 'Land Number', 'text', undefined, 1, true),
    field('parcelIdentifier', 'Parcel Identifier', 'text', undefined, 1, true),
    field('registrationNumber', 'Registration Number', 'text', undefined, 1, true),
    field('assetCategory', 'Land Category', 'select', [
      'Land',
      'Land Improvement',
      'Investment Property',
    ], 1, true),
    field('size', 'Size', 'text', undefined, 1, true),
    field('sizeUnit', 'Size Unit', 'select', ['Acres', 'Hectares', 'sqm'], 1, true),
    field('assetStatus', 'Land Status', 'select', [
      'Draft',
      'Active',
      'Under Dispute',
      'Locked',
    ], 1, true),
    field('purpose', 'Purpose', 'text', undefined, 1, true),
    field('zoningClassification', 'Zoning Classification', 'text', undefined, 1, true),
    field('ownershipVerification', 'Ownership Verification', 'text', undefined, 1, true),
    field('ownerConsiderationCost', 'Owner / Vendor Consideration', 'text', undefined, 1, true),
    field('externalSurveyorCost', 'External Surveyor Cost', 'text', undefined, 1, true),
    field('stampDutyCost', 'Stamp Duty Cost', 'text', undefined, 1, true),
    field('otherAcquisitionCost', 'Other Acquisition Cost', 'text', undefined, 1, true),
    field('totalCapitalizedCost', 'Total Land Cost', 'text', undefined, 1, true),
    field('capitalizationValue', 'Capitalization Value', 'text', undefined, 1, true),
    field('capitalizationBreakdown', 'Capitalization Breakdown', 'textarea', undefined, 2, true),
    field('glAccount', 'Fixed Asset GL Account', 'text', undefined, 1, true),
    field('custodian', 'Custodian', 'text', undefined, 1, true),
    field('assetNotes', 'Land Notes', 'textarea', undefined, 2, true),
  ],
};

const WORKSPACE_SECTIONS: Partial<
  Record<AcquisitionWorkspaceKind, WorkspaceSection[]>
> = {
  'parcel-identification': [
    {
      title: 'Parcel request',
      description: 'Capture the basic request and the land being considered.',
      keys: [
        'projectReference',
        'parcelLocation',
        'estimatedSize',
        'coordinates',
        'vendorName',
        'acquisitionType',
      ],
    },
    {
      title: 'Planning purpose',
      description:
        'Record why the land is being acquired and any opening comments.',
      keys: ['intendedUse', 'openingNotes'],
    },
    {
      title: 'Physical inspection',
      description:
        'Record the inspection officer, date, soil, terrain, access, drainage, and current occupation.',
      keys: [
        'inspectionDate',
        'inspectionOfficer',
        'soilType',
        'topography',
        'hasAccessRoad',
        'siteAccessRoute',
        'drainageCondition',
        'existingDevelopment',
      ],
    },
    {
      title: 'Planning and constraints',
      description:
        'Confirm planning compatibility, zoning, access, utilities, environmental status, flooding, and encumbrances.',
      keys: [
        'zoningClassification',
        'planningSchemeReference',
        'planningCompatible',
        'accessConfirmed',
        'hasUtilities',
        'environmentalClearance',
        'utilityAvailability',
        'isFloodProne',
        'encumbranceObserved',
      ],
    },
  ],
  'suitability-approval': [
    {
      title: 'Suitability review',
      description:
        'Review the captured parcel assessment and record the approval decision.',
      keys: ['assessmentRecommendation', 'approvalNotes'],
    },
  ],
  'cadastral-survey': [
    {
      title: 'Cadastre details',
      description:
        'Capture the cadastral description, area, map sheet, survey plan, and surveyor details.',
      keys: [
        'cadastreDescription',
        'regionId',
        'districtId',
        'townId',
        'totalArea',
        'areaUnit',
        'surveyPlanNumber',
        'mapSheetNumber',
        'surveyorSource',
        'surveyorBusinessPartnerId',
        'surveyorFeeAmount',
        'surveyorFeeDueDate',
        'surveyorName',
        'licensedSurveyor',
        'surveyDate',
        'surveyorSignedDate',
      ],
    },
    {
      title: 'Coordinates and demarcation',
      description:
        'Enter the beacon index and survey-plan coordinates in feet for the demarcated parcel boundary.',
      keys: [
        'beacon1Index',
        'beacon1NorthingFeet',
        'beacon1EastingFeet',
        'beacon1Bearing',
        'beacon1DistanceFeet',
        'beacon2Index',
        'beacon2NorthingFeet',
        'beacon2EastingFeet',
        'beacon2Bearing',
        'beacon2DistanceFeet',
        'beacon3Index',
        'beacon3NorthingFeet',
        'beacon3EastingFeet',
        'beacon3Bearing',
        'beacon3DistanceFeet',
        'beacon4Index',
        'beacon4NorthingFeet',
        'beacon4EastingFeet',
        'beacon4Bearing',
        'beacon4DistanceFeet',
        'boundaryCoordinates',
      ],
    },
    {
      title: 'Survey approval details',
      description:
        'Record beacon count, regional surveyor sign-off, and survey notes.',
      keys: [
        'surveyStatus',
        'isCertified',
        'beaconCount',
        'regionalSurveyorName',
        'regionalSurveyorSignedDate',
        'mainPortion',
        'coordinateReference',
        'surveyNotes',
      ],
    },
    {
      title: 'External surveyor payment',
      description:
        'Track the Accounts Payable request and receipt when an external surveyor is used.',
      keys: [
        'accountsPayableInvoiceNumber',
        'accountsPayableInvoiceStatus',
        'accountsPayablePaymentNumber',
        'accountsPayablePaymentStatus',
        'receiptNumber',
        'paymentReference',
        'paymentDate',
        'amountPaid',
        'paymentMethod',
        'isPaid',
        'paymentNotes',
      ],
    },
  ],
  'ownership-classification': [
    {
      title: 'Owner and tenure',
      description:
        'Classify ownership and record the owner, tenure, dates, and risk.',
      keys: [
        'ownershipType',
        'isCurrentOwner',
        'ownerName',
        'contactNumber',
        'address',
        'acquisitionMethod',
        'tenureType',
        'ownershipStartDate',
        'ownershipEndDate',
        'classificationRisk',
        'dateGapReason',
      ],
    },
    {
      title: 'Identity',
      description:
        'Capture identity document details for the owner or authorized representative.',
      keys: ['identificationType', 'identificationNumber'],
    },
    {
      title: 'Ownership cadastre',
      description:
        'Record the region, district, town, and feet-based beacon coordinates tied to the owner record.',
      keys: [
        'ownerRegionId',
        'ownerDistrictId',
        'ownerTownId',
        'ownerBeacon1NorthingFeet',
        'ownerBeacon1EastingFeet',
        'ownerBeacon2NorthingFeet',
        'ownerBeacon2EastingFeet',
        'ownerBeacon3NorthingFeet',
        'ownerBeacon3EastingFeet',
        'ownerBeacon4NorthingFeet',
        'ownerBeacon4EastingFeet',
      ],
    },
    {
      title: 'Witness 1',
      description: 'Record the first witness and oath details.',
      keys: [
        'witnessName1',
        'witnessContact1',
        'witnessRelation1',
        'witnessAddress1',
        'witnessSwornOath1',
        'witnessOathSwornBefore1',
        'witnessOathSwornDate1',
      ],
    },
    {
      title: 'Witness 2',
      description: 'Record the second witness and oath details.',
      keys: [
        'witnessName2',
        'witnessContact2',
        'witnessRelation2',
        'witnessAddress2',
        'witnessSwornOath2',
        'witnessOathSwornBefore2',
        'witnessOathSwornDate2',
        'classificationNotes',
      ],
    },
  ],
  'ownership-verification': [
    {
      title: 'Due diligence checks',
      description:
        'Confirm the ownership boundary comparison, overlap clearance, title search, owner identity, and authority to sell before agreement negotiation.',
      keys: [
        'dueDiligenceStatus',
        'titleSearchCompleted',
        'ownerIdentityVerified',
        'authorityToSellVerified',
        'overlapCleared',
        'encumbrancesFound',
        'litigationFound',
        'landsCommissionSearchReference',
        'searchReference',
        'ownershipVerified',
        'verificationNotes',
      ],
    },
  ],
  'agreement-negotiation': [
    {
      title: 'Commercial negotiation',
      description:
        'Record seller quote, offers, negotiated value, payment type, and terms.',
      keys: [
        'sellerQuote',
        'offerAmount',
        'counterOffer',
        'negotiatedValue',
        'paymentType',
        'offerTerms',
      ],
    },
    {
      title: 'Agreement preparation',
      description:
        'Capture agreement date, acceptance, generated status, and negotiation notes.',
      keys: [
        'agreementDate',
        'agreementDay',
        'agreementMonth',
        'agreementYear',
        'isAccepted',
        'agreementGenerated',
        'negotiationNotes',
      ],
    },
    {
      title: 'Agreement terms',
      description:
        'Capture root of title, parties, payment schedule, special conditions, and witnesses.',
      keys: [
        'rootOfTitle',
        'specialConditions',
        'partyDetails',
        'paymentSchedule',
        'agreementWitnessDetails',
      ],
    },
    {
      title: 'Agreement parties and payment',
      description:
        'Record the grantor, grantee, consideration, payment terms, and witnesses for the draft agreement.',
      keys: [
        'grantorName',
        'grantorAddress',
        'grantorPhone',
        'granteeName',
        'granteeAddress',
        'granteePhone',
        'agreementPaymentType',
        'agreementPaymentAmount',
        'agreementPaymentDueDate',
        'agreementPaymentMethod',
        'agreementWitness1Name',
        'agreementWitness1Address',
        'agreementWitness2Name',
        'agreementWitness2Address',
      ],
    },
  ],
  'agreement-approval': [
    {
      title: 'Approval controls',
      description:
        'Capture legal review, finance review, board reference, and approval conditions before approval.',
      keys: [
        'legalReviewComplete',
        'financeReviewComplete',
        'boardApprovalReference',
        'approvalConditions',
      ],
    },
  ],
  'vendor-payment': [
    {
      title: 'Approved vendor consideration',
      description:
        'Review the seller, approved amount, due date, payment method, and approval reference from the negotiated agreement.',
      keys: [
        'vendorName',
        'paymentPurpose',
        'agreedAmount',
        'vendorPaymentDueDate',
        'vendorPaymentMethod',
        'boardApprovalReference',
      ],
    },
    {
      title: 'Accounts Payable status',
      description:
        'Create the AP request and process the vendor payment before moving to instrument execution.',
      keys: [
        'accountsPayableInvoiceNumber',
        'accountsPayableInvoiceStatus',
        'accountsPayablePaymentNumber',
        'accountsPayablePaymentStatus',
      ],
    },
    {
      title: 'Processed vendor payment',
      description:
        'Payment evidence is synchronized from Accounts Payable after the vendor payment is completed.',
      keys: [
        'receiptNumber',
        'paymentReference',
        'paymentDate',
        'amountPaid',
        'paymentMethod',
        'isPaid',
        'paymentNotes',
      ],
    },
  ],
  execution: [
    {
      title: 'Instrument execution',
      description:
        'Record the executed instrument, signatories, witnesses, and execution notes.',
      keys: [
        'instrumentType',
        'instrumentNumber',
        'documentName',
        'documentType',
        'executionDate',
        'executedBy',
        'counterpartySignatory',
        'isExecuted',
        'witnessDetails',
        'executionNotes',
      ],
    },
  ],
  'statutory-consent': [
    {
      title: 'Consent submission',
      description:
        'Capture the authority, application number, dates, approval flag, and consent notes.',
      keys: [
        'consentAuthority',
        'applicationNumber',
        'submissionDate',
        'consentDate',
        'documentName',
        'documentType',
        'isApproved',
        'consentNotes',
      ],
    },
  ],
  'statutory-consent-approval': [
    {
      title: 'Consent approval',
      description:
        'Record approval reference, approval date, consent conditions, and notes.',
      keys: [
        'approvalReference',
        'approvalDate',
        'consentConditions',
        'approvalNotes',
      ],
    },
  ],
  'stamp-duty-assessment': [
    {
      title: 'Assessment details',
      description:
        'Record the valuation, duty amount, authority, assessment reference, and date.',
      keys: [
        'propertyValue',
        'stampDutyAmount',
        'assessmentAuthority',
        'assessmentReference',
        'assessmentDate',
        'isApproved',
        'assessmentNotes',
      ],
    },
  ],
  'stamp-duty-approval': [
    {
      title: 'Finance approval',
      description: 'Approve the duty amount before payment is processed.',
      keys: [
        'financeApprovalReference',
        'approvedDutyAmount',
        'approverName',
        'approvalNotes',
      ],
    },
  ],
  'stamp-duty-payment': [
    {
      title: 'Accounts Payable status',
      description:
        'Payment details are synchronized from the linked Accounts Payable invoice and payment.',
      keys: [
        'accountsPayableInvoiceNumber',
        'accountsPayableInvoiceStatus',
        'accountsPayablePaymentNumber',
        'accountsPayablePaymentStatus',
      ],
    },
    {
      title: 'Processed payment',
      description:
        'Receipt and transaction values become available after Accounts Payable processes the payment.',
      keys: [
        'receiptNumber',
        'paymentReference',
        'paymentDate',
        'amountPaid',
        'paymentMethod',
        'isPaid',
        'paymentNotes',
      ],
    },
  ],
  registration: [
    {
      title: 'Lands Commission registration',
      description:
        'Record publication, title reference, registration number, volume, folio, date, and notes.',
      keys: [
        'registryOffice',
        'publicationDate',
        'landTitleReference',
        'landTitleCapturedDate',
        'registrationNumber',
        'volume',
        'folio',
        'registrationDate',
        'isRegistered',
        'documentName',
        'registrationNotes',
      ],
    },
  ],
  'asset-creation': [
    {
      title: 'Land details',
      description:
        'Create the land record and prepare it for land bank or project handoff.',
      keys: [
        'assetCode',
        'assetNumber',
        'parcelIdentifier',
        'registrationNumber',
        'assetCategory',
        'size',
        'sizeUnit',
        'assetStatus',
      ],
    },
    {
      title: 'Cost capitalization',
      description:
        'Review the costs accumulated in Land Under Acquisition before registering the land in Fixed Assets and Land Bank.',
      keys: [
        'ownerConsiderationCost',
        'externalSurveyorCost',
        'stampDutyCost',
        'otherAcquisitionCost',
        'totalCapitalizedCost',
        'capitalizationValue',
        'capitalizationBreakdown',
        'glAccount',
      ],
    },
    {
      title: 'Finance and custody',
      description:
        'Capture purpose, zoning, ownership verification, custodian, and notes.',
      keys: [
        'purpose',
        'zoningClassification',
        'ownershipVerification',
        'custodian',
        'assetNotes',
      ],
    },
  ],
};

function workspaceSectionsFor(
  kind: AcquisitionWorkspaceKind
): Array<WorkspaceSection & { fields: WorkspaceField[] }> {
  const fields = WORKSPACE_FIELDS[kind];
  const byKey = new Map(fields.map((item) => [item.key, item]));
  const sections = WORKSPACE_SECTIONS[kind] || [
    {
      title: 'Stage entries',
      description: 'Capture the required entries for this procedure stage.',
      keys: fields.map((item) => item.key),
    },
  ];

  return sections
    .map((section) => ({
      ...section,
      fields: section.keys
        .map((key) => byKey.get(key))
        .filter((item): item is WorkspaceField => Boolean(item)),
    }))
    .filter((section) => section.fields.length > 0);
}

function defaultsFor(
  kind: AcquisitionWorkspaceKind,
  item?: LandAcquisitionItem
): Record<string, string | boolean> {
  const values: Record<string, string | boolean> = {};
  for (const config of WORKSPACE_FIELDS[kind]) {
    values[config.key] =
      config.type === 'check' ? '' : config.options?.[0] || '';
  }

  if (kind === 'cadastral-survey') {
    values.mainPortion = true;
    values.surveyorSource = 'Internal';
  }

  if (item) {
    values.projectReference = item.projectReference;
    values.parcelLocation = item.location;
    values.intendedUse = item.intendedUse;
    values.estimatedSize = item.estimatedSize;
    values.vendorName = item.ownerName || '';
  }

  return values;
}

function missingWorkspaceInputs(
  kind: AcquisitionWorkspaceKind,
  values: WorkspaceValues
) {
  if (kind === 'cadastral-survey') {
    const externalSurveyor = values.surveyorSource === 'External';
    const missing = WORKSPACE_FIELDS[kind].filter((config) => {
      const value = values[config.key];
      return typeof value === 'boolean' ? false : !`${value ?? ''}`.trim();
    }).filter((config) => {
      if (config.readOnly) {
        return false;
      }

      if (CADASTRAL_EXTERNAL_SURVEYOR_FIELDS.has(config.key)) {
        return externalSurveyor;
      }

      return true;
    });

    if (!externalSurveyor) return missing;

    const invoiceId = `${values.accountsPayableInvoiceId ?? ''}`.trim();
    const paymentId = `${values.accountsPayablePaymentId ?? ''}`.trim();
    const paid =
      values.isPaid === true || `${values.isPaid}`.toLowerCase() === 'true';

    if (!invoiceId)
      return [
        ...missing,
        field('accountsPayableRequest', 'Surveyor AP Request'),
      ];
    if (!paymentId)
      return [
        ...missing,
        field('accountsPayablePayment', 'Surveyor AP Payment'),
      ];
    return paid
      ? missing
      : [
          ...missing,
          field('accountsPayablePaymentProcessing', 'Finance Processing'),
        ];
  }

  if (kind === 'vendor-payment' || kind === 'stamp-duty-payment') {
    const invoiceId = `${values.accountsPayableInvoiceId ?? ''}`.trim();
    const paymentId = `${values.accountsPayablePaymentId ?? ''}`.trim();
    const paid =
      values.isPaid === true || `${values.isPaid}`.toLowerCase() === 'true';
    if (!invoiceId)
      return [field('accountsPayableRequest', 'Accounts Payable Request')];
    if (!paymentId)
      return [field('accountsPayablePayment', 'Accounts Payable Payment')];
    return paid
      ? []
      : [field('accountsPayablePaymentProcessing', 'Finance Processing')];
  }

  const missing = WORKSPACE_FIELDS[kind].filter((config) => {
    if (config.readOnly) {
      return false;
    }

    const value = values[config.key];
    return typeof value === 'boolean' ? false : !`${value ?? ''}`.trim();
  });

  if (kind === 'parcel-identification' && !`${values.vendorId ?? ''}`.trim()) {
    return [
      field('vendorId', 'Vendor / Owner'),
      ...missing.filter((config) => config.key !== 'vendorName'),
    ];
  }

  if (kind === 'ownership-classification') {
    let conditional = [...missing];
    const pastOwners = parsePastOwners(values.pastOwnersJson);
    if (values.isCurrentOwner === true) {
      conditional = conditional.filter(
        (config) =>
          ![
            'ownerName',
            'contactNumber',
            'address',
            'identificationType',
            'identificationNumber',
            'ownershipEndDate',
          ].includes(config.key)
      );

      if (
        !`${values.vendorId ?? ''}`.trim() &&
        !`${values.vendorName ?? ''}`.trim()
      ) {
        conditional.push(field('vendorName', 'Linked Current Owner'));
      }
    }
    if (
      values.isCurrentOwner === false &&
      !`${values.ownershipEndDate ?? ''}`.trim()
    ) {
      conditional.push(field('ownershipEndDate', 'Ownership End Date', 'date'));
    }
    if (!hasOwnershipDateGap(values)) {
      conditional = conditional.filter(
        (config) => config.key !== 'dateGapReason'
      );
    }
    conditional = conditional.filter(
      (config) => !WITNESS_OATH_FIELD_KEYS.has(config.key)
    );
    if (!hasCompleteWitnessOath(values)) {
      conditional.push(field('witnessOath', 'Witness Oath'));
    }
    if (hasIncompletePastOwners(pastOwners)) {
      conditional.push(field('pastOwnersJson', 'Past Owners'));
    }
    return conditional;
  }

  return missing;
}

const WITNESS_OATH_FIELD_KEYS = new Set([
  'witnessSwornOath1',
  'witnessOathSwornBefore1',
  'witnessOathSwornDate1',
  'witnessSwornOath2',
  'witnessOathSwornBefore2',
  'witnessOathSwornDate2',
]);

const WITNESS_ONE_OATH_FIELD_KEYS = new Set([
  'witnessSwornOath1',
  'witnessOathSwornBefore1',
  'witnessOathSwornDate1',
]);

const WITNESS_TWO_OATH_FIELD_KEYS = new Set([
  'witnessSwornOath2',
  'witnessOathSwornBefore2',
  'witnessOathSwornDate2',
]);

function requiredMarker(config: WorkspaceField) {
  if (config.readOnly) return null;

  return WITNESS_OATH_FIELD_KEYS.has(config.key) ? null : (
    <span className="text-destructive">*</span>
  );
}

function hasCompleteWitnessOath(values: WorkspaceValues) {
  const witnessOneSworn = values.witnessSwornOath1 === true;
  const witnessTwoSworn = values.witnessSwornOath2 === true;
  if (witnessOneSworn === witnessTwoSworn) return false;

  const witnessNumber = witnessOneSworn ? 1 : 2;
  const swornBefore =
    `${values[`witnessOathSwornBefore${witnessNumber}`] ?? ''}`.trim();
  const swornDate =
    `${values[`witnessOathSwornDate${witnessNumber}`] ?? ''}`.trim();
  return Boolean(swornBefore && swornDate);
}

function hideOtherWitnessOathField(
  kind: AcquisitionWorkspaceKind,
  key: string,
  values: WorkspaceValues
) {
  if (kind !== 'ownership-classification') return false;
  if (values.witnessSwornOath1 === true) {
    return WITNESS_TWO_OATH_FIELD_KEYS.has(key);
  }
  if (values.witnessSwornOath2 === true) {
    return WITNESS_ONE_OATH_FIELD_KEYS.has(key);
  }
  return false;
}

function workspaceDate(value: string | boolean | undefined): Date | null {
  if (typeof value !== 'string' || !value.trim()) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function workspaceDecimal(value: string | boolean | undefined): number | null {
  if (typeof value === 'boolean' || value == null) return null;
  const normalized = `${value}`.replace(/,/g, '').trim();
  if (!normalized) return null;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

function formatWorkspaceMoney(
  value: string | boolean | undefined,
  currency = 'GHS'
) {
  const amount = workspaceDecimal(value);
  if (amount == null) return '-';
  return new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency,
  }).format(amount);
}

function formatWorkspaceDate(value: string | boolean | undefined) {
  const date = workspaceDate(value);
  if (!date) return '-';
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(date);
}

function displayWorkspaceValue(value: string | boolean | undefined) {
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  const text = `${value ?? ''}`.trim();
  return text || '-';
}

function isEditableElement(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false;
  return Boolean(
    target.closest(
      'input, textarea, select, [contenteditable="true"], [role="textbox"], [cmdk-input]'
    )
  );
}

function hasOwnershipDateGap(values: WorkspaceValues) {
  const pastOwners = parsePastOwners(values.pastOwnersJson);
  const latestPastOwnerEnd = [...pastOwners]
    .map((owner) => workspaceDate(owner.ownershipEndDate))
    .filter((date): date is Date => Boolean(date))
    .sort((left, right) => right.getTime() - left.getTime())[0];
  const previousEnd =
    latestPastOwnerEnd ||
    workspaceDate(values.previousOwnerEndDate) ||
    workspaceDate(values.previousOwnershipEndDate) ||
    workspaceDate(values.priorOwnerEndDate) ||
    workspaceDate(values.precedingOwnerEndDate);
  const nextStart = workspaceDate(values.ownershipStartDate);

  if (!previousEnd || !nextStart) return false;

  const previousEndDay = Date.UTC(
    previousEnd.getUTCFullYear(),
    previousEnd.getUTCMonth(),
    previousEnd.getUTCDate()
  );
  const nextStartDay = Date.UTC(
    nextStart.getUTCFullYear(),
    nextStart.getUTCMonth(),
    nextStart.getUTCDate()
  );

  return nextStartDay > previousEndDay;
}

function hasPastOwnerDateGap(owners: PastOwnerDraft[], index: number) {
  if (index <= 0) return false;

  const previousEnd = workspaceDate(owners[index - 1]?.ownershipEndDate);
  const nextStart = workspaceDate(owners[index]?.ownershipStartDate);
  if (!previousEnd || !nextStart) return false;

  return nextStart.getTime() > previousEnd.getTime();
}

function hasIncompletePastOwners(owners: PastOwnerDraft[]) {
  return owners.some((owner, index) => {
    const missingRequired =
      !owner.ownerName.trim() ||
      !owner.contactNumber.trim() ||
      !owner.address.trim() ||
      !owner.identificationType.trim() ||
      !owner.identificationNumber.trim() ||
      !owner.ownershipStartDate ||
      !owner.ownershipEndDate;

    return (
      missingRequired ||
      (hasPastOwnerDateGap(owners, index) && !owner.dateGapReason.trim())
    );
  });
}

function inputLabels(kind: AcquisitionWorkspaceKind, keys: string[]) {
  const labels = new Map(
    WORKSPACE_FIELDS[kind].map((config) => [config.key, config.label])
  );
  labels.set('vendorId', 'Linked Vendor / Owner');
  labels.set('surveyorBusinessPartnerId', 'External Surveyor / Vendor');
  labels.set('accountsPayableRequest', 'Accounts Payable Request');
  labels.set('accountsPayablePayment', 'Accounts Payable Payment');
  labels.set('stageDocuments', 'Stage Documents');
  labels.set('witnessOath', 'Witness Oath');
  labels.set('accountsPayablePaymentProcessing', 'Finance Processing');
  return keys.map((key) => labels.get(key) || key);
}

const normalizeDocumentValue = (value?: string) =>
  (value || '').trim().toLowerCase();

const normalizeDocumentNameWithoutExtension = (value?: string) =>
  normalizeDocumentValue(value).replace(/\.[^/.\\]+$/, '');

const normalizedDocumentCandidates = (...values: Array<string | undefined>) =>
  values
    .flatMap((value) => [
      normalizeDocumentValue(value),
      normalizeDocumentNameWithoutExtension(value),
    ])
    .filter(Boolean);

const matchesRequirement = (
  document: Pick<LandAcquisitionDocument, 'documentName' | 'documentType'> & {
    fileName?: string;
  },
  requirement: LandAcquisitionStageDocumentRequirement
) => {
  const requirementNames = normalizedDocumentCandidates(
    requirement.documentName,
    requirement.requirementKey
  );
  const documentNames = normalizedDocumentCandidates(
    document.documentName,
    document.fileName
  );

  if (requirementNames.length > 0) {
    return requirementNames.some((name) => documentNames.includes(name));
  }

  return (
    !!document.documentType &&
    !!requirement.documentType &&
    normalizeDocumentValue(document.documentType) ===
      normalizeDocumentValue(requirement.documentType)
  );
};

const BUSINESS_PARTNER_CURRENT_OWNER_FIELDS = new Set([
  'contactNumber',
  'address',
  'identificationType',
  'identificationNumber',
  'ownershipEndDate',
]);

interface PastOwnerDraft {
  ownerName: string;
  contactNumber: string;
  address: string;
  identificationType: string;
  identificationNumber: string;
  ownershipStartDate: string;
  ownershipEndDate: string;
  dateGapReason: string;
}

const emptyPastOwner = (): PastOwnerDraft => ({
  ownerName: '',
  contactNumber: '',
  address: '',
  identificationType: 'Ghana Card',
  identificationNumber: '',
  ownershipStartDate: '',
  ownershipEndDate: '',
  dateGapReason: '',
});

function parsePastOwners(
  value: string | boolean | undefined
): PastOwnerDraft[] {
  if (typeof value !== 'string' || !value.trim()) return [];

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];

    return parsed.map((item) => ({
      ...emptyPastOwner(),
      ...item,
    }));
  } catch {
    return [];
  }
}

function serializePastOwners(owners: PastOwnerDraft[]) {
  const cleaned = owners.map((owner) => ({
    ownerName: owner.ownerName.trim(),
    contactNumber: owner.contactNumber.trim(),
    address: owner.address.trim(),
    identificationType: owner.identificationType.trim(),
    identificationNumber: owner.identificationNumber.trim(),
    ownershipStartDate: owner.ownershipStartDate,
    ownershipEndDate: owner.ownershipEndDate,
    dateGapReason: owner.dateGapReason.trim(),
  }));

  return cleaned.length ? JSON.stringify(cleaned) : '';
}

function completedWorkflowActionLabel(action: string) {
  if (action.startsWith('Submit for ')) {
    return action.replace('Submit for ', 'Submitted for ');
  }
  if (action.startsWith('Submit ')) {
    return action.replace('Submit ', 'Submitted ');
  }
  return action;
}

function numberFromWorkspace(
  value: string | boolean | undefined
): number | undefined {
  if (typeof value !== 'string' || value.trim() === '') return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function parseBoundaryWorkspacePoints(value: string | boolean | undefined) {
  if (typeof value !== 'string' || value.trim() === '') return [];

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];

    return parsed
      .map((item, index) => {
        const northing = Number(
          Array.isArray(item)
            ? item[0]
            : (item?.northing ??
                item?.Northing ??
                item?.northingFeet ??
                item?.NorthingFeet)
        );
        const easting = Number(
          Array.isArray(item)
            ? item[1]
            : (item?.easting ??
                item?.Easting ??
                item?.eastingFeet ??
                item?.EastingFeet)
        );
        if (!Number.isFinite(northing) || !Number.isFinite(easting))
          return null;

        return {
          label: `${item?.beacon ?? item?.Beacon ?? item?.beaconIndex ?? item?.BeaconIndex ?? `Beacon ${index + 1}`}`,
          northing,
          easting,
        };
      })
      .filter(
        (item): item is { label: string; northing: number; easting: number } =>
          Boolean(item)
      );
  } catch {
    return [];
  }
}

function cadastralPointsFromValues(values: WorkspaceValues) {
  const boundaryPoints = parseBoundaryWorkspacePoints(
    values.boundaryCoordinates
  );
  if (boundaryPoints.length > 0) return boundaryPoints;

  return [1, 2, 3, 4]
    .map((index) => {
      const northing = numberFromWorkspace(
        values[`beacon${index}NorthingFeet`]
      );
      const easting = numberFromWorkspace(values[`beacon${index}EastingFeet`]);
      if (northing == null || easting == null) return null;

      return {
        label: `${values[`beacon${index}Index`] || `Beacon ${index}`}`,
        northing,
        easting,
      };
    })
    .filter(
      (item): item is { label: string; northing: number; easting: number } =>
        Boolean(item)
    );
}

function ownerPointsFromValues(values: WorkspaceValues) {
  return [1, 2, 3, 4]
    .map((index) => {
      const northing = numberFromWorkspace(
        values[`ownerBeacon${index}NorthingFeet`]
      );
      const easting = numberFromWorkspace(
        values[`ownerBeacon${index}EastingFeet`]
      );
      if (northing == null || easting == null) return null;

      return { label: `Beacon ${index}`, northing, easting };
    })
    .filter(
      (item): item is { label: string; northing: number; easting: number } =>
        Boolean(item)
    );
}

function coordinateComparisonRows(
  cadastralValues: WorkspaceValues,
  classificationValues: WorkspaceValues
): BeaconComparisonRow[] {
  const cadastral = cadastralPointsFromValues(cadastralValues);
  const classification = ownerPointsFromValues(classificationValues);
  const maxRows = Math.max(cadastral.length, classification.length, 4);

  return Array.from({ length: maxRows }, (_, index) => {
    const surveyPoint = cadastral[index];
    const ownerPoint = classification[index];
    const deviationFeet =
      surveyPoint && ownerPoint
        ? Math.hypot(
            surveyPoint.northing - ownerPoint.northing,
            surveyPoint.easting - ownerPoint.easting
          )
        : undefined;

    return {
      label: ownerPoint?.label || surveyPoint?.label || `Beacon ${index + 1}`,
      cadastralNorthing: surveyPoint?.northing,
      cadastralEasting: surveyPoint?.easting,
      classificationNorthing: ownerPoint?.northing,
      classificationEasting: ownerPoint?.easting,
      deviationFeet,
    };
  });
}

function formatFeet(value?: number) {
  return value == null ? '-' : value.toFixed(2);
}

function useAcquisitionBoard() {
  const [stages, setStages] = React.useState<LandAcquisitionStage[]>([]);
  const [loading, setLoading] = React.useState(true);

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      const board =
        await estateAcquisitionService.getAcquisitionWorkflowBoard();
      setStages(board.stages);
    } catch (error) {
      console.error(error);
      toast.error('Unable to load land acquisition workflow board.');
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);

  return { stages, loading, reload: load };
}

export default function LandAcquisitionPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { hasAnyRole } = useAuth();
  const { stages, loading, reload } = useAcquisitionBoard();
  const [selectedStageId, setSelectedStageId] = React.useState(0);
  const [selectedItemId, setSelectedItemId] = React.useState<string | null>(
    null
  );
  const [draftItem, setDraftItem] = React.useState<LandAcquisitionItem | null>(
    null
  );
  const [workspaceOpen, setWorkspaceOpen] = React.useState(false);
  const [workspaceValues, setWorkspaceValues] = React.useState<
    Record<string, string | boolean>
  >({});
  const [savingWorkspace, setSavingWorkspace] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const [stageDocumentRequirements, setStageDocumentRequirements] =
    React.useState<Record<number, LandAcquisitionStageDocumentRequirement[]>>(
      {}
    );
  const [documentRequirementsError, setDocumentRequirementsError] =
    React.useState<string | null>(null);

  const selectedStage =
    stages.find((stage) => stage.id === selectedStageId) || stages[0];
  const allItems = React.useMemo(
    () => stages.flatMap((stage) => stage.items),
    [stages]
  );
  const selectedItem = React.useMemo(() => {
    if (selectedItemId)
      return allItems.find((item) => item.id === selectedItemId) || null;
    return selectedStage?.items[0] || null;
  }, [allItems, selectedItemId, selectedStage]);
  const selectedItemStage =
    (selectedItem &&
      stages.find((stage) => stage.order === selectedItem.stageOrder)) ||
    selectedStage;
  const draftStage = draftItem
    ? stages.find((stage) => stage.order === draftItem.stageOrder) || null
    : null;
  const workspaceStage = draftStage || selectedItemStage;

  React.useEffect(() => {
    if (!selectedItem && selectedStage?.items[0])
      setSelectedItemId(selectedStage.items[0].id);
  }, [selectedItem, selectedStage]);

  React.useEffect(() => {
    if (
      selectedItem &&
      selectedItemStage &&
      selectedStage &&
      selectedItem.stageOrder !== selectedStage.order
    ) {
      setSelectedStageId(selectedItemStage.id);
    }
  }, [selectedItem, selectedItemStage, selectedStage]);

  const filteredItems = React.useMemo(() => {
    const items = selectedStage?.items || [];
    const normalized = query.trim().toLowerCase();
    if (!normalized) return items;
    return items.filter((item) =>
      [
        item.projectReference,
        item.location,
        item.intendedUse,
        item.ownerName,
        item.status,
      ]
        .filter(Boolean)
        .some((value) => `${value}`.toLowerCase().includes(normalized))
    );
  }, [query, selectedStage]);
  const acquisitionPages = usePaginatedItems(filteredItems, 10);

  const metrics = React.useMemo(() => {
    const actionable = allItems.filter((item) =>
      ['Pending Approval', 'Submitted', 'Draft'].includes(item.status)
    ).length;
    return {
      total: allItems.length,
      actionable,
      documents: allItems.reduce((sum, item) => sum + item.documents, 0),
      activeStages: stages.filter((stage) => stage.count > 0).length,
    };
  }, [allItems, stages]);
  const canManageWorkflows = hasAnyRole([
    'admin',
    'SystemAdmin',
    'SuperAdmin',
    'TenantAdmin',
    'WorkflowAdmin',
  ]);
  const selectedItemIsAssignedApprovalStage = Boolean(
    selectedItem &&
      selectedItemStage &&
      selectedItem.stageOrder === selectedItemStage.order &&
      ACQUISITION_APPROVAL_STAGE_IDS.has(selectedItemStage.id) &&
      selectedItemStage.items.some((item) => item.id === selectedItem.id)
  );
  const canEditSelectedStage =
    canManageWorkflows ||
    (selectedItemStage
      ? hasAnyRole([selectedItemStage.requiredRole])
      : false) ||
    selectedItemIsAssignedApprovalStage;
  const canCreateAcquisition = stages.some((stage) => stage.order === 0);
  const requestedAcquisitionId = searchParams.get('acquisitionId');
  const requestedStage = searchParams.get('stage');
  const openedFromRouteRef = React.useRef<string | null>(null);

  React.useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        setDocumentRequirementsError(null);
        const requirements =
          await estateAcquisitionService.getActiveWorkflowDocumentRequirements();
        if (mounted) setStageDocumentRequirements(requirements);
      } catch (error) {
        console.error(error);
        if (mounted) {
          setStageDocumentRequirements({});
          setDocumentRequirementsError(
            'Required document configuration could not be loaded. Refresh the page before attaching files or submitting this stage.'
          );
        }
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  const openWorkspaceAtStage = async (
    item: LandAcquisitionItem,
    stage: LandAcquisitionStage
  ) => {
    setDraftItem(null);
    setSelectedStageId(stage.id);
    setSelectedItemId(item.id);
    const defaults = defaultsFor(stage.workspaceKind, item);
    try {
      const workspace = await estateAcquisitionService.getWorkspace(
        item.id,
        stage.id
      );
      const mergedValues = { ...defaults, ...workspace.values };
      if (
        stage.workspaceKind === 'cadastral-survey' &&
        typeof mergedValues.mainPortion !== 'boolean'
      ) {
        mergedValues.mainPortion = true;
      }
      setWorkspaceValues(mergedValues);
      setStageDocumentRequirements((current) => ({
        ...current,
        [stage.order]: workspace.documentRequirements,
      }));
    } catch (error) {
      console.error(error);
      setWorkspaceValues(defaults);
      toast.error('Unable to load the saved stage entries.');
    }
    setWorkspaceOpen(true);
  };

  const openWorkspace = async (item: LandAcquisitionItem) => {
    const stage =
      stages.find((candidate) => candidate.order === item.stageOrder) ||
      selectedStage;
    if (!stage) return;
    await openWorkspaceAtStage(item, stage);
  };

  React.useEffect(() => {
    if (!requestedAcquisitionId || loading || stages.length === 0) return;
    const routeKey = `${requestedAcquisitionId}:${requestedStage || ''}`;
    if (openedFromRouteRef.current === routeKey) return;

    const item = allItems.find(
      (candidate) => candidate.id === requestedAcquisitionId
    );
    if (!item) {
      setQuery(requestedAcquisitionId);
      return;
    }

    const parsedStage = requestedStage
      ? Number(requestedStage)
      : item.stageOrder;
    const targetStage =
      stages.find(
        (stage) => stage.order === parsedStage || stage.id === parsedStage
      ) || stages.find((stage) => stage.order === item.stageOrder);
    if (!targetStage) return;

    openedFromRouteRef.current = routeKey;
    setQuery(item.projectReference);
    void openWorkspaceAtStage(item, targetStage);
  }, [allItems, loading, requestedAcquisitionId, requestedStage, stages]);

  const openNewAcquisition = () => {
    const stage =
      stages.find((candidate) => candidate.order === 0) || selectedStage;
    if (!stage) return;

    const item: LandAcquisitionItem = {
      id: '',
      projectReference: '',
      location: '',
      status: 'Draft',
      stageOrder: 0,
      currentStage: stage.title,
      intendedUse: '',
      estimatedSize: '',
      documents: 0,
      notes: 0,
      lastActivity: new Date().toISOString().slice(0, 10),
      stageInputsComplete: false,
      missingInputs: WORKSPACE_FIELDS[stage.workspaceKind].map(
        (config) => config.key
      ),
    };

    setSelectedStageId(stage.id);
    setSelectedItemId(null);
    setDraftItem(item);
    setWorkspaceValues(defaultsFor(stage.workspaceKind, item));
    setWorkspaceOpen(true);
  };

  const runAction = async (
    item: LandAcquisitionItem,
    stage: LandAcquisitionStage,
    actionType: 'primary' | 'reject',
    comments?: string
  ) => {
    try {
      const result = await estateAcquisitionService.runWorkflowAction({
        acquisitionId: item.id,
        procedure: stage.id,
        actionType,
        comments,
      });
      if (!result.success)
        throw new Error(result.message || 'Workflow action failed.');
      const returnedItem = result.item;
      const nextStage =
        returnedItem &&
        stages.find((candidate) => candidate.order === returnedItem.stageOrder);
      if (nextStage && returnedItem) {
        // Keep the land acquisition board focused on the stage returned by the workflow engine after handoff.
        setSelectedStageId(nextStage.id);
        setSelectedItemId(returnedItem.id);
      }
      await reload();
    } catch (error) {
      console.error(error);
      throw error;
    }
  };

  const completeWorkflowTask = async (
    item: LandAcquisitionItem,
    stepInstanceId: string,
    comments?: string
  ) => {
    const result = await estateAcquisitionService.completeWorkflowTask(
      item.id,
      stepInstanceId,
      comments
    );
    if (!result.success)
      throw new Error(result.message || 'Workflow task failed.');

    const returnedItem = result.item;
    const nextStage =
      returnedItem &&
      stages.find((candidate) => candidate.order === returnedItem.stageOrder);
    if (nextStage && returnedItem) {
      // Keep the selected card aligned with the Estate workflow stage returned by the acquisition API.
      setSelectedStageId(nextStage.id);
      setSelectedItemId(returnedItem.id);
    }

    return { success: result.success, message: result.message };
  };

  const saveWorkspace = async (
    pendingDocuments: PendingAcquisitionDocument[] = []
  ) => {
    const item = draftItem || selectedItem;
    const stage =
      draftItem || !selectedItem
        ? draftStage || selectedStage
        : selectedItemStage || selectedStage;
    if (!item || !stage) return;
    try {
      setSavingWorkspace(true);
      const result = await estateAcquisitionService.saveWorkspace({
        acquisitionId: item.id || undefined,
        procedureId: stage.id,
        workspaceKind: stage.workspaceKind,
        values: workspaceValues,
      });
      if (pendingDocuments.length > 0) {
        await Promise.all(
          pendingDocuments.map((document) =>
            estateAcquisitionService.uploadDocument(
              result.acquisitionId,
              stage.id,
              document.file,
              document.documentType,
              document.documentName
            )
          )
        );
      }
      if (result.stageInputsComplete) {
        toast.success(
          pendingDocuments.length > 0
            ? 'Workspace and documents saved.'
            : 'Workspace saved. All stage inputs are complete.'
        );
      } else {
        toast.warning('Workspace saved as incomplete.', {
          description: `${result.missingInputs.length} required input${result.missingInputs.length === 1 ? '' : 's'} remaining.`,
        });
      }
      setWorkspaceOpen(false);
      setDraftItem(null);
      await reload();
    } catch (error: any) {
      console.error(error);
      toast.error(error?.message || 'Unable to save workspace.');
    } finally {
      setSavingWorkspace(false);
    }
  };

  if (loading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading land acquisition workflow
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Landmark className="h-4 w-4" />
            Estate workflow
          </div>
          <h1 className="mt-1 text-2xl font-semibold text-foreground">
            Land Acquisition Procedure
          </h1>
          <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
            Manage land acquisition requests through the configured RHEMA
            workflow stages, roles, and approvals.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {canManageWorkflows && (
            <Button
              variant="outline"
              onClick={() =>
                router.push(
                  '/administration/workflow?entityType=LandAcquisition'
                )
              }
            >
              <FolderOpen className="mr-2 h-4 w-4" />
              Workflow Admin
            </Button>
          )}
          <Button variant="outline" onClick={reload}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {canCreateAcquisition && (
            <Button variant="outline" onClick={openNewAcquisition}>
              <Plus className="mr-2 h-4 w-4" />
              New Acquisition
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-3 md:grid-cols-4">
        <MetricCard
          label="Acquisitions"
          value={metrics.total}
          icon={Landmark}
        />
        <MetricCard
          label="Actionable"
          value={metrics.actionable}
          icon={Clock3}
        />
        <MetricCard
          label="Active Stages"
          value={metrics.activeStages}
          icon={Layers3}
        />
        <MetricCard
          label="Documents"
          value={metrics.documents}
          icon={FileArchive}
        />
      </div>

      {stages.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>Workflow not configured</CardTitle>
            <CardDescription>
              Configure and publish the Land Acquisition workflow in
              Administration &gt; Workflow Setup. The acquisition board will
              show the stages, documents, checklist items, roles, and
              assignments returned by that workflow.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button
              variant="outline"
              onClick={() =>
                router.push(
                  '/administration/workflow?entityType=LandAcquisition'
                )
              }
            >
              <FolderOpen className="mr-2 h-4 w-4" />
              Open Workflow Setup
            </Button>
          </CardContent>
        </Card>
      ) : null}

      {stages.length > 0 ? (
        <>
          <div className="rounded-lg border bg-card p-2 text-card-foreground shadow-sm">
            <ScrollArea className="w-full whitespace-nowrap">
              <div className="flex gap-2 pb-2">
                {stages.map((stage) => (
                  <StageButton
                    key={stage.id}
                    stage={stage}
                    active={stage.id === selectedStageId}
                    onClick={() => {
                      setSelectedStageId(stage.id);
                      setSelectedItemId(stage.items[0]?.id || null);
                    }}
                  />
                ))}
              </div>
            </ScrollArea>
          </div>

          <div className="grid gap-5 xl:grid-cols-[440px_1fr]">
            <section className="rounded-lg border bg-card text-card-foreground shadow-sm">
              <div className="border-b p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h2 className="text-base font-semibold text-foreground">
                      {selectedStage?.title}
                    </h2>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {selectedStage?.description}
                    </p>
                  </div>
                  <Badge variant="secondary">
                    Procedure {selectedStage?.id ?? 0}
                  </Badge>
                </div>
                <div className="relative mt-4">
                  <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                    className="pl-9"
                    placeholder="Search acquisitions"
                  />
                </div>
              </div>
              <ScrollArea className="h-[620px]">
                <div className="space-y-3 p-4">
                  {filteredItems.length === 0 ? (
                    <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
                      No acquisitions are currently waiting at this procedure.
                    </div>
                  ) : (
                    acquisitionPages.items.map((item) => (
                      <AcquisitionCard
                        key={item.id}
                        item={item}
                        active={selectedItem?.id === item.id}
                        onSelect={() => setSelectedItemId(item.id)}
                        onOpen={() => openWorkspace(item)}
                      />
                    ))
                  )}
                  {filteredItems.length > acquisitionPages.pageSize ? <Pagination currentPage={acquisitionPages.currentPage} totalPages={acquisitionPages.totalPages} totalItems={acquisitionPages.totalItems} pageSize={acquisitionPages.pageSize} onPageChange={acquisitionPages.setCurrentPage} /> : null}
                </div>
              </ScrollArea>
            </section>

            <section className="rounded-lg border bg-card text-card-foreground shadow-sm">
              {selectedItem && selectedItemStage ? (
                <AcquisitionDetail
                  item={selectedItem}
                  stage={selectedItemStage}
                  stages={stages}
                  onOpenWorkspace={() => openWorkspace(selectedItem)}
                  onPrimary={(comments) =>
                    runAction(
                      selectedItem,
                      selectedItemStage,
                      'primary',
                      comments
                    )
                  }
                  onReject={(comments) =>
                    runAction(
                      selectedItem,
                      selectedItemStage,
                      'reject',
                      comments
                    )
                  }
                  onCompleteTask={(stepInstanceId, comments) =>
                    completeWorkflowTask(selectedItem, stepInstanceId, comments)
                  }
                  onReload={reload}
                  documentRequirements={
                    stageDocumentRequirements[selectedItemStage.id] ||
                    stageDocumentRequirements[selectedItemStage.order] ||
                    []
                  }
                  onOpenWorkflows={() =>
                    router.push(
                      '/administration/workflow?entityType=LandAcquisition'
                    )
                  }
                  canManageWorkflows={canManageWorkflows}
                />
              ) : (
                <div className="flex min-h-[520px] items-center justify-center text-sm text-muted-foreground">
                  Select an acquisition to view workflow details.
                </div>
              )}
            </section>
          </div>
        </>
      ) : null}

      {(draftItem || selectedItem) && workspaceStage && (
        <WorkspaceDialog
          open={workspaceOpen}
          onOpenChange={(open) => {
            setWorkspaceOpen(open);
            if (!open) setDraftItem(null);
          }}
          item={(draftItem || selectedItem) as LandAcquisitionItem}
          stage={workspaceStage}
          documentRequirements={
            stageDocumentRequirements[workspaceStage.id] ||
            stageDocumentRequirements[workspaceStage.order] ||
            []
          }
          documentRequirementsError={documentRequirementsError}
          values={workspaceValues}
          onChange={setWorkspaceValues}
          onSave={saveWorkspace}
          onReload={reload}
          saving={savingWorkspace}
          canEdit={canEditSelectedStage}
        />
      )}
    </div>
  );
}

function MetricCard({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: number;
  icon: React.ComponentType<{ className?: string }>;
}) {
  return (
    <div className="rounded-lg border bg-card p-4 text-card-foreground shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="mt-1 text-2xl font-semibold text-foreground">{value}</p>
        </div>
        <span className="flex h-10 w-10 items-center justify-center rounded-md bg-muted text-muted-foreground">
          <Icon className="h-5 w-5" />
        </span>
      </div>
    </div>
  );
}

function StageButton({
  stage,
  active,
  onClick,
}: {
  stage: LandAcquisitionStage;
  active: boolean;
  onClick: () => void;
}) {
  const Icon = stageIcons[stage.workspaceKind];
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex min-w-[210px] items-center gap-3 rounded-md border px-3 py-2 text-left transition ${
        active
          ? 'border-primary bg-primary text-primary-foreground'
          : 'border-border bg-card hover:bg-muted/60'
      }`}
    >
      <span
        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-md ${active ? 'bg-primary-foreground/15' : 'bg-muted text-muted-foreground'}`}
      >
        <Icon className="h-4 w-4" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-medium">
          {stage.order + 1}. {stage.title}
        </span>
        <span
          className={`block text-xs ${active ? 'text-white/70' : 'text-muted-foreground'}`}
        >
          {stage.count} records
        </span>
      </span>
    </button>
  );
}

function AcquisitionCard({
  item,
  active,
  onSelect,
  onOpen,
}: {
  item: LandAcquisitionItem;
  active: boolean;
  onSelect: () => void;
  onOpen: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={`w-full rounded-md border p-4 text-left transition ${active ? 'border-primary bg-muted/70' : 'border-border bg-card hover:bg-muted/60'}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-foreground">
            {item.projectReference}
          </p>
          <p className="mt-1 flex items-center gap-1 truncate text-sm text-muted-foreground">
            <MapPin className="h-3.5 w-3.5 shrink-0" />
            {item.location}
          </p>
        </div>
        <Badge
          className={item.riskLevel ? riskClasses[item.riskLevel] : undefined}
          variant="outline"
        >
          {item.riskLevel || 'Normal'}
        </Badge>
      </div>
      <div className="mt-3 grid grid-cols-2 gap-2 text-xs text-muted-foreground">
        <span>{item.estimatedSize}</span>
        <span>{item.valueEstimate || 'Value pending'}</span>
        <span>{item.documents} docs</span>
        <span>{item.notes} notes</span>
      </div>
      <div className="mt-3 flex items-center justify-between gap-2">
        <Badge variant="secondary">{item.status}</Badge>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          onClick={(event) => {
            event.stopPropagation();
            onOpen();
          }}
        >
          <FolderOpen className="mr-2 h-4 w-4" />
          Open
        </Button>
      </div>
    </button>
  );
}

function AcquisitionDetail({
  item,
  stage,
  stages,
  onOpenWorkspace,
  onPrimary,
  onReject,
  onCompleteTask,
  onReload,
  documentRequirements,
  onOpenWorkflows,
  canManageWorkflows,
}: {
  item: LandAcquisitionItem;
  stage: LandAcquisitionStage;
  stages: LandAcquisitionStage[];
  onOpenWorkspace: () => void;
  onPrimary: (comments?: string) => Promise<void>;
  onReject: (comments?: string) => Promise<void>;
  onCompleteTask: (
    stepInstanceId: string,
    comments?: string
  ) => Promise<{ success?: boolean; message?: string }>;
  onReload: () => Promise<void>;
  documentRequirements: LandAcquisitionStageDocumentRequirement[];
  onOpenWorkflows: () => void;
  canManageWorkflows: boolean;
}) {
  const Icon = stageIcons[stage.workspaceKind];
  const [summaryOpen, setSummaryOpen] = React.useState(false);
  const [summary, setSummary] = React.useState<LandAcquisitionSummary | null>(
    null
  );
  const [summaryLoading, setSummaryLoading] = React.useState(false);
  const [summaryLocations, setSummaryLocations] = React.useState<
    HrLocationLookup[]
  >([]);
  const [stageDocuments, setStageDocuments] = React.useState<
    LandAcquisitionDocument[]
  >([]);
  const [stageSubmitting, setStageSubmitting] = React.useState(false);
  const [handoffSubmitting, setHandoffSubmitting] = React.useState(false);
  const missingLabels = inputLabels(
    stage.workspaceKind,
    item.missingInputs || []
  );
  const isAssetCreationStage = stage.workspaceKind === 'asset-creation';
  const hasCreatedLandAsset =
    isAssetCreationStage && item.hasLandAsset === true;
  const hasPublishedLandAsset =
    isAssetCreationStage && item.status === 'Approved';
  const captureStageActionLabel = hasCreatedLandAsset
    ? 'Complete Land Creation'
    : stage.primaryAction;
  const assetWorkspaceNotSavedReason =
    isAssetCreationStage && !hasCreatedLandAsset
      ? 'Open the Land Creation workspace, review the auto-filled land details, then Save Workspace. Saving creates the Estate land record before Land Bank handoff.'
      : undefined;
  const forwardActionReason = item.stageInputsComplete
    ? undefined
    : assetWorkspaceNotSavedReason ||
      `Complete all stage inputs first: ${missingLabels.slice(0, 4).join(', ')}${missingLabels.length > 4 ? ` and ${missingLabels.length - 4} more` : ''}.`;
  const workflowActionDescription =
    item.stageOrder > stage.order
      ? completedWorkflowActionLabel(stage.primaryAction)
      : hasCreatedLandAsset
        ? 'Land created'
        : stage.primaryAction;
  const showCaptureStageSubmit =
    item.stageOrder === stage.order &&
    stage.order > 0 &&
    !ACQUISITION_APPROVAL_STAGE_IDS.has(stage.id) &&
    ['Pending Approval', 'Submitted', 'Rejected'].includes(item.status);
  const timelineStages = stages.length > 0 ? stages : [stage];

  const openSummary = async () => {
    setSummaryOpen(true);
    setSummaryLoading(true);
    try {
      const [nextSummary, locations] = await Promise.all([
        estateAcquisitionService.getSummary(item.id),
        estateAcquisitionService.getActiveHrLocations().catch(() => []),
      ]);
      setSummary(nextSummary);
      setSummaryLocations(locations);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to load the parcel summary.'
      );
    } finally {
      setSummaryLoading(false);
    }
  };

  React.useEffect(() => {
    setSummary(null);
    setSummaryOpen(false);
  }, [item.id]);

  React.useEffect(() => {
    let mounted = true;
    setStageDocuments([]);

    (async () => {
      try {
        const documents = await estateAcquisitionService.getDocuments(item.id);
        if (!mounted) return;
        setStageDocuments(
          documents.filter((document) => document.procedureId === stage.id)
        );
      } catch {
        if (mounted) setStageDocuments([]);
      }
    })();

    return () => {
      mounted = false;
    };
  }, [item.id, item.documents, stage.id]);

  const workflowStageAttachments = React.useMemo<
    WorkflowTaskAttachmentDto[]
  >(() => {
    const stageRequirementKey = `${stage.workspaceKind}-documents`;

    return stageDocuments.flatMap((document) => {
      const matchingRequirements = documentRequirements.filter((requirement) =>
        matchesRequirement(document, requirement)
      );
      const targets =
        matchingRequirements.length > 0
          ? matchingRequirements
          : [
              {
                id: stageRequirementKey,
                requirementKey: stageRequirementKey,
                documentName: `${stage.title} Documents`,
                documentType: document.documentType,
                isRequired: true,
              },
            ];

      return [
        {
          id: `estate-stage-${document.id}-${stageRequirementKey}`,
          requirementKey: stageRequirementKey,
          documentName: `${stage.title} Documents`,
          documentType: document.documentType,
          fileName: document.fileName,
          filePath: document.id,
          contentType: 'application/octet-stream',
          fileSizeBytes: 0,
          uploadedAt: document.uploadedAt,
          uploadedById: document.landAcquisitionId,
          uploadedByName: document.uploadedBy || 'Estate/Facility',
        },
        ...targets.map((requirement) => ({
          id: `estate-stage-${document.id}-${requirement.requirementKey}`,
          requirementKey: requirement.requirementKey,
          checklistItemId: requirement.id,
          documentName: requirement.documentName,
          documentType: requirement.documentType || document.documentType,
          fileName: document.fileName,
          filePath: document.id,
          contentType: 'application/octet-stream',
          fileSizeBytes: 0,
          uploadedAt: document.uploadedAt,
          uploadedById: document.landAcquisitionId,
          uploadedByName: document.uploadedBy || 'Estate/Facility',
        })),
      ];
    });
  }, [documentRequirements, stage.title, stage.workspaceKind, stageDocuments]);

  const submitCaptureStage = async () => {
    if (!item.stageInputsComplete) {
      toast.error(
        forwardActionReason ||
          'Complete all required stage inputs before submitting this stage.'
      );
      return;
    }

    try {
      setStageSubmitting(true);
      await onPrimary();
      toast.success(completedWorkflowActionLabel(captureStageActionLabel), {
        description: item.projectReference,
      });
      await onReload();
    } catch (error: any) {
      toast.error('Unable to submit workflow stage.', {
        description: error?.message || undefined,
      });
    } finally {
      setStageSubmitting(false);
    }
  };

  const publishToLandBank = async () => {
    if (!hasCreatedLandAsset) {
      toast.error('Create and save the Estate land record before Land Bank handoff.');
      return;
    }

    if (!item.stageInputsComplete) {
      toast.error(
        forwardActionReason ||
          'Complete all required land fields before Land Bank handoff.'
      );
      return;
    }

    try {
      setHandoffSubmitting(true);
      const result = await estateAcquisitionService.publishToLandBank(item.id);
      if (!result.success) {
        throw new Error(result.message || 'Land Bank handoff failed.');
      }
      toast.success('Transferred to Land Bank', {
        description: result.asset?.assetCode || item.projectReference,
      });
      await onReload();
    } catch (error: any) {
      toast.error('Unable to transfer land to Land Bank.', {
        description: error?.message || undefined,
      });
    } finally {
      setHandoffSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-[720px] flex-col">
      <div className="border-b p-5">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">{item.projectReference}</Badge>
              <Badge variant="secondary">{item.status}</Badge>
              <Badge
                className={
                  item.riskLevel ? riskClasses[item.riskLevel] : undefined
                }
                variant="outline"
              >
                {item.riskLevel || 'Normal'} risk
              </Badge>
            </div>
            <h2 className="mt-3 text-xl font-semibold text-foreground">
              {item.location}
            </h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {item.intendedUse}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" onClick={openSummary}>
              <FileClock className="mr-2 h-4 w-4" />
              Summary
            </Button>
            <Button variant="outline" onClick={onOpenWorkspace}>
              <FolderOpen className="mr-2 h-4 w-4" />
              Workspace
            </Button>
            {canManageWorkflows && (
              <Button variant="outline" onClick={onOpenWorkflows}>
                <FolderOpen className="mr-2 h-4 w-4" />
                Routing
              </Button>
            )}
          </div>
        </div>
      </div>

      <div className="grid gap-5 p-5 lg:grid-cols-[1fr_320px]">
        <div className="space-y-5">
          <div className="rounded-lg border p-4">
            <div className="flex items-start gap-3">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground">
                <Icon className="h-5 w-5" />
              </span>
              <div>
                <h3 className="text-base font-semibold text-foreground">
                  {stage.order + 1}. {stage.title}
                </h3>
                <p className="mt-1 text-sm text-muted-foreground">
                  {stage.description}
                </p>
              </div>
            </div>
            <Separator className="my-4" />
            <div className="grid gap-3 md:grid-cols-3">
              <Field label="Owner" value={item.ownerName || 'Not recorded'} />
              <Field
                label="Acquisition Type"
                value={item.acquisitionType || 'Not recorded'}
              />
              <Field label="Estimated Size" value={item.estimatedSize} />
              <Field
                label="Value Estimate"
                value={item.valueEstimate || 'Pending'}
              />
              <Field label="Documents" value={`${item.documents}`} />
              <Field label="Notes" value={`${item.notes}`} />
            </div>
          </div>

          <div className="rounded-lg border p-4">
            <h3 className="text-base font-semibold text-foreground">
              Procedure Timeline
            </h3>
            <div className="mt-4 grid gap-2 md:grid-cols-2 xl:grid-cols-4">
              {timelineStages.map((candidate) => {
                const done =
                  candidate.order < stage.order ||
                  (hasPublishedLandAsset && candidate.order <= stage.order);
                const active =
                  !hasPublishedLandAsset && candidate.order === stage.order;
                return (
                  <div
                    key={candidate.id}
                    className={`rounded-md border px-3 py-2 text-sm ${
                      active
                        ? 'border-primary bg-primary text-primary-foreground'
                        : done
                          ? 'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900/70 dark:bg-emerald-950/40 dark:text-emerald-300'
                          : 'border-border bg-card text-muted-foreground'
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      {done ? (
                        <CheckCircle2 className="h-4 w-4" />
                      ) : active ? (
                        <Clock3 className="h-4 w-4" />
                      ) : (
                        <ArrowRight className="h-4 w-4" />
                      )}
                      <span className="truncate">{candidate.title}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        <aside className="space-y-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Workflow Actions</CardTitle>
              <CardDescription>{workflowActionDescription}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {!item.stageInputsComplete && (
                <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/70 dark:bg-amber-950/40 dark:text-amber-200">
                  <p className="font-medium">Stage inputs incomplete</p>
                  <p className="mt-1 text-xs">{forwardActionReason}</p>
                </div>
              )}
              {showCaptureStageSubmit && (
                <Button
                  className="w-full justify-start"
                  disabled={!item.stageInputsComplete || stageSubmitting}
                  onClick={() => void submitCaptureStage()}
                  title={
                    !item.stageInputsComplete ? forwardActionReason : undefined
                  }
                >
                  {stageSubmitting ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Send className="mr-2 h-4 w-4" />
                  )}
                  {captureStageActionLabel}
                </Button>
              )}
              {hasCreatedLandAsset && !hasPublishedLandAsset && (
                <Button
                  className="w-full justify-start"
                  disabled={!item.stageInputsComplete || handoffSubmitting}
                  onClick={() => void publishToLandBank()}
                  title={
                    !item.stageInputsComplete ? forwardActionReason : undefined
                  }
                  variant="outline"
                >
                  {handoffSubmitting ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Building2 className="mr-2 h-4 w-4" />
                  )}
                  Transfer to Land Bank
                </Button>
              )}
              <WorkflowApprovalActions
                entityType="LandAcquisition"
                entityId={item.id}
                entityLabel="Land Acquisition"
                entityNumber={item.projectReference}
                status={item.status}
                showStepBadge
                currentStepName={stage.title}
                loadWorkflowSummary
                canSubmit={
                  item.status === 'Draft' ||
                  (item.status === 'Rejected' && item.stageOrder === 0)
                }
                canApproveReject={
                  ACQUISITION_APPROVAL_STAGE_IDS.has(stage.id) &&
                  (item.status === 'Pending Approval' ||
                    item.status === 'Submitted')
                }
                forwardActionsDisabled={!item.stageInputsComplete}
                forwardActionsDisabledReason={forwardActionReason}
                onSubmit={() => onPrimary()}
                onApprove={(comments) => onPrimary(comments)}
                onReject={(comments) => onReject(comments)}
                onCompleteTask={({ stepInstanceId, comments }) =>
                  onCompleteTask(stepInstanceId, comments)
                }
                onAfterAction={onReload}
                externalTaskAttachments={workflowStageAttachments}
                hideSatisfiedTaskDocumentUploads
                hideSatisfiedTaskDocumentSection
                hideDocumentChecklistItems
                onOpenWorkflows={
                  canManageWorkflows ? onOpenWorkflows : undefined
                }
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Procedure Source</CardTitle>
              <CardDescription>{stage.method} workflow step</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <Field label="Procedure" value={`${stage.id}`} />
              <Field label="Role" value={stage.requiredRole} />
              <Field
                label="Estimated Hours"
                value={`${stage.estimatedHours}`}
              />
              <Field label="Last Activity" value={item.lastActivity} />
            </CardContent>
          </Card>
        </aside>
      </div>
      <Dialog open={summaryOpen} onOpenChange={setSummaryOpen}>
        <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Parcel Summary - {item.projectReference}</DialogTitle>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Field label="Location" value={item.location} />
            <Field label="Current Stage" value={stage.title} />
            <Field label="Status" value={item.status} />
            <Field label="Owner" value={item.ownerName || 'Not recorded'} />
          </div>
          {summaryLoading ? (
            <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Loading recorded stage information...
            </div>
          ) : summary?.stages.length ? (
            <div className="space-y-4">
              {summary.stages.map((savedStage) => {
                const definition = timelineStages.find(
                  (candidate) =>
                    candidate.id === savedStage.procedureId ||
                    candidate.order === savedStage.procedureId
                );
                const labels = new Map(
                  (definition
                    ? WORKSPACE_FIELDS[definition.workspaceKind]
                    : []
                  ).map((workspaceField) => [
                    workspaceField.key,
                    workspaceField.label,
                  ])
                );
                const fields = definition
                  ? WORKSPACE_FIELDS[definition.workspaceKind]
                  : [];
                const entries = summaryEntries(
                  savedStage.values,
                  fields,
                  labels,
                  summaryLocations
                );
                return (
                  <section
                    key={savedStage.procedureId}
                    className="rounded-md border p-4"
                  >
                    <div className="mb-3 flex items-center gap-2">
                      <Badge variant="outline">
                        Stage {savedStage.procedureId + 1}
                      </Badge>
                      <h3 className="text-sm font-semibold text-foreground">
                        {savedStage.title}
                      </h3>
                    </div>
                    {entries.length ? (
                      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                        {entries.map((entry) => (
                          <Field
                            key={entry.key}
                            label={entry.label}
                            value={entry.value}
                          />
                        ))}
                      </div>
                    ) : (
                      <p className="text-sm text-muted-foreground">
                        No stage entries were saved.
                      </p>
                    )}
                  </section>
                );
              })}
            </div>
          ) : (
            <p className="rounded-md border p-4 text-sm text-muted-foreground">
              No saved stage information is available yet.
            </p>
          )}
          <WorkflowApprovalHistoryPanel
            entityType="LandAcquisition"
            entityId={item.id}
            entityLabel="Land Acquisition"
            entityNumber={item.projectReference}
            status={item.status}
            currentStepName={stage.title}
            showActions={false}
          />
        </DialogContent>
      </Dialog>
    </div>
  );
}

type SummaryValue = string | boolean | number | null;

type SummaryEntry = {
  key: string;
  label: string;
  value: string;
};

function summaryEntries(
  values: Record<string, SummaryValue>,
  fields: WorkspaceField[],
  labels: Map<string, string>,
  locations: HrLocationLookup[]
): SummaryEntry[] {
  const fieldByKey = new Map(
    fields.map((fieldConfig) => [fieldConfig.key, fieldConfig])
  );

  return Object.entries(values).flatMap(([key, value]) => {
    if (value === null || value === '') return [];

    if (key === 'pastOwnersJson') {
      return pastOwnerSummaryEntries(value);
    }

    const config = fieldByKey.get(key);
    return [
      {
        key,
        label: labels.get(key) || key.replace(/([a-z])([A-Z])/g, '$1 $2'),
        value: formatSummaryValue(
          resolveSummaryValue(key, value, config, locations)
        ),
      },
    ];
  });
}

function pastOwnerSummaryEntries(value: SummaryValue): SummaryEntry[] {
  const owners = parsePastOwners(
    typeof value === 'string' ? value : undefined
  ).filter((owner) =>
    Object.values(owner).some((entry) => `${entry || ''}`.trim())
  );

  return owners.map((owner, index) => ({
    key: `pastOwnersJson-${index}`,
    label: `Past Owner ${index + 1}`,
    value: [
      owner.ownerName,
      owner.contactNumber ? `Contact: ${owner.contactNumber}` : '',
      owner.address ? `Address: ${owner.address}` : '',
      owner.identificationNumber
        ? `ID: ${owner.identificationType || 'Identification'} ${owner.identificationNumber}`
        : '',
      owner.ownershipStartDate || owner.ownershipEndDate
        ? `Ownership: ${owner.ownershipStartDate || 'Not recorded'} to ${owner.ownershipEndDate || 'Not recorded'}`
        : '',
      owner.dateGapReason ? `Gap reason: ${owner.dateGapReason}` : '',
    ]
      .filter(Boolean)
      .join(' | '),
  }));
}

function resolveSummaryValue(
  key: string,
  value: SummaryValue,
  config: WorkspaceField | undefined,
  locations: HrLocationLookup[]
): SummaryValue {
  if (typeof value !== 'string') return value;

  const isLocationField =
    config?.type === 'region' ||
    config?.type === 'district' ||
    /(region|district|country|town)id$/i.test(key);
  if (!isLocationField) return value;

  return (
    locations.find(
      (location) =>
        location.id === value ||
        location.name.localeCompare(value, undefined, {
          sensitivity: 'accent',
        }) === 0
    )?.name || value
  );
}

function formatSummaryValue(value: string | boolean | number | null): string {
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (value === null || value === '') return 'Not recorded';
  return String(value);
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs font-medium uppercase text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 text-sm text-foreground">{value}</p>
    </div>
  );
}

function CoordinateComparisonPanel({
  cadastralValues,
  classificationValues,
}: {
  cadastralValues: WorkspaceValues;
  classificationValues: WorkspaceValues;
}) {
  const rows = React.useMemo(
    () => coordinateComparisonRows(cadastralValues, classificationValues),
    [cadastralValues, classificationValues]
  );
  const toleranceFeet = 5;
  const capturedRows = rows.filter(
    (row) =>
      row.cadastralNorthing != null ||
      row.cadastralEasting != null ||
      row.classificationNorthing != null ||
      row.classificationEasting != null
  );
  const comparedRows = rows.filter((row) => row.deviationFeet != null);
  const maxDeviation = comparedRows.reduce(
    (current, row) => Math.max(current, row.deviationFeet ?? 0),
    0
  );
  const matched = comparedRows.length > 0 && maxDeviation <= toleranceFeet;

  return (
    <section className="rounded-lg border bg-card p-4">
      <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 className="text-sm font-semibold text-foreground">
            Coordinate comparison
          </h3>
          <p className="mt-1 text-xs text-muted-foreground">
            Compare survey-plan beacons against the ownership classification
            beacons.
          </p>
        </div>
        <Badge variant={matched ? 'secondary' : 'outline'}>
          {comparedRows.length === 0
            ? 'Awaiting coordinates'
            : matched
              ? `Within ${toleranceFeet} ft`
              : `Max deviation ${maxDeviation.toFixed(2)} ft`}
        </Badge>
      </div>

      {capturedRows.length === 0 ? (
        <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          No saved cadastral or ownership classification coordinates are
          available yet.
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="text-xs uppercase text-muted-foreground">
              <tr className="border-b">
                <th className="px-3 py-2 text-left">Beacon</th>
                <th className="px-3 py-2 text-left">Cadastral N / E</th>
                <th className="px-3 py-2 text-left">Classification N / E</th>
                <th className="px-3 py-2 text-left">Deviation</th>
                <th className="px-3 py-2 text-left">Status</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const hasDeviation = row.deviationFeet != null;
                const rowMatched =
                  hasDeviation && (row.deviationFeet ?? 0) <= toleranceFeet;

                return (
                  <tr key={row.label} className="border-b last:border-0">
                    <td className="px-3 py-2 font-medium">{row.label}</td>
                    <td className="px-3 py-2">
                      N {formatFeet(row.cadastralNorthing)} / E{' '}
                      {formatFeet(row.cadastralEasting)}
                    </td>
                    <td className="px-3 py-2">
                      N {formatFeet(row.classificationNorthing)} / E{' '}
                      {formatFeet(row.classificationEasting)}
                    </td>
                    <td className="px-3 py-2">
                      {hasDeviation
                        ? `${row.deviationFeet?.toFixed(2)} ft`
                        : '-'}
                    </td>
                    <td className="px-3 py-2">
                      <Badge variant={rowMatched ? 'secondary' : 'outline'}>
                        {hasDeviation
                          ? rowMatched
                            ? 'Match'
                            : 'Review'
                          : 'Incomplete'}
                      </Badge>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function EvidenceField({
  label,
  value,
}: {
  label: string;
  value: React.ReactNode;
}) {
  return (
    <div className="rounded-md border bg-background p-3">
      <p className="text-xs font-medium uppercase text-muted-foreground">
        {label}
      </p>
      <div className="mt-1 break-words text-sm font-medium text-foreground">
        {value}
      </div>
    </div>
  );
}

function AccountsPayableEvidenceDialog({
  open,
  onOpenChange,
  values,
  subject,
  view,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  values: WorkspaceValues;
  subject: string;
  view: 'invoice' | 'receipt';
}) {
  const amountDue = workspaceDecimal(values.amountDue);
  const amountPaid = workspaceDecimal(values.amountPaid) ?? 0;
  const balance =
    amountDue == null ? null : Math.max(0, amountDue - amountPaid);
  const title =
    view === 'invoice'
      ? 'Accounts Payable Invoice'
      : 'Payment Receipt';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-h-[90vh] max-w-3xl overflow-y-auto"
        onKeyDownCapture={(event) => {
          if (isEditableElement(event.target)) event.stopPropagation();
        }}
      >
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">{subject}</Badge>
            <Badge
              variant={
                values.isPaid === true ||
                `${values.isPaid}`.toLowerCase() === 'true'
                  ? 'secondary'
                  : 'outline'
              }
            >
              {values.isPaid === true ||
              `${values.isPaid}`.toLowerCase() === 'true'
                ? 'Paid'
                : 'Pending payment'}
            </Badge>
          </div>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">Invoice</h3>
            <div className="grid gap-3 md:grid-cols-2">
              <EvidenceField
                label="Invoice number"
                value={displayWorkspaceValue(
                  values.accountsPayableInvoiceNumber
                )}
              />
              <EvidenceField
                label="Invoice status"
                value={displayWorkspaceValue(
                  values.accountsPayableInvoiceStatus
                )}
              />
              <EvidenceField
                label="Amount due"
                value={formatWorkspaceMoney(values.amountDue)}
              />
              <EvidenceField
                label="Balance"
                value={
                  balance == null
                    ? '-'
                    : formatWorkspaceMoney(`${balance}`)
                }
              />
            </div>
          </section>

          <section className="space-y-3">
            <h3 className="text-sm font-semibold">Receipt / payment</h3>
            <div className="grid gap-3 md:grid-cols-2">
              <EvidenceField
                label="Payment number"
                value={displayWorkspaceValue(
                  values.accountsPayablePaymentNumber
                )}
              />
              <EvidenceField
                label="Payment status"
                value={displayWorkspaceValue(
                  values.accountsPayablePaymentStatus
                )}
              />
              <EvidenceField
                label="Receipt number"
                value={displayWorkspaceValue(values.receiptNumber)}
              />
              <EvidenceField
                label="Payment reference"
                value={displayWorkspaceValue(values.paymentReference)}
              />
              <EvidenceField
                label="Payment date"
                value={formatWorkspaceDate(values.paymentDate)}
              />
              <EvidenceField
                label="Amount paid"
                value={formatWorkspaceMoney(values.amountPaid)}
              />
              <EvidenceField
                label="Payment method"
                value={displayWorkspaceValue(values.paymentMethod)}
              />
            </div>
          </section>

          {`${values.paymentNotes ?? ''}`.trim() && (
            <section className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
              {displayWorkspaceValue(values.paymentNotes)}
            </section>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function PastOwnersPanel({
  owners,
  disabled,
  onChange,
}: {
  owners: PastOwnerDraft[];
  disabled: boolean;
  onChange: (owners: PastOwnerDraft[]) => void;
}) {
  const updateOwner = (index: number, patch: Partial<PastOwnerDraft>) => {
    onChange(
      owners.map((owner, row) =>
        row === index ? { ...owner, ...patch } : owner
      )
    );
  };

  const addOwner = () => onChange([...owners, emptyPastOwner()]);
  const removeOwner = (index: number) =>
    onChange(owners.filter((_, row) => row !== index));

  return (
    <section className="rounded-lg border bg-card p-4">
      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 className="text-sm font-semibold text-foreground">
            Ownership History / Past Owners
          </h3>
          <p className="mt-1 text-xs text-muted-foreground">
            Add previous owners from oldest to newest. Date Gap Reason appears
            only when a later owner starts after the preceding owner ended.
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled}
          onClick={addOwner}
        >
          <Plus className="mr-2 h-4 w-4" />
          Add Past Owner
        </Button>
      </div>

      {owners.length === 0 ? (
        <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          No past owners added yet.
        </div>
      ) : (
        <div className="space-y-4">
          {owners.map((owner, index) => {
            const gapRequired = hasPastOwnerDateGap(owners, index);

            return (
              <div key={index} className="rounded-md border p-4">
                <div className="mb-4 flex items-center justify-between gap-3">
                  <h4 className="text-sm font-semibold text-foreground">
                    Past Owner {index + 1}
                  </h4>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    disabled={disabled}
                    onClick={() => removeOwner(index)}
                  >
                    <Ban className="mr-2 h-4 w-4" />
                    Remove
                  </Button>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="space-y-2">
                    <Label>
                      Owner Name <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      value={owner.ownerName}
                      disabled={disabled}
                      onChange={(event) =>
                        updateOwner(index, { ownerName: event.target.value })
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>
                      Contact Number <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      value={owner.contactNumber}
                      disabled={disabled}
                      onChange={(event) =>
                        updateOwner(index, {
                          contactNumber: event.target.value,
                        })
                      }
                    />
                  </div>
                  <div className="space-y-2 md:col-span-2">
                    <Label>
                      Address <span className="text-destructive">*</span>
                    </Label>
                    <Textarea
                      value={owner.address}
                      disabled={disabled}
                      rows={2}
                      onChange={(event) =>
                        updateOwner(index, { address: event.target.value })
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>
                      Identification Type{' '}
                      <span className="text-destructive">*</span>
                    </Label>
                    <Select
                      value={owner.identificationType || 'Ghana Card'}
                      disabled={disabled}
                      onValueChange={(value) =>
                        updateOwner(index, { identificationType: value })
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {[
                          'Ghana Card',
                          'Passport',
                          'Voter ID',
                          'Driver License',
                          'Other',
                        ].map((option) => (
                          <SelectItem key={option} value={option}>
                            {option}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>
                      Identification Number{' '}
                      <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      value={owner.identificationNumber}
                      disabled={disabled}
                      onChange={(event) =>
                        updateOwner(index, {
                          identificationNumber: event.target.value,
                        })
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>
                      Ownership Start Date{' '}
                      <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      type="date"
                      value={owner.ownershipStartDate}
                      disabled={disabled}
                      onChange={(event) =>
                        updateOwner(index, {
                          ownershipStartDate: event.target.value,
                        })
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>
                      Ownership End Date{' '}
                      <span className="text-destructive">*</span>
                    </Label>
                    <Input
                      type="date"
                      value={owner.ownershipEndDate}
                      disabled={disabled}
                      onChange={(event) =>
                        updateOwner(index, {
                          ownershipEndDate: event.target.value,
                        })
                      }
                    />
                  </div>
                  {gapRequired && (
                    <div className="space-y-2 md:col-span-2">
                      <Label>
                        Date Gap Reason{' '}
                        <span className="text-destructive">*</span>
                      </Label>
                      <Textarea
                        value={owner.dateGapReason}
                        disabled={disabled}
                        rows={2}
                        onChange={(event) =>
                          updateOwner(index, {
                            dateGapReason: event.target.value,
                          })
                        }
                      />
                      <p className="text-xs text-muted-foreground">
                        Required because this owner starts after the preceding
                        owner ended.
                      </p>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}

function WorkspaceDialog({
  open,
  onOpenChange,
  item,
  stage,
  documentRequirements,
  documentRequirementsError,
  values,
  onChange,
  onSave,
  onReload,
  saving,
  canEdit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item: LandAcquisitionItem;
  stage: LandAcquisitionStage;
  documentRequirements: LandAcquisitionStageDocumentRequirement[];
  documentRequirementsError: string | null;
  values: WorkspaceValues;
  onChange: React.Dispatch<React.SetStateAction<WorkspaceValues>>;
  onSave: (pendingDocuments?: PendingAcquisitionDocument[]) => Promise<void>;
  onReload: () => Promise<void>;
  saving: boolean;
  canEdit: boolean;
}) {
  const router = useRouter();
  const setValue = (key: string, value: string | boolean) =>
    onChange((current) => ({ ...current, [key]: value }));
  const sections = workspaceSectionsFor(stage.workspaceKind);
  const missingInputs = missingWorkspaceInputs(stage.workspaceKind, values);
  const [documents, setDocuments] = React.useState<LandAcquisitionDocument[]>(
    []
  );
  const [loadingDocuments, setLoadingDocuments] = React.useState(false);
  const [uploadingDocuments, setUploadingDocuments] = React.useState(false);
  const [pendingDocuments, setPendingDocuments] = React.useState<
    PendingAcquisitionDocument[]
  >([]);
  const [vendors, setVendors] = React.useState<BusinessPartnerDto[]>([]);
  const [vendorsLoading, setVendorsLoading] = React.useState(false);
  const [vendorsError, setVendorsError] = React.useState<string | null>(null);
  const [hrLocations, setHrLocations] = React.useState<HrLocationLookup[]>([]);
  const [hrLocationsLoading, setHrLocationsLoading] = React.useState(false);
  const [hrLocationsError, setHrLocationsError] = React.useState<string | null>(
    null
  );
  const [syncingPayable, setSyncingPayable] = React.useState(false);
  const [generationTemplates, setGenerationTemplates] = React.useState<
    CentralDocumentGenerationTemplate[]
  >([]);
  const [selectedTemplateCode, setSelectedTemplateCode] = React.useState('');
  const [generatingAgreement, setGeneratingAgreement] = React.useState(false);
  const [generatedAgreement, setGeneratedAgreement] =
    React.useState<GeneratedCentralDocumentResult | null>(null);
  const [existingAgreementRecord, setExistingAgreementRecord] =
    React.useState<CentralDocumentRecord | null>(null);
  const [loadingAgreementRecord, setLoadingAgreementRecord] =
    React.useState(false);
  const [preview, setPreview] = React.useState<{
    url: string;
    name: string;
  } | null>(null);
  const [payableEvidenceView, setPayableEvidenceView] = React.useState<
    'invoice' | 'receipt' | null
  >(null);
  const [cadastralComparisonValues, setCadastralComparisonValues] =
    React.useState<WorkspaceValues>({});
  const [classificationComparisonValues, setClassificationComparisonValues] =
    React.useState<WorkspaceValues>({});
  const assignmentMessage = ACQUISITION_APPROVAL_STAGE_IDS.has(stage.id)
    ? `You are not assigned to ${stage.title}. Required role: ${stage.requiredRole} or the active workflow assignee.`
    : `You are not assigned to ${stage.title}. Required role: ${stage.requiredRole}.`;
  const accountsPayableInvoiceId =
    `${values.accountsPayableInvoiceId || ''}`.trim();
  const accountsPayableSupplierId =
    `${values.accountsPayableSupplierId || ''}`.trim();
  const accountsPayablePaymentId =
    `${values.accountsPayablePaymentId || ''}`.trim();
  const accountsPayablePaid =
    Boolean(accountsPayableInvoiceId) &&
    (values.isPaid === true || `${values.isPaid}`.toLowerCase() === 'true');
  const otherAcquisitionServices = React.useMemo(
    () => parseOtherAcquisitionServices(values.otherAcquisitionServicesJson),
    [values.otherAcquisitionServicesJson]
  );
  const otherAcquisitionServicesTotal = React.useMemo(
    () => sumOtherAcquisitionServices(otherAcquisitionServices),
    [otherAcquisitionServices]
  );
  const otherAccountsPayableInvoiceId =
    `${values.otherAccountsPayableInvoiceId || ''}`.trim();
  const otherAccountsPayablePaymentId =
    `${values.otherAccountsPayablePaymentId || ''}`.trim();
  const otherAccountsPayablePaid =
    Boolean(otherAccountsPayableInvoiceId) &&
    (values.otherCostsPaid === true ||
      `${values.otherCostsPaid}`.toLowerCase() === 'true');
  const isAccountsPayableWorkspace =
    (stage.workspaceKind === 'cadastral-survey' &&
      values.surveyorSource === 'External') ||
    stage.workspaceKind === 'vendor-payment' ||
    stage.workspaceKind === 'stamp-duty-payment';
  const isAgreementWorkspace =
    stage.workspaceKind === 'agreement-negotiation' ||
    stage.workspaceKind === 'agreement-approval';
  const agreementDmsReference = `${values.agreementDmsReference || values.generatedAgreementReference || ''}`.trim();
  const agreementAlreadyGenerated =
    Boolean(agreementDmsReference) || Boolean(generatedAgreement);
  const accountsPayableSubject =
    stage.workspaceKind === 'cadastral-survey'
      ? 'external surveyor fee'
      : stage.workspaceKind === 'vendor-payment'
      ? 'vendor payment'
      : 'stamp duty payment';
  const isPublishedAssetWorkspace =
    stage.workspaceKind === 'asset-creation' && item.status === 'Approved';
  const workspaceCanEdit = canEdit && !isPublishedAssetWorkspace;
  const workspaceLockedMessage = isPublishedAssetWorkspace
    ? 'This land has already been transferred to Estate Land Bank, so the workspace is read-only.'
    : assignmentMessage;

  React.useEffect(() => {
    setGeneratedAgreement(null);
    setExistingAgreementRecord(null);
    setSelectedTemplateCode('');
    setPayableEvidenceView(null);
  }, [item.id, open, stage.id]);

  React.useEffect(() => {
    if (
      open &&
      stage.workspaceKind === 'cadastral-survey' &&
      typeof values.mainPortion !== 'boolean'
    ) {
      onChange((current) => ({ ...current, mainPortion: true }));
    }
  }, [onChange, open, stage.workspaceKind, values.mainPortion]);

  const refreshAccountsPayableStatus = async () => {
    if (!item.id) return;
    try {
      setSyncingPayable(true);
      const workspace = await estateAcquisitionService.getWorkspace(
        item.id,
        stage.id
      );
      onChange(workspace.values);
      await onReload();
      toast.success(
        workspace.values.isPaid === true
          ? 'Accounts Payable payment synchronized.'
          : 'Accounts Payable status refreshed.'
      );
    } catch (error: any) {
      toast.error(
        error?.message || 'Unable to refresh Accounts Payable status.'
      );
    } finally {
      setSyncingPayable(false);
    }
  };

  const createAccountsPayableRequest = async () => {
    if (!item.id || !workspaceCanEdit) return;
    try {
      setSyncingPayable(true);
      await estateAcquisitionService.saveWorkspace({
        acquisitionId: item.id,
        procedureId: stage.id,
        workspaceKind: stage.workspaceKind,
        values,
      });
      const workspace =
        await estateAcquisitionService.ensureAccountsPayableRequest(item.id);
      onChange(workspace.values);
      await onReload();
      toast.success('Accounts Payable request created.');
    } catch (error: any) {
      toast.error(
        error?.message || 'Unable to create the Accounts Payable request.'
      );
    } finally {
      setSyncingPayable(false);
    }
  };

  const updateOtherAcquisitionServices = (
    services: OtherAcquisitionServiceCost[]
  ) => {
    onChange((current) => ({
      ...current,
      otherAcquisitionServicesJson: serializeOtherAcquisitionServices(services),
      otherAcquisitionCost: `${sumOtherAcquisitionServices(services) || ''}`,
    }));
  };

  const createOtherAcquisitionCostsPayableRequest = async () => {
    if (!item.id || !workspaceCanEdit) return;
    const payableServices = otherAcquisitionServices.filter(
      (service) =>
        service.serviceName.trim() &&
        Number.isFinite(Number(service.amount)) &&
        Number(service.amount) > 0
    );
    if (payableServices.length === 0) {
      toast.error('Add at least one other acquisition service with an amount.');
      return;
    }

    try {
      setSyncingPayable(true);
      await estateAcquisitionService.saveWorkspace({
        acquisitionId: item.id,
        procedureId: stage.id,
        workspaceKind: stage.workspaceKind,
        values: {
          ...values,
          otherAcquisitionServicesJson:
            serializeOtherAcquisitionServices(payableServices),
          otherAcquisitionCost: `${sumOtherAcquisitionServices(payableServices)}`,
        },
      });
      const workspace =
        await estateAcquisitionService.ensureOtherAcquisitionCostsPayableRequest(
          item.id
        );
      onChange(workspace.values);
      await onReload();
      toast.success('Other acquisition costs AP request created.');
    } catch (error: any) {
      toast.error(
        error?.message ||
          'Unable to create the other acquisition costs AP request.'
      );
    } finally {
      setSyncingPayable(false);
    }
  };

  React.useEffect(() => {
    if (!open || !isAgreementWorkspace) {
      return;
    }

    let mounted = true;
    const loadTemplates = async () => {
      try {
        const templates =
          await documentManagementService.getGenerationTemplates('Estate');
        if (!mounted) return;
        const agreementTemplates = templates.filter((template) => {
          const text =
            `${template.templateCode} ${template.title} ${template.documentType} ${template.body}`.toLowerCase();
          return (
            template.templateCode === LAND_ACQUISITION_AGREEMENT_TEMPLATE_CODE ||
            text.includes('agreement') ||
            text.includes('conveyance') ||
            text.includes('assignment') ||
            text.includes('lease') ||
            text.includes('acquisition')
          );
        });
        const available = agreementTemplates.length
          ? agreementTemplates
          : templates;
        setGenerationTemplates(available);
        const preferredTemplate =
          available.find(
            (template) =>
              template.templateCode === LAND_ACQUISITION_AGREEMENT_TEMPLATE_CODE
          ) ?? available[0];
        setSelectedTemplateCode(
          (current) => current || preferredTemplate?.templateCode || ''
        );
      } catch {
        setGenerationTemplates([]);
      }
    };

    void loadTemplates();
    return () => {
      mounted = false;
    };
  }, [isAgreementWorkspace, open]);

  React.useEffect(() => {
    if (!open || !isAgreementWorkspace || !agreementDmsReference) {
      setExistingAgreementRecord(null);
      return;
    }

    if (
      generatedAgreement?.record.documentReference === agreementDmsReference
    ) {
      setExistingAgreementRecord(generatedAgreement.record);
      return;
    }

    let mounted = true;
    const loadAgreementRecord = async () => {
      try {
        setLoadingAgreementRecord(true);
        const record =
          await documentManagementService.getRecordByDocumentReference(
            agreementDmsReference,
            'Estate'
          );
        if (mounted) setExistingAgreementRecord(record);
      } catch (error) {
        console.error(error);
        if (mounted) setExistingAgreementRecord(null);
      } finally {
        if (mounted) setLoadingAgreementRecord(false);
      }
    };

    void loadAgreementRecord();
    return () => {
      mounted = false;
    };
  }, [
    agreementDmsReference,
    generatedAgreement?.record,
    isAgreementWorkspace,
    open,
  ]);

  const openAgreementRecord = async () => {
    const record = generatedAgreement?.record ?? existingAgreementRecord;
    if (record?.id) {
      router.push(`/document-management/records/${record.id}`);
      return;
    }

    if (!agreementDmsReference) {
      toast.error('No generated agreement reference was found.');
      return;
    }

    try {
      setLoadingAgreementRecord(true);
      const resolved = await documentManagementService.getRecordByDocumentReference(
        agreementDmsReference,
        'Estate'
      );
      if (!resolved?.id) {
        toast.error('The generated agreement could not be found in Central DMS.');
        return;
      }
      setExistingAgreementRecord(resolved);
      router.push(`/document-management/records/${resolved.id}`);
    } catch (error: any) {
      toast.error(error?.message || 'Unable to open the generated agreement.');
    } finally {
      setLoadingAgreementRecord(false);
    }
  };

  const generateAgreement = async () => {
    if (!item.id || !selectedTemplateCode || !workspaceCanEdit) {
      return;
    }

    if (agreementAlreadyGenerated) {
      toast.error('This agreement has already been generated.', {
        description: 'Open the existing DMS agreement instead.',
      });
      return;
    }

    try {
      setGeneratingAgreement(true);
      const result =
        await documentManagementService.generateDocumentFromTemplate({
          templateCode: selectedTemplateCode,
          sourceModule: 'Estate',
          sourceLabel: 'Land acquisition agreement',
          sourceEntityType: 'LandAcquisition',
          sourceRecordReference: item.projectReference,
          sourceRecordId: item.id,
          caseTitle: `${item.projectReference} - ${item.location}`,
          caseReference: item.projectReference,
          applicantName:
            `${values.grantorName || values.ownerName || item.ownerName || ''}` ||
            item.projectReference,
          purpose: 'Land acquisition agreement generation',
          mergeValues: {
            ...Object.fromEntries(
              Object.entries(values).map(([key, value]) => [
                key,
                `${value ?? ''}`,
              ])
            ),
            CaseReference: item.projectReference,
            Location: item.location,
            IntendedUse: item.intendedUse,
            EstimatedSize: item.estimatedSize,
            OwnerName: item.ownerName || '',
            GrantorName: `${values.grantorName || values.ownerName || item.ownerName || ''}`,
            GrantorAddress: `${values.grantorAddress || values.address || ''}`,
            GranteeName: `${values.granteeName || ''}`,
            GranteeAddress: `${values.granteeAddress || ''}`,
            NegotiatedValue: `${values.negotiatedValue || values.offerAmount || ''}`,
            PaymentType: `${values.agreementPaymentType || values.paymentType || ''}`,
            PaymentAmount: `${values.agreementPaymentAmount || values.negotiatedValue || ''}`,
            PaymentSchedule: `${values.paymentSchedule || ''}`,
            SpecialConditions: `${values.specialConditions || ''}`,
            RootOfTitle: `${values.rootOfTitle || ''}`,
            AgreementDate: `${values.agreementDate || ''}`,
            Witness1Name: `${values.agreementWitness1Name || ''}`,
            Witness1Address: `${values.agreementWitness1Address || ''}`,
            Witness2Name: `${values.agreementWitness2Name || ''}`,
            Witness2Address: `${values.agreementWitness2Address || ''}`,
          },
        });
      const nextValues: WorkspaceValues = {
        ...values,
        agreementGenerated: true,
        agreementTemplateReference: selectedTemplateCode,
        generatedAgreementReference: result.dmsReference,
        agreementDmsReference: result.dmsReference,
      };
      onChange(nextValues);
      await estateAcquisitionService.saveWorkspace({
        acquisitionId: item.id,
        procedureId: stage.id,
        workspaceKind: stage.workspaceKind,
        values: nextValues,
      });
      setGeneratedAgreement(result);
      setExistingAgreementRecord(result.record);
      await onReload();
      toast.success('Agreement generated.', {
        description: result.dmsReference,
      });
    } catch (error: any) {
      toast.error(error?.message || 'Unable to generate the agreement.');
    } finally {
      setGeneratingAgreement(false);
    }
  };

  const openAccountsPayable = () => {
    if (accountsPayablePaymentId) {
      router.push(`/finance/ap/payments/${accountsPayablePaymentId}`);
      return;
    }

    if (!accountsPayableInvoiceId || !accountsPayableSupplierId) return;
    const params = new URLSearchParams({
      businessPartnerId: accountsPayableSupplierId,
      invoiceId: accountsPayableInvoiceId,
      amount: `${values.amountDue || ''}`,
      referenceNumber: item.projectReference,
      description: `${accountsPayableSubject} for ${item.projectReference}`,
      source: 'land-acquisition',
      locked: 'true',
    });
    router.push(`/finance/ap/payments/create?${params.toString()}`);
  };

  const openOtherAcquisitionCostsAccountsPayable = () => {
    if (otherAccountsPayablePaymentId) {
      router.push(`/finance/ap/payments/${otherAccountsPayablePaymentId}`);
      return;
    }

    if (!otherAccountsPayableInvoiceId) return;
    const params = new URLSearchParams({
      invoiceId: otherAccountsPayableInvoiceId,
      amount: `${values.otherAmountDue || otherAcquisitionServicesTotal || ''}`,
      referenceNumber: item.projectReference,
      description: `other acquisition costs for ${item.projectReference}`,
      source: 'land-acquisition',
      locked: 'true',
    });
    router.push(`/finance/ap/payments/create?${params.toString()}`);
  };

  React.useEffect(() => {
    if (
      !open ||
      ![
        'parcel-identification',
        'cadastral-survey',
        'ownership-classification',
      ].includes(
        stage.workspaceKind
      )
    ) {
      return;
    }

    let cancelled = false;
    const loadVendors = async () => {
      setVendorsLoading(true);
      setVendorsError(null);
      try {
        const partners =
          await businessPartnerService.getAllPartnersForDropdown();
        if (cancelled) return;
        const activeApproved = partners.filter(isActiveApprovedBusinessPartner);
        const payablePartners = activeApproved.filter(isPayableBusinessPartner);
        setVendors(
          (payablePartners.length ? payablePartners : activeApproved).sort(
            (left, right) => left.partnerName.localeCompare(right.partnerName)
          )
        );
      } catch (error) {
        if (!cancelled) {
          setVendorsError(
            error instanceof Error ? error.message : 'Unable to load vendors.'
          );
        }
      } finally {
        if (!cancelled) setVendorsLoading(false);
      }
    };

    loadVendors();
    return () => {
      cancelled = true;
    };
  }, [open, stage.workspaceKind]);

  React.useEffect(() => {
    if (
      !open ||
      !['cadastral-survey', 'ownership-classification'].includes(
        stage.workspaceKind
      )
    ) {
      return;
    }

    let cancelled = false;
    const loadHrLocations = async () => {
      setHrLocationsLoading(true);
      setHrLocationsError(null);
      try {
        const locations = await estateAcquisitionService.getActiveHrLocations();
        if (!cancelled) setHrLocations(locations);
      } catch (error) {
        if (!cancelled) {
          setHrLocationsError(
            error instanceof Error
              ? error.message
              : 'Unable to load HR locations.'
          );
        }
      } finally {
        if (!cancelled) setHrLocationsLoading(false);
      }
    };

    void loadHrLocations();
    return () => {
      cancelled = true;
    };
  }, [open, stage.workspaceKind]);

  React.useEffect(() => {
    setPendingDocuments([]);
    setPreview((current) => {
      if (current?.url) URL.revokeObjectURL(current.url);
      return null;
    });
    if (!open || !item.id) {
      setDocuments([]);
      return;
    }

    let mounted = true;
    (async () => {
      try {
        setLoadingDocuments(true);
        const result = await estateAcquisitionService.getDocuments(item.id);
        if (mounted)
          setDocuments(
            result.filter((document) => document.procedureId === stage.id)
          );
      } catch (error) {
        console.error(error);
        if (mounted) toast.error('Unable to load stage documents.');
      } finally {
        if (mounted) setLoadingDocuments(false);
      }
    })();

    return () => {
      mounted = false;
    };
  }, [item.id, open, stage.id]);

  React.useEffect(() => {
    setCadastralComparisonValues({});
    setClassificationComparisonValues({});

    if (
      !open ||
      !item.id ||
      ![
        'cadastral-verification',
        'ownership-classification',
        'ownership-verification',
      ].includes(stage.workspaceKind)
    ) {
      return;
    }

    let mounted = true;
    (async () => {
      try {
        const [cadastral, classification] = await Promise.all([
          estateAcquisitionService.getWorkspace(item.id, 2),
          stage.workspaceKind === 'ownership-verification'
            ? estateAcquisitionService.getWorkspace(item.id, 4)
            : Promise.resolve(null),
        ]);

        if (!mounted) return;
        setCadastralComparisonValues(cadastral.values);
        if (classification)
          setClassificationComparisonValues(classification.values);
      } catch (error) {
        console.error(error);
        if (mounted) {
          toast.error('Unable to load coordinate comparison data.');
        }
      }
    })();

    return () => {
      mounted = false;
    };
  }, [item.id, open, stage.workspaceKind]);

  React.useEffect(
    () => () => {
      if (preview?.url) URL.revokeObjectURL(preview.url);
    },
    [preview?.url]
  );

  const openDocument = async (
    document: LandAcquisitionDocument,
    view: boolean
  ) => {
    if (!item.id) return;
    try {
      const blob = view
        ? await estateAcquisitionService.viewDocumentPdf(item.id, document.id)
        : await estateAcquisitionService.downloadDocument(item.id, document.id);
      const url = URL.createObjectURL(blob);

      if (view) {
        setPreview((current) => {
          if (current?.url) URL.revokeObjectURL(current.url);
          return {
            url,
            name: document.fileName.toLowerCase().endsWith('.pdf')
              ? document.fileName
              : `${document.fileName.replace(/\.[^.]+$/, '')}.pdf`,
          };
        });
      } else {
        const link = window.document.createElement('a');
        link.href = url;
        link.download = document.fileName;
        window.document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
      }
    } catch (error: any) {
      toast.error(error?.message || 'Unable to open acquisition document.');
    }
  };

  const attachDocument = async (
    pendingDocument: PendingAcquisitionDocument
  ) => {
    if (!workspaceCanEdit) {
      toast.error(workspaceLockedMessage);
      return;
    }

    if (!item.id) {
      setPendingDocuments((current) => [
        ...current.filter(
          (document) =>
            !pendingDocument.requirementId ||
            document.requirementId !== pendingDocument.requirementId
        ),
        pendingDocument,
      ]);
      return;
    }

    try {
      setUploadingDocuments(true);
      const uploaded = await estateAcquisitionService.uploadDocument(
        item.id,
        stage.id,
        pendingDocument.file,
        pendingDocument.documentType,
        pendingDocument.documentName
      );
      setDocuments((current) => [
        uploaded,
        ...current.filter(
          (document) =>
            pendingDocument.requirementId == null ||
            !matchesRequirement(document, {
              id: pendingDocument.requirementId,
              requirementKey: pendingDocument.requirementId,
              documentName: pendingDocument.documentName,
              documentType: pendingDocument.documentType,
              isRequired: true,
            })
        ),
      ]);
      toast.success('Document uploaded.');
    } catch (error: any) {
      console.error(error);
      toast.error(error?.message || 'Unable to upload document.');
    } finally {
      setUploadingDocuments(false);
    }
  };

  const hasRequirements = documentRequirements.length > 0;
  const unclassifiedDocuments = hasRequirements
    ? documents.filter(
        (document) =>
          !documentRequirements.some((requirement) =>
            matchesRequirement(document, requirement)
          )
      )
    : documents;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-h-[90vh] max-w-5xl overflow-hidden p-0"
        onKeyDownCapture={(event) => {
          if (isEditableElement(event.target)) event.stopPropagation();
        }}
      >
        <DialogHeader className="border-b px-6 py-4">
          <DialogTitle className="flex items-center gap-2">
            <FolderOpen className="h-5 w-5" />
            {stage.title} - {item.projectReference}
          </DialogTitle>
        </DialogHeader>
        <ScrollArea className="max-h-[68vh] px-6 py-5">
          <div className="space-y-5">
            {!workspaceCanEdit && (
              <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                <p className="font-medium">Workspace locked</p>
                <p className="mt-1 text-xs">{workspaceLockedMessage}</p>
              </div>
            )}
            {missingInputs.length > 0 && (
              <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/70 dark:bg-amber-950/40 dark:text-amber-200">
                <p className="font-medium">
                  {missingInputs.length} required input
                  {missingInputs.length === 1 ? '' : 's'} remaining
                </p>
                <p className="mt-1 text-xs">
                  You can save this draft, but workflow actions remain locked
                  until every input is completed.
                </p>
              </div>
            )}
            {stage.workspaceKind === 'cadastral-survey' && (
              <CadastralMapPanel values={values} onChange={onChange} />
            )}
            {stage.workspaceKind === 'cadastral-verification' && (
              <CadastralMapPanel
                values={cadastralComparisonValues}
                readOnly
                title="Cadastral Survey Boundary"
                description="Review the saved beacon coordinates captured during the Cadastral Survey stage."
              />
            )}
            {stage.workspaceKind === 'ownership-classification' && (
              <CoordinateComparisonPanel
                cadastralValues={cadastralComparisonValues}
                classificationValues={values}
              />
            )}
            {stage.workspaceKind === 'ownership-verification' && (
              <>
                <CadastralMapPanel
                  values={cadastralComparisonValues}
                  comparisonValues={classificationComparisonValues}
                  comparisonLabel="Ownership classification"
                  readOnly
                  title="Ownership Cadastral Verification Map"
                  description="View the saved cadastral survey boundary against the ownership classification boundary before approval."
                />
                <CoordinateComparisonPanel
                  cadastralValues={cadastralComparisonValues}
                  classificationValues={classificationComparisonValues}
                />
              </>
            )}
            {stage.workspaceKind === 'ownership-classification' && (
              <PastOwnersPanel
                owners={parsePastOwners(values.pastOwnersJson)}
                disabled={!workspaceCanEdit}
                onChange={(owners) =>
                  setValue('pastOwnersJson', serializePastOwners(owners))
                }
              />
            )}
            {isAccountsPayableWorkspace && (
              <section className="rounded-lg border bg-card p-4">
                <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="text-sm font-semibold text-foreground">
                        Accounts Payable request
                      </h3>
                      <Badge
                        variant={accountsPayablePaid ? 'secondary' : 'outline'}
                      >
                        {accountsPayablePaid
                          ? 'Paid'
                          : accountsPayableInvoiceId
                            ? `${values.accountsPayablePaymentStatus || 'Pending payment'}`
                            : 'Not linked'}
                      </Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {accountsPayableInvoiceId
                        ? `Invoice ${values.accountsPayableInvoiceNumber || accountsPayableInvoiceId} is the payment source for this acquisition.`
                        : stage.workspaceKind === 'cadastral-survey'
                          ? 'Create the payable from the external surveyor and fee amount before routing for survey verification.'
                          : stage.workspaceKind === 'vendor-payment'
                          ? 'Create the payable from the approved agreement amount before processing vendor payment.'
                          : 'Create the payable from the approved stamp duty assessment before processing payment.'}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {accountsPayableInvoiceId && (
                      <SupplierInvoiceWorkspaceButton invoiceId={accountsPayableInvoiceId} />
                    )}
                    {accountsPayableInvoiceId && (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => setPayableEvidenceView('invoice')}
                      >
                        <Eye className="mr-2 h-4 w-4" />
                        View Invoice
                      </Button>
                    )}
                    {accountsPayablePaymentId && (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => setPayableEvidenceView('receipt')}
                      >
                        <Receipt className="mr-2 h-4 w-4" />
                        View Receipt
                      </Button>
                    )}
                    {accountsPayableInvoiceId ? (
                      (Boolean(accountsPayablePaymentId) || ['Approved', 'PartiallyPaid', 'Overdue'].includes(String(values.accountsPayableInvoiceStatus))) &&
                      <Button
                        type="button"
                        size="sm"
                        onClick={openAccountsPayable}
                        disabled={syncingPayable}
                      >
                        <ExternalLink className="mr-2 h-4 w-4" />
                        {accountsPayablePaymentId
                          ? 'View AP Payment'
                          : 'Process in Accounts Payable'}
                      </Button>
                    ) : (
                      <Button
                        type="button"
                        size="sm"
                        onClick={() => void createAccountsPayableRequest()}
                        disabled={syncingPayable || !workspaceCanEdit}
                        title={
                          !workspaceCanEdit ? workspaceLockedMessage : undefined
                        }
                      >
                        {syncingPayable ? (
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <Plus className="mr-2 h-4 w-4" />
                        )}
                        Create AP Request
                      </Button>
                    )}
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() => void refreshAccountsPayableStatus()}
                      disabled={syncingPayable}
                    >
                      {syncingPayable ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <RefreshCw className="mr-2 h-4 w-4" />
                      )}
                      Refresh Status
                    </Button>
                  </div>
                </div>
              </section>
            )}
            {isAgreementWorkspace && (
              <section className="rounded-lg border bg-card p-4">
                <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="text-sm font-semibold text-foreground">
                        Agreement generation
                      </h3>
                      {agreementDmsReference ? (
                        <Badge variant="secondary">
                          {agreementDmsReference}
                        </Badge>
                      ) : (
                        <Badge variant="outline">Not generated</Badge>
                      )}
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Generate the acquisition agreement from a Central DMS Word
                      template, then route it for approval and internal
                      signature through workflow.
                    </p>
                  </div>
                  <div className="flex flex-col gap-2 sm:flex-row">
                    <Select
                      value={selectedTemplateCode || undefined}
                      onValueChange={setSelectedTemplateCode}
                      disabled={
                        generationTemplates.length === 0 ||
                        generatingAgreement ||
                        agreementAlreadyGenerated ||
                        !workspaceCanEdit
                      }
                    >
                      <SelectTrigger className="w-full sm:w-[260px]">
                        <SelectValue placeholder="Select DMS template" />
                      </SelectTrigger>
                      <SelectContent>
                        {generationTemplates.map((template) => (
                          <SelectItem
                            key={template.templateCode}
                            value={template.templateCode}
                          >
                            {template.title}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <Button
                      type="button"
                      onClick={() => void generateAgreement()}
                      disabled={
                        generatingAgreement ||
                        agreementAlreadyGenerated ||
                        generationTemplates.length === 0 ||
                        !selectedTemplateCode ||
                        !workspaceCanEdit
                      }
                      title={
                        agreementAlreadyGenerated
                          ? 'This agreement has already been generated.'
                          : !workspaceCanEdit
                            ? workspaceLockedMessage
                            : undefined
                      }
                    >
                      {generatingAgreement ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <FileSignature className="mr-2 h-4 w-4" />
                      )}
                      {agreementAlreadyGenerated ? 'Generated' : 'Generate'}
                    </Button>
                    {agreementAlreadyGenerated ? (
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => void openAgreementRecord()}
                        disabled={loadingAgreementRecord}
                      >
                        {loadingAgreementRecord ? (
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <Eye className="mr-2 h-4 w-4" />
                        )}
                        View Agreement
                      </Button>
                    ) : null}
                  </div>
                </div>
                {generationTemplates.length === 0 ? (
                  <p className="mt-3 rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                    No Estate agreement templates are active in Central DMS.
                  </p>
                ) : null}
                {agreementAlreadyGenerated ? (
                  <div className="mt-3 flex flex-wrap items-center gap-2 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
                    <span>
                      Generated DMS reference{' '}
                      <span className="font-semibold">
                        {agreementDmsReference || generatedAgreement?.dmsReference}
                      </span>
                    </span>
                    <Button
                      type="button"
                      variant="link"
                      size="sm"
                      className="h-auto px-0 text-emerald-900"
                      onClick={() => void openAgreementRecord()}
                      disabled={loadingAgreementRecord}
                    >
                      <Eye className="mr-1 h-3.5 w-3.5" />
                      View in DMS
                    </Button>
                    {generatedAgreement?.wordUrl ? (
                      <Button
                        asChild
                        variant="link"
                        size="sm"
                        className="h-auto px-0 text-emerald-900"
                      >
                        <a
                          href={generatedAgreement.wordUrl}
                          target="_blank"
                          rel="noreferrer"
                        >
                          <ExternalLink className="mr-1 h-3.5 w-3.5" />
                          Download Word
                        </a>
                      </Button>
                    ) : generatedAgreement?.pdfUrl ? (
                      <Button
                        asChild
                        variant="link"
                        size="sm"
                        className="h-auto px-0 text-emerald-900"
                      >
                        <a
                          href={generatedAgreement.pdfUrl}
                          target="_blank"
                          rel="noreferrer"
                        >
                          <ExternalLink className="mr-1 h-3.5 w-3.5" />
                          Open PDF
                        </a>
                      </Button>
                    ) : null}
                  </div>
                ) : null}
              </section>
            )}
            {stage.workspaceKind === 'stamp-duty-payment' && (
              <section className="rounded-lg border bg-card p-4">
                <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="text-sm font-semibold text-foreground">
                        Other acquisition services
                      </h3>
                      <Badge
                        variant={
                          otherAccountsPayablePaid ? 'secondary' : 'outline'
                        }
                      >
                        {otherAccountsPayablePaid
                          ? 'Paid'
                          : otherAccountsPayableInvoiceId
                            ? `${values.otherAccountsPayablePaymentStatus || 'Pending payment'}`
                            : 'Not linked'}
                      </Badge>
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Add legal, search, consent, facilitation, documentation,
                      or other service costs that must be capitalized into Land
                      Creation.
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {otherAccountsPayableInvoiceId && <SupplierInvoiceWorkspaceButton invoiceId={otherAccountsPayableInvoiceId} />}
                    {otherAccountsPayableInvoiceId ? (
                      (Boolean(otherAccountsPayablePaymentId) || ['Approved', 'PartiallyPaid', 'Overdue'].includes(String(values.otherAccountsPayableInvoiceStatus))) &&
                      <Button
                        type="button"
                        size="sm"
                        onClick={openOtherAcquisitionCostsAccountsPayable}
                        disabled={syncingPayable}
                      >
                        <ExternalLink className="mr-2 h-4 w-4" />
                        {otherAccountsPayablePaymentId
                          ? 'View Other Cost Payment'
                          : 'Process Other Costs in AP'}
                      </Button>
                    ) : (
                      <Button
                        type="button"
                        size="sm"
                        onClick={() =>
                          void createOtherAcquisitionCostsPayableRequest()
                        }
                        disabled={
                          syncingPayable ||
                          !workspaceCanEdit ||
                          otherAcquisitionServicesTotal <= 0
                        }
                        title={
                          !workspaceCanEdit
                            ? workspaceLockedMessage
                            : otherAcquisitionServicesTotal <= 0
                              ? 'Add at least one service cost first.'
                              : undefined
                        }
                      >
                        {syncingPayable ? (
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        ) : (
                          <Plus className="mr-2 h-4 w-4" />
                        )}
                        Create Other Cost AP
                      </Button>
                    )}
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      onClick={() =>
                        updateOtherAcquisitionServices([
                          ...otherAcquisitionServices,
                          emptyOtherAcquisitionService(),
                        ])
                      }
                      disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                      title={
                        otherAccountsPayableInvoiceId
                          ? 'Other cost AP request already exists.'
                          : !workspaceCanEdit
                            ? workspaceLockedMessage
                            : undefined
                      }
                    >
                      <Plus className="mr-2 h-4 w-4" />
                      Add Service
                    </Button>
                  </div>
                </div>
                <div className="mt-4 space-y-3">
                  {otherAcquisitionServices.length === 0 && (
                    <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                      No other acquisition services added yet.
                    </p>
                  )}
                  {otherAcquisitionServices.map((service, index) => (
                    <div
                      key={service.id}
                      className="grid gap-3 rounded-md border p-3 md:grid-cols-[1.3fr_1fr_140px_150px_auto]"
                    >
                      <div className="space-y-2">
                        <Label>Service / Cost</Label>
                        <Input
                          value={service.serviceName}
                          disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                          placeholder="e.g. Title search, consent processing"
                          onChange={(event) =>
                            updateOtherAcquisitionServices(
                              otherAcquisitionServices.map((item, row) =>
                                row === index
                                  ? { ...item, serviceName: event.target.value }
                                  : item
                              )
                            )
                          }
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Payee / Provider</Label>
                        <Input
                          value={service.payeeName}
                          disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                          placeholder="Optional"
                          onChange={(event) =>
                            updateOtherAcquisitionServices(
                              otherAcquisitionServices.map((item, row) =>
                                row === index
                                  ? { ...item, payeeName: event.target.value }
                                  : item
                              )
                            )
                          }
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Amount</Label>
                        <Input
                          type="number"
                          min="0"
                          step="0.01"
                          value={service.amount}
                          disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                          onChange={(event) =>
                            updateOtherAcquisitionServices(
                              otherAcquisitionServices.map((item, row) =>
                                row === index
                                  ? { ...item, amount: event.target.value }
                                  : item
                              )
                            )
                          }
                        />
                      </div>
                      <div className="space-y-2">
                        <Label>Due Date</Label>
                        <Input
                          type="date"
                          value={service.dueDate}
                          disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                          onChange={(event) =>
                            updateOtherAcquisitionServices(
                              otherAcquisitionServices.map((item, row) =>
                                row === index
                                  ? { ...item, dueDate: event.target.value }
                                  : item
                              )
                            )
                          }
                        />
                      </div>
                      <div className="flex items-end">
                        <Button
                          type="button"
                          size="icon"
                          variant="ghost"
                          disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                          onClick={() =>
                            updateOtherAcquisitionServices(
                              otherAcquisitionServices.filter(
                                (_, row) => row !== index
                              )
                            )
                          }
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                      <div className="space-y-2 md:col-span-5">
                        <Label>Notes</Label>
                        <Textarea
                          value={service.notes}
                          disabled={!workspaceCanEdit || Boolean(otherAccountsPayableInvoiceId)}
                          rows={2}
                          onChange={(event) =>
                            updateOtherAcquisitionServices(
                              otherAcquisitionServices.map((item, row) =>
                                row === index
                                  ? { ...item, notes: event.target.value }
                                  : item
                              )
                            )
                          }
                        />
                      </div>
                    </div>
                  ))}
                </div>
                <div className="mt-4 grid gap-3 rounded-md bg-muted/40 p-3 text-sm md:grid-cols-3">
                  <div>
                    <p className="text-xs text-muted-foreground">Service total</p>
                    <p className="font-semibold">
                      GHS {otherAcquisitionServicesTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">AP invoice</p>
                    <p className="font-medium">
                      {values.otherAccountsPayableInvoiceNumber ||
                        otherAccountsPayableInvoiceId ||
                        'Not created'}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">AP payment</p>
                    <p className="font-medium">
                      {values.otherAccountsPayablePaymentNumber ||
                        values.otherAccountsPayablePaymentStatus ||
                        'Pending'}
                    </p>
                  </div>
                </div>
                {values.otherPaymentNotes && (
                  <p className="mt-3 text-xs text-muted-foreground">
                    {values.otherPaymentNotes}
                  </p>
                )}
              </section>
            )}
            <section className="rounded-lg border bg-card p-4">
              <div className="mb-4 flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-foreground">
                    Stage documents
                  </h3>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Attach the configured documents required for this procedure
                    stage.
                  </p>
                </div>
                {!hasRequirements &&
                  (workspaceCanEdit ? (
                    <Button variant="outline" size="sm" asChild>
                      <label>
                        <Upload className="mr-2 h-4 w-4" />
                        Add Files
                        <input
                          className="sr-only"
                          type="file"
                          multiple
                          accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt"
                          onChange={(event) => {
                            const files = Array.from(event.target.files || []);
                            void Promise.all(
                              files.map((file) =>
                                attachDocument({
                                  file,
                                  documentType: stage.title,
                                  documentName: file.name,
                                })
                              )
                            );
                            event.currentTarget.value = '';
                          }}
                        />
                      </label>
                    </Button>
                  ) : (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled
                      title={workspaceLockedMessage}
                    >
                      <Upload className="mr-2 h-4 w-4" />
                      Add Files
                    </Button>
                  ))}
              </div>
              {documentRequirementsError && (
                <div className="mb-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                  {documentRequirementsError}
                </div>
              )}
              <div className="space-y-2">
                {loadingDocuments && (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Loading documents
                  </div>
                )}
                {!documentRequirementsError &&
                  !loadingDocuments &&
                  !hasRequirements &&
                  unclassifiedDocuments.length === 0 &&
                  pendingDocuments.length === 0 && (
                    <p className="text-sm text-muted-foreground">
                      No documents attached for this stage.
                    </p>
                  )}
                {hasRequirements &&
                  documentRequirements.map((requirement) => {
                    const attached = documents.filter((document) =>
                      matchesRequirement(document, requirement)
                    );
                    const pending = pendingDocuments.filter(
                      (document) => document.requirementId === requirement.id
                    );
                    const complete = attached.length > 0 || pending.length > 0;

                    return (
                      <div
                        key={requirement.id}
                        className="rounded-md border px-3 py-3 text-sm"
                      >
                        <div className="flex flex-wrap items-start justify-between gap-3">
                          <div className="min-w-0">
                            <div className="flex items-center gap-2">
                              <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                              <p className="font-medium">
                                {requirement.documentName}
                                {requirement.isRequired && (
                                  <span className="text-destructive"> *</span>
                                )}
                              </p>
                            </div>
                            <p className="mt-1 text-xs text-muted-foreground">
                              {requirement.documentType || 'Document'}
                            </p>
                          </div>
                          <div className="flex items-center gap-2">
                            <Badge variant={complete ? 'secondary' : 'outline'}>
                              {complete ? 'Attached' : 'Pending'}
                            </Badge>
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={uploadingDocuments || !workspaceCanEdit}
                              title={
                                !workspaceCanEdit
                                  ? workspaceLockedMessage
                                  : undefined
                              }
                              asChild={workspaceCanEdit}
                            >
                              {workspaceCanEdit ? (
                                <label>
                                  <Upload className="mr-2 h-4 w-4" />
                                  Upload
                                  <input
                                    className="sr-only"
                                    type="file"
                                    accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt"
                                    onChange={(event) => {
                                      const file = event.target.files?.[0];
                                      if (!file) return;
                                      void attachDocument({
                                        file,
                                        requirementId: requirement.id,
                                        documentType:
                                          requirement.documentType ||
                                          stage.title,
                                        documentName: requirement.documentName,
                                      });
                                      event.currentTarget.value = '';
                                    }}
                                  />
                                </label>
                              ) : (
                                <>
                                  <Upload className="mr-2 h-4 w-4" />
                                  Upload
                                </>
                              )}
                            </Button>
                          </div>
                        </div>
                        {[...attached, ...pending].length > 0 && (
                          <div className="mt-3 space-y-2">
                            {attached.map((document) => (
                              <div
                                key={document.id}
                                className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/20 px-3 py-2"
                              >
                                <div className="min-w-0">
                                  <p className="truncate font-medium">
                                    {document.fileName}
                                  </p>
                                  <p className="text-xs text-muted-foreground">
                                    {document.uploadedBy || 'System'}
                                  </p>
                                </div>
                                <div className="flex flex-wrap gap-1">
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="ghost"
                                    onClick={() =>
                                      void openDocument(document, true)
                                    }
                                  >
                                    <Eye className="mr-1 h-4 w-4" />
                                    View
                                  </Button>
                                  <Button
                                    type="button"
                                    size="sm"
                                    variant="ghost"
                                    onClick={() =>
                                      void openDocument(document, false)
                                    }
                                  >
                                    <Download className="mr-1 h-4 w-4" />
                                    Download
                                  </Button>
                                </div>
                              </div>
                            ))}
                            {pending.map((document) => (
                              <div
                                key={`${document.file.name}-${requirement.id}`}
                                className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-dashed px-3 py-2"
                              >
                                <div className="min-w-0">
                                  <p className="truncate font-medium">
                                    {document.file.name}
                                  </p>
                                  <p className="text-xs text-muted-foreground">
                                    Ready to save
                                  </p>
                                </div>
                                <Button
                                  type="button"
                                  variant="ghost"
                                  disabled={!workspaceCanEdit}
                                  title={
                                    !workspaceCanEdit
                                      ? workspaceLockedMessage
                                      : undefined
                                  }
                                  onClick={() =>
                                    setPendingDocuments((current) =>
                                      current.filter(
                                        (item) =>
                                          item.requirementId !== requirement.id
                                      )
                                    )
                                  }
                                >
                                  <Ban className="mr-1 h-4 w-4" />
                                  Remove
                                </Button>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                {unclassifiedDocuments.map((document) => (
                  <div
                    key={document.id}
                    className="flex flex-wrap items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm"
                  >
                    <div className="flex min-w-0 items-center gap-2">
                      <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
                      <div className="min-w-0">
                        <p className="truncate font-medium">
                          {document.documentName || document.fileName}
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {document.documentType} ·{' '}
                          {document.uploadedBy || 'System'}
                        </p>
                      </div>
                    </div>
                    <div className="flex flex-wrap gap-1">
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        onClick={() => void openDocument(document, true)}
                      >
                        <Eye className="mr-1 h-4 w-4" />
                        View
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        onClick={() => void openDocument(document, false)}
                      >
                        <Download className="mr-1 h-4 w-4" />
                        Download
                      </Button>
                    </div>
                  </div>
                ))}
                {pendingDocuments
                  .filter(
                    (document) => !hasRequirements || !document.requirementId
                  )
                  .map((document, index) => (
                    <div
                      key={`${document.file.name}-${index}`}
                      className="grid gap-2 rounded-md border border-dashed p-3 md:grid-cols-[1fr_180px_auto]"
                    >
                      <Input
                        value={document.documentName}
                        onChange={(event) =>
                          setPendingDocuments((current) =>
                            current.map((item, row) =>
                              row === index
                                ? { ...item, documentName: event.target.value }
                                : item
                            )
                          )
                        }
                      />
                      <Input
                        value={document.documentType}
                        onChange={(event) =>
                          setPendingDocuments((current) =>
                            current.map((item, row) =>
                              row === index
                                ? { ...item, documentType: event.target.value }
                                : item
                            )
                          )
                        }
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        disabled={!workspaceCanEdit}
                        title={
                          !workspaceCanEdit ? workspaceLockedMessage : undefined
                        }
                        onClick={() =>
                          setPendingDocuments((current) =>
                            current.filter((_, row) => row !== index)
                          )
                        }
                      >
                        <Ban className="mr-1 h-4 w-4" />
                        Remove
                      </Button>
                    </div>
                  ))}
              </div>
            </section>
            {sections.map((section) => (
              <section
                key={section.title}
                className="rounded-lg border bg-card p-4"
              >
                <div className="mb-4">
                  <h3 className="text-sm font-semibold text-foreground">
                    {section.title}
                  </h3>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {section.description}
                  </p>
                </div>
                <div className="grid gap-4 md:grid-cols-2">
                  {section.fields
                    .filter(
                      (config) =>
                        !hideOtherWitnessOathField(
                          stage.workspaceKind,
                          config.key,
                          values
                        ) &&
                        !(
                          stage.workspaceKind === 'cadastral-survey' &&
                          CADASTRAL_EXTERNAL_SURVEYOR_FIELDS.has(
                            config.key
                          ) &&
                          values.surveyorSource !== 'External'
                        ) &&
                        !(
                          stage.workspaceKind === 'ownership-classification' &&
                          config.key === 'dateGapReason' &&
                          !hasOwnershipDateGap(values)
                        )
                    )
                    .map((config) =>
                      config.key === 'vendorName' &&
                      stage.workspaceKind === 'parcel-identification' ? (
                        <div key={config.key} className="space-y-2">
                          <Label>
                            Vendor / Owner{' '}
                            <span className="text-destructive">*</span>
                          </Label>
                          <Select
                            value={`${values.vendorId || ''}`}
                            disabled={vendorsLoading || !workspaceCanEdit}
                            onValueChange={(vendorId) => {
                              const vendor = vendors.find(
                                (candidate) => candidate.id === vendorId
                              );
                              onChange((current) => ({
                                ...current,
                                vendorId,
                                vendorName: vendor?.partnerName || '',
                              }));
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue
                                placeholder={
                                  vendorsLoading
                                    ? 'Loading vendors...'
                                    : 'Select a captured vendor'
                                }
                              />
                            </SelectTrigger>
                            <SelectContent>
                              {vendors.map((vendor) => (
                                <SelectItem key={vendor.id} value={vendor.id}>
                                  {vendor.partnerName} ({vendor.partnerCode})
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          {vendorsError && (
                            <p className="text-xs text-destructive">
                              {vendorsError}
                            </p>
                          )}
                          {!vendorsLoading &&
                            !vendorsError &&
                            vendors.length === 0 && (
                              <p className="text-xs text-muted-foreground">
                                No active approved vendors are available in the
                                business partner register.
                              </p>
                            )}
                        </div>
                      ) : config.key === 'surveyorBusinessPartnerId' &&
                        stage.workspaceKind === 'cadastral-survey' ? (
                        <div key={config.key} className="space-y-2">
                          <Label>
                            External Surveyor / Vendor{' '}
                            <span className="text-destructive">*</span>
                          </Label>
                          <Select
                            value={`${values.surveyorBusinessPartnerId || ''}`}
                            disabled={vendorsLoading || !workspaceCanEdit}
                            onValueChange={(surveyorBusinessPartnerId) => {
                              const vendor = vendors.find(
                                (candidate) =>
                                  candidate.id === surveyorBusinessPartnerId
                              );
                              onChange((current) => ({
                                ...current,
                                surveyorBusinessPartnerId,
                                surveyorName: vendor?.partnerName || '',
                              }));
                            }}
                          >
                            <SelectTrigger>
                              <SelectValue
                                placeholder={
                                  vendorsLoading
                                    ? 'Loading vendors...'
                                    : 'Select external surveyor'
                                }
                              />
                            </SelectTrigger>
                            <SelectContent>
                              {vendors.map((vendor) => (
                                <SelectItem key={vendor.id} value={vendor.id}>
                                  {vendor.partnerName} ({vendor.partnerCode})
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          {vendorsError && (
                            <p className="text-xs text-destructive">
                              {vendorsError}
                            </p>
                          )}
                          {!vendorsLoading &&
                            !vendorsError &&
                            vendors.length === 0 && (
                              <p className="text-xs text-muted-foreground">
                                No active approved supplier or contractor
                                Business Partners are available.
                              </p>
                            )}
                        </div>
                      ) : config.key === 'ownerName' &&
                        stage.workspaceKind === 'ownership-classification' ? (
                        <div key={config.key} className="space-y-2">
                          <Label>
                            {values.isCurrentOwner === true
                              ? 'Business Partner / Current Owner'
                              : 'Past Owner Name'}{' '}
                            <span className="text-destructive">*</span>
                          </Label>
                          {values.isCurrentOwner === true ? (
                            <>
                              <Select
                                value={`${values.vendorId || ''}`}
                                disabled={vendorsLoading || !workspaceCanEdit}
                                onValueChange={(vendorId) => {
                                  const vendor = vendors.find(
                                    (candidate) => candidate.id === vendorId
                                  );
                                  onChange((current) => ({
                                    ...current,
                                    vendorId,
                                    vendorName: vendor?.partnerName || '',
                                    ownerName: vendor?.partnerName || '',
                                    contactNumber:
                                      vendor?.phone ||
                                      `${current.contactNumber || ''}`,
                                    address:
                                      vendor?.physicalAddress ||
                                      `${current.address || ''}`,
                                    identificationType: '',
                                    identificationNumber: '',
                                    ownershipEndDate: '',
                                  }));
                                }}
                              >
                                <SelectTrigger>
                                  <SelectValue
                                    placeholder={
                                      vendorsLoading
                                        ? 'Loading business partners...'
                                        : 'Select business partner'
                                    }
                                  />
                                </SelectTrigger>
                                <SelectContent>
                                  {vendors.map((vendor) => (
                                    <SelectItem
                                      key={vendor.id}
                                      value={vendor.id}
                                    >
                                      {vendor.partnerName} ({vendor.partnerCode}
                                      )
                                    </SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                              {vendorsError && (
                                <p className="text-xs text-destructive">
                                  {vendorsError}
                                </p>
                              )}
                              {!vendorsLoading &&
                                !vendorsError &&
                                vendors.length === 0 && (
                                  <p className="text-xs text-muted-foreground">
                                    No active approved business partners are
                                    available for current-owner classification.
                                  </p>
                                )}
                              {`${values.vendorName || values.ownerName || ''}`.trim() && (
                                <p className="text-xs text-muted-foreground">
                                  Owner details and identity are sourced from
                                  the selected Business Partner and are not
                                  required again in this stage.
                                </p>
                              )}
                            </>
                          ) : values.isCurrentOwner === false ? (
                            <Input
                              value={`${values.ownerName || ''}`}
                              placeholder="Enter the past owner's full name"
                              disabled={!workspaceCanEdit}
                              onChange={(event) =>
                                setValue('ownerName', event.target.value)
                              }
                            />
                          ) : (
                            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                              Select whether this is the current owner first.
                            </p>
                          )}
                        </div>
                      ) : stage.workspaceKind === 'ownership-classification' &&
                        values.isCurrentOwner === true &&
                        BUSINESS_PARTNER_CURRENT_OWNER_FIELDS.has(
                          config.key
                        ) ? (
                        <div
                          key={config.key}
                          className={
                            config.span === 2
                              ? 'space-y-2 md:col-span-2'
                              : 'space-y-2'
                          }
                        >
                          <Label>{config.label}</Label>
                          <div className="rounded-md border bg-muted px-3 py-2 text-sm text-muted-foreground">
                            {config.key === 'ownershipEndDate'
                              ? 'Not applicable for current owner'
                              : 'Sourced from Business Partner record'}
                          </div>
                        </div>
                      ) : stage.workspaceKind === 'ownership-classification' &&
                        config.key === 'dateGapReason' ? (
                        <div
                          key={config.key}
                          className="space-y-2 md:col-span-2"
                        >
                          <Label>
                            Date Gap Reason{' '}
                            <span className="text-destructive">*</span>
                          </Label>
                          <Textarea
                            value={`${values.dateGapReason || ''}`}
                            disabled={!workspaceCanEdit}
                            onChange={(event) =>
                              setValue('dateGapReason', event.target.value)
                            }
                            rows={4}
                          />
                          <p className="text-xs text-muted-foreground">
                            Required because the current ownership starts after
                            the most recent past ownership ended.
                          </p>
                        </div>
                      ) : config.type === 'region' ||
                        config.type === 'district' ? (
                        <HrLocationControl
                          key={config.key}
                          config={config}
                          locations={hrLocations}
                          values={values}
                          loading={hrLocationsLoading}
                          error={hrLocationsError}
                          disabled={Boolean(
                            !workspaceCanEdit || config.readOnly
                          )}
                          onChange={onChange}
                        />
                      ) : (
                        <WorkspaceControl
                          key={config.key}
                          config={config}
                          value={values[config.key]}
                          disabled={!workspaceCanEdit || config.readOnly}
                          onChange={(value) => {
                            if (!workspaceCanEdit || config.readOnly) return;
                            if (
                              config.key === 'witnessSwornOath1' ||
                              config.key === 'witnessSwornOath2'
                            ) {
                              const witnessNumber = config.key.endsWith('1')
                                ? 1
                                : 2;
                              const otherWitnessNumber =
                                witnessNumber === 1 ? 2 : 1;
                              onChange((current) => ({
                                ...current,
                                [config.key]: value,
                                ...(value === true
                                  ? {
                                      [`witnessSwornOath${otherWitnessNumber}`]: false,
                                      [`witnessOathSwornBefore${otherWitnessNumber}`]:
                                        '',
                                      [`witnessOathSwornDate${otherWitnessNumber}`]:
                                        '',
                                    }
                                  : {}),
                              }));
                              return;
                            }
                            if (config.key === 'isCurrentOwner') {
                              const currentOwner = value === true;
                              onChange((current) => ({
                                ...current,
                                isCurrentOwner: currentOwner,
                                ownerName: currentOwner
                                  ? `${current.vendorName || ''}`
                                  : '',
                                vendorId: currentOwner
                                  ? `${current.vendorId || ''}`
                                  : '',
                                vendorName: currentOwner
                                  ? `${current.vendorName || ''}`
                                  : '',
                                contactNumber: currentOwner
                                  ? `${current.contactNumber || ''}`
                                  : '',
                                address: currentOwner
                                  ? `${current.address || ''}`
                                  : '',
                                identificationType: currentOwner
                                  ? ''
                                  : `${current.identificationType || ''}`,
                                identificationNumber: currentOwner
                                  ? ''
                                  : `${current.identificationNumber || ''}`,
                                ownershipEndDate: currentOwner
                                  ? ''
                                  : `${current.ownershipEndDate || ''}`,
                              }));
                              return;
                            }
                            if (
                              stage.workspaceKind === 'cadastral-survey' &&
                              config.key === 'surveyorSource'
                            ) {
                              onChange((current) => ({
                                ...current,
                                surveyorSource: value,
                                ...(value === 'Internal'
                                  ? {
                                      surveyorBusinessPartnerId: '',
                                      surveyorFeeAmount: '',
                                      surveyorFeeDueDate: '',
                                    }
                                  : {}),
                              }));
                              return;
                            }
                            setValue(config.key, value);
                          }}
                        />
                      )
                    )}
                </div>
              </section>
            ))}
          </div>
        </ScrollArea>

        <Dialog
          open={Boolean(preview)}
          onOpenChange={(open) => !open && setPreview(null)}
        >
          <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{preview?.name}</DialogTitle>
            </DialogHeader>
            {preview ? (
              <ProcedurePdfViewer
                fileUrl={preview.url}
                fileName={preview.name}
              />
            ) : null}
          </DialogContent>
        </Dialog>

        {payableEvidenceView && (
          <AccountsPayableEvidenceDialog
            open={Boolean(payableEvidenceView)}
            onOpenChange={(open) => !open && setPayableEvidenceView(null)}
            values={values}
            subject={accountsPayableSubject}
            view={payableEvidenceView}
          />
        )}

        <DialogFooter className="border-t px-6 py-4">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            <Ban className="mr-2 h-4 w-4" />
            Close
          </Button>
          {stage.workspaceKind === 'stamp-duty-payment' ? (
            <Button
              onClick={() => void refreshAccountsPayableStatus()}
              disabled={syncingPayable}
            >
              {syncingPayable ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Refresh Payment Status
            </Button>
          ) : (
            <Button
              onClick={() => onSave(pendingDocuments)}
              disabled={saving || !workspaceCanEdit}
              title={!workspaceCanEdit ? workspaceLockedMessage : undefined}
            >
              {saving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Save className="mr-2 h-4 w-4" />
              )}
              Save Workspace
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function HrLocationControl({
  config,
  locations,
  values,
  loading,
  error,
  disabled,
  onChange,
}: {
  config: WorkspaceField;
  locations: HrLocationLookup[];
  values: WorkspaceValues;
  loading: boolean;
  error: string | null;
  disabled: boolean;
  onChange: React.Dispatch<React.SetStateAction<WorkspaceValues>>;
}) {
  const isDistrict = config.type === 'district';
  const regionKey = config.key.startsWith('owner')
    ? 'ownerRegionId'
    : 'regionId';
  const currentValue = `${values[config.key] || ''}`;
  const selectedRegionValue = `${values[regionKey] || ''}`;
  const matchesValue = (location: HrLocationLookup, value: string) =>
    location.id === value ||
    location.name.localeCompare(value, undefined, { sensitivity: 'accent' }) ===
      0;
  const levelName = (location: HrLocationLookup) =>
    `${location.levelName || ''}`.trim().toLowerCase();
  const selectedRegion = locations.find((location) =>
    matchesValue(location, selectedRegionValue)
  );
  const selectedLocation = locations.find((location) =>
    matchesValue(location, currentValue)
  );
  const options = locations.filter((location) => {
    const level = levelName(location);
    if (!isDistrict) return level === 'region' || level.endsWith(' region');
    if (!(level === 'district' || level.endsWith(' district'))) return false;
    return selectedRegion
      ? location.parentLocationId === selectedRegion.id
      : false;
  });

  return (
    <div className="space-y-2">
      <Label>
        {config.label} {requiredMarker(config)}
      </Label>
      <Select
        value={selectedLocation?.id || ''}
        disabled={disabled || loading || (isDistrict && !selectedRegion)}
        onValueChange={(locationId) => {
          onChange((current) => {
            const next = { ...current, [config.key]: locationId };
            if (!isDistrict) {
              const districtKey = config.key.startsWith('owner')
                ? 'ownerDistrictId'
                : 'districtId';
              next[districtKey] = '';
            }
            return next;
          });
        }}
      >
        <SelectTrigger>
          <SelectValue
            placeholder={
              loading
                ? `Loading ${config.label.toLowerCase()}s...`
                : isDistrict && !selectedRegion
                  ? 'Select a region first'
                  : `Select ${config.label.toLowerCase()}`
            }
          />
        </SelectTrigger>
        <SelectContent>
          {options.map((location) => (
            <SelectItem key={location.id} value={location.id}>
              {location.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && <p className="text-xs text-destructive">{error}</p>}
      {!loading && !error && options.length === 0 && (
        <p className="text-xs text-muted-foreground">
          {isDistrict && !selectedRegion
            ? 'Select an HR Region to load its Districts.'
            : `No active HR ${config.label} locations are configured.`}
        </p>
      )}
    </div>
  );
}

function WorkspaceControl({
  config,
  value,
  disabled = false,
  onChange,
}: {
  config: WorkspaceField;
  value: string | boolean | undefined;
  disabled?: boolean;
  onChange: (value: string | boolean) => void;
}) {
  const className = config.span === 2 ? 'space-y-2 md:col-span-2' : 'space-y-2';

  if (config.type === 'check') {
    return (
      <label className="flex items-center gap-3 rounded-md border bg-card p-3 text-sm font-medium text-foreground">
        <div className="min-w-0 flex-1">
          <Label>
            {config.label} {requiredMarker(config)}
          </Label>
        </div>
        <Select
          disabled={disabled}
          value={typeof value === 'boolean' ? `${value}` : ''}
          onValueChange={(next) => onChange(next === 'true')}
        >
          <SelectTrigger className="w-28">
            <SelectValue placeholder="Select" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="true">Yes</SelectItem>
            <SelectItem value="false">No</SelectItem>
          </SelectContent>
        </Select>
      </label>
    );
  }

  if (config.type === 'textarea') {
    return (
      <div className={className}>
        <Label>
          {config.label} {requiredMarker(config)}
        </Label>
        <Textarea
          value={`${value || ''}`}
          disabled={disabled}
          onChange={(event) => onChange(event.target.value)}
          rows={4}
        />
      </div>
    );
  }

  if (config.type === 'select') {
    return (
      <div className={className}>
        <Label>
          {config.label} {requiredMarker(config)}
        </Label>
        <Select
          value={`${value || ''}`}
          disabled={disabled}
          onValueChange={onChange}
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(config.options || []).map((option) => (
              <SelectItem key={option} value={option}>
                {option}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    );
  }

  return (
    <div className={className}>
      <Label>
        {config.label} {requiredMarker(config)}
      </Label>
      <Input
        type={config.type === 'date' ? 'date' : 'text'}
        value={`${value || ''}`}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}
