export const PROPERTY_MANAGEMENT_PERMISSIONS = [
  {
    id: 'property-management.access',
    name: 'Access Property Management',
    description:
      'Access Estate / Property Management workspaces and navigation.',
  },
  {
    id: 'property-management.dashboard.read',
    name: 'View Property Dashboard',
    description:
      'View Estate / Property Management operating dashboard and handoff status.',
  },
  {
    id: 'property-management.case.read',
    name: 'View Property Cases',
    description:
      'View Property Management procedure cases, stages, documents, and activity.',
  },
  {
    id: 'property-management.case.create',
    name: 'Create Property Cases',
    description:
      'Open new Property Management procedure cases and intake records.',
  },
  {
    id: 'property-management.case.update',
    name: 'Update Property Cases',
    description:
      'Update Property Management intake fields, checklists, documents, and notes.',
  },
  {
    id: 'property-management.case.approve',
    name: 'Approve Property Cases',
    description:
      'Approve Property Management stages, exceptions, publishing, and close-out decisions.',
  },
  {
    id: 'property-management.handoff.create',
    name: 'Create Property Handoffs',
    description:
      'Create downstream Project, Legal, Finance AR, Facilities, Maintenance, Helpdesk, Reports, or document handoffs from Property Management.',
  },
  {
    id: 'property-management.units.manage',
    name: 'Manage Property And Units',
    description:
      'Manage received property, site, unit, space, availability, readiness, and Project Management handoff context.',
  },
  {
    id: 'property-management.leases.manage',
    name: 'Manage Property Leases',
    description:
      'Manage lease operations, renewal tracking, termination context, and Legal / Finance AR lease handoff readiness.',
  },
  {
    id: 'property-management.occupancy.manage',
    name: 'Manage Occupancy And Availability',
    description:
      'Manage tenant / occupant links, occupancy status, availability state, restrictions, and unit release decisions.',
  },
  {
    id: 'property-management.handover.manage',
    name: 'Manage Handover Operations',
    description:
      'Manage move-in, move-out, transfer, keys, access, inspection, clearance, and possession handover records.',
  },
  {
    id: 'property-management.documents.manage',
    name: 'Manage Property Records Index',
    description:
      'Index Property Management documents, classify module metadata, track access and retention, and reference Central DMS records.',
  },
  {
    id: 'property-management.billing.view',
    name: 'View Property Billing Context',
    description:
      'View Property Management billing context, service charge instructions, arrears follow-up, and Finance AR handoff readiness without replacing Finance permissions.',
  },
  {
    id: 'property-management.portal.access',
    name: 'Use Property Portal',
    description:
      'Access tenant/client Property Management self-service views for occupancy, handover, documents, invoice status, uploads, and feedback.',
  },
] as const;

