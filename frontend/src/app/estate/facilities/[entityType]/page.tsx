'use client';

import Link from 'next/link';
import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  ArrowRight,
  Briefcase,
  ClipboardCheck,
  CreditCard,
  Database,
  ExternalLink,
  FileText,
  Loader2,
  MessageSquare,
  Wrench,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';
import { FacilitiesProviderRegister } from './FacilitiesProviderRegister';
import { FacilitiesDutyLookup } from './FacilitiesDutyLookup';
import { FacilitiesOperatingRegister } from './FacilitiesOperatingRegister';
import { FacilitiesPropertyInvoices } from './FacilitiesPropertyInvoices';
import {
  EstateManagedAssetType,
  estateLandManagementService,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  estateFacilitiesService,
  type CreateFacilitiesArInvoiceRequest,
  type CreateFacilitiesArPaymentRequest,
  type EstateFacilityDutyRosterItem,
  type FacilitiesPropertyOption,
  type FacilitiesStaffOption,
  type FacilitiesIssueVoucherOption,
  type FacilitiesUnitOption,
  type FacilitiesArInvoice,
  type FacilitiesArPayment,
  type FacilitiesBillingDmsPublication,
  type FacilitiesProcedureWorkspace,
  type FacilitiesWorkspaceField,
  type PublishFacilitiesBillingDocumentRequest,
  type UpsertEstateFacilityDutyRosterRequest,
} from '@/services/estate-facilities.service';

function fieldDisplayValue(field: FacilitiesWorkspaceField) {
  if (field.type === 'select' && field.options && field.options.length > 0) {
    return field.options.join(' / ');
  }

  if (field.type === 'date') {
    return 'Date';
  }

  if (field.type === 'textarea') {
    return 'Long text';
  }

  return 'Text';
}

type OperationalHandoff = {
  title: string;
  description: string;
  sourceLabel: string;
  icon: React.ComponentType<{ className?: string }>;
  primaryAction: {
    label: string;
    href: string;
  };
  secondaryActions: Array<{
    label: string;
    href: string;
  }>;
  checkpoints: string[];
};

const todayInputValue = () => new Date().toISOString().slice(0, 10);

const defaultDutyRosterForm = (): UpsertEstateFacilityDutyRosterRequest => ({
  employeeNumber: '',
  staffName: '',
  staffType: 'Cleaner',
  dutyType: 'Cleaning',
  propertyReference: '',
  propertyUnit: '',
  serviceAreaType: 'Common Area',
  serviceAreaName: '',
  frequency: 'Daily',
  dayPattern: 'Mon-Fri',
  startDate: todayInputValue(),
  endDate: '',
  shiftStart: '08:00',
  shiftEnd: '17:00',
  supervisorName: '',
  toolsIssued: '',
  suppliesIssued: '',
  inventoryIssueVoucherId: null,
  inventoryIssueVoucherNumber: null,
  checklist: 'Sweep / mop; disinfect touch points; empty bins; report defects',
  attendanceStatus: 'Pending',
  completionStatus: 'Scheduled',
  qualityStatus: 'Not inspected',
  linkedMaintenanceReference: '',
  linkedComplaintReference: '',
  linkedProcedureCaseReference: '',
  notes: '',
});

const defaultBillingDmsForm: PublishFacilitiesBillingDocumentRequest = {
  documentTitle: '',
  documentType: 'Invoice',
  billingDocumentKind: 'Invoice',
  facilitiesBillingReference: '',
  financeArReference: '',
  propertyUnit: '',
  customerAccountReference: '',
  repositoryPath: '',
  externalDocumentUrl: '',
  fileName: '',
  contentType: 'application/pdf',
  notes: '',
};

