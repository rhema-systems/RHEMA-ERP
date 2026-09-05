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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  estateFacilitiesService,
  type CreateFacilitiesArInvoiceRequest,
  type CreateFacilitiesArPaymentRequest,
  type EstateFacilityDutyRosterItem,
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

const serviceProviderGates = [
  {
    title: 'Approved Supplier Source',
    description:
      'Facilities uses approved Procurement suppliers or business partners as the provider source.',
  },
  {
    title: 'Contract And Compliance',
    description:
      'Procurement owns contracts, Legal supports contract review, and Facilities records operational readiness.',
  },
  {
    title: 'Operational Assignment',
    description:
      'Facilities maps approved providers to service categories, properties, coverage, SLAs, emergency availability, and assignment readiness.',
  },
  {
    title: 'Performance And AP',
    description:
      'Facilities records SLA, quality, complaints, and performance while Finance AP owns invoices, payments, supplier balances, and postings.',
  },
];

const serviceProviderLifecycleStates = [
  'Approved Supplier',
  'Contract / Compliance Validated',
  'Operational Profile Active',
  'Assignment Readiness Approved',
  'Linked To Facilities Workstreams',
  'Performance Reviewed',
  'Finance AP Referenced',
  'Renew / Suspend / Close',
];

const staffCleanerGates = [
  {
    title: 'HR Employee Source',
    description:
      'Facilities references active HR or Payroll employee profiles instead of creating separate staff records.',
  },
  {
    title: 'Administration User Link',
    description:
      'Administration User Management owns login accounts and User-Employee Links connects accounts to employee records when system access is needed.',
  },
  {
    title: 'Roster And Duty Area',
    description:
      'Facilities assigns sites, floors, routes, shifts, supervisors, tools, and supplies.',
  },
  {
    title: 'Attendance Exceptions',
    description:
      'Facilities records duty attendance follow-up, absence, lateness, replacement coverage, and exceptions.',
  },
  {
    title: 'Quality And HR Escalation',
    description:
      'Facilities supervises duty quality, links complaints or maintenance issues, and sends HR escalation where staffing or conduct action is required.',
  },
];

const staffCleanerLifecycleStates = [
  'HR Employee Profile',
  'User Link Checked',
  'Facilities Duty Profile',
  'Roster / Tools Assigned',
  'Attendance Exceptions',
  'Duty Quality Inspected',
  'Linked Follow-up Routed',
  'Duty Period Closed',
];

const assetOperationsGates = [
  {
    title: 'Source Asset Record',
    description:
      'Project handover, Finance Fixed Assets, Maintenance Management, Procurement, or Inventory supplies the asset source reference.',
  },
  {
    title: 'Facilities Operating View',
    description:
      'Facilities tracks location, custodian, access, service impact, condition, warranty, inspection readiness, and operating status.',
  },
  {
    title: 'Maintenance And Complaints',
    description:
      'Maintenance owns job cards and work orders while Facilities links service history, complaints, providers, and recurring defects.',
  },
  {
    title: 'Finance Asset Actions',
    description:
      'Finance Fixed Assets owns capitalization, depreciation, valuation, transfer, disposal, and retirement.',
  },
];

const assetOperationsLifecycleStates = [
  'Project / Finance / Maintenance Source',
  'Location / Custody Validated',
  'Finance / Project Context Linked',
  'Condition / Warranty Baseline',
  'Documents Indexed',
  'Maintenance / Complaint Links',
  'Finance Or Operational Action',
  'Operating Period Closed',
];

