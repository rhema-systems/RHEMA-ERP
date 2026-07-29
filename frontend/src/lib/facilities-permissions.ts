export const FACILITIES_PERMISSIONS = [
  {
    id: 'facilities.access',
    name: 'Access Facilities',
    description: 'Access Estate / Facilities workspaces and navigation.',
  },
  {
    id: 'facilities.dashboard.read',
    name: 'View Facilities Dashboard',
    description:
      'View Estate / Facilities operating dashboard and handoff status.',
  },
  {
    id: 'facilities.case.read',
    name: 'View Facilities Cases',
    description:
      'View Facilities procedure cases, stages, documents, and activity.',
  },
  {
    id: 'facilities.case.create',
    name: 'Create Facilities Cases',
    description: 'Open new Facilities procedure cases and intake records.',
  },
  {
    id: 'facilities.case.update',
    name: 'Update Facilities Cases',
    description:
      'Update Facilities intake fields, checklists, documents, and notes.',
  },
  {
    id: 'facilities.case.approve',
    name: 'Approve Facilities Cases',
    description:
      'Approve Facilities stages, exceptions, publishing, and close-out decisions.',
  },
  {
    id: 'facilities.handoff.create',
    name: 'Create Facilities Handoffs',
    description:
      'Create downstream Project, Legal, Procurement, HR, Finance, Maintenance, Helpdesk, Inventory, Reports, or document handoffs from Facilities.',
  },
  {
    id: 'facilities.documents.manage',
    name: 'Manage Facilities Document Index & DMS Readiness',
    description:
      'Index source-module documents, classify access, track expiry, and flag Central Document Management migration needs.',
  },
  {
    id: 'facilities.providers.manage',
    name: 'Manage Facilities Providers',
    description:
      'Manage Facilities service provider profiles, compliance, contracts, rates, and scorecards.',
  },
  {
    id: 'facilities.staff.manage',
    name: 'Manage Facilities Staff',
    description:
      'Manage cleaner/staff rosters, duty areas, attendance, supervision, and performance.',
  },
  {
    id: 'facilities.finance.view',
    name: 'View Facilities Finance Context',
    description:
      'View Facilities billing, arrears, budget, expense, and payment-confirmation context without replacing Finance permissions.',
  },
  {
    id: 'facilities.billing.manage',
    name: 'Manage Facilities Billing',
    description:
      'Prepare Facilities service charge, invoice, receipt, arrears, statement, and Finance AR handoff packages.',
  },
  {
    id: 'facilities.billing.dms.publish',
    name: 'Publish Facilities Billing Documents To DMS',
    description:
      'Publish Facilities billing invoices, receipts, statements, demand notices, adjustments, deposits, and dispute evidence to Central DMS.',
  },
  {
    id: 'facilities.portal.access',
    name: 'Use Facilities Portal',
    description:
      'Access tenant/client Facilities self-service views for complaints, requests, documents, invoices, and uploads.',
  },
] as const;

export const FACILITIES_ROLE_MATRIX = [
  {
    role: 'FacilitiesManager',
    label: 'Facilities Manager',
    purpose:
      'Owns Facilities approvals, dashboards, escalations, reports, provider decisions, and close-out governance.',
    permissions: [
      'facilities.access',
      'facilities.dashboard.read',
      'facilities.case.read',
      'facilities.case.create',
      'facilities.case.update',
      'facilities.case.approve',
      'facilities.handoff.create',
      'facilities.documents.manage',
      'facilities.providers.manage',
      'facilities.staff.manage',
      'facilities.finance.view',
      'facilities.billing.manage',
      'facilities.billing.dms.publish',
    ],
    moduleBoundary:
      'Needs separate target-module permissions for Property Management, Procurement, HR, Finance, Maintenance, Helpdesk, Inventory, Reports, Finance Fixed Assets, or Central Document Management execution.',
  },
  {
    role: 'FacilitiesSupervisor',
    label: 'Facilities Supervisor',
    purpose:
      'Triages Facilities cases, assigns work, monitors SLAs, inspects completion, and supervises staff/cleaners.',
    permissions: [
      'facilities.access',
      'facilities.dashboard.read',
      'facilities.case.read',
      'facilities.case.create',
      'facilities.case.update',
      'facilities.handoff.create',
      'facilities.staff.manage',
      'facilities.billing.manage',
    ],
    moduleBoundary:
      'Can initiate handoffs, but execution still requires the owning Property Management, Procurement, HR, Finance, Maintenance, Helpdesk, Inventory, Reports, Fixed Assets, or Central DMS permissions.',
  },
  {
    role: 'FacilitiesOfficer',
    label: 'Facilities Officer',
    purpose:
      'Handles day-to-day intake, case updates, property/unit context, lease support, and follow-up notes.',
    permissions: [
      'facilities.access',
      'facilities.case.read',
      'facilities.case.create',
      'facilities.case.update',
      'facilities.handoff.create',
    ],
    moduleBoundary:
      'Can prepare handoffs but cannot approve Facilities exceptions or operate target modules without additional permissions.',
  },
  {
    role: 'FacilitiesDocumentControl',
    label: 'Facilities Document Index & DMS Readiness',
    purpose:
      'Owns Facilities document indexing, source-module references, access classification, expiry reminders, and central DMS migration readiness.',
    permissions: [
      'facilities.access',
      'facilities.case.read',
      'facilities.case.update',
      'facilities.documents.manage',
    ],
    moduleBoundary:
      'Document index access does not grant source-module approval authority or Central Document Management repository/versioning authority.',
  },
  {
    role: 'FacilitiesFinanceOfficer',
    label: 'Facilities Finance Officer',
    purpose:
      'Reviews billing/payment context, arrears, service charges, budgets, expenses, and Finance handoff readiness.',
    permissions: [
      'facilities.access',
      'facilities.dashboard.read',
      'facilities.case.read',
      'facilities.case.update',
      'facilities.finance.view',
      'facilities.billing.manage',
      'facilities.billing.dms.publish',
    ],
    moduleBoundary:
      'Requires Finance permissions to create invoices, receipts, AP transactions, budgets, or GL postings.',
  },
  {
    role: 'FacilitiesServiceProvider',
    label: 'Facilities Service Provider',
    purpose:
      'Restricted provider access for assigned work updates, completion evidence, compliance documents, and invoice uploads.',
    permissions: [
      'facilities.access',
      'facilities.case.read',
      'facilities.case.update',
    ],
    moduleBoundary:
      'Provider work-order execution remains controlled by Maintenance and future provider portal permissions.',
  },
  {
    role: 'FacilitiesStaffCleaner',
    label: 'Facilities Staff / Cleaner',
    purpose:
      'Restricted access for duty roster, attendance, task completion, issue reporting, and supervision evidence.',
    permissions: [
      'facilities.access',
      'facilities.case.read',
      'facilities.case.update',
    ],
    moduleBoundary:
      'No approval, finance, provider, document-control, or cross-module execution permissions by default.',
  },
  {
    role: 'TenantClientPortalUser',
    label: 'Tenant / Client Portal User',
    purpose:
      'External self-service access for complaints, service requests, lease documents, invoice status, uploads, and feedback.',
    permissions: ['facilities.portal.access'],
    moduleBoundary:
      'External users should not receive internal Facilities dashboard, approval, document-control, or handoff permissions.',
  },
] as const;