function FacilitiesWorkflowOverview({
  workspace,
}: {
  workspace: FacilitiesProcedureWorkspace;
}) {
  const mandatoryDocuments = workspace.requiredDocuments.filter(
    (document) => document.isMandatory
  ).length;

  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle>{workspace.procedure.title} Workspace</CardTitle>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 md:grid-cols-4">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.stages.length}
            </div>
            <div className="text-sm text-muted-foreground">stages</div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">{mandatoryDocuments}</div>
            <div className="text-sm text-muted-foreground">
              mandatory documents
            </div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.intakeFields.length}
            </div>
            <div className="text-sm text-muted-foreground">intake fields</div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.handoffs.length}
            </div>
            <div className="text-sm text-muted-foreground">handoffs</div>
          </div>
        </div>

        {workspace.stages.length === 0 ? (
          <div className="rounded-md border border-dashed border-border bg-muted/40 p-4">
            <div className="font-medium">Setup required</div>
          </div>
        ) : null}

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.25fr)_minmax(0,0.75fr)]">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Stage board</div>
            <div className="space-y-3">
              {workspace.stages.map((stage, index) => (
                <div
                  key={`${stage.name}-${index}`}
                  className="rounded-md border border-border bg-card p-3"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <div className="font-medium">
                        {index + 1}. {stage.name}
                      </div>
                    </div>
                    <Badge variant="secondary" className="w-fit">
                      {stage.owner}
                    </Badge>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {stage.checklist.slice(0, 4).map((item) => (
                      <Badge key={item} variant="outline">
                        {item}
                      </Badge>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 font-medium">Documents required</div>
              <div className="space-y-2">
                {workspace.requiredDocuments.map((document) => (
                  <div
                    key={document.name}
                    className="rounded-md border border-border bg-card p-3"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="font-medium">{document.name}</div>
                      <Badge
                        variant={document.isMandatory ? 'default' : 'outline'}
                      >
                        {document.isMandatory ? 'Required' : 'Optional'}
                      </Badge>
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      From: {document.requiredFrom}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 font-medium">Intake fields</div>
              <div className="grid gap-2">
                {workspace.intakeFields.map((field) => (
                  <div
                    key={field.key}
                    className="flex items-center justify-between gap-3 rounded-md border border-border bg-card p-3 text-sm"
                  >
                    <span className="font-medium">{field.label}</span>
                    <span className="text-muted-foreground">
                      {fieldDisplayValue(field)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="grid gap-4 lg:grid-cols-2">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Outputs</div>
            <div className="flex flex-wrap gap-2">
              {workspace.outputs.map((output) => (
                <Badge key={output} variant="secondary">
                  {output}
                </Badge>
              ))}
            </div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Handoffs</div>
            <div className="space-y-2">
              {workspace.handoffs.map((handoff) => (
                <div
                  key={`${handoff.fromRole}-${handoff.toRole}-${handoff.trigger}`}
                  className="rounded-md border border-border bg-card p-3 text-sm"
                >
                  <div className="font-medium">
                    {handoff.fromRole} → {handoff.toRole}
                  </div>
                  <div className="mt-1 text-muted-foreground">
                    {handoff.trigger}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function FacilitiesMaintenanceWorkspace({
  workspace,
  operationalHandoff,
  canOpenOperationalHandoff,
}: {
  workspace: FacilitiesProcedureWorkspace;
  operationalHandoff: OperationalHandoff | null;
  canOpenOperationalHandoff: boolean;
}) {
  return (
    <div className="space-y-4">
      {workspace.stages.length === 0 ? (
        <div className="rounded-md border border-dashed border-border bg-muted/40 p-4">
          <div className="font-medium">Setup required</div>
        </div>
      ) : null}

      {operationalHandoff ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Maintenance Handoff</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {!canOpenOperationalHandoff ? (
              <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
                Maintenance access is required for job cards and work orders.
              </div>
            ) : null}
            <Button
              asChild={canOpenOperationalHandoff}
              disabled={!canOpenOperationalHandoff}
              className="w-full"
            >
              {canOpenOperationalHandoff ? (
                <Link href={operationalHandoff.primaryAction.href}>
                  {operationalHandoff.primaryAction.label}
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Link>
              ) : (
                <span>
                  {operationalHandoff.primaryAction.label}
                  <ArrowRight className="ml-2 h-4 w-4" />
                </span>
              )}
            </Button>
            <div className="grid gap-2 sm:grid-cols-2">
              {operationalHandoff.secondaryActions.map((action) => (
                <Button
                  key={action.href}
                  asChild={canOpenOperationalHandoff}
                  disabled={!canOpenOperationalHandoff}
                  variant="outline"
                  size="sm"
                >
                  {canOpenOperationalHandoff ? (
                    <Link href={action.href}>
                      {action.label}
                      <ExternalLink className="ml-2 h-3.5 w-3.5" />
                    </Link>
                  ) : (
                    <span>
                      {action.label}
                      <ExternalLink className="ml-2 h-3.5 w-3.5" />
                    </span>
                  )}
                </Button>
              ))}
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

function dateInputValue(date: Date) {
  return date.toISOString().slice(0, 10);
}

function addDays(date: Date, days: number) {
  const nextDate = new Date(date);
  nextDate.setDate(nextDate.getDate() + days);
  return nextDate;
}

const defaultArInvoiceForm: CreateFacilitiesArInvoiceRequest = {
  customerId: '',
  propertyUnit: '',
  invoiceDate: dateInputValue(new Date()),
  dueDate: dateInputValue(addDays(new Date(), 30)),
  reference: '',
  notes: 'Facilities AR instruction',
  currencyCode: 'GHS',
  exchangeRate: 1,
  lineItems: [
    {
      lineItemType: 'GLAccount',
      description: 'Facilities service charge',
      quantity: 1,
      unitPrice: 0,
      discountPercentage: 0,
    },
  ],
};

const defaultArPaymentForm: CreateFacilitiesArPaymentRequest = {
  customerId: '',
  paymentDate: dateInputValue(new Date()),
  totalAmount: 0,
  paymentMethod: 'Cash',
  currencyCode: 'USD',
  exchangeRate: 1,
  transactionReference: '',
  notes: 'Facilities AR receipt',
  isCreditNote: false,
};

function getOperationalHandoff(entityType: string): OperationalHandoff | null {
  if (entityType === 'EstateFacilityPropertySite') {
    return {
      title: 'Property / Site Operating View Handoff',
      description:
        'Estate / Facilities keeps the operating view for sites, buildings, floors, common areas, occupancy impact, service zones, responsible officers, access, and site operating documents. Property Management remains the register owner for property/unit commercial records, leases, occupants, and availability.',
      sourceLabel:
        'Property Management / Project Handover',
      icon: Database,
      primaryAction: {
        label: 'Open Property & Units',
        href: '/estate/property-management/EstatePropertyManagementPropertyUnit',
      },
      secondaryActions: [
        {
          label: 'Property Dashboard',
          href: '/estate/property-management/dashboard',
        },
        {
          label: 'Occupancy / Availability',
          href: '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
        },
        {
          label: 'Move-in / Handover',
          href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
        },
        { label: 'Central DMS', href: '/document-management' },
      ],
      checkpoints: [
        'Use Property Management as the source for property, unit, lease, occupant, and availability records.',
        'Use Facilities for operational site hierarchy, common areas, service zones, access constraints, site responsibility, and service readiness.',
        'Link Maintenance Intake, Complaint Management, Staff/Cleaner, Service Provider, Asset Operating View, and Document Index records when they affect a site.',
        'Keep site operating documents indexed for Central DMS instead of storing duplicate copies.',
        'Route commercial changes back to Property Management and finance impacts to Finance AR.',
      ],
    };
  }

  if (entityType === 'EstateFacilityLease') {
    return {
      title: 'Lease / Occupancy Coordination Handoff',
      description:
        'Estate / Facilities coordinates viewing readiness, handover access, services, move-in constraints, renewal/termination operational impact, and service-charge triggers. Property Management owns lease setup, tenant/occupant records, availability, and move-in/handover; Finance AR owns invoices and receipts.',
      sourceLabel: 'Property Management',
      icon: FileText,
      primaryAction: {
        label: 'Open Lease Management',
        href: '/estate/property-management/EstatePropertyManagementLease',
      },
      secondaryActions: [
        {
          label: 'Tenant / Occupant Register',
          href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
        },
        {
          label: 'Move-in / Handover',
          href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
        },
        {
          label: 'Property Billing',
          href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
        },
        { label: 'Finance AR', href: '/finance/ar/invoices' },
      ],
      checkpoints: [
        'Use Property Management for agreement, signature, tenant, move-in date, and lease status.',
        'Use Facilities for viewing readiness, keys/access, utilities readiness, common-area services, and operational handover constraints.',
        'Start Facilities service-charge billing instructions only after the lease/occupancy trigger is confirmed by the owning workspace.',
        'Route agreement and handover documents to Central DMS with property and lease metadata.',
        'Send finance execution to Finance AR; Facilities records only the source instruction and result reference.',
      ],
    };
  }

  if (entityType === 'EstateFacilityServiceProvider') {
    return {
      title: 'Service Provider Operations Handoff',
      description:
        'Estate / Facilities manages provider operating profiles, service coverage, assignment readiness, workstream links, SLA monitoring, and performance. Procurement owns supplier onboarding, contracts, rate cards, supplier compliance, and supplier invoice workspaces; Inventory prepares landed-cost supplier invoice drafts from receipts; Finance owns settlement, balances, and postings.',
      sourceLabel: 'Procurement',
      icon: Briefcase,
      primaryAction: {
        label: 'Open Procurement Suppliers',
        href: '/procurement/business-partners',
      },
      secondaryActions: [
        { label: 'Procurement Contracts', href: '/procurement/contracts' },
        { label: 'Purchase Orders', href: '/procurement/purchase-orders' },
        { label: 'Supplier Invoices', href: '/procurement/supplier-invoices' },
      ],
      checkpoints: [
        'Use approved Procurement suppliers or business partners as the provider source.',
        'Link Procurement contract, compliance, insurance, tax, license, and rate references.',
        'Use Facilities for service coverage, assignment readiness, approved workstreams, SLA monitoring, quality, and provider performance.',
        'Link Maintenance Intake, Complaint Management, Asset Operating View, Staff/Cleaner, or Document records when the provider is used.',
        'Open provider supplier invoices from Procurement, landed-cost invoice preparation from Inventory, and settlement evidence from the linked supplier invoice.',
      ],
    };
  }

  if (entityType === 'EstateFacilityStaffCleaner') {
    return {
      title: 'Staff & Cleaner Duty Operations Handoff',
      description:
        'HR / Payroll remains the source for employee records and employment status. Administration owns login accounts and User-Employee Links. Estate / Facilities manages duty areas, rosters, attendance follow-up, tools, supplies, supervision, service quality, linked workstream follow-up, and HR escalations.',
      sourceLabel: 'HR / Payroll',
      icon: ClipboardCheck,
      primaryAction: {
        label: 'Open Employee Profiles',
        href: '/hr/payroll/employee-profiles',
      },
      secondaryActions: [
        {
          label: 'User-Employee Links',
          href: '/administration/user-employee-links',
        },
        {
          label: 'User Management',
          href: '/administration/identity-management/users',
        },
        { label: 'Payroll', href: '/hr/payroll' },
        { label: 'HR Reports', href: '/reports/hr' },
        {
          label: 'Staff Workspace',
          href: '/estate/facilities/EstateFacilityStaffCleaner',
        },
      ],
      checkpoints: [
        'Use HR / Payroll employee profiles as the staff source.',
        'Use Administration User-Employee Links only to connect an existing user account to an employee record.',
        'Use Administration User Management for login accounts, roles, and access.',
        'Use Facilities for duty assignment, roster, site coverage, and supervision.',
        'Record attendance exceptions, tools, supplies, access, and replacement coverage in Facilities.',
        'Route repair issues to Maintenance Intake and complaints to Helpdesk through Facilities Complaint Management.',
        'Escalate employment, payroll, conduct, or staffing actions back to HR.',
      ],
    };
  }

  if (entityType === 'EstateFacilityAssetRegister') {
    return {
      title: 'Facilities Asset Operating View Handoff',
      description:
        'Project Management can hand over delivered assets, Finance Fixed Assets owns the financial register, and Maintenance Management owns job cards, work orders, repairs, and inspections. Estate / Facilities keeps only the operating view for location, custodian, access, service impact, condition, warranty, documents, complaints, providers, and linked maintenance history.',
      sourceLabel:
        'Project Management / Finance Fixed Assets / Maintenance',
      icon: Database,
      primaryAction: {
        label: 'Open Fixed Assets Register',
        href: '/finance/fixed-assets/register',
      },
      secondaryActions: [
        { label: 'Project Handover', href: '/development/projects' },
        {
          label: 'Fixed Assets Dashboard',
          href: '/finance/fixed-assets/dashboard',
        },
        {
          label: 'Asset Verification',
          href: '/finance/fixed-assets/verification',
        },
        { label: 'Maintenance Assets', href: '/maintenance/assets' },
        { label: 'Maintenance Work Orders', href: '/maintenance/work-orders' },
      ],
      checkpoints: [
        'Use Project Management handover, Finance Fixed Assets, or Maintenance asset records as the source record.',
        'Use Facilities for location, custodian, access, condition, warranty, service impact, documents, and inspection follow-up.',
        'Use Maintenance for job cards, work orders, repairs, inspections, and service history execution.',
        'Use Property Management when an asset affects unit block, release, occupancy, common area use, or handover.',
        'Use Finance Fixed Assets for capitalization, depreciation, valuation, transfer, disposal, and retirement.',
      ],
    };
  }

  if (entityType === 'EstateFacilityDocument') {
    return {
      title: 'Facilities Document Index & DMS Readiness Handoff',
      description:
        'Facilities indexes and controls retrieval metadata for source-module documents while Central Document Management owns the repository, versioning, comments, PDF viewer annotations, redlines, retention audit, and full document history. Facilities prepares metadata and readiness; it does not duplicate source documents.',
      sourceLabel: 'Central DMS',
      icon: FileText,
      primaryAction: {
        label: 'Open Property & Facilities Records',
        href: '/estate/property-management/EstatePropertyManagementDocumentRecordIndex',
      },
      secondaryActions: [
        {
          label: 'Central DMS',
          href: '/document-management',
        },
        {
          label: 'DMS Metadata Templates',
          href: '/document-management/CentralDocumentMetadataTemplate',
        },
        {
          label: 'DMS Version Control',
          href: '/document-management/CentralDocumentVersion',
        },
        { label: 'Project Documents', href: '/development/projects' },
        { label: 'Procurement Contracts', href: '/procurement/contracts' },
        { label: 'Maintenance Work Orders', href: '/maintenance/work-orders' },
        { label: 'Finance Reports', href: '/finance/reports' },
        { label: 'Legal', href: '/legal' },
      ],
      checkpoints: [
        'Do not duplicate source-module documents inside Facilities.',
        'Define the module metadata template, source owner, source record, related Facilities entity, access level, retention, expiry, and review dates.',
        'Track comments, versioning, PDF viewer annotation, redline, and audit-readiness requirements for Central DMS.',
        'Use Facilities only for indexing, retrieval metadata, visibility, expiry alerts, and migration readiness.',
        'Return corrections, renewals, or replacements to the owning source module.',
      ],
    };
  }

  if (entityType === 'EstateFacilityBillingServiceCharge') {
    return {
      title: 'Facilities Billing / Service Charge Handoff',
      description:
        'Estate / Facilities prepares billing instruction packages for facilities-origin service charges, common-area recoveries, utilities, provider pass-throughs, deposits, arrears, statements, demand notices, disputes, and adjustments. Finance AR owns customer accounts, invoices, receipts, allocations, balances, statements, deposits, and GL postings.',
      sourceLabel: 'Finance AR',
      icon: CreditCard,
      primaryAction: {
        label: 'Open Property Billing',
        href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
      },
      secondaryActions: [
        {
          label: 'Create AR Invoice',
          href: '/finance/ar/invoices/new?source=estate-facilities',
        },
        { label: 'AR Invoices', href: '/finance/ar/invoices' },
        {
          label: 'Record Receipt',
          href: '/finance/ar/payments/new?source=estate-facilities',
        },
        { label: 'AR Customers', href: '/finance/ar/customers' },
        { label: 'AR Reports', href: '/finance/ar/reports' },
        { label: 'Central DMS', href: '/document-management' },
      ],
      checkpoints: [
        'Include the Facilities reference on every downstream Finance AR instruction.',
        'Use Facilities for service charge setup, allocation basis, service evidence, common-area recovery, provider pass-throughs, arrears follow-up, and dispute explanation.',
        'Use Property Management when the charge affects unit, lease, occupant, availability, handover, or property records.',
        'Use Finance AR for customer accounts, invoices, receipts, allocations, balances, statements, deposits, and GL postings.',
        'Publish invoices, receipts, statements, demand notices, adjustments, approvals, and dispute evidence into Central DMS.',
      ],
    };
  }

  if (entityType === 'EstateFacilityMaintenance') {
    return {
      title: 'Maintenance Intake Handoff',
      description:
        'Estate / Facilities captures requester, property, unit, asset, SLA, safety, service impact, and evidence here, then execution continues in the existing Maintenance Management job card and work order flow.',
      sourceLabel: 'Facilities',
      icon: Wrench,
      primaryAction: {
        label: 'Create Maintenance Job Card',
        href: '/maintenance/job-cards?create=1&source=estate-facilities',
      },
      secondaryActions: [
        { label: 'Open Job Cards', href: '/maintenance/job-cards' },
        { label: 'Open Work Orders', href: '/maintenance/work-orders' },
        { label: 'Maintenance Dashboard', href: '/maintenance/dashboard' },
      ],
      checkpoints: [
        'Include the Facilities reference in the job card or work order notes.',
        'Use the Facilities case reference, property/unit, evidence, priority, SLA, safety, and service-impact context in Maintenance.',
        'Let Maintenance Management own job cards, work orders, technicians, parts, execution, inspection, and technical closure.',
        'Return job card, work order, inspection, cost, and closure references to the Facilities intake.',
        'Close the Facilities case after maintenance inspection, requester feedback, and linked workspace updates are recorded.',
      ],
    };
  }

  if (entityType === 'EstateFacilityComplaint') {
    return {
      title: 'Complaint Management Handoff',
      description:
        'Estate / Facilities captures complainant, property, tenant, evidence, severity, SLA context, and service impact here; Helpdesk owns complaint ticket SLA, escalation, investigation, resolution, reopening, and closure history.',
      sourceLabel: 'Facilities',
      icon: MessageSquare,
      primaryAction: {
        label: 'Create Complaint Ticket',
        href: '/helpdesk/tickets/new?scope=support-internal&source=estate-facilities',
      },
      secondaryActions: [
        {
          label: 'Open Complaint Queue',
          href: '/helpdesk/queue?scope=support-internal',
        },
        {
          label: 'Complaint Dashboard',
          href: '/helpdesk/dashboard?scope=support-internal',
        },
      ],
      checkpoints: [
        'Include the Facilities reference in the complaint ticket.',
        'Use the Facilities case reference, property/unit, complainant, evidence, severity, SLA context, and service impact in Helpdesk.',
        'Let Helpdesk own ticket SLA timers, assignment, escalation, investigation, resolution, reopening, and closure history.',
        'Route linked repair work to Maintenance Intake and linked service issues to Provider, Staff, Asset, Records, or Property Management where required.',
        'Return Helpdesk status, closure notes, complainant feedback, and recurrence flags to the Facilities case before final closeout.',
      ],
    };
  }

  return null;
}

export default function FacilitiesProcedureWorkspacePage() {
  const router = useRouter();
  const { hasAnyPermission } = useAuth();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType)
    ? params.entityType[0]
    : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [workspace, setWorkspace] =
    React.useState<FacilitiesProcedureWorkspace | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [arInvoiceForm, setArInvoiceForm] =
    React.useState<CreateFacilitiesArInvoiceRequest>(defaultArInvoiceForm);
  const [selectedBillingProperty, setSelectedBillingProperty] = React.useState<EstateManagedAsset | null>(null);
  const searchBillingProperties = React.useCallback(async (query: string) =>
    (await estateLandManagementService.getManagedAssets({ search: query, take: 30 }))
      .filter((asset) => asset.assetType !== EstateManagedAssetType.Land && asset.customerBusinessPartnerId),
  []);
  const [arInvoiceResult, setArInvoiceResult] =
    React.useState<FacilitiesArInvoice | null>(null);
  const [arInvoiceError, setArInvoiceError] = React.useState<string | null>(
    null
  );
  const [isCreatingArInvoice, setIsCreatingArInvoice] = React.useState(false);
  const [arPaymentForm, setArPaymentForm] =
    React.useState<CreateFacilitiesArPaymentRequest>(defaultArPaymentForm);
  const [arPaymentResult, setArPaymentResult] =
    React.useState<FacilitiesArPayment | null>(null);
  const [arPaymentError, setArPaymentError] = React.useState<string | null>(
    null
  );
  const [isCreatingArPayment, setIsCreatingArPayment] = React.useState(false);
  const [billingDmsForm, setBillingDmsForm] =
    React.useState<PublishFacilitiesBillingDocumentRequest>(
      defaultBillingDmsForm
    );
  const [billingDmsPublication, setBillingDmsPublication] =
    React.useState<FacilitiesBillingDmsPublication | null>(null);
  const [billingDmsError, setBillingDmsError] = React.useState<string | null>(
    null
  );
  const [isPublishingBillingDms, setIsPublishingBillingDms] =
    React.useState(false);
  const [dutyRosterItems, setDutyRosterItems] = React.useState<
    EstateFacilityDutyRosterItem[]
  >([]);
  const [dutyRosterDate, setDutyRosterDate] = React.useState(todayInputValue);
  const [editingDutyId, setEditingDutyId] = React.useState<string | null>(null);
  const [dutyRosterForm, setDutyRosterForm] =
    React.useState<UpsertEstateFacilityDutyRosterRequest>(
      defaultDutyRosterForm()
    );
  const [selectedDutyStaff, setSelectedDutyStaff] = React.useState<FacilitiesStaffOption | null>(null);
  const [selectedDutyProperty, setSelectedDutyProperty] = React.useState<FacilitiesPropertyOption | null>(null);
  const [selectedDutyUnit, setSelectedDutyUnit] = React.useState<FacilitiesUnitOption | null>(null);
  const [dutyRosterError, setDutyRosterError] = React.useState<string | null>(
    null
  );
  const [isLoadingDutyRoster, setIsLoadingDutyRoster] = React.useState(false);
  const [isSavingDutyRoster, setIsSavingDutyRoster] = React.useState(false);
  const searchDutyUnits = React.useCallback(
    (query: string) => selectedDutyProperty
      ? estateFacilitiesService.searchDutyUnits(selectedDutyProperty.id, query)
      : Promise.resolve([]),
    [selectedDutyProperty]
  );

  const updateDutyRosterForm = (
    key: keyof UpsertEstateFacilityDutyRosterRequest,
    value: string
  ) => {
    setDutyRosterForm((current) => ({
      ...current,
      [key]: value,
    }));
  };

  const loadDutyRoster = async () => {
    setDutyRosterError(null);
    try {
      setIsLoadingDutyRoster(true);
      const items = await estateFacilitiesService.getDutyRoster(dutyRosterDate);
      setDutyRosterItems(items);
    } catch {
      setDutyRosterError('Unable to load Facilities duty roster.');
    } finally {
      setIsLoadingDutyRoster(false);
    }
  };

  const createDutyRosterItem = async (
    event: React.FormEvent<HTMLFormElement>
  ) => {
    event.preventDefault();
    setDutyRosterError(null);

    if (!dutyRosterForm.staffName.trim() || !dutyRosterForm.employeeNumber?.trim()) {
      setDutyRosterError('Select a cleaner or staff member from HR.');
      return;
    }

    if (!dutyRosterForm.propertyReference?.trim()) {
      setDutyRosterError('Select a property or site from Estate.');
      return;
    }

    if (!dutyRosterForm.serviceAreaName.trim()) {
      setDutyRosterError(
        'Apartment, unit, floor, block, or common area is required.'
      );
      return;
    }

    try {
      setIsSavingDutyRoster(true);
      const request = {
        ...dutyRosterForm,
        employeeNumber: dutyRosterForm.employeeNumber?.trim() || null,
        propertyReference: dutyRosterForm.propertyReference?.trim() || null,
        propertyUnit: dutyRosterForm.propertyUnit?.trim() || null,
        dayPattern: dutyRosterForm.dayPattern?.trim() || null,
        endDate: dutyRosterForm.endDate?.trim() || null,
        supervisorName: dutyRosterForm.supervisorName?.trim() || null,
        toolsIssued: dutyRosterForm.toolsIssued?.trim() || null,
        suppliesIssued: dutyRosterForm.suppliesIssued?.trim() || null,
        checklist: dutyRosterForm.checklist?.trim() || null,
        linkedMaintenanceReference:
          dutyRosterForm.linkedMaintenanceReference?.trim() || null,
        linkedComplaintReference:
          dutyRosterForm.linkedComplaintReference?.trim() || null,
        linkedProcedureCaseReference:
          dutyRosterForm.linkedProcedureCaseReference?.trim() || null,
        notes: dutyRosterForm.notes?.trim() || null,
      };
      if (editingDutyId) {
        await estateFacilitiesService.updateDutyRosterItem(editingDutyId, request);
      } else {
        await estateFacilitiesService.createDutyRosterItem(request);
      }
      await loadDutyRoster();
      setDutyRosterForm(defaultDutyRosterForm());
      setSelectedDutyStaff(null);
      setSelectedDutyProperty(null);
      setSelectedDutyUnit(null);
      setEditingDutyId(null);
    } catch (error) {
      setDutyRosterError(error instanceof Error ? error.message : 'Unable to save Facilities duty roster assignment.');
    } finally {
      setIsSavingDutyRoster(false);
    }
  };

  const editDutyRosterItem = (item: EstateFacilityDutyRosterItem) => {
    setEditingDutyId(item.id);
    setSelectedDutyStaff({
      id: item.employeeProfileId || item.employeeNumber || item.id,
      employeeProfileId: item.employeeProfileId || null,
      employeeNumber: item.employeeNumber || '',
      staffName: item.staffName,
      department: null,
      position: null,
    });
    setSelectedDutyUnit(item.propertyUnit ? {
      id: item.id,
      assetCode: item.propertyUnit,
      name: item.propertyUnit,
      projectUnitCode: null,
      blockName: null,
      floorLabel: null,
      unitType: null,
      assetType: '',
    } : null);
    setSelectedDutyProperty(null);
    if (item.propertyReference) {
      void estateFacilitiesService.searchDutyProperties(item.propertyReference).then((properties) => {
        setSelectedDutyProperty(properties.find((property) => property.assetCode === item.propertyReference) || null);
      }).catch(() => setDutyRosterError('Could not resolve the saved property. Select it again before changing the unit.'));
    }
    setDutyRosterForm({
      employeeProfileId: item.employeeProfileId,
      employeeNumber: item.employeeNumber,
      staffName: item.staffName,
      staffType: item.staffType,
      dutyType: item.dutyType,
      propertyReference: item.propertyReference,
      propertyUnit: item.propertyUnit,
      serviceAreaType: item.serviceAreaType,
      serviceAreaName: item.serviceAreaName,
      frequency: item.frequency,
      dayPattern: item.dayPattern,
      startDate: item.startDate.slice(0, 10),
      endDate: item.endDate?.slice(0, 10) || '',
      shiftStart: item.shiftStart,
      shiftEnd: item.shiftEnd,
      supervisorName: item.supervisorName,
      toolsIssued: item.toolsIssued,
      suppliesIssued: item.suppliesIssued,
      inventoryIssueVoucherId: item.inventoryIssueVoucherId,
      inventoryIssueVoucherNumber: item.inventoryIssueVoucherNumber,
      checklist: item.checklist,
      attendanceStatus: item.attendanceStatus,
      completionStatus: item.completionStatus,
      qualityStatus: item.qualityStatus,
      linkedMaintenanceReference: item.linkedMaintenanceReference,
      linkedComplaintReference: item.linkedComplaintReference,
      linkedProcedureCaseReference: item.linkedProcedureCaseReference,
      notes: item.notes,
    });
    document.getElementById('duty-staff-name')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  };

  const recordDutyAttendance = async (item: EstateFacilityDutyRosterItem, present: boolean) => {
    setDutyRosterError(null);
    try {
      const updated = await estateFacilitiesService.updateDutyAttendance(
        item.id,
        {
          attendanceStatus: present ? 'Present' : 'Absent',
          completionStatus: present ? 'Completed' : 'Missed',
          qualityStatus: present ? 'Pending inspection' : 'Not inspected',
          linkedMaintenanceReference: item.linkedMaintenanceReference || null,
          linkedComplaintReference: item.linkedComplaintReference || null,
          notes: present ? 'Duty completed.' : 'Staff absent; replacement coverage required.',
        }
      );
      setDutyRosterItems((current) =>
        current.map((row) => (row.id === updated.id ? updated : row))
      );
    } catch (error) {
      setDutyRosterError(error instanceof Error ? error.message : 'Unable to update duty attendance.');
    }
  };

  const updateArInvoiceForm = (
    key: keyof CreateFacilitiesArInvoiceRequest,
    value: string | number
  ) => {
    setArInvoiceForm((current) => ({
      ...current,
      [key]: value,
    }));
  };

  const updateArInvoiceLine = (
    key: keyof CreateFacilitiesArInvoiceRequest['lineItems'][number],
    value: string | number
  ) => {
    setArInvoiceForm((current) => ({
      ...current,
      lineItems: [
        {
          ...current.lineItems[0],
          [key]: value,
        },
      ],
    }));
  };

  const updateArPaymentForm = (
    key: keyof CreateFacilitiesArPaymentRequest,
    value: string | number | boolean
  ) => {
    setArPaymentForm((current) => ({
      ...current,
      [key]: value,
    }));
  };

  const createArInvoice = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setArInvoiceError(null);
    setArInvoiceResult(null);

    if (!selectedBillingProperty || !arInvoiceForm.customerId.trim()) {
      setArInvoiceError('Select a property with an assigned customer.');
      return;
    }

    if (!arInvoiceForm.lineItems[0].description.trim()) {
      setArInvoiceError('Invoice line description is required.');
      return;
    }

    if (Number(arInvoiceForm.lineItems[0].unitPrice) <= 0) {
      setArInvoiceError('Amount must be greater than zero.');
      return;
    }

    try {
      setIsCreatingArInvoice(true);
      const invoice = await estateFacilitiesService.createArInvoice({
        ...arInvoiceForm,
        invoiceDate: new Date(arInvoiceForm.invoiceDate).toISOString(),
        dueDate: arInvoiceForm.dueDate
          ? new Date(arInvoiceForm.dueDate).toISOString()
          : undefined,
        exchangeRate: Number(arInvoiceForm.exchangeRate) || 1,
        lineItems: arInvoiceForm.lineItems.map((item) => ({
          ...item,
          quantity: Number(item.quantity) || 1,
          unitPrice: Number(item.unitPrice) || 0,
          discountPercentage: Number(item.discountPercentage) || 0,
          productId: item.productId?.trim() || undefined,
          glAccountId: item.glAccountId?.trim() || undefined,
        })),
      });

      setArInvoiceResult(invoice);
      setBillingDmsForm((current) => ({
        ...current,
        documentTitle: current.documentTitle || invoice.invoiceNumber,
        documentType: 'Invoice',
        billingDocumentKind: 'Invoice',
        financeArReference: invoice.invoiceNumber,
        customerAccountReference: invoice.customerId,
        notes: current.notes || 'Finance AR invoice created from Facilities.',
      }));
    } catch {
      setArInvoiceError('The Finance AR invoice could not be created.');
    } finally {
      setIsCreatingArInvoice(false);
    }
  };

  const createArPayment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setArPaymentError(null);
    setArPaymentResult(null);

    if (!arPaymentForm.customerId.trim()) {
      setArPaymentError('Finance AR customer ID is required.');
      return;
    }

    if (Number(arPaymentForm.totalAmount) <= 0) {
      setArPaymentError('Receipt amount must be greater than zero.');
      return;
    }

    try {
      setIsCreatingArPayment(true);
      const payment = await estateFacilitiesService.createArPayment({
        ...arPaymentForm,
        paymentDate: new Date(arPaymentForm.paymentDate).toISOString(),
        totalAmount: Number(arPaymentForm.totalAmount) || 0,
        exchangeRate: Number(arPaymentForm.exchangeRate) || 1,
        transactionReference:
          arPaymentForm.transactionReference?.trim() || undefined,
      });

      setArPaymentResult(payment);
      setBillingDmsForm((current) => ({
        ...current,
        documentTitle: current.documentTitle || payment.paymentNumber,
        documentType: 'Receipt',
        billingDocumentKind: 'Receipt',
        financeArReference: payment.paymentNumber,
        customerAccountReference: payment.customerId,
        notes: current.notes || 'Finance AR receipt created from Facilities.',
      }));
    } catch {
      setArPaymentError('The Finance AR receipt could not be recorded.');
    } finally {
      setIsCreatingArPayment(false);
    }
  };

  const updateBillingDmsForm = (
    key: keyof PublishFacilitiesBillingDocumentRequest,
    value: string
  ) => {
    setBillingDmsForm((current) => ({
      ...current,
      [key]: value,
    }));
  };

  const publishBillingDocument = async (
    event: React.FormEvent<HTMLFormElement>
  ) => {
    event.preventDefault();
    setBillingDmsError(null);
    setBillingDmsPublication(null);

    if (!billingDmsForm.documentTitle?.trim()) {
      setBillingDmsError('Document title is required.');
      return;
    }

    try {
      setIsPublishingBillingDms(true);
      const publication =
        await estateFacilitiesService.publishBillingDocumentToDms(
          billingDmsForm
        );
      setBillingDmsPublication(publication);
    } catch {
      setBillingDmsError(
        'The billing document could not be published to Central DMS.'
      );
    } finally {
      setIsPublishingBillingDms(false);
    }
  };

  React.useEffect(() => {
    let mounted = true;

    const loadWorkspace = async () => {
      if (!entityType) {
        setLoadError('Workspace was not found.');
        setIsLoading(false);
        return;
      }

      try {
        const data =
          await estateFacilitiesService.getProcedureWorkspace(entityType);
        if (mounted) {
          setWorkspace(data);
          setLoadError(data ? null : 'Workspace was not found.');
        }
      } catch {
        if (mounted) {
          setLoadError('Workspace was not found.');
          setWorkspace(null);
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadWorkspace();

    return () => {
      mounted = false;
    };
  }, [entityType]);

  React.useEffect(() => {
    if (entityType !== 'EstateFacilityStaffCleaner') {
      return;
    }

    void loadDutyRoster();
  }, [entityType, dutyRosterDate]);

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading procedure workspace
        </div>
      </div>
    );
  }

  if (!workspace || loadError) {
    return (
      <div className="space-y-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/estate/facilities')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Facilities
        </Button>
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Workspace unavailable</CardTitle>
            <CardDescription>{loadError}</CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  const { procedure } = workspace;
  const operationalHandoff = getOperationalHandoff(procedure.entityType);
  const OperationalHandoffIcon = operationalHandoff?.icon;
  const canOpenOperationalHandoff =
    procedure.entityType !== 'EstateFacilityMaintenance' ||
    hasAnyPermission(['maintenance.access']);
  const hasConfiguredWorkflow = workspace.stages.length > 0;
  const showServiceProviderRegister =
    procedure.entityType === 'EstateFacilityServiceProvider';
  const showStaffCleanerRegister =
    procedure.entityType === 'EstateFacilityStaffCleaner';
  const showBillingServiceChargeRegister =
    procedure.entityType === 'EstateFacilityBillingServiceCharge';
  const showMaintenanceIntakeRegister =
    procedure.entityType === 'EstateFacilityMaintenance';
  const showComplaintIntakeRegister =
    procedure.entityType === 'EstateFacilityComplaint';
  const operatingMode = procedure.entityType === 'EstateFacilityPropertySite' ? 'site'
    : procedure.entityType === 'EstateFacilityLease' ? 'lease' : null;
  const useCaseWorkspace = showMaintenanceIntakeRegister || showComplaintIntakeRegister;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/estate/facilities')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Facilities
        </Button>
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div className="space-y-2">
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                {procedure.title}
              </h1>
            </div>
          </div>
          {hasConfiguredWorkflow ? (
            <Badge variant="secondary">{workspace.stages.length} stages</Badge>
          ) : null}
        </div>
      </div>

      {useCaseWorkspace ? (
        <>
          <ProcedureCaseWorkspace
            module="Facilities"
            entityType={procedure.entityType}
            defaultTitle={procedure.title}
            workspaceType="Case Workflow"
          />
          {showMaintenanceIntakeRegister ? (
            <FacilitiesMaintenanceWorkspace
              workspace={workspace}
              operationalHandoff={operationalHandoff}
              canOpenOperationalHandoff={canOpenOperationalHandoff}
            />
          ) : null}
        </>
      ) : hasConfiguredWorkflow && !operatingMode ? (
        <FacilitiesWorkflowOverview workspace={workspace} />
      ) : null}

      {operatingMode ? <FacilitiesOperatingRegister mode={operatingMode} /> : null}

      {operationalHandoff && !operatingMode && !useCaseWorkspace && !showServiceProviderRegister && !showStaffCleanerRegister ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
              <div className="flex items-start gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                  {OperationalHandoffIcon ? (
                    <OperationalHandoffIcon className="h-5 w-5 text-primary" />
                  ) : null}
                </div>
                <div>
                  <CardTitle>{operationalHandoff.title}</CardTitle>
                  <Badge variant="outline" className="mt-3 w-fit">
                    {operationalHandoff.sourceLabel}
                  </Badge>
                </div>
              </div>
              {canOpenOperationalHandoff ? (
                <Button asChild>
                  <Link href={operationalHandoff.primaryAction.href}>
                    {operationalHandoff.primaryAction.label}
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Link>
                </Button>
              ) : (
                <Button disabled title="Requires Maintenance module access">
                  {operationalHandoff.primaryAction.label}
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {!canOpenOperationalHandoff ? (
              <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
                This handoff opens the Maintenance Management module. Your
                current user does not have Maintenance access, so the
                Maintenance route would redirect to the main dashboard. Ask an
                administrator to grant <code>maintenance.access</code>, or use a
                Maintenance-authorized user to continue the job card/work order
                flow.
              </div>
            ) : null}
            <div className="flex flex-wrap gap-2">
              {operationalHandoff.secondaryActions.map((action) => (
                <Button
                  key={action.href}
                  asChild={canOpenOperationalHandoff}
                  disabled={!canOpenOperationalHandoff}
                  variant="outline"
                  size="sm"
                >
                  {canOpenOperationalHandoff ? (
                    <Link href={action.href}>
                      {action.label}
                      <ExternalLink className="ml-2 h-3.5 w-3.5" />
                    </Link>
                  ) : (
                    <span>
                      {action.label}
                      <ExternalLink className="ml-2 h-3.5 w-3.5" />
                    </span>
                  )}
                </Button>
              ))}
            </div>
          </CardContent>
        </Card>
      ) : null}

      {showServiceProviderRegister ? (
        <FacilitiesProviderRegister />
      ) : null}

      {showStaffCleanerRegister ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <ClipboardCheck className="h-5 w-5 text-primary" />
              <CardTitle>Staff & Cleaner Duties</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-3">
              <div className="mb-3">
                <div className="font-medium">
                  Cleaner Timetable / Duty Roster
                </div>
              </div>
              <form
                className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"
                onSubmit={createDutyRosterItem}
              >
                <div className="space-y-1">
                  <Label htmlFor="duty-staff-name">Cleaner / staff name</Label>
                  <FacilitiesDutyLookup
                    id="duty-staff-name"
                    label="Cleaner / staff name"
                    selectedLabel={selectedDutyStaff ? `${selectedDutyStaff.staffName} (${selectedDutyStaff.employeeNumber})` : undefined}
                    search={estateFacilitiesService.searchDutyStaff}
                    describe={(staff) => ({
                      title: `${staff.staffName} (${staff.employeeNumber})`,
                      detail: [staff.department, staff.position].filter(Boolean).join(' / '),
                    })}
                    onSelect={(staff) => {
                      setSelectedDutyStaff(staff);
                      setDutyRosterForm((current) => ({
                        ...current,
                        employeeProfileId: staff.employeeProfileId,
                        employeeNumber: staff.employeeNumber,
                        staffName: staff.staffName,
                      }));
                    }}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-employee-number">Employee no.</Label>
                  <Input
                    id="duty-employee-number"
                    value={dutyRosterForm.employeeNumber || ''}
                    readOnly
                    placeholder="Selected from HR"
                  />
                  {selectedDutyStaff?.department || selectedDutyStaff?.position ? (
                    <p className="text-xs text-muted-foreground">{[selectedDutyStaff.department, selectedDutyStaff.position].filter(Boolean).join(' / ')}</p>
                  ) : null}
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-property">Property / site</Label>
                  <FacilitiesDutyLookup
                    id="duty-property"
                    label="Property / site"
                    selectedLabel={selectedDutyProperty ? `${selectedDutyProperty.name} (${selectedDutyProperty.assetCode})` : undefined}
                    search={estateFacilitiesService.searchDutyProperties}
                    describe={(property) => ({
                      title: `${property.name} (${property.assetCode})`,
                      detail: [property.projectTitle, property.location].filter(Boolean).join(' / '),
                    })}
                    onSelect={(property) => {
                      setSelectedDutyProperty(property);
                      setSelectedDutyUnit(null);
                      setDutyRosterForm((current) => ({
                        ...current,
                        propertyReference: property.assetCode,
                        propertyUnit: '',
                        serviceAreaName: property.name,
                      }));
                    }}
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-unit">Unit / parcel</Label>
                  <FacilitiesDutyLookup
                    id="duty-unit"
                    key={selectedDutyProperty?.id || 'no-property'}
                    label="Unit / parcel"
                    selectedLabel={selectedDutyUnit ? `${selectedDutyUnit.name} (${selectedDutyUnit.projectUnitCode || selectedDutyUnit.assetCode})` : undefined}
                    disabled={!selectedDutyProperty}
                    minimumQueryLength={0}
                    search={searchDutyUnits}
                    describe={(unit) => ({
                      title: `${unit.name} (${unit.projectUnitCode || unit.assetCode})`,
                      detail: [unit.blockName, unit.floorLabel, unit.unitType].filter(Boolean).join(' / '),
                    })}
                    onSelect={(unit) => {
                      setSelectedDutyUnit(unit);
                      setDutyRosterForm((current) => ({
                        ...current,
                        propertyUnit: unit.projectUnitCode || unit.assetCode,
                        serviceAreaName: unit.name,
                        serviceAreaType: unit.unitType || (String(unit.assetType).toLowerCase() === 'land' || unit.assetType === 0 ? 'Land parcel' : 'Property unit'),
                      }));
                    }}
                  />
                  {selectedDutyProperty ? <p className="text-xs text-muted-foreground">Leave unselected for common areas or the whole site.</p> : null}
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-area-type">Area type</Label>
                  <Input
                    id="duty-area-type"
                    value={dutyRosterForm.serviceAreaType}
                    onChange={(event) =>
                      updateDutyRosterForm(
                        'serviceAreaType',
                        event.target.value
                      )
                    }
                    placeholder="Apartment, floor, common area"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-area-name">Duty area</Label>
                  <Input
                    id="duty-area-name"
                    value={dutyRosterForm.serviceAreaName}
                    onChange={(event) =>
                      updateDutyRosterForm(
                        'serviceAreaName',
                        event.target.value
                      )
                    }
                    placeholder="Area / route / room"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-type">Duty type</Label>
                  <Input
                    id="duty-type"
                    value={dutyRosterForm.dutyType}
                    onChange={(event) =>
                      updateDutyRosterForm('dutyType', event.target.value)
                    }
                    placeholder="Cleaning, sanitation, inspection"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-frequency">Frequency</Label>
                  <Select value={dutyRosterForm.frequency} onValueChange={(value) => updateDutyRosterForm('frequency', value)}>
                    <SelectTrigger id="duty-frequency"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Daily">Daily</SelectItem>
                      <SelectItem value="Weekly">Weekly</SelectItem>
                      <SelectItem value="One-off">One-off</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-day-pattern">Day pattern</Label>
                  <Input
                    id="duty-day-pattern"
                    value={dutyRosterForm.dayPattern || ''}
                    onChange={(event) =>
                      updateDutyRosterForm('dayPattern', event.target.value)
                    }
                    placeholder="Mon-Fri / Sat / one-off"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-start-date">Start date</Label>
                  <Input
                    id="duty-start-date"
                    type="date"
                    value={dutyRosterForm.startDate}
                    onChange={(event) =>
                      updateDutyRosterForm('startDate', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-end-date">End date</Label>
                  <Input id="duty-end-date" type="date" value={dutyRosterForm.endDate || ''} onChange={(event) => updateDutyRosterForm('endDate', event.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-shift-start">Shift start</Label>
                  <Input
                    id="duty-shift-start"
                    type="time"
                    value={dutyRosterForm.shiftStart}
                    onChange={(event) =>
                      updateDutyRosterForm('shiftStart', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-shift-end">Shift end</Label>
                  <Input
                    id="duty-shift-end"
                    type="time"
                    value={dutyRosterForm.shiftEnd}
                    onChange={(event) =>
                      updateDutyRosterForm('shiftEnd', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-supervisor">Supervisor</Label>
                  <Input
                    id="duty-supervisor"
                    value={dutyRosterForm.supervisorName || ''}
                    onChange={(event) =>
                      updateDutyRosterForm('supervisorName', event.target.value)
                    }
                    placeholder="Facilities supervisor"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-maintenance">Maintenance ref.</Label>
                  <Input
                    id="duty-maintenance"
                    value={dutyRosterForm.linkedMaintenanceReference || ''}
                    onChange={(event) =>
                      updateDutyRosterForm(
                        'linkedMaintenanceReference',
                        event.target.value
                      )
                    }
                    placeholder="Optional work order"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-complaint">Complaint ref.</Label>
                  <Input
                    id="duty-complaint"
                    value={dutyRosterForm.linkedComplaintReference || ''}
                    onChange={(event) =>
                      updateDutyRosterForm(
                        'linkedComplaintReference',
                        event.target.value
                      )
                    }
                    placeholder="Optional complaint"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-tools">Tools / supplies</Label>
                  <Input
                    id="duty-tools"
                    value={dutyRosterForm.toolsIssued || ''}
                    onChange={(event) =>
                      updateDutyRosterForm('toolsIssued', event.target.value)
                    }
                    placeholder="Mop, bins, PPE"
                  />
                </div>
                <div className="space-y-1">
                  <Label>Inventory issue voucher</Label>
                  <FacilitiesDutyLookup<FacilitiesIssueVoucherOption>
                    label="issued voucher"
                    selectedLabel={dutyRosterForm.inventoryIssueVoucherNumber || undefined}
                    search={estateFacilitiesService.searchIssueVouchers}
                    describe={(voucher) => ({ title: voucher.voucherNumber, detail: voucher.supplies || voucher.status })}
                    onSelect={(voucher) => setDutyRosterForm((current) => ({
                      ...current,
                      inventoryIssueVoucherId: voucher.id,
                      inventoryIssueVoucherNumber: voucher.voucherNumber,
                      suppliesIssued: voucher.supplies,
                    }))}
                  />
                </div>
                {dutyRosterForm.inventoryIssueVoucherNumber ? (
                  <div className="flex items-end gap-2 md:col-span-2">
                    <Input aria-label="Issued supplies" value={dutyRosterForm.suppliesIssued || ''} readOnly />
                    <Button type="button" variant="outline" size="sm" onClick={() => setDutyRosterForm((current) => ({
                      ...current, inventoryIssueVoucherId: null, inventoryIssueVoucherNumber: null, suppliesIssued: '',
                    }))}>Clear voucher</Button>
                  </div>
                ) : null}
                <div className="space-y-1 md:col-span-2 xl:col-span-4">
                  <Label htmlFor="duty-checklist">Checklist / notes</Label>
                  <Textarea
                    id="duty-checklist"
                    value={dutyRosterForm.checklist || ''}
                    onChange={(event) =>
                      updateDutyRosterForm('checklist', event.target.value)
                    }
                    placeholder="Cleaning checklist and reporting notes"
                  />
                </div>
                <div className="flex flex-wrap items-center gap-2 md:col-span-2 xl:col-span-4">
                  <Button type="submit" disabled={isSavingDutyRoster}>
                    {isSavingDutyRoster ? 'Saving duty...' : editingDutyId ? 'Save duty' : 'Add duty roster'}
                  </Button>
                  {editingDutyId ? <Button type="button" variant="outline" onClick={() => { setEditingDutyId(null); setDutyRosterForm(defaultDutyRosterForm()); }}>Cancel edit</Button> : null}
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => void loadDutyRoster()}
                  >
                    Refresh roster
                  </Button>
                  {dutyRosterError ? (
                    <span className="text-sm text-destructive">
                      {dutyRosterError}
                    </span>
                  ) : null}
                </div>
              </form>
            </div>
            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
                <div>
                  <div className="font-medium">Cleaner timetable</div>
                  <p className="text-sm text-muted-foreground">
                    {isLoadingDutyRoster
                      ? 'Loading roster...'
                      : `${dutyRosterItems.length} duty assignment(s)`}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Label htmlFor="duty-roster-date">Date</Label>
                  <Input id="duty-roster-date" type="date" className="w-40" value={dutyRosterDate} onChange={(event) => setDutyRosterDate(event.target.value)} />
                </div>
              </div>
              {dutyRosterItems.length === 0 && !isLoadingDutyRoster ? (
                <div className="rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">
                  No cleaner timetable has been created yet.
                </div>
              ) : (
                <div className="grid gap-3">
                  {dutyRosterItems.map((item) => (
                    <div
                      key={item.id}
                      className="rounded-md border border-border bg-muted/30 p-3"
                    >
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <div className="font-medium">
                            {item.staffName} - {item.serviceAreaName}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            {item.rosterReference} | {item.dutyType} |{' '}
                            {item.frequency}{' '}
                            {item.dayPattern ? `(${item.dayPattern})` : ''}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            {item.propertyReference || 'No property ref'} /{' '}
                            {item.propertyUnit || item.serviceAreaType} |{' '}
                            {new Date(item.startDate).toLocaleDateString()} |{' '}
                            {item.shiftStart} - {item.shiftEnd}
                          </div>
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Badge variant="secondary">
                            {item.attendanceStatus}
                          </Badge>
                          <Badge variant="outline">
                            {item.completionStatus}
                          </Badge>
                          <Badge variant="outline">{item.qualityStatus}</Badge>
                        </div>
                      </div>
                      <div className="mt-3 flex flex-wrap items-center gap-2">
                        <Button type="button" size="sm" variant="outline" onClick={() => editDutyRosterItem(item)}>Edit schedule</Button>
                        {item.linkedMaintenanceReference ? (
                          <Badge variant="outline">
                            Maintenance: {item.linkedMaintenanceReference}
                          </Badge>
                        ) : null}
                        {item.linkedComplaintReference ? (
                          <Badge variant="outline">
                            Complaint: {item.linkedComplaintReference}
                          </Badge>
                        ) : null}
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() => void recordDutyAttendance(item, true)}
                          disabled={dutyRosterDate !== todayInputValue() || item.completionStatus === 'Completed'}
                        >
                          Mark present / completed
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() => void recordDutyAttendance(item, false)}
                          disabled={dutyRosterDate !== todayInputValue() || item.attendanceStatus === 'Absent'}
                        >
                          Mark absent
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      ) : null}

      {showBillingServiceChargeRegister ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <CreditCard className="h-5 w-5 text-primary" />
              <CardTitle>Facilities Billing / Service Charge</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              className="rounded-md border border-border bg-background p-4"
              onSubmit={createArInvoice}
            >
              <div className="mb-4">
                <div className="font-medium">Create Finance AR Invoice</div>
                <p className="mt-1 text-sm text-muted-foreground">
                  Bill the selected property through Finance AR. Finance approval,
                  receipts, balances, and posting remain in Finance.
                </p>
              </div>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <div className="space-y-2">
                  <Label htmlFor="ar-property">Property or unit</Label>
                  <FacilitiesDutyLookup<EstateManagedAsset>
                    id="ar-property"
                    label="Property or unit"
                    selectedLabel={selectedBillingProperty
                      ? `${selectedBillingProperty.name} (${selectedBillingProperty.assetCode})`
                      : undefined}
                    search={searchBillingProperties}
                    describe={(asset) => ({
                      title: `${asset.name} (${asset.assetCode})`,
                      detail: asset.lesseeName || asset.location || undefined,
                    })}
                    onSelect={(asset) => {
                      setSelectedBillingProperty(asset);
                      setArInvoiceResult(null);
                      setArInvoiceForm((current) => ({
                        ...current,
                        customerId: asset.customerBusinessPartnerId || '',
                        propertyUnit: asset.assetCode,
                        reference: asset.assetCode,
                      }));
                    }}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-customer">Customer</Label>
                  <Input id="ar-customer" value={selectedBillingProperty?.lesseeName || ''} readOnly placeholder="Select a property" />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-invoice-reference">
                    Facilities reference
                  </Label>
                  <Input
                    id="ar-invoice-reference"
                    value={arInvoiceForm.reference}
                    onChange={(event) =>
                      updateArInvoiceForm('reference', event.target.value)
                    }
                    placeholder="FAC-BILL-0001"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-invoice-date">Invoice date</Label>
                  <Input
                    id="ar-invoice-date"
                    type="date"
                    value={arInvoiceForm.invoiceDate}
                    onChange={(event) =>
                      updateArInvoiceForm('invoiceDate', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-due-date">Due date</Label>
                  <Input
                    id="ar-due-date"
                    type="date"
                    value={arInvoiceForm.dueDate}
                    onChange={(event) =>
                      updateArInvoiceForm('dueDate', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2 md:col-span-2">
                  <Label htmlFor="ar-line-description">Line description</Label>
                  <Input
                    id="ar-line-description"
                    value={arInvoiceForm.lineItems[0].description}
                    onChange={(event) =>
                      updateArInvoiceLine('description', event.target.value)
                    }
                    placeholder="Facilities service charge"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-line-amount">Amount</Label>
                  <Input
                    id="ar-line-amount"
                    type="number"
                    min="0"
                    step="0.01"
                    value={arInvoiceForm.lineItems[0].unitPrice}
                    onChange={(event) =>
                      updateArInvoiceLine(
                        'unitPrice',
                        Number(event.target.value)
                      )
                    }
                    placeholder="0.00"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-currency">Currency</Label>
                  <Input
                    id="ar-currency"
                    value={arInvoiceForm.currencyCode}
                    onChange={(event) =>
                      updateArInvoiceForm('currencyCode', event.target.value)
                    }
                    maxLength={3}
                  />
                </div>
                <div className="space-y-2 md:col-span-2">
                  <Label htmlFor="ar-gl-account">Revenue GL account ID</Label>
                  <Input
                    id="ar-gl-account"
                    value={arInvoiceForm.lineItems[0].glAccountId || ''}
                    onChange={(event) =>
                      updateArInvoiceLine('glAccountId', event.target.value)
                    }
                    placeholder="Optional GL account GUID"
                  />
                </div>
                <div className="space-y-2 md:col-span-2">
                  <Label htmlFor="ar-invoice-notes">AR notes</Label>
                  <Textarea
                    id="ar-invoice-notes"
                    value={arInvoiceForm.notes}
                    onChange={(event) =>
                      updateArInvoiceForm('notes', event.target.value)
                    }
                    placeholder="Facilities AR instruction"
                  />
                </div>
              </div>
              {arInvoiceError ? (
                <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                  {arInvoiceError}
                </div>
              ) : null}
              {arInvoiceResult ? (
                <div className={`mt-3 flex flex-col gap-2 rounded-md p-3 text-sm sm:flex-row sm:items-center sm:justify-between ${arInvoiceResult.status === 'Sent' ? 'border border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300' : 'border border-amber-500/30 bg-amber-500/10 text-amber-800 dark:text-amber-300'}`}>
                  <span>
                    Finance AR invoice {arInvoiceResult.invoiceNumber}{' '}
                    {arInvoiceResult.status === 'Sent'
                      ? 'was sent and is available to the customer.'
                      : `was created with status ${arInvoiceResult.status}. Finance must release it before it appears as a customer bill.`}
                  </span>
                  <Button asChild size="sm" variant="outline">
                    <Link href={`/finance/ar/invoices/${arInvoiceResult.id}`}>
                      Open AR invoice
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                </div>
              ) : null}
              <div className="mt-4 flex flex-wrap items-center gap-2">
                <Button type="submit" disabled={isCreatingArInvoice}>
                  {isCreatingArInvoice
                    ? 'Creating invoice...'
                    : 'Create AR invoice'}
                </Button>
                <Badge variant="outline">
                  Finance AR
                </Badge>
              </div>
            </form>
            <FacilitiesPropertyInvoices propertyUnit={selectedBillingProperty?.assetCode} refreshKey={arInvoiceResult?.id} />
            <form
              className="rounded-md border border-border bg-background p-4"
              onSubmit={createArPayment}
            >
              <div className="mb-4">
                <div className="font-medium">Record Finance AR Receipt</div>
                <p className="mt-1 text-sm text-muted-foreground">
                  Record a receipt through the existing Finance AR payment API.
                  Allocation to invoices remains inside Finance AR.
                </p>
              </div>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <div className="space-y-2">
                  <Label htmlFor="ar-payment-customer-id">
                    Finance AR customer ID
                  </Label>
                  <Input
                    id="ar-payment-customer-id"
                    value={arPaymentForm.customerId}
                    onChange={(event) =>
                      updateArPaymentForm('customerId', event.target.value)
                    }
                    placeholder="Customer GUID from Finance AR"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-payment-date">Receipt date</Label>
                  <Input
                    id="ar-payment-date"
                    type="date"
                    value={arPaymentForm.paymentDate}
                    onChange={(event) =>
                      updateArPaymentForm('paymentDate', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-payment-amount">Receipt amount</Label>
                  <Input
                    id="ar-payment-amount"
                    type="number"
                    min="0"
                    step="0.01"
                    value={arPaymentForm.totalAmount}
                    onChange={(event) =>
                      updateArPaymentForm(
                        'totalAmount',
                        Number(event.target.value)
                      )
                    }
                    placeholder="0.00"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-payment-method">Payment method</Label>
                  <select
                    id="ar-payment-method"
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                    value={arPaymentForm.paymentMethod}
                    onChange={(event) =>
                      updateArPaymentForm('paymentMethod', event.target.value)
                    }
                  >
                    <option>Cash</option>
                    <option>Bank Transfer</option>
                    <option>Cheque</option>
                    <option>Mobile Money</option>
                    <option>Card</option>
                  </select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-payment-currency">Currency</Label>
                  <Input
                    id="ar-payment-currency"
                    value={arPaymentForm.currencyCode}
                    onChange={(event) =>
                      updateArPaymentForm('currencyCode', event.target.value)
                    }
                    maxLength={3}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="ar-payment-reference">
                    Transaction reference
                  </Label>
                  <Input
                    id="ar-payment-reference"
                    value={arPaymentForm.transactionReference}
                    onChange={(event) =>
                      updateArPaymentForm(
                        'transactionReference',
                        event.target.value
                      )
                    }
                    placeholder="Bank, cheque, MOMO, or receipt reference"
                  />
                </div>
                <div className="space-y-2 md:col-span-2">
                  <Label htmlFor="ar-payment-notes">Receipt notes</Label>
                  <Textarea
                    id="ar-payment-notes"
                    value={arPaymentForm.notes}
                    onChange={(event) =>
                      updateArPaymentForm('notes', event.target.value)
                    }
                    placeholder="Facilities AR receipt"
                  />
                </div>
              </div>
              {arPaymentError ? (
                <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                  {arPaymentError}
                </div>
              ) : null}
              {arPaymentResult ? (
                <div className="mt-3 flex flex-col gap-2 rounded-md border border-emerald-500/30 bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-300 sm:flex-row sm:items-center sm:justify-between">
                  <span>
                    Finance AR receipt {arPaymentResult.paymentNumber} recorded
                    with status {arPaymentResult.status}.
                  </span>
                  <Button asChild size="sm" variant="outline">
                    <Link href="/finance/ar/payments">
                      Open AR payments
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                </div>
              ) : null}
              <div className="mt-4 flex flex-wrap items-center gap-2">
                <Button type="submit" disabled={isCreatingArPayment}>
                  {isCreatingArPayment
                    ? 'Recording receipt...'
                    : 'Record AR receipt'}
                </Button>
                <Badge variant="outline">
                  Finance AR
                </Badge>
              </div>
            </form>
            <form
              className="rounded-md border border-border bg-background p-4"
              onSubmit={publishBillingDocument}
            >
              <div className="mb-4">
                <div className="font-medium">
                  Publish Billing Document To DMS
                </div>
                <p className="mt-1 text-sm text-muted-foreground">
                  Create the Central DMS record for invoices, receipts,
                  statements, demand notices, adjustments, deposits, and dispute
                  evidence using the Facilities billing source chain.
                </p>
              </div>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                <div className="space-y-2">
                  <Label htmlFor="billing-document-title">Document title</Label>
                  <Input
                    id="billing-document-title"
                    value={billingDmsForm.documentTitle}
                    onChange={(event) =>
                      updateBillingDmsForm('documentTitle', event.target.value)
                    }
                    placeholder="Service charge invoice - Block A"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="billing-document-type">Document type</Label>
                  <select
                    id="billing-document-type"
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                    value={billingDmsForm.documentType}
                    onChange={(event) => {
                      updateBillingDmsForm('documentType', event.target.value);
                      updateBillingDmsForm(
                        'billingDocumentKind',
                        event.target.value
                      );
                    }}
                  >
                    <option>Invoice</option>
                    <option>Receipt</option>
                    <option>Statement</option>
                    <option>Demand notice</option>
                    <option>Adjustment</option>
                    <option>Deposit evidence</option>
                    <option>Dispute evidence</option>
                    <option>Approval note</option>
                  </select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="facilities-billing-reference">
                    Facilities billing reference
                  </Label>
                  <Input
                    id="facilities-billing-reference"
                    value={billingDmsForm.facilitiesBillingReference}
                    onChange={(event) =>
                      updateBillingDmsForm(
                        'facilitiesBillingReference',
                        event.target.value
                      )
                    }
                    placeholder="FAC-BILL-0001"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="finance-ar-reference">
                    Finance AR reference
                  </Label>
                  <Input
                    id="finance-ar-reference"
                    value={billingDmsForm.financeArReference}
                    onChange={(event) =>
                      updateBillingDmsForm(
                        'financeArReference',
                        event.target.value
                      )
                    }
                    placeholder="AR-INV / RCPT / STM"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="billing-property-unit">Property / unit</Label>
                  <Input
                    id="billing-property-unit"
                    value={billingDmsForm.propertyUnit}
                    onChange={(event) =>
                      updateBillingDmsForm('propertyUnit', event.target.value)
                    }
                    placeholder="Property, block, unit, or service area"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="billing-customer-account">
                    Customer account
                  </Label>
                  <Input
                    id="billing-customer-account"
                    value={billingDmsForm.customerAccountReference}
                    onChange={(event) =>
                      updateBillingDmsForm(
                        'customerAccountReference',
                        event.target.value
                      )
                    }
                    placeholder="Finance AR customer account"
                  />
                </div>
                <div className="space-y-2 xl:col-span-2">
                  <Label htmlFor="billing-repository-url">
                    Repository path or URL
                  </Label>
                  <Input
                    id="billing-repository-url"
                    value={
                      billingDmsForm.externalDocumentUrl ||
                      billingDmsForm.repositoryPath
                    }
                    onChange={(event) => {
                      updateBillingDmsForm(
                        event.target.value.startsWith('http')
                          ? 'externalDocumentUrl'
                          : 'repositoryPath',
                        event.target.value
                      );
                    }}
                    placeholder="DMS/file path or document URL"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="billing-file-name">File name</Label>
                  <Input
                    id="billing-file-name"
                    value={billingDmsForm.fileName}
                    onChange={(event) =>
                      updateBillingDmsForm('fileName', event.target.value)
                    }
                    placeholder="invoice.pdf"
                  />
                </div>
                <div className="space-y-2 md:col-span-2 xl:col-span-3">
                  <Label htmlFor="billing-dms-notes">Notes</Label>
                  <Textarea
                    id="billing-dms-notes"
                    value={billingDmsForm.notes}
                    onChange={(event) =>
                      updateBillingDmsForm('notes', event.target.value)
                    }
                    placeholder="Approval, allocation, arrears, dispute, or filing notes"
                  />
                </div>
              </div>
              {billingDmsError ? (
                <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                  {billingDmsError}
                </div>
              ) : null}
              {billingDmsPublication ? (
                <div className="mt-3 flex flex-col gap-2 rounded-md border border-emerald-500/30 bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-300 sm:flex-row sm:items-center sm:justify-between">
                  <span>
                    Published as {billingDmsPublication.documentReference}
                  </span>
                  <Button asChild size="sm" variant="outline">
                    <Link
                      href={`/document-management/records/${billingDmsPublication.id}`}
                    >
                      Open DMS record
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                  </Button>
                </div>
              ) : null}
              <div className="mt-4 flex flex-wrap items-center gap-2">
                <Button type="submit" disabled={isPublishingBillingDms}>
                  {isPublishingBillingDms
                    ? 'Publishing...'
                    : 'Publish to Central DMS'}
                </Button>
                <Badge variant="outline">
                  Central DMS
                </Badge>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : null}

    </div>
  );
}