const documentIndexGates = [
  {
    title: 'Source Module Owner',
    description:
      'Legal, Procurement, Project Management, Maintenance, Finance, Estate, Helpdesk, Workflow, Property Management, or Facilities remains the source document owner.',
  },
  {
    title: 'Metadata Template',
    description:
      'Facilities defines module metadata for documents so Central DMS can migrate records with consistent tags and required fields.',
  },
  {
    title: 'Expiry And Access',
    description:
      'Facilities tracks expiry, renewal, confidentiality, retention, retrieval, and workspace visibility.',
  },
  {
    title: 'Comments And Annotations',
    description:
      'Facilities records future comments, versioning, PDF viewer annotation, redline, and audit-readiness needs without becoming the repository.',
  },
  {
    title: 'Central DMS',
    description:
      'Central Document Management owns repository, versioning, comments, PDF viewer annotations, and audit trail.',
  },
];

const documentIndexLifecycleStates = [
  'Source Document Referenced',
  'Metadata Defined',
  'Access / Ownership Validated',
  'Lifecycle Controls Set',
  'Facilities Workspace Linked',
  'Comments / Annotation Ready',
  'Index Published',
  'Central DMS Ready',
];

const billingServiceChargeGates = [
  {
    title: 'Facilities Billing Source',
    description:
      'Facilities captures service charge, recovery, deposit, statement, arrears, adjustment, and dispute instructions with Source: Estate / Facilities.',
  },
  {
    title: 'Property And Customer Context',
    description:
      'Property Management supplies property, unit, occupant, lease, service, and availability context when the billing action affects property operations.',
  },
  {
    title: 'Finance AR Execution',
    description:
      'Finance AR owns customer accounts, invoices, receipts, allocations, statements, balances, deposits, and GL postings.',
  },
  {
    title: 'DMS Filing',
    description:
      'Central DMS stores invoice, receipt, statement, demand notice, adjustment, approval, and dispute documents for versioning and audit.',
  },
];

const billingServiceChargeLifecycleStates = [
  'Billing Trigger Logged',
  'Billable Party Validated',
  'Charge Basis Defined',
  'Instruction Approved',
  'Sent To Finance AR',
  'Finance Result Tracked',
  'Arrears / Dispute Follow-up',
  'DMS Published / Closed',
];

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
            <CardDescription className="mt-2 max-w-4xl">
              Dedicated Facilities workspace. This screen does not create a
              generic procedure case; stages, checklists, documents, fields,
              outputs, and handoffs are driven from the Facilities workflow /
              catalog configuration.
            </CardDescription>
          </div>
          <Badge variant="outline">No generic case view</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 md:grid-cols-4">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.stages.length}
            </div>
            <div className="text-sm text-muted-foreground">workflow stages</div>
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
            <div className="font-medium">Workflow not configured</div>
            <p className="mt-1 text-sm text-muted-foreground">
              No Facilities stages, checklists, documents, fields, outputs, or
              handoffs are predefined here. Configure and publish the workflow
              in Administration &gt; Workflow Setup for this entity type.
            </p>
          </div>
        ) : null}

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.25fr)_minmax(0,0.75fr)]">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Operational stage board</div>
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
                      <div className="mt-1 text-sm text-muted-foreground">
                        {stage.summary}
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
            <div className="mb-3 font-medium">Expected outputs</div>
            <div className="flex flex-wrap gap-2">
              {workspace.outputs.map((output) => (
                <Badge key={output} variant="secondary">
                  {output}
                </Badge>
              ))}
            </div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Module handoffs</div>
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
          <div className="font-medium">Workflow not configured</div>
          <div className="mt-1 text-sm text-muted-foreground">
            Configure and publish the Facilities workflow for this entity type.
          </div>
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
  invoiceDate: dateInputValue(new Date()),
  dueDate: dateInputValue(addDays(new Date(), 30)),
  reference: '',
  notes: 'Source: Estate / Facilities -> Finance AR',
  currencyCode: 'USD',
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
  notes: 'Source: Estate / Facilities -> Finance AR',
  isCreditNote: false,
};