export const PROPERTY_MANAGEMENT_ROLE_MATRIX = [
  {
    role: 'PropertyManager',
    label: 'Property Manager',
    purpose:
      'Owns Property Management approvals, dashboards, occupancy decisions, lease operations, handover governance, records health, and close-out control.',
    permissions: [
      'property-management.access',
      'property-management.dashboard.read',
      'property-management.case.read',
      'property-management.case.create',
      'property-management.case.update',
      'property-management.case.approve',
      'property-management.handoff.create',
      'property-management.units.manage',
      'property-management.leases.manage',
      'property-management.occupancy.manage',
      'property-management.handover.manage',
      'property-management.documents.manage',
      'property-management.billing.view',
    ],
    moduleBoundary:
      'Needs separate target-module permissions for Project Management, Legal, Finance AR, Facilities, Maintenance, Helpdesk, Reports, or Central Document Management execution.',
  },
  {
    role: 'PropertySupervisor',
    label: 'Property Supervisor',
    purpose:
      'Reviews property cases, validates occupancy and availability changes, monitors handover quality, and supervises operational follow-up.',
    permissions: [
      'property-management.access',
      'property-management.dashboard.read',
      'property-management.case.read',
      'property-management.case.create',
      'property-management.case.update',
      'property-management.handoff.create',
      'property-management.units.manage',
      'property-management.occupancy.manage',
      'property-management.handover.manage',
      'property-management.billing.view',
    ],
    moduleBoundary:
      'Can validate and route Property Management work, but final approvals and target-module execution still require the relevant manager or owning-module permissions.',
  },
  {
    role: 'PropertyOfficer',
    label: 'Property Officer',
    purpose:
      'Handles day-to-day property intake, unit updates, tenant/occupant context, handover notes, availability changes, and case follow-up.',
    permissions: [
      'property-management.access',
      'property-management.case.read',
      'property-management.case.create',
      'property-management.case.update',
      'property-management.handoff.create',
      'property-management.units.manage',
      'property-management.occupancy.manage',
      'property-management.handover.manage',
    ],
    moduleBoundary:
      'Can prepare and update Property Management records but cannot approve exceptions or operate Finance, Legal, Maintenance, Facilities, or DMS records without additional permissions.',
  },
  {
    role: 'PropertyLeaseOfficer',
    label: 'Property Lease Officer',
    purpose:
      'Manages lease operation context, renewal and termination tracking, tenant lease links, Legal handoff readiness, and Finance AR billing handoff readiness.',
    permissions: [
      'property-management.access',
      'property-management.dashboard.read',
      'property-management.case.read',
      'property-management.case.create',
      'property-management.case.update',
      'property-management.handoff.create',
      'property-management.leases.manage',
      'property-management.billing.view',
    ],
    moduleBoundary:
      'Lease operation access does not grant Legal drafting/vetting authority or Finance AR invoice, receipt, allocation, statement, or GL authority.',
  },
  {
    role: 'PropertyBillingOfficer',
    label: 'Property Billing Officer',
    purpose:
      'Reviews billing context, service charge allocation, deposits, arrears follow-up, disputes, and Finance AR handoff readiness.',
    permissions: [
      'property-management.access',
      'property-management.dashboard.read',
      'property-management.case.read',
      'property-management.case.create',
      'property-management.case.update',
      'property-management.handoff.create',
      'property-management.billing.view',
    ],
    moduleBoundary:
      'Requires Finance AR permissions to create invoices, receipts, customer accounts, allocations, statements, balances, or GL postings.',
  },
  {
    role: 'PropertyRecordsOfficer',
    label: 'Property Records Officer',
    purpose:
      'Owns Property Management records indexing, metadata templates, access classification, retention review, and central DMS reference readiness.',
    permissions: [
      'property-management.access',
      'property-management.case.read',
      'property-management.case.update',
      'property-management.documents.manage',
    ],
    moduleBoundary:
      'Records index access does not grant source-module approval authority or Central Document Management repository, versioning, annotation, or comment authority.',
  },
  {
    role: 'PropertyHandoverOfficer',
    label: 'Property Handover Officer',
    purpose:
      'Coordinates move-in, move-out, transfer, keys, access, condition inspection, possession evidence, and linked occupancy or billing updates.',
    permissions: [
      'property-management.access',
      'property-management.case.read',
      'property-management.case.create',
      'property-management.case.update',
      'property-management.handoff.create',
      'property-management.handover.manage',
      'property-management.occupancy.manage',
    ],
    moduleBoundary:
      'Can coordinate handover evidence and status updates, but Finance clearance, maintenance execution, and legal release documents remain with their owning modules.',
  },
  {
    role: 'PropertyPortalUser',
    label: 'Tenant / Occupant Portal User',
    purpose:
      'External self-service access for occupancy profile, handover appointments, document references, billing status, uploads, and feedback.',
    permissions: ['property-management.portal.access'],
    moduleBoundary:
      'External users should not receive internal Property Management dashboard, approval, document-control, billing-context, or handoff permissions.',
  },
] as const;
