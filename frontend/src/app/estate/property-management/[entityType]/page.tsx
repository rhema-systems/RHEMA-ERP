'use client';

import Link from 'next/link';
import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  ArrowRight,
  Building2,
  ClipboardList,
  CreditCard,
  ExternalLink,
  FileSignature,
  FileText,
  Home,
  KeyRound,
  Loader2,
  Send,
  Users,
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
import { Separator } from '@/components/ui/separator';
import { estatePropertyManagementService } from '@/services/estate-property-management.service';
import type { FacilitiesProcedureWorkspace } from '@/services/estate-facilities.service';

type PropertyHandoff = {
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

const billingOwnershipRows = [
  {
    label: 'Property Management owns',
    detail:
      'Billing instruction package, service charge allocation, property/unit/occupant context, arrears follow-up, payment promises, disputes, and returned corrections.',
  },
  {
    label: 'Finance AR owns',
    detail:
      'Customer accounts, invoices, receipts, allocations, statements, balances, GL postings, and accounting audit trail.',
  },
  {
    label: 'Correction loop',
    detail:
      'Finance AR returns incomplete or disputed instructions to Property Management; corrected instructions are resubmitted before posting.',
  },
  {
    label: 'Source label',
    detail:
      'Every downstream Finance AR handoff should state Source: Estate / Property Management.',
  },
];

const leaseOwnershipRows = [
  {
    label: 'Property Management owns',
    detail:
      'Lease instruction package, unit readiness, tenant link, commercial terms, occupancy impact, renewal tracking, and handoff coordination.',
  },
  {
    label: 'Legal owns',
    detail:
      'Lease instrument drafting, vetting, execution, renewals, variations, terminations, consents, and legal document authority.',
  },
  {
    label: 'Finance AR owns',
    detail:
      'Customer accounts, invoices, receipts, allocations, statements, arrears balances, deposits, and GL posting.',
  },
  {
    label: 'Operational updates',
    detail:
      'Executed or approved leases trigger Occupancy / Availability, Move-in / Move-out / Handover, Billing, and Property Records updates.',
  },
];

const tenantOccupantOwnershipRows = [
  {
    label: 'Property Management owns',
    detail:
      'Occupant profile, unit link, lease context, access/contact instructions, service context, occupancy events, and closure follow-up.',
  },
  {
    label: 'Source systems own',
    detail:
      'CRM, Finance AR Customers, Business Partner, or Tenant Administration remain the source systems for identity and account records.',
  },
  {
    label: 'Finance AR owns',
    detail:
      'Customer account, invoices, receipts, deposits, arrears balances, statements, allocations, and GL postings.',
  },
  {
    label: 'Service execution',
    detail:
      'Complaints, maintenance, helpdesk tickets, and facilities work are linked here, but executed by their owning modules.',
  },
];

const occupancyAvailabilityOwnershipRows = [
  {
    label: 'Property Management owns',
    detail:
      'Current unit status, requested status, leasing visibility, holds, releases, exception notes, review dates, and the approved availability audit trail.',
  },
  {
    label: 'Lease and tenant events',
    detail:
      'Lease approvals, reservations, expiries, terminations, tenant move-in, move-out, transfers, and access events trigger status changes here.',
  },
  {
    label: 'Facilities and Maintenance execute',
    detail:
      'Maintenance blocks, defects, safety restrictions, access issues, and releases are linked here, but executed by Facilities or Maintenance Management.',
  },
  {
    label: 'Billing impact is routed',
    detail:
      'Billing starts, stops, holds, adjustments, arrears reviews, deposits, and final accounts are routed to Billing / Service Charge and Finance AR.',
  },
];

const handoverOwnershipRows = [
  {
    label: 'Property Management owns',
    detail:
      'Handover scheduling, attendance, possession status, keys, access items, inspection evidence, signatures, and closure approval.',
  },
  {
    label: 'Billing impact is routed',
    detail:
      'Billing start, stop, deposit, deductions, chargebacks, receipts, arrears, and final accounts are routed to Billing / Service Charge and Finance AR.',
  },
  {
    label: 'Facilities and Maintenance execute',
    detail:
      'Defects, inspections, safety issues, repairs, post-maintenance releases, and service readiness are linked here but executed by Facilities or Maintenance Management.',
  },
  {
    label: 'Linked records are updated',
    detail:
      'Occupancy / Availability, Tenant / Occupant, Lease Management, Billing, Property Records, and Central DMS receive handover outcomes.',
  },
];

const documentIndexOwnershipRows = [
  {
    label: 'Property Management owns',
    detail:
      'Records index entries, source classification, module metadata, linked property operations, access context, retention, lifecycle, and review follow-up.',
  },
  {
    label: 'Central DMS owns',
    detail:
      'File storage, repository permissions, versioning, PDF viewer annotations, comments, renditions, and document collaboration controls.',
  },
  {
    label: 'Metadata is module-defined',
    detail:
      'Property, unit, lease, tenant, billing, occupancy, handover, maintenance, complaint, Legal, and Project documents use the right template.',
  },
  {
    label: 'Source is always clear',
    detail:
      'Every index entry states Source: Estate / Property Management and links back to the source module or operation that produced the document.',
  },
];

const propertyUnitOwnershipRows = [
  {
    label: 'Estate owns land bank',
    detail:
      'Estate Land Management prepares and marks the land source ready before Project Management pulls it for development.',
  },
  {
    label: 'Project Management creates',
    detail:
      'Project Management develops the property structure, units, spaces, common areas, completion status, and readiness package.',
  },
  {
    label: 'Property Management receives',
    detail:
      'Property Management validates identifiers, hierarchy, readiness, condition, availability, facilities, billing context, and records links.',
  },
  {
    label: 'Release controls downstream',
    detail:
      'Only approved releases move into leasing, occupancy, billing, facilities, maintenance, complaints, reports, and records operations.',
  },
];

function getPropertyHandoff(entityType: string): PropertyHandoff {
  if (entityType === 'EstatePropertyManagementDocumentRecordIndex') {
    return {
      title: 'Property Documents / Records Index Handoff',
      description:
        'Property Management owns the operational records index, source classification, module metadata, document relationships, access context, retention, lifecycle, review status, and DMS references; the central DMS owns file storage, repository permissions, versioning, PDF viewer annotations, comments, renditions, and collaboration control.',
      sourceLabel:
        'Source: Estate / Property Management - Property Documents / Records Index',
      icon: FileText,
      primaryAction: {
        label: 'Open Central DMS',
        href: '/document-management',
      },
      secondaryActions: [
        {
          label: 'Property Register',
          href: '/estate/property-management/EstatePropertyManagementPropertyUnit',
        },
        {
          label: 'Lease Workspace',
          href: '/estate/property-management/EstatePropertyManagementLease',
        },
        {
          label: 'Tenant / Occupant',
          href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
        },
        {
          label: 'Billing Workspace',
          href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
        },
        {
          label: 'Occupancy / Availability',
          href: '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
        },
        {
          label: 'Handover Workspace',
          href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
        },
        {
          label: 'Facilities Documents',
          href: '/estate/facilities/EstateFacilityDocument',
        },
        {
          label: 'DMS Metadata Templates',
          href: '/document-management/CentralDocumentMetadataTemplate',
        },
      ],
      checkpoints: [
        'Use this workspace for Property Management document source classification, metadata, relationships, access, retention, lifecycle, and DMS reference tracking.',
        'Define metadata by module and document type so Central DMS can classify, search, secure, and retrieve documents correctly.',
        'Use Central DMS for file storage, repository permissions, versioning, PDF viewer annotations, comments, and renditions.',
        'Link documents back to property, unit, lease, tenant, billing, occupancy, handover, maintenance, complaint, Legal, Finance AR, or Project operations.',
        'Track DMS link gaps, metadata exceptions, open comments, unresolved annotations, superseded versions, and restricted records without duplicating the file.',
      ],
    };
  }

  if (entityType === 'EstatePropertyManagementMoveInMoveOutHandover') {
    return {
      title: 'Move-in / Move-out / Handover Handoff',
      description:
        'Property Management controls scheduling, possession, keys, access, condition inspection, clearance checks, defects, signatures, and signed handover evidence; Tenant / Occupant, Occupancy / Availability, Billing, Lease, Facilities, Maintenance, and Records workspaces receive the resulting updates.',
      sourceLabel:
        'Source: Estate / Property Management - Move-in / Move-out / Handover Operations',
      icon: KeyRound,
      primaryAction: {
        label: 'Open Tenant / Occupant',
        href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
      },
      secondaryActions: [
        {
          label: 'Occupancy / Availability',
          href: '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
        },
        {
          label: 'Billing Workspace',
          href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
        },
        {
          label: 'Lease Workspace',
          href: '/estate/property-management/EstatePropertyManagementLease',
        },
        {
          label: 'Facilities Maintenance',
          href: '/estate/facilities/EstateFacilityMaintenance',
        },
        { label: 'Maintenance Work Orders', href: '/maintenance/work-orders' },
        {
          label: 'Records Index',
          href: '/estate/property-management/EstatePropertyManagementDocumentRecordIndex',
        },
      ],
      checkpoints: [
        'Use this workspace for scheduling, attendance, possession, keys, access, inspection, meter readings, signatures, photos, and handover evidence.',
        'Confirm readiness, Finance AR clearance, maintenance restrictions, access items, and lease or termination authority before possession changes.',
        'Update Tenant / Occupant Operations with move-in, move-out, transfer, or access changes.',
        'Update Occupancy / Availability when possession is issued, returned, blocked, or released.',
        'Route billing start, stop, deposit, deduction, final account, arrears, receipt, or clearance actions to Billing / Finance AR.',
        'Route defects, safety issues, repairs, and post-maintenance release actions to Facilities or Maintenance Management.',
      ],
    };
  }

  if (entityType === 'EstatePropertyManagementOccupancyAvailability') {
    return {
      title: 'Occupancy / Availability Handoff',
      description:
        'Property Management owns the unit status source for availability, reservation, occupation, maintenance block, management hold, release, closure, and leasing visibility; Lease, Tenant / Occupant, Billing, Facilities, and Maintenance workspaces supply the events that change that status.',
      sourceLabel:
        'Source: Estate / Property Management - Occupancy / Availability Operations',
      icon: Home,
      primaryAction: {
        label: 'Open Property Register',
        href: '/estate/property-management/EstatePropertyManagementPropertyUnit',
      },
      secondaryActions: [
        {
          label: 'Lease Workspace',
          href: '/estate/property-management/EstatePropertyManagementLease',
        },
        {
          label: 'Tenant / Occupant',
          href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
        },
        {
          label: 'Billing Workspace',
          href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
        },
        {
          label: 'Handover Workspace',
          href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
        },
        {
          label: 'Facilities Maintenance',
          href: '/estate/facilities/EstateFacilityMaintenance',
        },
      ],
      checkpoints: [
        'Use Property Management as the source of unit availability and occupancy status.',
        'Validate current unit, lease, occupant, maintenance, complaint, billing, and document restrictions before status changes.',
        'Publish only approved availability to leasing views, dashboards, and operational reports.',
        'Route billing start, stop, hold, adjustment, deposit, arrears, or final-account impact to Billing / Service Charge and Finance AR.',
        'Use Facilities or Maintenance Management to place units under maintenance, block them, and release them back to Property Management.',
      ],
    };
  }

  if (entityType === 'EstatePropertyManagementBillingServiceCharge') {
    return {
      title: 'Billing / Service Charge Handoff',
      description:
        'Property Management prepares the approved billing instruction package, service charge allocation, deposit, arrears follow-up, dispute evidence, and correction response; Finance AR owns customer accounts, invoices, receipts, allocations, statements, balances, and GL postings.',
      sourceLabel: 'Source: Estate / Property Management -> Finance AR',
      icon: CreditCard,
      primaryAction: {
        label: 'Create AR Invoice',
        href: '/finance/ar/invoices/new?source=estate-property-management',
      },
      secondaryActions: [
        { label: 'AR Customers', href: '/finance/ar/customers' },
        { label: 'AR Invoices', href: '/finance/ar/invoices' },
        { label: 'AR Payments', href: '/finance/ar/payments' },
        {
          label: 'Record Receipt',
          href: '/finance/cash/transactions/receipts',
        },
        {
          label: 'Lease Workspace',
          href: '/estate/property-management/EstatePropertyManagementLease',
        },
        {
          label: 'Tenant / Occupant',
          href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
        },
      ],
      checkpoints: [
        'Verify property, unit, occupant, and lease context before sending the billing instruction.',
        'Use Property Management for why, where, who to bill, service charge allocation, arrears follow-up, payment promises, disputes, and returned corrections.',
        'Use Finance AR for invoices, receipts, allocations, statements, balances, and GL postings.',
        'Return disputed, incomplete, or incorrect billing instructions to Property Management before Finance correction or posting.',
      ],
    };
  }

  if (entityType === 'EstatePropertyManagementLease') {
    return {
      title: 'Lease Operations Handoff',
      description:
        'Property Management manages the lease instruction package, tenant/customer link, commercial terms, renewal tracking, and operational handoffs; Legal owns lease instruments and Finance AR owns billing, receipts, arrears, customer accounts, and postings.',
      sourceLabel: 'Source: Estate / Property Management - Lease Management',
      icon: FileSignature,
      primaryAction: {
        label: 'Open Legal Lease Procedure',
        href: '/legal/LegalLeaseVariationRenewalSublease',
      },
      secondaryActions: [
        {
          label: 'Property Register',
          href: '/estate/property-management/EstatePropertyManagementPropertyUnit',
        },
        {
          label: 'Tenant / Occupant',
          href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
        },
        {
          label: 'Billing Workspace',
          href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
        },
        {
          label: 'Occupancy / Availability',
          href: '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
        },
        {
          label: 'Handover Workspace',
          href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
        },
        { label: 'AR Customers', href: '/finance/ar/customers' },
        { label: 'AR Invoices', href: '/finance/ar/invoices' },
      ],
      checkpoints: [
        'Select only units released as Active for Leasing.',
        'Link the tenant / occupant and Finance AR customer context before Legal or billing handoff.',
        'Route drafting, renewal, variation, termination, consent, and sublease documents through Legal.',
        'Send rent, deposit, service charge, billing cycle, invoice, receipt, and arrears work through Billing / Finance AR.',
        'Update occupancy, handover, and document index records after approval, execution, expiry, or termination.',
      ],
    };
  }

  if (entityType === 'EstatePropertyManagementTenantOccupant') {
    return {
      title: 'Tenant / Occupant Source Handoff',
      description:
        'Property Management manages the occupant operating profile, unit link, lease context, access, contact instructions, occupancy events, service context, billing status, and closure follow-up; CRM, Finance AR Customers, Business Partner, and Tenant Administration remain the source systems for identity and account records.',
      sourceLabel:
        'Source: CRM / Finance AR / Tenant Administration -> Estate / Property Management',
      icon: Users,
      primaryAction: {
        label: 'Open AR Customers',
        href: '/finance/ar/customers',
      },
      secondaryActions: [
        {
          label: 'Tenant Administration',
          href: '/administration/tenant-management',
        },
        {
          label: 'Lease Workspace',
          href: '/estate/property-management/EstatePropertyManagementLease',
        },
        {
          label: 'Property Register',
          href: '/estate/property-management/EstatePropertyManagementPropertyUnit',
        },
        {
          label: 'Occupancy / Availability',
          href: '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
        },
        {
          label: 'Handover Workspace',
          href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
        },
        {
          label: 'Billing Workspace',
          href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
        },
        {
          label: 'Facilities Complaints',
          href: '/estate/facilities/EstateFacilityComplaint',
        },
        { label: 'Maintenance', href: '/maintenance/work-orders' },
      ],
      checkpoints: [
        'Use CRM, Finance AR customer, Business Partner, or Tenant Administration as the tenant / occupant source.',
        'Use Property Management for occupancy status, access status, unit link, service context, operational contact details, and permitted users.',
        'Use Legal for lease instruments, renewals, variations, terminations, and consent documents.',
        'Use Finance AR for customer account, invoices, receipts, arrears, deposits, statements, payment promises, and closure balances.',
        'Use Facilities, Maintenance, or Helpdesk for complaint and maintenance execution while keeping occupant context linked here.',
      ],
    };
  }

  return {
    title: 'Project Management To Property Management Handoff',
    description:
      'Estate land bank records are made ready under Estate, Project Management pulls them for development, then pushes completed properties, sites, units, spaces, common areas, and service areas into Property Management for receiving, readiness, availability, billing, facilities, maintenance, records, and operational release.',
    sourceLabel:
      'Source: Estate Land Bank -> Project Management -> Estate / Property Management',
    icon: Building2,
    primaryAction: {
      label: 'Open Project Management',
      href: '/development/projects',
    },
    secondaryActions: [
      { label: 'Project Operations', href: '/development/project-operations' },
      { label: 'Land Management', href: '/estate/land-management' },
      { label: 'Facilities Management', href: '/estate/facilities' },
    ],
    checkpoints: [
      'Use Estate Land Management to mark demarcated land bank records ready for Project Management.',
      'Create or structurally update property and unit records in Project Management from that land source.',
      'Push completed property, site, unit, space, common area, service area, readiness, and completion records into Property Management.',
      'Validate identifiers, hierarchy, readiness, condition, utilities, access, facilities, billing setup, availability, and records links before release.',
      'Release only clean or approved-exception units as Active for Leasing, Active for Operations, Blocked, Under Maintenance, Not Ready, or Closed.',
    ],
  };
}

export default function PropertyManagementWorkspacePage() {
  const router = useRouter();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType)
    ? params.entityType[0]
    : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [workspace, setWorkspace] =
    React.useState<FacilitiesProcedureWorkspace | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

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
          await estatePropertyManagementService.getProcedureWorkspace(
            entityType
          );
        if (mounted) {
          setWorkspace(data);
          setLoadError(data ? null : 'Workspace was not returned by the API.');
        }
      } catch {
        if (mounted) {
          setWorkspace(null);
          setLoadError('Unable to load workspace from the API.');
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

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading property management workspace
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
          onClick={() => router.push('/estate/property-management')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Property Management
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
  const handoff = getPropertyHandoff(procedure.entityType);
  const HandoffIcon = handoff.icon;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/estate/property-management')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Property Management
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

      <ProcedureCaseWorkspace
        module="PropertyManagement"
        entityType={procedure.entityType}
        defaultTitle={procedure.title}
      />

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div className="flex items-start gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                <HandoffIcon className="h-5 w-5 text-primary" />
              </div>
              <div>
                <CardTitle>{handoff.title}</CardTitle>
                <Badge variant="outline" className="mt-3 w-fit">
                  {handoff.sourceLabel}
                </Badge>
              </div>
            </div>
            <Button asChild>
              <Link href={handoff.primaryAction.href}>
                {handoff.primaryAction.label}
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            {handoff.secondaryActions.map((action) => (
              <Button key={action.href} asChild variant="outline" size="sm">
                <Link href={action.href}>
                  {action.label}
                  <ExternalLink className="ml-2 h-3.5 w-3.5" />
                </Link>
              </Button>
            ))}
          </div>
        </CardContent>
      </Card>

      {procedure.entityType ===
      'EstatePropertyManagementBillingServiceCharge' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <CreditCard className="h-5 w-5 text-primary" />
              <CardTitle>Finance AR Handoff Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {billingOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {procedure.entityType === 'EstatePropertyManagementLease' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <FileSignature className="h-5 w-5 text-primary" />
              <CardTitle>Lease Lifecycle Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {leaseOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {procedure.entityType === 'EstatePropertyManagementTenantOccupant' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Users className="h-5 w-5 text-primary" />
              <CardTitle>Tenant / Occupant Lifecycle Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {tenantOccupantOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {procedure.entityType ===
      'EstatePropertyManagementOccupancyAvailability' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Home className="h-5 w-5 text-primary" />
              <CardTitle>Occupancy / Availability Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {occupancyAvailabilityOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {procedure.entityType ===
      'EstatePropertyManagementMoveInMoveOutHandover' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <KeyRound className="h-5 w-5 text-primary" />
              <CardTitle>Move-in / Move-out / Handover Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {handoverOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {procedure.entityType ===
      'EstatePropertyManagementDocumentRecordIndex' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <FileText className="h-5 w-5 text-primary" />
              <CardTitle>Property Records / DMS Readiness Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {documentIndexOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {procedure.entityType === 'EstatePropertyManagementPropertyUnit' ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Building2 className="h-5 w-5 text-primary" />
              <CardTitle>Property And Unit Receiving Controls</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {propertyUnitOwnershipRows.map((row) => (
              <div
                key={row.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="font-medium">{row.label}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