const maintenanceIntakeGates = [
  {
    title: 'Facilities Intake',
    description:
      'Facilities captures requester, location, evidence, SLA, safety, service impact, and Source: Estate / Facilities.',
  },
  {
    title: 'Maintenance Execution',
    description:
      'Maintenance Management owns job cards, work orders, technicians, parts, execution, inspection, and technical closure.',
  },
  {
    title: 'Linked Impact',
    description:
      'Facilities updates complaints, assets, providers, records, and Property Management when a unit block, release, occupancy, or handover effect exists.',
  },
  {
    title: 'Facilities Closeout',
    description:
      'Facilities closes the intake after Maintenance status, inspection result, requester feedback, and linked updates are recorded.',
  },
];

const maintenanceIntakeLifecycleStates = [
  'Facilities Intake Logged',
  'Location / Impact Validated',
  'Priority / SLA Triage',
  'Sent To Maintenance',
  'Job Card / Work Order Linked',
  'Execution Status Tracked',
  'Feedback / Inspection Received',
  'Facilities Intake Closed',
];

const complaintIntakeGates = [
  {
    title: 'Facilities Complaint Intake',
    description:
      'Facilities captures complainant, property, tenant, evidence, service impact, severity, SLA context, and Source: Estate / Facilities.',
  },
  {
    title: 'Helpdesk Ticket Lifecycle',
    description:
      'Helpdesk owns complaint ticket SLA timers, assignment, escalation, investigation, resolution, reopening, and ticket closure history.',
  },
  {
    title: 'Linked Facilities Actions',
    description:
      'Facilities routes linked maintenance, provider, staff, asset, document, or Property Management updates without duplicating Helpdesk ownership.',
  },
  {
    title: 'Facilities Feedback Closeout',
    description:
      'Facilities closes the intake after Helpdesk status, complainant feedback, linked updates, recurrence checks, and reporting are recorded.',
  },
];

const complaintIntakeLifecycleStates = [
  'Complaint Intake Logged',
  'Estate Context Validated',
  'Severity / SLA Classified',
  'Sent To Helpdesk',
  'Ticket Status Tracked',
  'Linked Action Coordinated',
  'Feedback / Resolution Received',
  'Facilities Complaint Closed',
];

