'use client';

import React from 'react';
import dynamic from 'next/dynamic';
import { useRouter } from 'next/navigation';
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
  ShieldCheck,
  Stamp,
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
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  ACQUISITION_STAGES,
  estateAcquisitionService,
  type AcquisitionWorkspaceKind,
  type LandAcquisitionDocument,
  type LandAcquisitionItem,
  type LandAcquisitionSummary,
  type LandAcquisitionStage,
} from '@/services/estate-acquisition.service';

const CadastralMapPanel = dynamic(() => import('./CadastralMapPanel'), {
  ssr: false,
});

type FieldType = 'text' | 'date' | 'textarea' | 'select' | 'check';

interface WorkspaceField {
  key: string;
  label: string;
  type: FieldType;
  options?: string[];
  span?: 1 | 2;
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

const field = (
  key: string,
  label: string,
  type: FieldType = 'text',
  options?: string[],
  span: 1 | 2 = 1
): WorkspaceField => ({
  key,
  label,
  type,
  options,
  span,
});

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
  ],
  'suitability-approval': [
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
    field('assessmentRecommendation', 'Assessment Recommendation', 'select', [
      'Proceed to Cadastral Survey',
      'Return for More Information',
      'Reject Site',
    ]),
    field(
      'approvalNotes',
      'Physical Assessment Notes',
      'textarea',
      undefined,
      2
    ),
  ],
  'cadastral-survey': [
    field('cadastreDescription', 'Cadastre Description'),
    field('regionId', 'Region'),
    field('districtId', 'District'),
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
    field('surveyorName', 'Surveyor Name'),
    field('licensedSurveyor', 'Licensed Surveyor'),
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
  ],
  'cadastral-verification': [
    field('cadastralMatch', 'Cadastral Match', 'check'),
    field('cadastralMatchVerified', 'Cadastral Match Verified', 'check'),
    field('overlapCleared', 'Overlap Cleared', 'check'),
    field('boundaryConfirmed', 'Boundary Confirmed', 'check'),
    field('verificationReference', 'Verification Reference'),
    field('verificationOfficer', 'Verification Officer'),
    field('verificationDate', 'Verification Date', 'date'),
    field('verificationNotes', 'Verification Notes', 'textarea', undefined, 2),
  ],
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
    field('percentage', 'Ownership Percentage'),
    field('isCurrentOwner', 'Current Owner', 'check'),
    field('identificationType', 'Identification Type', 'select', [
      'Ghana Card',
      'Passport',
      'Voter ID',
      'Driver License',
      'Other',
    ]),
    field('identificationNumber', 'Identification Number'),
    field('interestHeld', 'Interest Held'),
    field('classificationRisk', 'Classification Risk', 'select', [
      'Low',
      'Medium',
      'High',
    ]),
    field('dateGapReason', 'Date Gap Reason', 'textarea', undefined, 2),
    field('ownerRegionId', 'Ownership Region'),
    field('ownerDistrictId', 'Ownership District'),
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
  ],
  'agreement-approval': [
    field('agreementDate', 'Agreement Date', 'date'),
    field('isFamilyLand', 'Family Land', 'check'),
    field('isStoolLand', 'Stool Land', 'check'),
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
    field('documentName', 'Payment Document Name'),
    field('receiptNumber', 'Receipt Number'),
    field('paymentReference', 'Payment Reference'),
    field('paymentDate', 'Payment Date', 'date'),
    field('amountPaid', 'Amount Paid'),
    field('paymentMethod', 'Payment Method', 'select', [
      'Bank Transfer',
      'Cheque',
      'Cash',
      'Mobile Money',
    ]),
    field('isPaid', 'Paid', 'check'),
    field('paymentNotes', 'Payment Notes', 'textarea', undefined, 2),
  ],
  registration: [
    field('registryOffice', 'Registry Office'),
    field('registrationNumber', 'Registration Number'),
    field('volume', 'Volume'),
    field('folio', 'Folio'),
    field('registrationDate', 'Registration Date', 'date'),
    field('isRegistered', 'Registered', 'check'),
    field('documentName', 'Registration Document Name'),
    field('registrationNotes', 'Registration Notes', 'textarea', undefined, 2),
  ],
  'asset-creation': [
    field('assetCode', 'Asset Code'),
    field('assetNumber', 'Asset Number'),
    field('parcelIdentifier', 'Parcel Identifier'),
    field('registrationNumber', 'Registration Number'),
    field('ownerName', 'Owner Name'),
    field('assetLocation', 'Asset Location'),
    field('assetCategory', 'Asset Category', 'select', [
      'Land',
      'Land Improvement',
      'Investment Property',
    ]),
    field('size', 'Size'),
    field('sizeUnit', 'Size Unit', 'select', ['Acres', 'Hectares', 'sqm']),
    field('assetStatus', 'Asset Status', 'select', [
      'Draft',
      'Active',
      'Under Dispute',
      'Locked',
    ]),
    field('purpose', 'Purpose'),
    field('zoningClassification', 'Zoning Classification'),
    field('ownershipVerification', 'Ownership Verification'),
    field('capitalizationValue', 'Capitalization Value'),
    field('glAccount', 'GL Account'),
    field('custodian', 'Custodian'),
    field('assetNotes', 'Asset Notes', 'textarea', undefined, 2),
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
  ],
  'suitability-approval': [
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
    {
      title: 'Assessment decision',
      description:
        'State the recommendation and notes that justify the decision.',
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
  ],
  'cadastral-verification': [
    {
      title: 'Verification checks',
      description:
        'Confirm the survey matches the parcel, boundaries are consistent, and overlaps are cleared.',
      keys: [
        'cadastralMatch',
        'cadastralMatchVerified',
        'overlapCleared',
        'boundaryConfirmed',
        'verificationReference',
        'verificationOfficer',
        'verificationDate',
        'verificationNotes',
      ],
    },
  ],
  'ownership-classification': [
    {
      title: 'Owner and tenure',
      description:
        'Classify ownership and record the owner, interest, tenure, dates, and risk.',
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
        'percentage',
        'interestHeld',
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
        'Confirm search, owner identity, and authority to sell before agreement negotiation.',
      keys: [
        'dueDiligenceStatus',
        'titleSearchCompleted',
        'ownerIdentityVerified',
        'authorityToSellVerified',
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
        'Capture agreement date parts, acceptance, generated status, and negotiation notes.',
      keys: [
        'agreementDay',
        'agreementMonth',
        'agreementYear',
        'isAccepted',
        'agreementGenerated',
        'negotiationNotes',
      ],
    },
  ],
  'agreement-approval': [
    {
      title: 'Agreement terms',
      description:
        'Review root of title, parties, payment schedule, special conditions, and witnesses.',
      keys: [
        'agreementDate',
        'isFamilyLand',
        'isStoolLand',
        'rootOfTitle',
        'specialConditions',
        'partyDetails',
        'paymentSchedule',
        'agreementWitnessDetails',
      ],
    },
    {
      title: 'Approval controls',
      description:
        'Capture the parties, payment, witnesses, and approval controls for the agreement.',
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
        'legalReviewComplete',
        'financeReviewComplete',
        'boardApprovalReference',
        'approvalConditions',
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
      title: 'Payment evidence',
      description:
        'Capture payment receipt, reference, date, amount, method, and notes.',
      keys: [
        'documentName',
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
        'Record registry office, registration number, volume, folio, date, and notes.',
      keys: [
        'registryOffice',
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
      title: 'Land asset details',
      description:
        'Create the land asset and prepare it for land bank or project handoff.',
      keys: [
        'assetCode',
        'assetNumber',
        'parcelIdentifier',
        'registrationNumber',
        'ownerName',
        'assetLocation',
        'assetCategory',
        'size',
        'sizeUnit',
        'assetStatus',
      ],
    },
    {
      title: 'Finance and custody',
      description:
        'Capture purpose, zoning, ownership verification, capitalization, GL account, custodian, and notes.',
      keys: [
        'purpose',
        'zoningClassification',
        'ownershipVerification',
        'capitalizationValue',
        'glAccount',
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
  values: Record<string, string | boolean>
) {
  const missing = WORKSPACE_FIELDS[kind].filter((config) => {
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
    const conditional = [...missing];
    if (values.isCurrentOwner === true && !`${values.vendorId ?? ''}`.trim()) {
      conditional.push(field('vendorId', 'Linked Vendor / Owner'));
    }
    if (
      values.isCurrentOwner === false &&
      !`${values.ownershipEndDate ?? ''}`.trim()
    ) {
      conditional.push(field('ownershipEndDate', 'Ownership End Date', 'date'));
    }
    return conditional;
  }

  return missing;
}

function inputLabels(kind: AcquisitionWorkspaceKind, keys: string[]) {
  const labels = new Map(
    WORKSPACE_FIELDS[kind].map((config) => [config.key, config.label])
  );
  labels.set('vendorId', 'Linked Vendor / Owner');
  return keys.map((key) => labels.get(key) || key);
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
  const [publishingReady, setPublishingReady] = React.useState<string | null>(
    null
  );
  const [query, setQuery] = React.useState('');

  const selectedStage =
    stages.find((stage) => stage.id === selectedStageId) || stages[0];
  const allItems = React.useMemo(
    () => stages.flatMap((stage) => stage.items),
    [stages]
  );
  const selectedItem = React.useMemo(() => {
    if (selectedItemId)
      return allItems.find((item) => item.id === selectedItemId) || null;
    return selectedStage?.items[0] || allItems[0] || null;
  }, [allItems, selectedItemId, selectedStage]);

  React.useEffect(() => {
    if (!selectedItem && selectedStage?.items[0])
      setSelectedItemId(selectedStage.items[0].id);
  }, [selectedItem, selectedStage]);

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
  const canCreateAcquisition = stages.some((stage) => stage.order === 0);

  const openWorkspace = async (item: LandAcquisitionItem) => {
    setDraftItem(null);
    const stage =
      stages.find((candidate) => candidate.order === item.stageOrder) ||
      selectedStage;
    if (!stage) return;
    setSelectedStageId(stage.id);
    setSelectedItemId(item.id);
    const defaults = defaultsFor(stage.workspaceKind, item);
    try {
      const workspace = await estateAcquisitionService.getWorkspace(
        item.id,
        stage.id
      );
      setWorkspaceValues({ ...defaults, ...workspace.values });
    } catch (error) {
      console.error(error);
      setWorkspaceValues(defaults);
      toast.error('Unable to load the saved stage entries.');
    }
    setWorkspaceOpen(true);
  };

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
      await reload();
    } catch (error) {
      console.error(error);
      throw error;
    }
  };

  const saveWorkspace = async (
    pendingDocuments: PendingAcquisitionDocument[] = []
  ) => {
    const item = draftItem || selectedItem;
    if (!item || !selectedStage) return;
    try {
      setSavingWorkspace(true);
      const result = await estateAcquisitionService.saveWorkspace({
        acquisitionId: item.id || undefined,
        procedureId: selectedStage.id,
        workspaceKind: selectedStage.workspaceKind,
        values: workspaceValues,
      });
      if (pendingDocuments.length > 0) {
        await Promise.all(
          pendingDocuments.map((document) =>
            estateAcquisitionService.uploadDocument(
              result.acquisitionId,
              selectedStage.id,
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
    } catch (error) {
      console.error(error);
      toast.error('Unable to save workspace.');
    } finally {
      setSavingWorkspace(false);
    }
  };

  const markReadyForProjectManagement = async (item: LandAcquisitionItem) => {
    if (!item.id) return;

    try {
      setPublishingReady(item.id);
      const result =
        await estateAcquisitionService.markReadyForProjectManagement(item.id);
      result.success
        ? toast.success(
            result.message || 'Land is ready for project management.'
          )
        : toast.error(result.message || 'Unable to publish land.');
      await reload();
    } catch (error) {
      console.error(error);
      toast.error(
        'Demarcate the land boundary before marking it ready for project management.'
      );
    } finally {
      setPublishingReady(null);
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
                filteredItems.map((item) => (
                  <AcquisitionCard
                    key={item.id}
                    item={item}
                    active={selectedItem?.id === item.id}
                    onSelect={() => setSelectedItemId(item.id)}
                    onOpen={() => openWorkspace(item)}
                  />
                ))
              )}
            </div>
          </ScrollArea>
        </section>

        <section className="rounded-lg border bg-card text-card-foreground shadow-sm">
          {selectedItem && selectedStage ? (
            <AcquisitionDetail
              item={selectedItem}
              stage={selectedStage}
              onOpenWorkspace={() => openWorkspace(selectedItem)}
              onPrimary={() =>
                runAction(selectedItem, selectedStage, 'primary')
              }
              onReject={(comments) =>
                runAction(selectedItem, selectedStage, 'reject', comments)
              }
              onReload={reload}
              onMarkReady={() => markReadyForProjectManagement(selectedItem)}
              markingReady={publishingReady === selectedItem.id}
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

      {(draftItem || selectedItem) && selectedStage && (
        <WorkspaceDialog
          open={workspaceOpen}
          onOpenChange={(open) => {
            setWorkspaceOpen(open);
            if (!open) setDraftItem(null);
          }}
          item={draftItem || selectedItem!}
          stage={selectedStage}
          values={workspaceValues}
          onChange={setWorkspaceValues}
          onSave={saveWorkspace}
          saving={savingWorkspace}
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
  onOpenWorkspace,
  onPrimary,
  onReject,
  onReload,
  onMarkReady,
  markingReady,
  onOpenWorkflows,
  canManageWorkflows,
}: {
  item: LandAcquisitionItem;
  stage: LandAcquisitionStage;
  onOpenWorkspace: () => void;
  onPrimary: () => Promise<void>;
  onReject: (comments?: string) => Promise<void>;
  onReload: () => Promise<void>;
  onMarkReady: () => Promise<void>;
  markingReady: boolean;
  onOpenWorkflows: () => void;
  canManageWorkflows: boolean;
}) {
  const Icon = stageIcons[stage.workspaceKind];
  const [summaryOpen, setSummaryOpen] = React.useState(false);
  const [summary, setSummary] = React.useState<LandAcquisitionSummary | null>(
    null
  );
  const [summaryLoading, setSummaryLoading] = React.useState(false);
  const missingLabels = inputLabels(
    stage.workspaceKind,
    item.missingInputs || []
  );
  const forwardActionReason = item.stageInputsComplete
    ? undefined
    : `Complete all stage inputs first: ${missingLabels.slice(0, 4).join(', ')}${missingLabels.length > 4 ? ` and ${missingLabels.length - 4} more` : ''}.`;

  const openSummary = async () => {
    setSummaryOpen(true);
    setSummaryLoading(true);
    try {
      setSummary(await estateAcquisitionService.getSummary(item.id));
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
            <Button
              variant="outline"
              onClick={onMarkReady}
              disabled={markingReady || stage.order < 2}
            >
              {markingReady ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Building2 className="mr-2 h-4 w-4" />
              )}
              Ready for Projects
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
              {ACQUISITION_STAGES.map((candidate) => {
                const done = candidate.order < stage.order;
                const active = candidate.order === stage.order;
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
              <CardDescription>{stage.primaryAction}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {!item.stageInputsComplete && (
                <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/70 dark:bg-amber-950/40 dark:text-amber-200">
                  <p className="font-medium">Stage inputs incomplete</p>
                  <p className="mt-1 text-xs">{forwardActionReason}</p>
                </div>
              )}
              <WorkflowApprovalActions
                entityType="LandAcquisition"
                entityId={item.id}
                entityLabel="Land Acquisition"
                entityNumber={item.projectReference}
                status={item.status}
                showStepBadge
                currentStepName={stage.title}
                canSubmit={
                  item.status === 'Draft' ||
                  (item.status === 'Rejected' && item.stageOrder === 0)
                }
                canApproveReject={
                  item.status === 'Pending Approval' ||
                  item.status === 'Submitted'
                }
                forwardActionsDisabled={!item.stageInputsComplete}
                forwardActionsDisabledReason={forwardActionReason}
                onSubmit={onPrimary}
                onApprove={() => onPrimary()}
                onReject={(comments) => onReject(comments)}
                onAfterAction={onReload}
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
                const definition = ACQUISITION_STAGES.find(
                  (candidate) => candidate.id === savedStage.procedureId
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
                const entries = Object.entries(savedStage.values).filter(
                  ([, value]) => value !== null && value !== ''
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
                        {entries.map(([key, value]) => (
                          <Field
                            key={key}
                            label={
                              labels.get(key) ||
                              key.replace(/([a-z])([A-Z])/g, '$1 $2')
                            }
                            value={formatSummaryValue(value)}
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

function WorkspaceDialog({
  open,
  onOpenChange,
  item,
  stage,
  values,
  onChange,
  onSave,
  saving,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  item: LandAcquisitionItem;
  stage: LandAcquisitionStage;
  values: Record<string, string | boolean>;
  onChange: (values: Record<string, string | boolean>) => void;
  onSave: (pendingDocuments?: PendingAcquisitionDocument[]) => Promise<void>;
  saving: boolean;
}) {
  const setValue = (key: string, value: string | boolean) =>
    onChange({ ...values, [key]: value });
  const sections = workspaceSectionsFor(stage.workspaceKind);
  const missingInputs = missingWorkspaceInputs(stage.workspaceKind, values);
  const [documents, setDocuments] = React.useState<LandAcquisitionDocument[]>(
    []
  );
  const [loadingDocuments, setLoadingDocuments] = React.useState(false);
  const [pendingDocuments, setPendingDocuments] = React.useState<
    PendingAcquisitionDocument[]
  >([]);
  const [vendors, setVendors] = React.useState<BusinessPartnerDto[]>([]);
  const [vendorsLoading, setVendorsLoading] = React.useState(false);
  const [vendorsError, setVendorsError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (!open || stage.workspaceKind !== 'parcel-identification') return;

    let cancelled = false;
    const loadVendors = async () => {
      setVendorsLoading(true);
      setVendorsError(null);
      try {
        const partners =
          await businessPartnerService.getAllPartnersForDropdown();
        if (cancelled) return;
        setVendors(
          partners
            .filter((partner) => {
              const status = partner.status?.toLowerCase();
              const isActive = status === 'active' || status === 'approved';
              const isApproved =
                !partner.approvalStatus ||
                partner.approvalStatus.toLowerCase() === 'approved';
              return isActive && isApproved && !partner.isBlacklisted;
            })
            .sort((left, right) =>
              left.partnerName.localeCompare(right.partnerName)
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
    setPendingDocuments([]);
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

  const openDocument = async (document: LandAcquisitionDocument) => {
    if (!item.id) return;
    try {
      const blob = await estateAcquisitionService.downloadDocument(
        item.id,
        document.id
      );
      const url = URL.createObjectURL(blob);
      const link = window.document.createElement('a');
      link.href = url;
      link.download = document.fileName;
      window.document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error: any) {
      toast.error(error?.message || 'Unable to download acquisition document.');
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-5xl overflow-hidden p-0">
        <DialogHeader className="border-b px-6 py-4">
          <DialogTitle className="flex items-center gap-2">
            <FolderOpen className="h-5 w-5" />
            {stage.title} - {item.projectReference}
          </DialogTitle>
        </DialogHeader>
        <ScrollArea className="max-h-[68vh] px-6 py-5">
          <div className="space-y-5">
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
            {(stage.workspaceKind === 'cadastral-survey' ||
              stage.workspaceKind === 'cadastral-verification') && (
              <CadastralMapPanel values={values} onChange={onChange} />
            )}
            <section className="rounded-lg border bg-card p-4">
              <div className="mb-4 flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-foreground">
                    Stage documents
                  </h3>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Attach files that support this procedure stage.
                  </p>
                </div>
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
                        setPendingDocuments((current) => [
                          ...current,
                          ...files.map((file) => ({
                            file,
                            documentType: stage.title,
                            documentName: file.name,
                          })),
                        ]);
                        event.currentTarget.value = '';
                      }}
                    />
                  </label>
                </Button>
              </div>
              <div className="space-y-2">
                {loadingDocuments && (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Loading documents
                  </div>
                )}
                {!loadingDocuments &&
                  documents.length === 0 &&
                  pendingDocuments.length === 0 && (
                    <p className="text-sm text-muted-foreground">
                      No documents attached for this stage.
                    </p>
                  )}
                {documents.map((document) => (
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
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      onClick={() => void openDocument(document)}
                    >
                      <Download className="mr-1 h-4 w-4" />
                      Download
                    </Button>
                  </div>
                ))}
                {pendingDocuments.map((document, index) => (
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
                  {section.fields.map((config) =>
                    config.key === 'vendorName' &&
                    stage.workspaceKind === 'parcel-identification' ? (
                      <div key={config.key} className="space-y-2">
                        <Label>
                          Vendor / Owner{' '}
                          <span className="text-destructive">*</span>
                        </Label>
                        <Select
                          value={`${values.vendorId || ''}`}
                          disabled={vendorsLoading}
                          onValueChange={(vendorId) => {
                            const vendor = vendors.find(
                              (candidate) => candidate.id === vendorId
                            );
                            onChange({
                              ...values,
                              vendorId,
                              vendorName: vendor?.partnerName || '',
                            });
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
                    ) : config.key === 'ownerName' &&
                      stage.workspaceKind === 'ownership-classification' ? (
                      <div key={config.key} className="space-y-2">
                        <Label>
                          {values.isCurrentOwner === true
                            ? 'Linked Current Owner'
                            : 'Past Owner Name'}{' '}
                          <span className="text-destructive">*</span>
                        </Label>
                        {values.isCurrentOwner === true ? (
                          <div className="rounded-md border bg-muted px-3 py-2 text-sm text-foreground">
                            {`${values.vendorName || values.ownerName || ''}`.trim() ||
                              'No vendor was selected during Parcel Identification.'}
                          </div>
                        ) : values.isCurrentOwner === false ? (
                          <Input
                            value={`${values.ownerName || ''}`}
                            placeholder="Enter the past owner's full name"
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
                    ) : (
                      <WorkspaceControl
                        key={config.key}
                        config={config}
                        value={values[config.key]}
                        onChange={(value) => {
                          if (config.key === 'isCurrentOwner') {
                            const currentOwner = value === true;
                            onChange({
                              ...values,
                              isCurrentOwner: currentOwner,
                              ownerName: currentOwner
                                ? `${values.vendorName || ''}`
                                : '',
                              ownershipEndDate: currentOwner
                                ? ''
                                : `${values.ownershipEndDate || ''}`,
                            });
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
        <DialogFooter className="border-t px-6 py-4">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            <Ban className="mr-2 h-4 w-4" />
            Close
          </Button>
          <Button onClick={() => onSave(pendingDocuments)} disabled={saving}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save Workspace
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function WorkspaceControl({
  config,
  value,
  onChange,
}: {
  config: WorkspaceField;
  value: string | boolean | undefined;
  onChange: (value: string | boolean) => void;
}) {
  const className = config.span === 2 ? 'space-y-2 md:col-span-2' : 'space-y-2';

  if (config.type === 'check') {
    return (
      <label className="flex items-center gap-3 rounded-md border bg-card p-3 text-sm font-medium text-foreground">
        <div className="min-w-0 flex-1">
          <Label>
            {config.label} <span className="text-destructive">*</span>
          </Label>
        </div>
        <Select
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
          {config.label} <span className="text-destructive">*</span>
        </Label>
        <Textarea
          value={`${value || ''}`}
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
          {config.label} <span className="text-destructive">*</span>
        </Label>
        <Select value={`${value || ''}`} onValueChange={onChange}>
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
        {config.label} <span className="text-destructive">*</span>
      </Label>
      <Input
        type={config.type === 'date' ? 'date' : 'text'}
        value={`${value || ''}`}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}