function getOperationalHandoff(entityType: string): OperationalHandoff | null {
  if (entityType === 'EstateFacilityPropertySite') {
    return {
      title: 'Property / Site Operating View Handoff',
      description:
        'Estate / Facilities keeps the operating view for sites, buildings, floors, common areas, occupancy impact, service zones, responsible officers, access, and site operating documents. Property Management remains the register owner for property/unit commercial records, leases, occupants, and availability.',
      sourceLabel:
        'Source: Property Management / Project Handover -> Estate / Facilities',
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
      sourceLabel: 'Source: Property Management -> Estate / Facilities',
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
        'Estate / Facilities manages provider operating profiles, service coverage, assignment readiness, workstream links, SLA monitoring, and performance. Procurement owns supplier onboarding, contracts, rate cards, and supplier compliance; Legal supports contract review; Finance AP owns invoices, payments, balances, and postings.',
      sourceLabel: 'Source: Procurement -> Estate / Facilities',
      icon: Briefcase,
      primaryAction: {
        label: 'Open Procurement Suppliers',
        href: '/procurement/business-partners',
      },
      secondaryActions: [
        { label: 'Procurement Contracts', href: '/procurement/contracts' },
        { label: 'Purchase Orders', href: '/procurement/purchase-orders' },
        { label: 'AP Invoices', href: '/finance/ap/invoices' },
        { label: 'AP Payments', href: '/finance/ap/payments' },
      ],
      checkpoints: [
        'Use approved Procurement suppliers or business partners as the provider source.',
        'Link Procurement contract, compliance, insurance, tax, license, and rate references.',
        'Use Facilities for service coverage, assignment readiness, approved workstreams, SLA monitoring, quality, and provider performance.',
        'Link Maintenance Intake, Complaint Management, Asset Operating View, Staff/Cleaner, or Document records when the provider is used.',
        'Send invoice, payment, balance, posting, retention, and dispute processing to Finance AP.',
      ],
    };
  }

  if (entityType === 'EstateFacilityStaffCleaner') {
    return {
      title: 'Staff & Cleaner Duty Operations Handoff',
      description:
        'HR / Payroll remains the source for employee records and employment status. Administration owns login accounts and User-Employee Links. Estate / Facilities manages duty areas, rosters, attendance follow-up, tools, supplies, supervision, service quality, linked workstream follow-up, and HR escalations.',
      sourceLabel: 'Source: HR / Payroll -> Estate / Facilities',
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
        'Source: Project Management / Finance Fixed Assets / Maintenance -> Estate / Facilities',
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
      sourceLabel:
        'Source: Source Module -> Estate / Facilities -> Central DMS',
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
      sourceLabel: 'Source: Estate / Facilities -> Finance AR',
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
        'State Source: Estate / Facilities on every downstream Finance AR instruction.',
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
      sourceLabel: 'Source: Estate / Facilities',
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
        'State Source: Estate / Facilities in the job card or work order notes.',
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
      sourceLabel: 'Source: Estate / Facilities',
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
        'State Source: Estate / Facilities in the complaint ticket.',
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
  const [dutyRosterForm, setDutyRosterForm] =
    React.useState<UpsertEstateFacilityDutyRosterRequest>(
      defaultDutyRosterForm()
    );
  const [dutyRosterError, setDutyRosterError] = React.useState<string | null>(
    null
  );
  const [isLoadingDutyRoster, setIsLoadingDutyRoster] = React.useState(false);
  const [isSavingDutyRoster, setIsSavingDutyRoster] = React.useState(false);

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
      const items = await estateFacilitiesService.getDutyRoster();
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

    if (!dutyRosterForm.staffName.trim()) {
      setDutyRosterError('Cleaner or staff name is required.');
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
      const item = await estateFacilitiesService.createDutyRosterItem({
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
      });
      setDutyRosterItems((current) => [item, ...current]);
      setDutyRosterForm(defaultDutyRosterForm());
    } catch {
      setDutyRosterError('Unable to save Facilities duty roster assignment.');
    } finally {
      setIsSavingDutyRoster(false);
    }
  };

  const markDutyCompleted = async (item: EstateFacilityDutyRosterItem) => {
    setDutyRosterError(null);
    try {
      const updated = await estateFacilitiesService.updateDutyAttendance(
        item.id,
        {
          attendanceStatus: 'Present',
          completionStatus: 'Completed',
          qualityStatus: item.qualityStatus || 'Pending inspection',
          linkedMaintenanceReference: item.linkedMaintenanceReference || null,
          linkedComplaintReference: item.linkedComplaintReference || null,
          notes:
            'Marked completed from Facilities Staff & Cleaner Duties workspace.',
        }
      );
      setDutyRosterItems((current) =>
        current.map((row) => (row.id === updated.id ? updated : row))
      );
    } catch {
      setDutyRosterError('Unable to update duty attendance.');
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

    if (!arInvoiceForm.customerId.trim()) {
      setArInvoiceError('Finance AR customer ID is required.');
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
  }, [entityType]);

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
    hasConfiguredWorkflow &&
    procedure.entityType === 'EstateFacilityServiceProvider';
  const showStaffCleanerRegister =
    procedure.entityType === 'EstateFacilityStaffCleaner';
  const showAssetOperationsRegister =
    hasConfiguredWorkflow &&
    procedure.entityType === 'EstateFacilityAssetRegister';
  const showBillingServiceChargeRegister =
    hasConfiguredWorkflow &&
    procedure.entityType === 'EstateFacilityBillingServiceCharge';
  const showDocumentIndexRegister =
    hasConfiguredWorkflow && procedure.entityType === 'EstateFacilityDocument';
  const showMaintenanceIntakeRegister =
    hasConfiguredWorkflow &&
    procedure.entityType === 'EstateFacilityMaintenance';
  const useMaintenanceWorkspace = showMaintenanceIntakeRegister;
  const showComplaintIntakeRegister =
    hasConfiguredWorkflow && procedure.entityType === 'EstateFacilityComplaint';

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
            <Badge variant="outline" className="w-fit">
              {procedure.entityType}
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                {procedure.title}
              </h1>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge variant="secondary">{procedure.source}</Badge>
            <Badge variant="secondary">{workspace.stages.length} stages</Badge>
          </div>
        </div>
      </div>

      {useMaintenanceWorkspace ? (
        <>
          <ProcedureCaseWorkspace
            module="Facilities"
            entityType={procedure.entityType}
            defaultTitle={procedure.title}
            workspaceType="Case Workflow"
          />
          <FacilitiesMaintenanceWorkspace
            workspace={workspace}
            operationalHandoff={operationalHandoff}
            canOpenOperationalHandoff={canOpenOperationalHandoff}
          />
        </>
      ) : (
        <FacilitiesWorkflowOverview workspace={workspace} />
      )}

      {operationalHandoff && !useMaintenanceWorkspace ? (
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
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Briefcase className="h-5 w-5 text-primary" />
              <CardTitle>Service Provider Operations</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="mb-3 text-sm font-medium">
                Provider Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-3 xl:grid-cols-7">
                {serviceProviderLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index < serviceProviderLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {serviceProviderGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">Approved Supplier</Badge>
              <Badge variant="secondary">Active Contract</Badge>
              <Badge variant="secondary">Compliant</Badge>
              <Badge variant="outline">Available</Badge>
              <Badge variant="outline">Performance Reviewed</Badge>
              <Badge variant="outline">Finance AP Referenced</Badge>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {showMaintenanceIntakeRegister && !useMaintenanceWorkspace ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Wrench className="h-5 w-5 text-primary" />
              <CardTitle>Maintenance Intake</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="mb-3 text-sm font-medium">
                Maintenance Intake Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-4 xl:grid-cols-8">
                {maintenanceIntakeLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index < maintenanceIntakeLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {maintenanceIntakeGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">Source: Estate / Facilities</Badge>
              <Badge variant="secondary">Sent To Maintenance</Badge>
              <Badge variant="secondary">Job Card Linked</Badge>
              <Badge variant="outline">Work Order Linked</Badge>
              <Badge variant="outline">Requester Feedback</Badge>
              <Badge variant="outline">Facilities Closeout</Badge>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {showComplaintIntakeRegister ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <MessageSquare className="h-5 w-5 text-primary" />
              <CardTitle>Complaint Management</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="mb-3 text-sm font-medium">
                Complaint Intake Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-4 xl:grid-cols-8">
                {complaintIntakeLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index < complaintIntakeLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {complaintIntakeGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">Source: Estate / Facilities</Badge>
              <Badge variant="secondary">Sent To Helpdesk</Badge>
              <Badge variant="secondary">Ticket Linked</Badge>
              <Badge variant="outline">Linked Action</Badge>
              <Badge variant="outline">Feedback Received</Badge>
              <Badge variant="outline">Facilities Closeout</Badge>
            </div>
          </CardContent>
        </Card>
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
            <div>
              <div className="mb-3 text-sm font-medium">
                Staff Duty Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-4 xl:grid-cols-8">
                {staffCleanerLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index < staffCleanerLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {staffCleanerGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">
                {'Source: HR / Payroll -> Estate / Facilities'}
              </Badge>
              <Badge variant="secondary">User Link Checked</Badge>
              <Badge variant="secondary">Roster Assigned</Badge>
              <Badge variant="secondary">Tools / Access Ready</Badge>
              <Badge variant="outline">Late</Badge>
              <Badge variant="outline">Absent</Badge>
              <Badge variant="outline">Replacement Coverage</Badge>
              <Badge variant="outline">Quality Follow-up</Badge>
              <Badge variant="outline">HR Escalation</Badge>
            </div>
            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3">
                <div className="font-medium">
                  Cleaner Timetable / Duty Roster
                </div>
                <p className="text-sm text-muted-foreground">
                  Assign cleaners or Facilities staff to apartments, units,
                  floors, blocks, routes, or common areas. HR remains the
                  employee source; Maintenance and Helpdesk are linked by
                  reference when a duty creates a repair or complaint follow-up.
                </p>
              </div>
              <form
                className="grid gap-3 md:grid-cols-2 xl:grid-cols-4"
                onSubmit={createDutyRosterItem}
              >
                <div className="space-y-1">
                  <Label htmlFor="duty-staff-name">Cleaner / staff name</Label>
                  <Input
                    id="duty-staff-name"
                    value={dutyRosterForm.staffName}
                    onChange={(event) =>
                      updateDutyRosterForm('staffName', event.target.value)
                    }
                    placeholder="Name from HR"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-employee-number">Employee no.</Label>
                  <Input
                    id="duty-employee-number"
                    value={dutyRosterForm.employeeNumber || ''}
                    onChange={(event) =>
                      updateDutyRosterForm('employeeNumber', event.target.value)
                    }
                    placeholder="Optional HR reference"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-property">Property / site</Label>
                  <Input
                    id="duty-property"
                    value={dutyRosterForm.propertyReference || ''}
                    onChange={(event) =>
                      updateDutyRosterForm(
                        'propertyReference',
                        event.target.value
                      )
                    }
                    placeholder="Estate, block, or property"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="duty-unit">Apartment / unit</Label>
                  <Input
                    id="duty-unit"
                    value={dutyRosterForm.propertyUnit || ''}
                    onChange={(event) =>
                      updateDutyRosterForm('propertyUnit', event.target.value)
                    }
                    placeholder="Apartment, unit, floor, route"
                  />
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
                  <Input
                    id="duty-frequency"
                    value={dutyRosterForm.frequency}
                    onChange={(event) =>
                      updateDutyRosterForm('frequency', event.target.value)
                    }
                    placeholder="Daily, weekly, one-off"
                  />
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
                    {isSavingDutyRoster ? 'Saving duty...' : 'Add duty roster'}
                  </Button>
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
                  <div className="font-medium">Active roster assignments</div>
                  <p className="text-sm text-muted-foreground">
                    {isLoadingDutyRoster
                      ? 'Loading roster...'
                      : `${dutyRosterItems.length} duty assignment(s)`}
                  </p>
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
                          onClick={() => void markDutyCompleted(item)}
                          disabled={item.completionStatus === 'Completed'}
                        >
                          Mark present / completed
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

      {showAssetOperationsRegister ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Database className="h-5 w-5 text-primary" />
              <CardTitle>Facilities Asset Operating View</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="mb-3 text-sm font-medium">
                Asset Operating View Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-4 xl:grid-cols-8">
                {assetOperationsLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index < assetOperationsLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {assetOperationsGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">
                {
                  'Source: Project Management / Finance Fixed Assets / Maintenance -> Estate / Facilities'
                }
              </Badge>
              <Badge variant="secondary">Custodian Assigned</Badge>
              <Badge variant="secondary">Under Warranty</Badge>
              <Badge variant="outline">Needs Inspection</Badge>
              <Badge variant="outline">Under Maintenance</Badge>
              <Badge variant="outline">Service Impact</Badge>
              <Badge variant="outline">Finance Action</Badge>
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
            <div>
              <div className="mb-3 text-sm font-medium">
                Billing / Service Charge Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-4 xl:grid-cols-8">
                {billingServiceChargeLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index <
                      billingServiceChargeLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {billingServiceChargeGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">
                {'Source: Estate / Facilities -> Finance AR'}
              </Badge>
              <Badge variant="secondary">Service Charge</Badge>
              <Badge variant="secondary">AR Instruction</Badge>
              <Badge variant="outline">Invoice Reference</Badge>
              <Badge variant="outline">Receipt Reference</Badge>
              <Badge variant="outline">Statement Reference</Badge>
              <Badge variant="outline">Arrears Follow-up</Badge>
              <Badge variant="outline">Central DMS</Badge>
            </div>
            <form
              className="rounded-md border border-border bg-background p-4"
              onSubmit={createArInvoice}
            >
              <div className="mb-4">
                <div className="font-medium">Create Finance AR Invoice</div>
                <p className="mt-1 text-sm text-muted-foreground">
                  Create a draft invoice through the existing Finance AR API.
                  Finance AR remains responsible for posting, receipts,
                  allocation, balances, and GL.
                </p>
              </div>
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <div className="space-y-2">
                  <Label htmlFor="ar-customer-id">Finance AR customer ID</Label>
                  <Input
                    id="ar-customer-id"
                    value={arInvoiceForm.customerId}
                    onChange={(event) =>
                      updateArInvoiceForm('customerId', event.target.value)
                    }
                    placeholder="Customer GUID from Finance AR"
                  />
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
                    placeholder="Source: Estate / Facilities -> Finance AR"
                  />
                </div>
              </div>
              {arInvoiceError ? (
                <div className="mt-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                  {arInvoiceError}
                </div>
              ) : null}
              {arInvoiceResult ? (
                <div className="mt-3 flex flex-col gap-2 rounded-md border border-emerald-500/30 bg-emerald-500/10 p-3 text-sm text-emerald-700 dark:text-emerald-300 sm:flex-row sm:items-center sm:justify-between">
                  <span>
                    Finance AR invoice {arInvoiceResult.invoiceNumber} created
                    with status {arInvoiceResult.status}.
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
                  {'Source: Estate / Facilities -> Finance AR'}
                </Badge>
              </div>
            </form>
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
                    placeholder="Source: Estate / Facilities -> Finance AR"
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
                  {'Source: Estate / Facilities -> Finance AR'}
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
                    Published as {billingDmsPublication.documentReference} /{' '}
                    {billingDmsPublication.sourceLabel}
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
                  {'Source: Estate / Facilities -> Finance AR -> Central DMS'}
                </Badge>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : null}

      {showDocumentIndexRegister ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <FileText className="h-5 w-5 text-primary" />
              <CardTitle>Facilities Document Index & DMS Readiness</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="mb-3 text-sm font-medium">
                Document Index Lifecycle Chain
              </div>
              <div className="grid gap-2 md:grid-cols-4 xl:grid-cols-8">
                {documentIndexLifecycleStates.map((state, index) => (
                  <div
                    key={state}
                    className="rounded-md border border-border bg-background p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <Badge variant="outline">{index + 1}</Badge>
                      {index < documentIndexLifecycleStates.length - 1 ? (
                        <ArrowRight className="h-4 w-4 text-muted-foreground" />
                      ) : null}
                    </div>
                    <div className="mt-3 text-sm font-medium">{state}</div>
                  </div>
                ))}
              </div>
            </div>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              {documentIndexGates.map((gate) => (
                <div
                  key={gate.title}
                  className="rounded-md border border-border bg-background p-4"
                >
                  <div className="font-medium">{gate.title}</div>
                </div>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="default">Indexed</Badge>
              <Badge variant="secondary">Metadata Template</Badge>
              <Badge variant="secondary">Expiry Tracked</Badge>
              <Badge variant="secondary">Restricted Access</Badge>
              <Badge variant="outline">Comments Ready</Badge>
              <Badge variant="outline">Future Versioning</Badge>
              <Badge variant="outline">PDF Viewer Annotation</Badge>
              <Badge variant="outline">Central DMS Ready</Badge>
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
