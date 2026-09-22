'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  AlarmClock,
  Building2,
  Sparkles,
  DoorOpen,
  Plus,
  FireExtinguisher,
  Siren,
  LayoutDashboard,
  Users,
  Trophy,
  ShoppingCart,
  Package,
  CreditCard,
  UserCheck,
  BadgeAlert,
  Briefcase,
  BookText,
  Megaphone,
  Settings,
  ChevronDown,
  ChevronRight,
  Menu,
  X,
  Shield,
  FileText,
  FileInput,
  Scale,
  Signpost,
  Banknote,
  Globe,
  GraduationCap,
  TrendingDown,
  IdCard,
  Stamp,
  ShieldAlert,
  Route,
  CalendarDays,
  Globe2,
  ListTree,
  ListFilter,
  Mail,
  Building,
  BarChart3,
  LineChart,
  Bell,
  Database,
  Home,
  Code,
  HelpCircle,
  Workflow,
  Wrench,
  Calendar,
  CalendarRange,
  Check,
  ClipboardCheck,
  ClipboardPlus,
  ListChecks,
  OctagonX,
  FileSearch,
  FolderArchive,
  Medal,
  AlertTriangle,
  Clock,
  CheckSquare,
  Activity,
  FolderTree,
  FileCheck,
  History,
  Gavel,
  Network,
  ShieldQuestion,
  Users2,
  Grid3x3,
  Award,
  Star,
  Target,
  DollarSign,
  TrendingUp,
  TriangleAlert,
  AlertCircle,
  ClipboardList,
  ClipboardPen,
  Tag,
  MapPin,
  Truck,
  ShieldCheck,
  Droplet,
  BookOpen,
  MessageSquare,
  Smartphone,
  RotateCcw,
  RefreshCw,
  CalendarClock,
  Phone,
  Swords,
  BookTemplate,
  Landmark,
  HardHat,
  Bandage,
  PersonStanding,
  GitBranch,
  Search,
  ArrowRightLeft,
  Boxes,
  Undo2,
  Repeat2,
  KeyRound,
  PackageCheck,
  CalendarCheck,
  MessagesSquare,
  NotebookPen,
  ScrollText,
  Timer,
  BellRing,
  Upload,
  Fingerprint,
  Receipt,
  Coins,
  ShieldPlus,
  ScanLine,
  WalletCards,
  Layers,
  FileBarChart,
  Sprout,
  Library,
  Gauge,
  Lightbulb,
  Handshake,
  FastForward,
  HandCoins,
  UserPlus,
  Stethoscope,
  HeartPulse,
  Hospital,
  FileHeart,
  Leaf,
  Recycle,
  Plane,
  Layers3,
} from 'lucide-react';

import { cn } from '../../lib/utils';
import { Button } from '../ui/button';
import { useAuth } from '../../hooks/use-auth';
import {
  hrOperationalTrainingLinks,
  hrSetupNavChildren,
} from '../../config/hr-setup-nav';

export interface NavItem {
  title: string;
  href: string;
  icon: React.ComponentType<any>;
  children?: NavItem[];
  roles?: string[];
  permissions?: string[];
  accessMode?: 'all' | 'any';
  navigationSurface?: 'operations' | 'settings';
  /**
   * What the screen is for, in one line. The settings surfaces print it as a tile caption;
   * without it they fall back to the item's ancestor path, which reads as a breadcrumb rather
   * than an explanation and is empty entirely for an item with no parent group.
   */
  description?: string;
  fallback?: NavItem;
}

export function canAccessNavItem(
  item: Pick<NavItem, 'roles' | 'permissions' | 'accessMode'>,
  hasAnyRole: (roles: string[]) => boolean,
  hasAnyPermission: (permissions: string[]) => boolean
): boolean {
  const checks: boolean[] = [];

  if (item.roles?.length) {
    checks.push(hasAnyRole(item.roles));
  }

  if (item.permissions?.length) {
    checks.push(hasAnyPermission(item.permissions));
  }

  if (checks.length === 0) {
    return true;
  }

  return item.accessMode === 'any'
    ? checks.some(Boolean)
    : checks.every(Boolean);
}

export function filterNavigationByAccess(
  items: NavItem[],
  hasAnyRole: (roles: string[]) => boolean,
  hasAnyPermission: (permissions: string[]) => boolean
): NavItem[] {
  return items.flatMap(item => {
    if (!canAccessNavItem(item, hasAnyRole, hasAnyPermission)) {
      return item.fallback
        ? filterNavigationByAccess([item.fallback], hasAnyRole, hasAnyPermission)
        : [];
    }
    const children = item.children
      ? filterNavigationByAccess(item.children, hasAnyRole, hasAnyPermission)
      : undefined;
    return item.children && !children?.length ? [] : [{ ...item, children }];
  });
}

const ADMINISTRATION_ROLES = [
  'admin',
  'Admin',
  'SystemAdmin',
  'SuperAdmin',
  'TenantAdmin',
];

const DOCUMENT_MANAGEMENT_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Estate Officer',
  'Estate Manager',
  'Head of Estate',
  'Property Management Officer',
  'Property Management Supervisor',
  'Property Manager',
  'Property Officer',
  'PropertyManager',
  'PropertySupervisor',
  'PropertyOfficer',
  'PropertyRecordsOfficer',
  'Document Controller',
  'Document Control Officer',
  'Records Officer',
  'PropertyRecordsOfficer',
  'Property Records Officer',
  'FacilitiesDocumentControl',
  'Legal Officer',
  'Legal Manager',
  'Head of Legal',
];

const ESTATE_CORE_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Estate Officer',
  'Estate Manager',
  'Head of Estate',
  'Land Registry Officer',
  'Survey Officer',
  'Records Officer',
  'Acquisition Committee',
];

const PROPERTY_MANAGEMENT_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Estate Officer',
  'Estate Manager',
  'Head of Estate',
  'Property Manager',
  'Property Officer',
  'PropertyManager',
  'PropertySupervisor',
  'PropertyOfficer',
  'PropertyLeaseOfficer',
  'PropertyBillingOfficer',
  'PropertyRecordsOfficer',
  'PropertyHandoverOfficer',
  'Executive Approver',
  'Authorised Signatory',
  'Managing Director',
];

const FACILITIES_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Estate Officer',
  'Estate Manager',
  'Head of Estate',
  'Facilities Manager',
  'Facilities Officer',
  'FacilitiesManager',
  'FacilitiesSupervisor',
  'FacilitiesOfficer',
  'FacilitiesDocumentControl',
  'FacilitiesFinanceOfficer',
];

const LEGAL_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Legal',
  'Legal Officer',
  'Senior Legal Officer',
  'Legal Manager',
  'Head of Legal',
  'Legal Admin Assistant',
];

const FINANCE_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Finance User',
  'Finance Clerk',
  'Finance Officer',
  'Accounts Officer',
  'Accounts Payable',
  'Accounts Payable Officer',
  'Accounts Receivable Officer',
  'Senior Accountant',
  'Finance Manager',
  'Financial Controller',
  'Chief Accountant',
  'Budget Officer',
];

const PROCUREMENT_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Procurement User',
  'Procurement Officer',
  'Procurement Manager',
];

const INVENTORY_ROLES = [
  ...ADMINISTRATION_ROLES,
  'Inventory User',
  'Inventory Officer',
  'Inventory Manager',
  'Warehouse Officer',
  'Warehouse Manager',
];

export const navigationItems: NavItem[] = [
  {
    title: 'Dashboard',
    href: '/dashboard',
    icon: LayoutDashboard,
  },
  {
    // The two-way switcher's desk side — /me has its own chrome ("Back to ERP"
    // lives there). The /me layout handles unlinked users with a friendly page.
    title: 'My Self-Service',
    href: '/me',
    icon: Sparkles,
  },
  {
    title: 'Finance',
    href: '/finance',
    icon: CreditCard,
    roles: FINANCE_ROLES,
    permissions: ['Finance.Read', 'Finance.Admin'],
    accessMode: 'any',
    children: [
      { title: 'Dashboard', href: '/finance/dashboard', icon: LayoutDashboard },
      {
        title: 'Approval Workbench',
        href: '/finance/approvals',
        icon: ShieldCheck,
        roles: [
          'SuperAdmin',
          'TenantAdmin',
          'Manager',
          'Accounts Officer',
          'Senior Accountant',
          'Chief Accountant',
          'Finance Manager',
          'Financial Controller',
          'Managing Director',
        ],
      },
      {
        title: 'General Ledger',
        href: '/finance/general-ledger',
        icon: FileText,
        children: [
          {
            title: 'Chart of Accounts',
            href: '/finance/accounts',
            icon: CreditCard,
            navigationSurface: 'settings',
          },
          {
            title: 'Journal Entries',
            href: '/finance/journal-entries',
            icon: FileText,
          },
          {
            // Journal batches were delivered as a complete controlled workspace, but the
            // route was previously absent from navigation. Keep this operational feature
            // beside individual journals and let the existing permission filter hide it
            // from users who are not authorised to view Finance batch controls.
            title: 'Journal Batches',
            href: '/finance/journal-batches',
            icon: Layers3,
            permissions: ['Finance.JournalBatches.View'],
          },
          {
            title: 'Recurring Journals',
            href: '/finance/recurring-journals',
            icon: Repeat2,
          },
          {
            // Opening balances are a controlled operational lifecycle (prepare,
            // validate, approve and post), not stable Finance setup. Keep the
            // workspace discoverable beside the journals it ultimately creates.
            title: 'Opening Balances',
            href: '/finance/opening-balances',
            icon: Database,
            permissions: ['Finance.Read'],
          },
          {
            title: 'Journal Approval Queue',
            href: '/finance/journal-entries/approvals',
            icon: ShieldCheck,
            roles: [
              'SuperAdmin',
              'TenantAdmin',
              'Manager',
              'Accounts Officer',
              'Senior Accountant',
              'Finance Manager',
              'Financial Controller',
            ],
          },
        ],
      },
      {
        title: 'Fiscal Management',
        href: '/finance/fiscal',
        icon: Calendar,
        navigationSurface: 'settings',
        children: [
          {
            title: 'Fiscal Years',
            href: '/finance/fiscal-years',
            icon: Calendar,
          },
          {
            title: 'Fiscal Periods',
            href: '/finance/fiscal-periods',
            icon: Calendar,
          },
        ],
      },
      {
        title: 'Fixed Assets',
        href: '/finance/fixed-assets',
        icon: Package,
        children: [
          {
            title: 'Dashboard',
            href: '/finance/fixed-assets/dashboard',
            icon: LayoutDashboard,
          },
          {
            title: 'Asset Register',
            href: '/finance/fixed-assets/register',
            icon: FileText,
          },
          {
            title: 'Depreciation',
            href: '/finance/fixed-assets/depreciation',
            icon: TrendingUp,
          },
          {
            title: 'Valuations',
            href: '/finance/fixed-assets/valuations',
            icon: BarChart3,
          },
          {
            title: 'Transfers',
            href: '/finance/fixed-assets/transfers',
            icon: Activity,
          },
          {
            title: 'Disposals',
            href: '/finance/fixed-assets/disposals',
            icon: FileText,
          },
          {
            title: 'Verification',
            href: '/finance/fixed-assets/verification',
            icon: ClipboardCheck,
          },
          // { title: 'Valuations', href: '/finance/fixed-assets/valuations', icon: TrendingUp },
          {
            title: 'Capital Projects',
            href: '/finance/fixed-assets/capital-projects',
            icon: Briefcase,
          },
          {
            title: 'Leases (IFRS 16)',
            href: '/finance/fixed-assets/leases',
            icon: FileText,
          },
          {
            title: 'Import',
            href: '/finance/fixed-assets/import',
            icon: FileText,
          },
          {
            title: 'Reports',
            href: '/finance/fixed-assets/reports',
            icon: BarChart3,
            children: [
              {
                title: 'Reports Overview',
                href: '/finance/fixed-assets/reports',
                icon: LayoutDashboard,
              },
              {
                title: 'Asset Register',
                href: '/finance/fixed-assets/reports?report=asset-register',
                icon: FileText,
              },
              {
                title: 'Disposal Activity',
                href: '/finance/fixed-assets/reports?report=disposal-activity',
                icon: RotateCcw,
              },
              {
                title: 'Transfer History',
                href: '/finance/fixed-assets/reports?report=transfer-history',
                icon: Activity,
              },
            ],
          },
        ],
      },
      {
        title: 'Accounts Payable',
        href: '/finance/ap',
        icon: FileText,
        children: [
          {
            title: 'Dashboard',
            href: '/finance/ap/dashboard',
            icon: LayoutDashboard,
          },
          {
            title: 'Suppliers',
            href: '/finance/ap/suppliers',
            icon: Users,
            permissions: ['Finance.Read'],
          },
          /* Procurement and Inventory own supplier masters, purchase orders, approvals,
           * and physical receipt workflows. Finance consumes their accounting evidence.
          {
            title: 'Purchase Orders',
            href: '/finance/ap/purchase-orders',
            icon: ShoppingCart,
          },
          {
            title: 'PO Approval Queue',
            href: '/finance/ap/purchase-orders/approvals',
            icon: ShieldCheck,
            roles: [
              'SuperAdmin',
              'TenantAdmin',
              'Manager',
              'Accounts Officer',
              'Senior Accountant',
              'Finance Manager',
              'Financial Controller',
            ],
          },
          {
            title: 'Goods Receipts',
            href: '/finance/ap/receipts',
            icon: Package,
          },
          */
          { title: 'Invoices', href: '/finance/ap/invoices', icon: FileText },
          /* Procurement and Inventory own the supplier-return workflow.
          {
            title: 'Supplier Returns',
            href: '/finance/ap/returns',
            icon: RotateCcw,
          },
          */
          {
            title: 'Supplier Debit Notes',
            href: '/finance/ap/supplier-debit-notes',
            icon: Receipt,
            permissions: ['Finance.Read'],
          },
          { title: 'Payments', href: '/finance/ap/payments', icon: CreditCard },
          {
            title: 'New AP Adjustment',
            href: '/finance/subledger-adjustments/new?module=AP',
            icon: FileText,
          },
          {
            title: 'AP Journal Entries',
            href: '/finance/journal-entries?sourceModule=AP',
            icon: FileText,
          },
          {
            title: 'Reports',
            href: '/finance/ap/reports',
            icon: BarChart3,
            children: [
              {
                title: 'Reports Overview',
                href: '/finance/ap/reports',
                icon: LayoutDashboard,
              },
              {
                title: 'AP Aging Analysis',
                href: '/finance/ap/reports?tab=aging',
                icon: CalendarClock,
              },
              {
                title: 'Cash Requirements',
                href: '/finance/ap/reports?tab=cash',
                icon: Banknote,
              },
              {
                title: 'Supplier Statements',
                href: '/finance/ap/reports?tab=statements',
                icon: FileText,
              },
              {
                title: 'Supplier Detailed Ledger',
                href: '/finance/ap/reports/supplier-detailed-ledger',
                icon: FileText,
              },
              {
                title: 'Procurement Reconciliation',
                href: '/finance/ap/reports?tab=procurement-reconciliation',
                icon: FileText,
              },
            ],
          },
        ],
      },
      {
        title: 'Accounts Receivable',
        href: '/finance/ar',
        icon: Users,
        children: [
          {
            title: 'Dashboard',
            href: '/finance/ar/dashboard',
            icon: LayoutDashboard,
          },
          /* Sales and the shared business-partner master own these customer workflows.
          {
            title: 'Customer Partners',
            href: '/procurement/business-partners?partnerType=Customer',
            icon: Users,
            permissions: [
              'Finance.Read',
              'Finance.AR.Invoices.Manage',
              'Finance.Admin',
            ],
            accessMode: 'any',
          },
          { title: 'Quotes', href: '/sales/crm/quotes', icon: FileText },
          { title: 'Sales Orders', href: '/sales/orders', icon: ShoppingCart },
          { title: 'Deliveries', href: '/sales/deliveries', icon: Truck },
          */
          {
            title: 'Customers',
            href: '/finance/ar/customers',
            icon: Users,
            permissions: [
              'Finance.Read',
              'Finance.AR.Invoices.Manage',
              'Finance.Admin',
            ],
            accessMode: 'any',
          },
          { title: 'Invoices', href: '/finance/ar/invoices', icon: FileText },
          /* Sales owns customer-return and sales-credit-note source workflows.
          {
            title: 'Customer Returns',
            href: '/sales/return-orders',
            icon: RotateCcw,
          },
          {
            title: 'Credit Notes',
            href: '/sales/credit-notes',
            icon: CreditCard,
          },
          */
          { title: 'Receipts', href: '/finance/ar/receipts', icon: CreditCard },
          {
            title: 'Collection Follow-up',
            href: '/finance/ar/collections',
            icon: BellRing,
            permissions: [
              'Finance.AR.Collections.View',
              'Finance.AR.Collections.Manage',
              'Finance.Admin',
            ],
            accessMode: 'any',
          },
          /* Sales owns the customer-refund source workflow.
          { title: 'Refunds', href: '/sales/refunds', icon: DollarSign },
          */
          {
            title: 'New AR Adjustment',
            href: '/finance/subledger-adjustments/new?module=AR',
            icon: FileText,
          },
          {
            title: 'AR Journal Entries',
            href: '/finance/journal-entries?sourceModule=AR',
            icon: FileText,
          },
          {
            title: 'Reports',
            href: '/finance/ar/reports',
            icon: BarChart3,
            children: [
              {
                title: 'Reports Overview',
                href: '/finance/ar/reports',
                icon: LayoutDashboard,
              },
              {
                title: 'AR Aging Analysis',
                href: '/finance/ar/reports?tab=aging',
                icon: CalendarClock,
              },
              {
                title: 'Customer Statements',
                href: '/finance/ar/reports?tab=statements',
                icon: FileText,
              },
              {
                title: 'Customer Detailed Ledger',
                href: '/finance/ar/reports/customer-detailed-ledger',
                icon: FileText,
              },
            ],
          },
        ],
      },

      {
        title: 'Cash Management',
        href: '/finance/cash',
        icon: CreditCard,
        children: [
          {
            title: 'Bank Accounts',
            href: '/finance/cash/accounts',
            icon: Building,
            navigationSurface: 'settings',
          },
          // Till sessions are a live custody workspace, not a report. Keep the operational link
          // in Finance while all analytical/report entry points remain under the shared Reports menu.
          {
            title: 'Cashier Till Sessions',
            href: '/finance/cash/till-sessions',
            icon: WalletCards,
          },
          {
            title: 'Cash Transactions',
            href: '/finance/cash/transactions',
            icon: Activity,
          },
          {
            title: 'Bank Deposits',
            href: '/finance/cash/deposits',
            icon: Landmark,
          },
          {
            title: 'Bank Reconciliation',
            href: '/finance/cash/reconciliation',
            icon: ClipboardCheck,
          },
          {
            title: 'Cash Reports',
            href: '/finance/cash/reports',
            icon: BarChart3,
            children: [
              {
                title: 'Reports Overview',
                href: '/finance/cash/reports',
                icon: LayoutDashboard,
              },
              {
                title: 'Cash Position',
                href: '/finance/cash/reports/cash-position',
                icon: Banknote,
              },
              {
                title: 'Cash Flow Statement',
                href: '/finance/reports/cash-flow',
                icon: TrendingUp,
              },
            ],
          },
        ],
      },
      {
        title: 'Budgeting',
        href: '/finance/budgeting',
        icon: BarChart3,
        children: [
          {
            title: 'Scenarios',
            href: '/finance/budgeting/scenarios',
            icon: BarChart3,
          },
          {
            title: 'My Returns',
            href: '/finance/budgeting/my-returns',
            icon: FileText,
          },
          {
            title: 'Budget Revisions',
            href: '/finance/budgeting/revisions',
            icon: RefreshCw,
          },
        ],
      },
      {
        title: 'Unit Accounting',
        href: '/finance/unit-accounting',
        icon: BarChart3,
        children: [
          {
            title: 'Unit Accounts',
            href: '/finance/unit-accounts',
            icon: BarChart3,
            navigationSurface: 'settings',
          },
          {
            title: 'Unit Journal Entries',
            href: '/finance/unit-journal-entries',
            icon: FileText,
          },
          {
            title: 'Unit Budgets',
            href: '/finance/unit-budgets',
            icon: BarChart3,
          },
          {
            title: 'Allocations',
            href: '/finance/allocations',
            icon: BarChart3,
          },
        ],
      },
      {
        title: 'Multi-Currency',
        href: '/finance/multi-currency',
        icon: CreditCard,
        children: [
          {
            title: 'Exchange Rates',
            href: '/finance/exchange-rates',
            icon: BarChart3,
            permissions: [
              'Finance.Read',
              'Finance.FX.Rates.Manage',
              'Finance.Admin',
            ],
            accessMode: 'any',
          },
          {
            title: 'Rate Trends',
            href: '/finance/exchange-rates/trends',
            icon: TrendingUp,
          },
          {
            title: 'Revaluation',
            href: '/finance/revaluation',
            icon: BarChart3,
          },
        ],
      },
      {
        title: 'Taxation',
        href: '/finance/tax',
        icon: BarChart3,
        children: [
          {
            title: 'Tax Calculator',
            href: '/finance/tax/calculator',
            icon: BarChart3,
          },
          {
            title: 'Tax Reports',
            href: '/finance/tax/reports',
            icon: FileText,
            children: [
              {
                title: 'Reports Overview',
                href: '/finance/tax/reports',
                icon: LayoutDashboard,
              },
              {
                title: 'Input VAT Register',
                href: '/finance/tax/reports/input-vat',
                icon: FileText,
              },
              {
                title: 'Output VAT Register',
                href: '/finance/tax/reports/output-vat',
                icon: TrendingUp,
              },
              {
                title: 'VAT Reconciliation',
                href: '/finance/tax/reports/vat-reconciliation',
                icon: Scale,
              },
              {
                title: 'WHT Summary',
                href: '/finance/tax/reports/withholding-tax',
                icon: FileCheck,
              },
              {
                title: 'WHT Certificates',
                href: '/finance/tax/reports/wht-certificates',
                icon: FileText,
              },
            ],
          },
        ],
      },
      {
        title: 'Financial Reports',
        href: '/finance/reports',
        icon: BarChart3,
        children: [
          {
            title: 'Reports Overview',
            href: '/finance/reports',
            icon: LayoutDashboard,
          },
          {
            title: 'Statement Layouts',
            href: '/finance/reports/layouts',
            icon: BookTemplate,
            permissions: [
              'Finance.Read',
              'Finance.Reports.Layouts.Manage',
              'Finance.Reports.Layouts.Publish',
            ],
            accessMode: 'any',
          },
          {
            title: 'Ad Hoc Report Builder',
            href: '/reports/financial/ad-hoc',
            icon: ListFilter,
            permissions: ['Finance.Reports.AdHoc.Build'],
          },
          {
            title: 'Trial Balance',
            href: '/finance/reports/trial-balance',
            icon: FileText,
          },
          {
            title: 'Income Statement',
            href: '/finance/reports/income-statement',
            icon: TrendingUp,
          },
          {
            title: 'Balance Sheet',
            href: '/finance/reports/balance-sheet',
            icon: Scale,
          },
          {
            title: 'Cash Flow Statement',
            href: '/finance/reports/cash-flow',
            icon: Banknote,
          },
          {
            title: 'Multi-Currency Detail',
            href: '/finance/reports/multi-currency',
            icon: Globe,
          },
          {
            title: 'Detailed Ledger',
            href: '/finance/reports/detailed-ledger',
            icon: ListTree,
          },
        ],
      },
    ],
  },
  {
    title: 'Human Resources',
    href: '/hr',
    icon: UserCheck,
    // Mirrors the /hr layout's AuthGuard. hr.access is granted to every general internal
    // role by the seeder — the gate exists to exclude external (candidate/business-partner)
    // accounts, not to hide HR from staff. SuperAdmin/TenantAdmin auto-pass.
    permissions: ['hr.access'],
    children: [
      // The flat list had 34 rows and the flyout could not show them all, so HR is
      // grouped one level down (the same shape Estate and Procurement already use). A group's
      // href is only its natural landing page — the row itself is a hover target, and it lights
      // up when any screen beneath it is active (isActiveDeep).
      {
        title: 'Employees',
        href: '/hr/employees',
        icon: Users,
        children: [
          // The register's detail/profile/sub-record reads are HR.Employee.Read on the
          // API now (they carry salary, identifiers and bank data), so the desk entry follows. The
          // shared employee picker rides the open paged read and does not need this.
          { title: 'Employees', href: '/hr/employees', icon: Users, permissions: ['HR.Employee.Read'] },
          // Approving one of these WRITES onto the employee record, which is
          // why it sits with the register and carries the same permission.
          {
            title: 'Change Requests',
            href: '/hr/employees/change-requests',
            icon: UserCheck,
            permissions: ['HR.Employee.Read'],
          },
          // Issuing a letter is making a statement about an employee record,
          // so it carries the same permission and sits with the register.
          {
            title: 'Letter Requests',
            href: '/hr/employees/letter-requests',
            icon: Mail,
            permissions: ['HR.Employee.Read'],
          },
          // The identification-expiry sweep. Read-gated, not write-gated: seeing what is
          // about to lapse is the point, and the run button is the only part the API gates on Write.
          {
            title: 'ID Expiry',
            href: '/hr/employees/identification-expiry',
            icon: BadgeAlert,
            permissions: ['HR.Employee.Read'],
          },
          // Employee bulk import (docs/HR/areas/employees/HR-EMPLOYEE-IMPORT-DESIGN.md). Write-gated, the same
          // tier as the create form: loading and amending employee records is what Write means,
          // and the HR role holds Write but not Admin — Admin hid this from every HR desk user.
          {
            title: 'Import',
            href: '/hr/employees/import',
            icon: Upload,
            permissions: ['HR.Employee.Write'],
          },
          // Read-only view of the org's own records. Lives here rather than under Administration
          // because it is looked at daily, not configured — and the administration tree is gated to
          // admin roles, which would hide it from the HR officers the API gate lets in.
          { title: 'Organogram', href: '/hr/organogram', icon: Network },
          // Operational, not configuration — written and published continuously,
          // so it sits at the top of HR rather than in administration settings. HR.Company, because
          // an announcement is a communication from the organisation, not an act on a record.
          {
            title: 'Announcements',
            href: '/hr/announcements',
            icon: Megaphone,
            permissions: ['HR.Company.Read'],
          },
          // The policy library and its acknowledgements. Same HR.Company
          // family: a policy is issued by the organisation, not performed on a record.
          {
            title: 'Policy Library',
            href: '/hr/policies',
            icon: BookText,
            permissions: ['HR.Company.Read'],
          },
        ],
      },
      {
        title: 'Pay & Benefits',
        href: '/hr/payroll',
        icon: HandCoins,
        children: [
          {
            // Menu gate only. PayrollController is the payroll developer's file and
            // still bare [Authorize] (cross-module #11), so this hides the desk from staff who
            // could otherwise open every screen; it does not protect the API. Compensation Read
            // is the nearest HR family until payroll seeds its own.
            title: 'Payroll',
            href: '/hr/payroll',
            icon: CreditCard,
            permissions: ['HR.Compensation.Read'],
            children: [
              { title: 'Run Desk', href: '/hr/payroll#runs', icon: CalendarClock },
              {
                title: 'Employee Profiles',
                href: '/hr/payroll/employee-profiles',
                icon: Users,
              },
              {
                title: 'Bonus Exceptions',
                href: '/hr/payroll/bonus-exceptions',
                icon: Award,
              },
              {
                title: 'Salary Advance',
                href: '/hr/payroll/salary-advance',
                icon: DollarSign,
              },
              {
                title: 'Loan Transaction',
                href: '/hr/payroll/loan-transaction',
                icon: CreditCard,
              },
              {
                title: 'Loan Transaction Amendment',
                href: '/hr/payroll/loan-transaction-amendment',
                icon: RotateCcw,
              },
              {
                title: 'Loan Repayment',
                href: '/hr/payroll/loan-repayment',
                icon: FileCheck,
              },
              { title: 'Tax Relief', href: '/hr/payroll/tax-relief', icon: Tag },
              {
                title: 'Allowances & Ded Exception',
                href: '/hr/payroll/allowances-deductions-exception',
                icon: CheckSquare,
              },
              {
                title: 'Promotion Arrears',
                href: '/hr/payroll/promotion-arrears',
                icon: TrendingUp,
              },
              {
                title: 'Overtime Summary',
                href: '/hr/payroll/overtime-summary',
                icon: Clock,
              },
              {
                title: 'Opening Balance',
                href: '/hr/payroll/opening-balance',
                icon: Database,
              },
              {
                title: 'Contributions',
                href: '/hr/payroll/contributions',
                icon: CreditCard,
              },
            ],
          },
          {
            title: 'Emoluments',
            href: '/hr/emoluments',
            icon: Coins,
            // Org-wide pay data — the compensation read tier. An employee's own pay
            // makeup is an ownership check on the API and arrives as a self-service screen.
            permissions: ['HR.Compensation.Read'],
          },
          {
            title: 'Benefits',
            href: '/hr/benefits',
            icon: ShieldPlus,
            // The enrollment desk — same tier. Own benefits/beneficiaries are self-service.
            permissions: ['HR.Compensation.Read'],
          },
          {
            // The enforcement side of a sponsored training nomination. It sits outside Performance
            // because the obligation is a training commitment, not an appraisal outcome — Training
            // links here too once that area is built.
            //
            // The screen is the desk register (getAll + on-behalf/waive/settle actions),
            // so it rides on the desk read. The employee's own bonds surface through My Training /
            // the API's /mine read; a self-service accept screen is still owed.
            title: 'Service Bonds',
            href: '/hr/service-bonds',
            icon: HandCoins,
            permissions: ['HR.Training.Read'],
          },
          {
            // FR-HR-135 names the chain: Department Head → HR → Managing Director. An approved
            // budget is not only a plan — it sets the establishment, which then gates whether a
            // vacancy may be opened at all (FR-HR-136).
            // TDC's name for it is the Manpower Recruitment Budget — the plan of
            // posts to recruit against, not a payroll budget.
            title: 'Manpower Recruitment Budgets',
            href: '/hr/manpower-budgets',
            icon: Banknote,
            permissions: ['HR.ManpowerBudget.Read'],
          },
        ],
      },
      {
        title: 'Time & Leave',
        href: '/hr/leave',
        icon: CalendarDays,
        children: [
          {
            // The org-wide surfaces authorize on HR.Leave.* (SuperAdmin/TenantAdmin
            // auto-pass). Requests stays open — it is per-employee and the API's self-or-permission
            // check lets anyone work their own history — and Approvals is the workflow assignee's
            // queue, deliberately not an HR permission. Year-End is the admin tier: it rewrites
            // every balance in the tenant, so plain HR staff do not see it.
            title: 'Leave Management',
            href: '/hr/leave',
            icon: CalendarDays,
            children: [
              { title: 'Requests', href: '/hr/leave/requests', icon: CalendarDays },
              // No permission gate: the calendar's scope selector defaults to Everyone, which the
              // server refuses without HR.Leave.Read — but "my team" and "mine" are open to all
              // staff, so hiding the leaf would hide those too.
              { title: 'Calendar', href: '/hr/leave/calendar', icon: CalendarDays },
              // Org-wide, so it carries the read permission the per-employee Requests screen does not.
              { title: 'Register', href: '/hr/leave/register', icon: ClipboardList, permissions: ['HR.Leave.Read'] },
              { title: 'Approvals', href: '/hr/leave/approvals', icon: CheckSquare },
              { title: 'Plans', href: '/hr/leave/plans', icon: CalendarClock, permissions: ['HR.Leave.Read'] },
              { title: 'Balances', href: '/hr/leave/balances', icon: Database, permissions: ['HR.Leave.Read'] },
              { title: 'Adjustments', href: '/hr/leave/adjustments', icon: RotateCcw, permissions: ['HR.Leave.Read'] },
              { title: 'Encashments', href: '/hr/leave/encashments', icon: DollarSign, permissions: ['HR.Leave.Read'] },
              { title: 'Compliance', href: '/hr/leave/compliance', icon: FileCheck, permissions: ['HR.Leave.Read'] },
              { title: 'Year-End', href: '/hr/leave/year-end', icon: CalendarClock, permissions: ['HR.Leave.Admin'] },
            ],
          },
          {
            // Every child here is a desk surface (org-wide search, monitoring, exports,
            // biometrics), so the whole section rides on the attendance read tier. Employee
            // self-service (punch, own records, own regularizations) lives in the portal
            // and authorizes by ownership, not by this permission.
            title: 'Attendance & Time',
            href: '/hr/attendance',
            icon: Clock,
            permissions: ['HR.Attendance.Read'],
            children: [
              {
                title: 'Daily Attendance',
                href: '/hr/attendance/daily',
                icon: CalendarCheck,
              },
              {
                title: 'Attendance Records',
                href: '/hr/attendance/records',
                icon: ClipboardList,
              },
              {
                title: 'Punch Logs',
                href: '/hr/attendance/logs',
                icon: ScrollText,
              },
              {
                title: 'Regularizations',
                href: '/hr/attendance/regularizations',
                icon: FileCheck,
              },
              {
                title: 'Overtime Requests',
                href: '/hr/attendance/overtime',
                icon: Timer,
              },
              {
                title: 'Remote Work',
                href: '/hr/attendance/remote-work',
                icon: Home,
              },
              {
                title: 'Monthly Summaries',
                href: '/hr/attendance/summaries',
                icon: Database,
              },
              { title: 'Alerts', href: '/hr/attendance/alerts', icon: BellRing },
              {
                title: 'Bulk Imports',
                href: '/hr/attendance/imports',
                icon: Upload,
              },
              {
                title: 'Payroll Exports',
                href: '/hr/attendance/payroll-exports',
                icon: DollarSign,
              },
              {
                title: 'Biometrics',
                href: '/hr/attendance/biometrics',
                icon: Fingerprint,
              },
            ],
          },
          {
            // The company schedule. Built 2026-08-28 — the backend had 90 endpoints and no screen had
            // ever called one of them. Events and bookings are day-to-day work and live here; rooms,
            // closures, milestones and fiscal years are set up once and live under Administration.
            title: 'Company Schedule',
            href: '/hr/company-schedule',
            icon: CalendarDays,
            children: [
              {
                title: 'Events',
                href: '/hr/company-schedule/events',
                icon: CalendarDays,
                permissions: ['HR.Company.Read'],
              },
              {
                title: 'Room Bookings',
                href: '/hr/company-schedule/bookings',
                icon: CalendarCheck,
                permissions: ['HR.Company.Read'],
              },
              {
                // Round 4, D5. The diary: everything the signed-in person is down for, assembled
                // from the same commitment sources the interview clash check fans out over.
                // ⚠ NO permission — the server takes the employee from the token, so this is
                // self-service and gating it would lock people out of their own schedule.
                title: 'My Schedule',
                href: '/hr/company-schedule/my-schedule',
                icon: CalendarClock,
              },
              {
                // ⚠ Company WRITE, not Read: the team view exposes other people's leave and
                // travel, which is desk information rather than general reading.
                title: 'Team Schedule',
                href: '/hr/company-schedule/team',
                icon: Users,
                permissions: ['HR.Company.Write'],
              },
            ],
          },
          {
            // One request covers the whole trip — approval, itinerary, bookings, the advance
            // and the expenses claimed against it. Approval runs on the generic workflow engine, so a
            // screen reads the live step from the workflow record rather than from `status`.
            title: 'Staff Travel',
            href: '/hr/travel',
            icon: Plane,
            children: [
              { title: 'Register', href: '/hr/travel', icon: Plane, permissions: ['HR.Travel.Read'] },
              // My Travel re-homed to the portal (/me/travel).
              // Claims get their own entry because the finance desk works a queue ACROSS trips —
              // "approved and unpaid" — which no single travel request can show.
              { title: 'Group Travel', href: '/hr/travel/groups', icon: Users2, permissions: ['HR.Travel.Read'] },
              { title: 'Expense Claims', href: '/hr/travel/claims', icon: Receipt, permissions: ['HR.Travel.Read'] },
              // The visa register had a client and no screen, so eleven fields on its DTOs
              // were unreachable. Reference data rather than day-to-day work, so it sits last.
              { title: 'Visa Requirements', href: '/hr/travel/visa-requirements', icon: Globe2, permissions: ['HR.Travel.Read'] },
              // Moved out of Administration → HR alongside the visa register it reads like: an
              // advisory is raised against a destination, expires and is re-issued as conditions
              // change, on the same cadence as the trips it warns about. The policies it used to
              // sit beside are set once and stay in Administration.
              { title: 'Destination Alerts', href: '/hr/travel/alerts', icon: TriangleAlert, permissions: ['HR.Travel.Read'] },
              { title: 'Dashboard', href: '/hr/travel/dashboard', icon: LayoutDashboard, permissions: ['HR.Travel.Read'] },
            ],
          },
        ],
      },
      {
        title: 'Talent & Performance',
        href: '/hr/performance',
        icon: Target,
        children: [
          {
            // Cycles, the goal cascade, and the appraisal run itself. Ordered by who acts: the
            // employee's own work first, then the manager's, then HR's — then what happens around
            // the sign-off (calibration before it, appeals after it, outcomes off the back of it),
            // with the deadline override last as the exception path.
            title: 'Performance',
            href: '/hr/performance',
            icon: Target,
            // The desk registers ride on HR.Performance.Read and the deadline override
            // on Write; everything an employee, peer or manager does as themselves stays open — the
            // API holds those to the token actor, not to a permission.
            children: [
              // The my-shaped screens (My Appraisals, Peer Reviews, Check-ins,
              // Journal, my/team development plans, my goals) moved to the self-service portal —
              // desk users reach them via "My Self-Service". What stays here is the desk's work.
              { title: 'Analytics', href: '/hr/performance/analytics', icon: BarChart3, permissions: ['HR.Performance.Read'] },
              { title: 'Appraisal Cycles', href: '/hr/performance/cycles', icon: CalendarRange, permissions: ['HR.Performance.Read'] },
              { title: 'Team Appraisals', href: '/hr/performance/team-appraisals', icon: UserCheck },
              { title: 'Interim Reviews', href: '/hr/performance/interim-reviews', icon: CalendarCheck },
              { title: 'Conversations', href: '/hr/performance/conversations', icon: MessagesSquare },
              {
                title: 'Development Plans',
                href: '/hr/performance/development-plans',
                icon: GraduationCap,
                permissions: ['HR.Performance.Read'],
              },
              { title: 'Improvement Plans', href: '/hr/performance/pip', icon: ClipboardPen },
              { title: 'HR Review', href: '/hr/performance/hr-review', icon: ClipboardList, permissions: ['HR.Performance.Read'] },
              { title: 'Calibration', href: '/hr/performance/calibration', icon: Scale, permissions: ['HR.Performance.Read'] },
              { title: 'Appeals', href: '/hr/performance/appeals', icon: Gavel, permissions: ['HR.Performance.Read'] },
              { title: 'Recommendations', href: '/hr/performance/recommendations', icon: Lightbulb, permissions: ['HR.Performance.Read'] },
              { title: 'Proposals', href: '/hr/performance/proposals', icon: Handshake, permissions: ['HR.Performance.Read'] },
              { title: 'Company Goals', href: '/hr/performance/company-goals', icon: Building2, permissions: ['HR.Performance.Read'] },
              { title: 'Unit Goals', href: '/hr/performance/unit-goals', icon: Layers },
              { title: 'Employee Goals', href: '/hr/performance/employee-goals', icon: Target },
              { title: 'Team Goals', href: '/hr/performance/team-goals', icon: Users },
              { title: 'Goals At Risk', href: '/hr/performance/at-risk', icon: AlertTriangle, permissions: ['HR.Performance.Read'] },
              // Appraisal notifications were a self surface on the desk; they were folded
              // them into the portal's unified feed (/me/notifications).
              {
                title: 'Deadline Enforcement',
                href: '/hr/performance/deadline-enforcement',
                icon: FastForward,
                permissions: ['HR.Performance.Write'],
              },
            ],
          },
          {
            // Ordered along the training lifecycle: a schedule is planned, people are nominated onto it
            // (or queued on its waitlist), those nominations are approved, and the run is closed off
            // with a completion. Self-service sits at the top because most people only ever need that.
            // The catalog, plans and budgets are setup and live under Administration → HR → Training.
            //
            // Desk registers gate on HR.Training.Read (leave-slice shape — the parent stays
            // open so the self-service children remain reachable). Schedules is the published calendar,
            // Requests is the desk register + on-behalf create, and Nomination Approvals is the approver
            // queue — workflow-validated per request.
            //
            // The my-* screens (My Training, My Learning Paths, my mentoring pairs)
            // re-homed to the portal (/me/training, /me/learning, /me/mentoring); Mentoring and the new
            // Enrollments entry are the desk registers that remained.
            title: 'Training',
            href: '/hr/training',
            icon: GraduationCap,
            //
            // Training Activities is the per-employee grouped view TDC
            // asked for; Mentoring moved out to its own section below — TDC's feedback was that it did
            // not belong inside the Training menu.
            children: [
              // The front of the training cycle — assess a gap, plan for it, budget it — was
              // stranded under Administration while every step downstream of it (Requests,
              // Nomination Approvals, Enrollments, Completions) already lived here. It is
              // per-employee, per-cycle casework, not catalogue. The pages moved under /hr with
              // it, so they gate on HR.Training.Read like the rest of the desk rather than on the
              // admin.hr grant the /administration layout demands.
              ...hrOperationalTrainingLinks.map(link => ({
                title: link.title,
                href: link.href,
                icon: link.icon,
                description: link.description,
                permissions: ['HR.Training.Read'],
              })),
              { title: 'Enrollments', href: '/hr/training/enrollments', icon: Route, permissions: ['HR.Training.Read'] },
              { title: 'Training Activities', href: '/hr/training/activities', icon: Activity, permissions: ['HR.Training.Read'] },
              { title: 'Analytics', href: '/hr/training/analytics', icon: TrendingUp, permissions: ['HR.Training.Read'] },
              { title: 'Schedules', href: '/hr/training/schedules', icon: CalendarClock },
              { title: 'Requests', href: '/hr/training/requests', icon: ClipboardList, permissions: ['HR.Training.Read'] },
              { title: 'Nomination Approvals', href: '/hr/training/approvals', icon: UserCheck },
              { title: 'Completions', href: '/hr/training/completions', icon: Award, permissions: ['HR.Training.Read'] },
              { title: 'Certificates', href: '/hr/training/certificates', icon: Stamp, permissions: ['HR.Training.Read'] },
              { title: 'Employee Certificates', href: '/hr/training/employee-certificates', icon: IdCard, permissions: ['HR.Training.Read'] },
              { title: 'Compliance', href: '/hr/training/compliance', icon: ShieldAlert, permissions: ['HR.Training.Read'] },
            ],
          },
          {
            // Mentoring was a child of Training; TDC's demo feedback
            // wanted it as its own section. The routes are unchanged (the desk register is still under
            // /hr/training/mentoring, the schemes under Administration) — what moved is where the
            // navigation puts them. A mentee's own pairs stay in the portal (/me/mentoring).
            title: 'Mentoring',
            href: '/hr/training/mentoring',
            icon: Handshake,
            children: [
              { title: 'Mentoring Pairs', href: '/hr/training/mentoring', icon: Handshake, permissions: ['HR.Training.Read'] },
              { title: 'Programmes', href: '/administration/hr/training/mentoring', icon: Settings, permissions: ['HR.Training.Read'] },
            ],
          },
          {
            // The gap view is the operational screen: what positions require against what
            // people have been assessed at. "Below requirement" and "not assessed" stay separate all
            // the way to the tiles — an unknown is not a training need.
            title: 'Competencies',
            href: '/hr/competencies',
            icon: GraduationCap,
            children: [
              { title: 'Organisation Gaps', href: '/hr/competencies', icon: TrendingDown, permissions: ['HR.Competency.Read'] },
              { title: 'Assess Competencies', href: '/hr/competencies/assess', icon: ClipboardCheck, permissions: ['HR.Competency.Write'] },
              // "My Competencies" moved to My Self-Service → Career & Jobs (/me/competencies) on
              // A self-only screen has no place on the desk menu.
            ],
          },
          {
            // FR-HR-134: a position is measured against its APPROVED job description, and only
            // one stands at a time — approving a new version retires the one before it. Coverage is its
            // own entry because the useful question is not how many descriptions exist but how many
            // positions still have none.
            title: 'Job Descriptions',
            href: '/hr/job-descriptions',
            icon: FileText,
            children: [
              { title: 'Register', href: '/hr/job-descriptions', icon: FileText, permissions: ['HR.JobArchitecture.Read'] },
              { title: 'Coverage Gaps', href: '/hr/job-descriptions/gaps', icon: ClipboardList, permissions: ['HR.JobArchitecture.Read'] },
            ],
          },
          {
            // The plan is per POSITION, not per person, and it is versioned: approving a new
            // plan archives the one the post had before and points it at the successor. Only an
            // approved plan is the position's active version — a draft deliberately holds no slot.
            title: 'Succession Planning',
            href: '/hr/succession',
            icon: Network,
            children: [
              { title: 'Plans', href: '/hr/succession', icon: Network, permissions: ['HR.Succession.Read'] },
              // Pools are NOT tied to a post, which is what separates them from a plan — so they get
              // their own entry rather than living inside one. Note that Performance writes into the
              // "Appraisal Nominations" pool on its own, without anyone opening this screen.
              { title: 'Talent Pools', href: '/hr/succession/pools', icon: Users2, permissions: ['HR.Succession.Read'] },
              // Calibration sessions and the nine box. Separate from plans and pools because a session
              // is an EVENT with a close, not a register: finalizing freezes it for good.
              { title: 'Talent Reviews', href: '/hr/succession/reviews', icon: Grid3x3, permissions: ['HR.Succession.Read'] },
              // The criteria search had an endpoint and a client method but no screen.
              // Also hosted inside a plan's Candidates tab.
              { title: 'Find Candidates', href: '/hr/succession/candidate-search', icon: Search, permissions: ['HR.Succession.Read'] },
              { title: 'Dashboard', href: '/hr/succession/dashboard', icon: LayoutDashboard, permissions: ['HR.Succession.Read'] },
            ],
            // No "my succession" entry, and there must never be one: readiness, retention risk and
            // nine-box placement are assessments made ABOUT a candidate, not records belonging to them.
            // Succession inverts the self-service rule the rest of HR follows.
          },
          {
            // ONE exit register for every route out — resignation, retirement, contract
            // expiry, redundancy, dismissal, death. Before it, the only way to leave was a disciplinary
            // case, so the other routes had enum members and nothing that could reach them. The
            // disciplinary route writes here too: Discipline keeps the decision, the exit lives here.
            // NOTE the title: '/procurement/awards' already exists and also uses the Award
            // icon, so a bare "Awards" here would give the sidebar two entries a user cannot tell
            // apart. Medal is deliberately a different glyph for the same reason.
            title: 'Awards & Recognition',
            href: '/hr/awards',
            icon: Medal,
            children: [
              { title: 'Register', href: '/hr/awards', icon: Medal, permissions: ['HR.Awards.Read'] },
              // The self surface (My Awards, Nominate, Vote, Score) moved to the
              // portal under /me/awards — reachable via "My Self-Service". Only the desk stays here.
              { title: 'Who Qualifies', href: '/hr/awards/eligibility', icon: Users, permissions: ['HR.Awards.Read'] },
              { title: 'Results', href: '/hr/awards/results', icon: Trophy, permissions: ['HR.Awards.Read'] },
              { title: 'Long Service', href: '/hr/awards/long-service', icon: Medal, permissions: ['HR.Awards.Read'] },
            ],
          },
        ],
      },
      {
        title: 'Workforce Lifecycle',
        href: '/hr/recruitment',
        icon: Route,
        children: [
          {
            // Ordered the way a hire happens rather than alphabetically: a gap in the establishment
            // becomes a requisition, an approved requisition becomes a vacancy, publishing the vacancy
            // raises the adverts, and the adverts bring in candidates and applications.
            //
            // The pipeline board and the screening workspace are not listed: both belong to a single
            // vacancy and are reached from it, so a top-level link would have nowhere to go.
            // Desk registers gate on HR.Recruitment.Read (the leave-slice shape — the
            // parent stays open so the self-service children remain reachable). Requisitions stays
            // open as the manager's entry point (raising one is self-service; the register tab needs
            // the desk read), and My Panel is the panelist's own surface, validated per record.
            title: 'Recruitment',
            href: '/hr/recruitment',
            icon: UserPlus,
            children: [
              { title: 'Dashboard', href: '/hr/recruitment/dashboard', icon: LayoutDashboard, permissions: ['HR.Recruitment.Read'] },
              { title: 'Establishment', href: '/hr/recruitment/establishment', icon: Building2, permissions: ['HR.Recruitment.Read'] },
              { title: 'Requisitions', href: '/hr/recruitment/requisitions', icon: ClipboardList, permissions: ['HR.Recruitment.Read'] },
              { title: 'Vacancies', href: '/hr/recruitment/vacancies', icon: Briefcase, permissions: ['HR.Recruitment.Read'] },
              { title: 'Adverts', href: '/hr/recruitment/postings', icon: Megaphone, permissions: ['HR.Recruitment.Read'] },
              { title: 'Candidates', href: '/hr/recruitment/candidates', icon: Users, permissions: ['HR.Recruitment.Read'] },
              { title: 'Talent Pool', href: '/hr/recruitment/talent-pool', icon: Star, permissions: ['HR.Recruitment.Read'] },
              { title: 'Applications', href: '/hr/recruitment/applications', icon: FileText, permissions: ['HR.Recruitment.Read'] },
              { title: 'Interviews', href: '/hr/recruitment/interviews', icon: CalendarClock, permissions: ['HR.Recruitment.Read'] },
              // Round 4, lane E. Between interviews and offers because that is where it falls in the
              // hire: a test is sat before a decision, and its marking queue is a daily job.
              { title: 'Assessments', href: '/hr/recruitment/assessments', icon: ClipboardCheck, permissions: ['HR.Recruitment.Read'] },
              // The only recruitment screen a non-HR employee can use: a panelist's own sessions. The
              // interview schedule above answers 403 for them, so without this they have no way in.
              { title: 'Offers', href: '/hr/recruitment/offers', icon: HandCoins, permissions: ['HR.Recruitment.Read'] },
              { title: 'Hires', href: '/hr/recruitment/hires', icon: UserCheck, permissions: ['HR.Recruitment.Read'] },
              {
                title: 'Pre-Employment Checks',
                href: '/hr/recruitment/pre-employment-checks',
                icon: ShieldCheck,
                permissions: ['HR.Recruitment.Read'],
              },
              // Last because it is the only one that is not a step in the hire: it looks back at
              // the whole year rather than moving a single candidate along.
              {
                title: 'Analytics',
                href: '/hr/recruitment/analytics',
                icon: TrendingUp,
                permissions: ['HR.Recruitment.Read'],
              },
            ],
          },
          {
            // Ordered along the induction lifecycle rather than alphabetically: the dashboard says what
            // needs chasing, sessions are the scheduled runs, enrollments are who is on them, and My
            // Orientation is the participant's own view. Onboarding sits below because it is a different
            // artifact — a new hire's task checklist rather than a programme — and the queues answer the
            // cross-plan question ("what is overdue anywhere?") no single plan can.
            //
            // The catalogue, categories and onboarding templates are setup and live under
            // Administration → HR → Orientation & Onboarding.
            title: 'Orientation & Onboarding',
            href: '/hr/orientation',
            icon: GraduationCap,
            children: [
              { title: 'Dashboard', href: '/hr/orientation/dashboard', icon: LayoutDashboard, permissions: ['HR.Orientation.Read'] },
              { title: 'Sessions', href: '/hr/orientation/sessions', icon: CalendarClock, permissions: ['HR.Orientation.Read'] },
              { title: 'Enrollments', href: '/hr/orientation/enrollments', icon: Users, permissions: ['HR.Orientation.Read'] },
              // My Orientation re-homed to the portal (/me/orientation).
              { title: 'Onboarding Plans', href: '/hr/orientation/onboarding', icon: ListChecks, permissions: ['HR.Orientation.Read'] },
              { title: 'Task Queues', href: '/hr/orientation/onboarding/queues', icon: ClipboardCheck, permissions: ['HR.Orientation.Read'] },
            ],
          },
          {
            // The probation is per EMPLOYMENT TERM, not per person: a rehire gets a new one,
            // and an employee may accumulate several over a career. Length comes from the staff
            // category (FR-HR-031, senior 6 / junior 3), not from whoever fills in the form.
            title: 'Probation & Confirmation',
            href: '/hr/probation',
            icon: UserCheck,
            children: [
              { title: 'Register', href: '/hr/probation', icon: UserCheck, permissions: ['HR.Probation.Read'] },
              // The reviewer queue is deliberately its own entry: probation reviews are conducted by
              // LINE MANAGERS, who hold no HR permission at all, so this is the one screen in the area
              // most of its users will ever open. (The employee's own reviews and
              // the oath affirmation re-homed to the portal — /me/probation and /me/oath.)
              { title: 'Reviews to Conduct', href: '/hr/probation/reviews', icon: ClipboardCheck },
              // FR-HR-030. Not probation, but the same FRD section (onboarding), and it is the only
              // other place an oath would sensibly live.
              { title: 'Oaths of Secrecy', href: '/hr/probation/oaths', icon: ScrollText, permissions: ['HR.Probation.Read'] },
            ],
          },
          {
            // One record covers promotion, transfer, demotion, secondment, acting appointment,
            // lateral move and redesignation — they share an approval route, a checklist and a set of
            // documents, and differ only in their subtype detail.
            title: 'Staff Movements',
            href: '/hr/movements',
            icon: ArrowRightLeft,
            children: [
              { title: 'Register', href: '/hr/movements', icon: ArrowRightLeft, permissions: ['HR.Movements.Read'] },
              // Standalone: most acting appointments never come from a movement at all.
              { title: 'Acting Appointments', href: '/hr/movements/acting', icon: UserCheck, permissions: ['HR.Movements.Read'] },
              // Answering a demotion notice removes it from the pending queue, so a filed
              // appeal was visible on no list at all. Both queues live here.
              { title: 'Demotion Appeals', href: '/hr/movements/appeals', icon: Gavel, permissions: ['HR.Movements.Read'] },
              // My Movements re-homed to the portal (/me/movements).
            ],
          },
          {
            title: 'Separations & Exit',
            href: '/hr/separations',
            icon: DoorOpen,
            children: [
              { title: 'Register', href: '/hr/separations', icon: DoorOpen, permissions: ['HR.Separation.Read'] },
              { title: 'Raise a separation', href: '/hr/separations/new', icon: Plus, permissions: ['HR.Separation.Write'] },
            ],
          },
        ],
      },
      {
        title: 'Conduct & Relations',
        href: '/hr/discipline',
        icon: Gavel,
        children: [
          {
            // The case is the unit of work: one record carries the allegation, the
            // investigation, the hearing, the decision, whichever sanction follows, and the appeal.
            title: 'Discipline',
            href: '/hr/discipline',
            icon: Gavel,
            children: [
              { title: 'Cases', href: '/hr/discipline', icon: Gavel, permissions: ['HR.Discipline.Read'] },
              // Sub-entity queues: which investigations have run past FR-HR-178's four weeks, and which
              // hearings are scheduled or have happened without their notes recorded.
              { title: 'Investigations & Hearings', href: '/hr/discipline/queues', icon: Search, permissions: ['HR.Discipline.Read'] },
              // Open by design: the officer who confirms a sanction is a head of department or the MD,
              // and the register above answers 403 for them — this is where their work appears. The
              // engine decides its contents, per case and per step.
              { title: 'Awaiting My Confirmation', href: '/hr/discipline/approvals', icon: Check },
              // "My Record" moved to the portal (/me/discipline).
            ],
          },
          {
            // FR-HR-181. Separate from Discipline on purpose: a disciplinary case is
            // raised ABOUT an employee and a grievance BY one, which gives them opposite permissions —
            // HR cannot file, escalate or withdraw a grievance at all.
            // The employee surface (mine, filing, the detail) moved to the portal
            // under /me/grievances.
            // Renamed and re-homed to /hr/employee-relations. The register has held
            // mediations, welfare matters and union consultations from the start, so "Grievances" had
            // become the wrong name for it — and its rows now open the DESK's case file rather than
            // the employee's portal detail, which they had been doing by accident.
            title: 'Employee relations',
            href: '/hr/employee-relations',
            icon: MessagesSquare,
            children: [
              { title: 'Case register', href: '/hr/employee-relations', icon: ClipboardList, permissions: ['HR.Discipline.Read'] },
              { title: 'Anonymous concerns', href: '/hr/employee-relations/concerns', icon: ShieldQuestion, permissions: ['HR.Discipline.Read'] },
              { title: 'Who answers each rung', href: '/hr/employee-relations/responders', icon: Network, permissions: ['HR.Discipline.Read'] },
              { title: 'Analytics', href: '/hr/employee-relations/analytics', icon: BarChart3, permissions: ['HR.Discipline.Read'] },
            ],
          },
        ],
      },
      {
        // Company property in a named employee's hands: the register, the requisition that
        // asks for one, and the responsibility document they sign for it.
        //
        // The parent now points at the HR register hub, which answers 403 without HR — so
        // "My Assets" keeps its own entry below it, as staff movements and travel do, because it
        // is the one screen in this group every employee can reach.
        title: 'Company Assets',
        href: '/hr/assets',
        icon: Package,
        children: [
          { title: 'Register', href: '/hr/assets/register', icon: Boxes, permissions: ['HR.Assets.Read'] },
          { title: 'Assignments', href: '/hr/assets/assignments', icon: Package, permissions: ['HR.Assets.Read'] },
          { title: 'Requisitions', href: '/hr/assets/requisitions', icon: ClipboardList, permissions: ['HR.Assets.Read'] },
          { title: 'Transfers', href: '/hr/assets/transfers', icon: ArrowRightLeft, permissions: ['HR.Assets.Read'] },
          { title: 'Surcharges', href: '/hr/assets/surcharges', icon: Receipt, permissions: ['HR.Assets.Read'] },
          // The payroll desk's view of both money surfaces. Its own entry because it is somebody
          // else's worklist entirely: HR declares here, payroll deducts elsewhere.
          { title: 'Payroll Deductions', href: '/hr/assets/payroll', icon: Coins, permissions: ['HR.Assets.Read'] },
          // The three watchlist groups get their own entries rather than living behind the
          // register: each is somebody's worklist, and a list you have to go looking for is a list
          // nobody works.
          { title: 'Maintenance', href: '/hr/assets/maintenance', icon: Wrench, permissions: ['HR.Assets.Read'] },
          { title: 'Insurance', href: '/hr/assets/insurance', icon: ShieldCheck, permissions: ['HR.Assets.Read'] },
          { title: 'Returns', href: '/hr/assets/returns', icon: Undo2, permissions: ['HR.Assets.Read'] },
          { title: 'Register Report', href: '/hr/assets/report', icon: FileText, permissions: ['HR.Assets.Read'] },
          { title: 'Reminders', href: '/hr/assets/reminders', icon: BellRing, permissions: ['HR.Assets.Read'] },
          // "My Assets" moved to the portal (/me/assets), via "My Self-Service".
        ],
      },
      {
        // Management registers (clients, engagements, invoicing) — attendance read
        // tier. A consultant's own timesheet surface is self-service.
        title: 'Consulting',
        href: '/hr/consulting',
        icon: Briefcase,
        permissions: ['HR.Attendance.Read'],
        children: [
          { title: 'Clients', href: '/hr/consulting/clients', icon: Building2 },
          {
            title: 'Engagements',
            href: '/hr/consulting/engagements',
            icon: FileCheck,
          },
          {
            title: 'Timesheets',
            href: '/hr/consulting/timesheets',
            icon: Clock,
          },
          { title: 'Invoices', href: '/hr/consulting/invoices', icon: Receipt },
        ],
      },
      {
        // Medical & Health. Everything here is gated on the HR.Medical.* permission
        // policies rather than a role, so an HR user without medical permissions sees 403s.
        // The reference registers ship first; health records, claims and the clinical
        // surface follow. Note occupational health and return-to-work live under
        // Safety (SHE owns them) but ride the same medical policies.
        title: 'Medical & Health',
        href: '/hr/medical',
        icon: HeartPulse,
        children: [
          { title: 'Dashboard', href: '/hr/medical/dashboard', icon: LayoutDashboard, permissions: ['HR.Medical.Read'] },
          { title: 'Medical Claims', href: '/hr/medical/claims', icon: Receipt, permissions: ['HR.Medical.Read'] },
          // "My Medical Claims" moved to the portal (/me/medical/claims).
          { title: 'NHIS Claims', href: '/hr/medical/nhis', icon: Landmark, permissions: ['HR.Medical.Read'] },
          { title: 'Clinical', href: '/hr/medical/clinical', icon: ClipboardCheck, permissions: ['HR.Medical.Read'] },
          // Medical boards (residue plan G4). Leave and separation READ these; neither writes one.
          { title: 'Medical Boards', href: '/hr/medical/boards', icon: Gavel, permissions: ['HR.Medical.Read'] },
          { title: 'Health Records', href: '/hr/medical/health', icon: FileHeart, permissions: ['HR.Medical.Read'] },
          { title: 'Healthcare Facilities', href: '/hr/medical/facilities', icon: Hospital, permissions: ['HR.Medical.Read'] },
          { title: 'Physicians', href: '/hr/medical/physicians', icon: Stethoscope, permissions: ['HR.Medical.Read'] },
          { title: 'Insurance Providers', href: '/hr/medical/insurance', icon: ShieldPlus, permissions: ['HR.Medical.Read'] },
          { title: 'Benefit Schemes', href: '/hr/medical/schemes', icon: Layers, permissions: ['HR.Medical.Read'] },
        ],
      },
    ],
  },
  {
    // Safety (SHE) is its own top-level module in the sidebar (moved out of Human Resources on
    // 31 registers made the HR flyout unusable). The routes still live under
    // /hr/safety (so the /hr layout's hr.access applies too), but since 2026-09-03 the
    // menu and the /hr/safety layout gate on the SHE module's OWN permission, she.access — held by
    // Safety Officer, SHE Manager, HR (read-only in SHE) and the administrators, not by every
    // employee. Staff report incidents, hazards and stop-work from My Self-Service → My Safety.
    // Every register below carries its own HR.She.* / HR.Medical.* permission.
    // Reference data lives under Administration → HR → Safety.
    title: 'Safety (SHE)',
    href: '/hr/safety',
    icon: HardHat,
    permissions: ['she.access'],
    children: [
      { title: 'Dashboard', href: '/hr/safety/dashboard', icon: LayoutDashboard, permissions: ['HR.She.Read'] },
      // 31 registers grouped one level down, like Human Resources. A group's href is
      // only its natural landing page; the row lights up when any screen beneath it is active.
      {
        title: 'Incidents & Hazards',
        href: '/hr/safety/incidents',
        icon: AlertTriangle,
        children: [
          // The open employee actions (report incident/hazard/environmental,
          // raise stop-work) and My PPE moved to the self-service portal under /me/safety —
          // reachable via "My Self-Service". Only the desk registers stay here.
          { title: 'Incidents', href: '/hr/safety/incidents', icon: AlertTriangle, permissions: ['HR.She.Read'] },
          { title: 'Hazards', href: '/hr/safety/hazards', icon: ShieldAlert, permissions: ['HR.She.Read'] },
          { title: 'Risk Assessments', href: '/hr/safety/risk-assessments', icon: ClipboardList, permissions: ['HR.She.Read'] },
          { title: 'Stop-Work Orders', href: '/hr/safety/stop-work', icon: OctagonX, permissions: ['HR.She.Read'] },
          // The unified CA tracker (one queue over all five action stores) and the
          // computed-KPI layer (snapshot compute + the analytics screen).
          { title: 'Corrective Actions', href: '/hr/safety/corrective-actions', icon: ListChecks, permissions: ['HR.She.Read'] },
        ],
      },
      {
        title: 'Inspections & Audits',
        href: '/hr/safety/inspections',
        icon: ClipboardCheck,
        children: [
          { title: 'Inspections', href: '/hr/safety/inspections', icon: ClipboardCheck, permissions: ['HR.She.Read'] },
          // Audits + the stop-work register (raise lives in the open block above).
          { title: 'SHE Audits', href: '/hr/safety/audits', icon: FileSearch, permissions: ['HR.She.Read'] },
          { title: 'Permits to Work', href: '/hr/safety/permits', icon: FileCheck, permissions: ['HR.She.Read'] },
          { title: 'Regulatory Compliance', href: '/hr/safety/regulatory', icon: Scale, permissions: ['HR.She.Read'] },
        ],
      },
      {
        title: 'PPE & Equipment',
        href: '/hr/safety/ppe',
        icon: Package,
        children: [
          { title: 'PPE Stock', href: '/hr/safety/ppe', icon: Package, permissions: ['HR.She.Read'] },
          { title: 'PPE Issuance', href: '/hr/safety/ppe/issuances', icon: Users, permissions: ['HR.She.Read'] },
          { title: 'Safety Equipment', href: '/hr/safety/equipment', icon: FireExtinguisher, permissions: ['HR.She.Read'] },
          { title: 'Safety Signage', href: '/hr/safety/signs', icon: Signpost, permissions: ['HR.She.Read'] },
          { title: 'Emergency Plans', href: '/hr/safety/emergency', icon: Siren, permissions: ['HR.She.Read'] },
        ],
      },
      {
        title: 'People & Health',
        href: '/hr/safety/training',
        icon: Users,
        children: [
          // The SHE training record — deliberately separate from corporate Training.
          { title: 'Safety Training', href: '/hr/safety/training', icon: GraduationCap, permissions: ['HR.She.Read'] },
          { title: 'Contractors', href: '/hr/safety/contractors', icon: Handshake, permissions: ['HR.She.Read'] },
          { title: 'Safety Committees', href: '/hr/safety/committees', icon: Users, permissions: ['HR.She.Read'] },
          // Occ-health + RTW ride the HR.Medical.* policies, not the SHE HR-role gate.
          { title: 'Occupational Health', href: '/hr/safety/occupational-health', icon: Stethoscope, permissions: ['HR.Medical.Read'] },
          { title: 'Return to Work', href: '/hr/safety/return-to-work', icon: HeartPulse, permissions: ['HR.Medical.Read'] },
        ],
      },
      {
        title: 'Environment',
        href: '/hr/safety/environmental',
        icon: Leaf,
        children: [
          { title: 'Environmental', href: '/hr/safety/environmental', icon: Leaf, permissions: ['HR.She.Read'] },
          { title: 'Waste Management', href: '/hr/safety/waste', icon: Recycle, permissions: ['HR.She.Read'] },
          // Part D environmental core (FR-ENV): the permit/licence register with the
          // statutory renewal ladder, monitoring schedules, the regulatory-updates register,
          // sustainability, compliance reviews/clearance and the monthly environmental report.
          { title: 'Environmental Permits', href: '/hr/safety/environmental/permits', icon: FileCheck, permissions: ['HR.She.Read'] },
          { title: 'Monitoring Schedules', href: '/hr/safety/environmental/monitoring-schedules', icon: CalendarClock, permissions: ['HR.She.Read'] },
          { title: 'Regulatory Updates', href: '/hr/safety/environmental/regulatory-updates', icon: Scale, permissions: ['HR.She.Read'] },
          { title: 'Sustainability', href: '/hr/safety/environmental/sustainability', icon: Sprout, permissions: ['HR.She.Read'] },
          { title: 'Environmental Reviews', href: '/hr/safety/environmental/reviews', icon: ClipboardCheck, permissions: ['HR.She.Read'] },
          { title: 'Monthly Env. Reports', href: '/hr/safety/environmental/monthly-reports', icon: FileBarChart, permissions: ['HR.She.Read'] },
        ],
      },
      {
        title: 'Performance & Records',
        href: '/hr/safety/performance',
        icon: Gauge,
        children: [
          { title: 'Performance Snapshots', href: '/hr/safety/performance', icon: Gauge, permissions: ['HR.She.Read'] },
          { title: 'SHE Analytics', href: '/hr/safety/performance/analytics', icon: BarChart3, permissions: ['HR.She.Read'] },
          // The controlled document register (versions ride the central DMS).
          { title: 'Document Register', href: '/hr/safety/documents', icon: FolderArchive, permissions: ['HR.She.Read'] },
        ],
      },
    ],
  },
  {
    title: 'Maintenance',
    href: '/maintenance',
    icon: Wrench,
    permissions: ['maintenance.access'],
    children: [
      {
        title: 'Dashboard',
        href: '/maintenance/dashboard',
        icon: LayoutDashboard,
      },
      { title: 'Job Cards', href: '/maintenance/job-cards', icon: FileText },
      {
        title: 'Work Orders',
        href: '/maintenance/work-orders',
        icon: FileText,
      },
      { title: 'Assets', href: '/maintenance/assets', icon: Package },
      { title: 'Sites', href: '/maintenance/sites', icon: MapPin },
      {
        title: 'Asset Admission',
        href: '/maintenance/asset-admission',
        icon: ClipboardCheck,
      },
      { title: 'Technicians', href: '/maintenance/technicians', icon: Users },
      {
        title: 'Quality Control',
        href: '/maintenance/quality-control',
        icon: ClipboardCheck,
      },
      {
        title: 'Scheduled Maintenance',
        href: '/maintenance/scheduled',
        icon: Calendar,
      },
      {
        title: 'Condition Monitoring',
        href: '/maintenance/condition-monitoring',
        icon: Activity,
      },
      // Emergency Maintenance temporarily hidden
      // { title: 'Emergency Maintenance', href: '/maintenance/emergency', icon: AlertTriangle },
      // Inspections temporarily hidden
      // { title: 'Inspections', href: '/maintenance/inspections', icon: ClipboardCheck },
      {
        title: 'Maintenance History',
        href: '/maintenance/history',
        icon: Clock,
      },
      { title: 'Tool Management', href: '/maintenance/tools', icon: Wrench },
      { title: 'Reports', href: '/maintenance/reports', icon: BarChart3 },
      // Asset Analytics temporarily hidden
      // { title: 'Asset Analytics', href: '/maintenance/analytics', icon: BarChart3 },
    ],
  },
  {
    title: 'Fleet',
    href: '/maintenance/fleet',
    icon: Truck,
    permissions: ['fleet.access'],
    children: [
      {
        title: 'Dashboard',
        href: '/maintenance/fleet/dashboard',
        icon: LayoutDashboard,
      },
      { title: 'Fleets', href: '/maintenance/fleet/vehicles', icon: Truck },
      { title: 'Drivers', href: '/maintenance/fleet/drivers', icon: UserCheck },
      { title: 'Trips', href: '/maintenance/fleet/trips', icon: MapPin },
      {
        title: 'Compliance',
        href: '/maintenance/fleet/compliance',
        icon: ShieldCheck,
      },
      {
        title: 'Fuel (Reports)',
        href: '/maintenance/fleet/fuel',
        icon: Droplet,
      },
      {
        title: 'Defects',
        href: '/maintenance/fleet/defects',
        icon: AlertTriangle,
      },
      {
        title: 'Incidents',
        href: '/maintenance/fleet/incidents',
        icon: AlertTriangle,
      },
      { title: 'Tyres', href: '/maintenance/fleet/tyres', icon: Package },
      {
        title: 'Batteries',
        href: '/maintenance/fleet/batteries',
        icon: Package,
      },
      {
        title: 'External Repairs',
        href: '/maintenance/fleet/external-repairs',
        icon: Wrench,
      },
      { title: 'Costs', href: '/maintenance/fleet/costs', icon: DollarSign },
      { title: 'PM Plans', href: '/maintenance/fleet/pm', icon: Calendar },
    ],
  },
  {
    title: 'Projects',
    href: '/development/projects',
    icon: Briefcase,
    children: [
      {
        title: 'Projects',
        href: '/development/projects',
        icon: Briefcase,
        permissions: ['project.access'],
      },
      {
        title: 'Town Planning',
        href: '/development/planning',
        icon: MapPin,
        permissions: ['project.access'],
        children: [
          {
            title: 'Planning Dashboard',
            href: '/development/planning/dashboard',
            icon: BarChart3,
          },
          {
            title: 'SOP Procedures',
            href: '/development/planning',
            icon: ClipboardList,
          },
          {
            title: 'Workflow Setup',
            href: '/administration/workflow?q=Planning',
            icon: Workflow,
          },
          {
            title: 'Documents',
            href: '/document-management?module=Planning',
            icon: FileText,
          },
          {
            title: 'Reports',
            href: '/reports?module=planning',
            icon: FileCheck,
          },
        ],
      },
      {
        title: 'Operations',
        href: '/development/project-operations',
        icon: Activity,
        permissions: ['project.access'],
      },
      {
        title: 'Portfolios',
        href: '/development/portfolios',
        icon: FolderTree,
        permissions: ['project.access'],
      },
      {
        title: 'Programs',
        href: '/development/programs',
        icon: Target,
        permissions: ['project.access'],
      },
      {
        title: 'Dependencies',
        href: '/development/project-dependencies',
        icon: AlertCircle,
        permissions: ['project.access'],
      },
      {
        title: 'Analytics',
        href: '/development/project-analytics',
        icon: TrendingUp,
        permissions: ['project.access'],
      },
      {
        title: 'Reports',
        href: '/development/project-reports',
        icon: FileCheck,
        permissions: ['project.access'],
      },
      {
        title: 'Approvals',
        href: '/development/project-approvals',
        icon: ClipboardCheck,
        permissions: ['project.access'],
      },
      {
        title: 'Billing',
        href: '/development/project-billing',
        icon: DollarSign,
        permissions: ['project.access'],
      },
      {
        title: 'Materials',
        href: '/development/project-materials',
        icon: Package,
        permissions: ['project.access'],
      },
      {
        title: 'Mobile',
        href: '/development/project-mobile',
        icon: Smartphone,
        permissions: ['project.access'],
      },
      {
        title: 'Tasks',
        href: '/development/tasks',
        icon: FileText,
        permissions: ['project.access'],
      },
      {
        title: 'Timesheets',
        href: '/development/timesheets',
        icon: Clock,
        permissions: ['project.access'],
      },
      {
        title: 'Expenses',
        href: '/development/expenses',
        icon: DollarSign,
        permissions: ['project.access'],
      },
      {
        title: 'Resources',
        href: '/development/resources',
        icon: Users,
        permissions: ['project.access'],
      },
      {
        title: 'Timeline',
        href: '/development/timeline',
        icon: BarChart3,
        permissions: ['project.access'],
      },
      {
        title: 'Civil Engineering',
        href: '/development/civil-engineering',
        icon: Building2,
        children: [
          {
            title: 'Design & Delivery',
            href: '/development/civil-engineering/design-inputs',
            icon: FileInput,
            children: [
              {
                title: 'Design Inputs',
                href: '/development/civil-engineering/design-inputs',
                icon: FileInput,
                permissions: ['civil-engineering.design-input.respond'],
              },
              {
                title: 'Task Assignments',
                href: '/development/civil-engineering/direct-tasks',
                icon: ClipboardList,
                permissions: ['civil-engineering.workspace.read'],
              },
            ],
          },
          {
            title: 'Maintenance',
            href: '/development/civil-engineering/maintenance-intakes',
            icon: Wrench,
            children: [
              {
                title: 'Intake',
                href: '/development/civil-engineering/maintenance-intakes',
                icon: ClipboardPlus,
                permissions: ['civil-engineering.maintenance.manage'],
              },
              {
                title: 'Assessments',
                href: '/development/civil-engineering/maintenance-assessments',
                icon: ClipboardCheck,
                permissions: ['civil-engineering.maintenance.manage'],
              },
              {
                title: 'Costing Handoffs',
                href: '/development/civil-engineering/maintenance-costing-handoffs',
                icon: DollarSign,
                permissions: ['civil-engineering.maintenance.manage'],
              },
              {
                title: 'Execution',
                href: '/development/civil-engineering/maintenance-execution-links',
                icon: Wrench,
                permissions: ['civil-engineering.maintenance.manage'],
              },
              {
                title: 'Completion',
                href: '/development/civil-engineering/maintenance-completion-controls',
                icon: ClipboardCheck,
                permissions: ['civil-engineering.maintenance.manage'],
              },
              {
                title: 'Complaint Resolution',
                href: '/development/civil-engineering/complaint-resolutions',
                icon: MessageSquare,
                permissions: ['civil-engineering.workspace.read'],
              },
            ],
          },
          {
            title: 'Permitting',
            href: '/development/civil-engineering/development-approval-files',
            icon: ShieldCheck,
            children: [
              {
                title: 'Development Approval Files',
                href: '/development/civil-engineering/development-approval-files',
                icon: Building2,
                permissions: ['civil-engineering.permitting.manage'],
              },
              {
                title: 'File Handoffs',
                href: '/development/civil-engineering/development-approval-handoffs',
                icon: ArrowRightLeft,
                permissions: ['civil-engineering.permitting.manage'],
              },
              {
                title: 'Engineering Reviews',
                href: '/development/civil-engineering/permitting-engineering-reviews',
                icon: ClipboardCheck,
                permissions: ['civil-engineering.permitting.manage'],
              },
              {
                title: 'HOD Decisions',
                href: '/development/civil-engineering/permitting-hod-decisions',
                icon: ShieldCheck,
                permissions: ['civil-engineering.permitting.manage'],
              },
            ],
          },
          {
            title: 'Administration',
            href: '/development/civil-engineering/migration-batches',
            icon: Settings,
            children: [
              {
                title: 'Migration Workbench',
                href: '/development/civil-engineering/migration-batches',
                icon: FileCheck,
                permissions: ['civil-engineering.migration.manage'],
              },
            ],
          },
        ],
      },
    ],
  },
  {
    title: 'Procurement',
    href: '/procurement',
    icon: Briefcase,
    roles: PROCUREMENT_ROLES,
    permissions: [
      'procurement.records.read',
      'procurement.supplier.read',
      'procurement.purchase-order.read',
      'procurement.audit.read',
    ],
    accessMode: 'any',
    children: [
      {
        title: 'Supplier Management',
        href: '/procurement/suppliers',
        icon: Users,
        children: [
          {
            title: 'Supplier Application Portal',
            href: '/supplier-application',
            icon: Globe2,
          },
          {
            title: 'Business Partners',
            href: '/procurement/business-partners',
            icon: Users,
            permissions: [
              'procurement.records.read',
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Registrations',
            href: '/administration/procurement/registrations',
            icon: FileText,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
            ],
            accessMode: 'any',
          },
          {
            title: 'Pending Partners',
            href: '/administration/procurement/business-partners/pending',
            icon: Users,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Onboarding Tokens',
            href: '/administration/procurement/supplier-onboarding-tokens',
            icon: KeyRound,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.payment.verify',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Applicant Access',
            href: '/administration/procurement/supplier-applicant-access',
            icon: ShieldCheck,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Evidence Packs',
            href: '/administration/procurement/supplier-evidence-packs',
            icon: FileCheck,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Eligibility',
            href: '/administration/procurement/supplier-eligibility',
            icon: ClipboardCheck,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Due Diligence',
            href: '/administration/procurement/supplier-due-diligence',
            icon: ShieldCheck,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Approved Vendor List',
            href: '/administration/procurement/supplier-avl',
            icon: ListTree,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Risk & Concentration',
            href: '/administration/procurement/supplier-risk',
            icon: TrendingUp,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Performance',
            href: '/administration/procurement/supplier-performance',
            icon: Activity,
            permissions: [
              'procurement.supplier.manage',
              'procurement.supplier.review',
              'procurement.supplier.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Supplier Master Changes',
            href: '/administration/procurement/supplier-master-changes',
            icon: Building2,
            permissions: ['procurement.supplier.manage'],
          },
        ],
      },
      {
        title: 'Purchasing',
        href: '/procurement/purchasing',
        icon: ShoppingCart,
        children: [
          {
            title: 'Purchase Requests',
            href: '/procurement/purchase-requisitions',
            icon: FileText,
          },
          {
            title: 'Sourcing Cases',
            href: '/procurement/sourcing-cases',
            icon: FileCheck,
            permissions: ['procurement.records.read'],
          },
          { title: 'RFQs', href: '/procurement/rfqs', icon: FileText },
          {
            title: 'Purchase Orders',
            href: '/procurement/purchase-orders',
            icon: ShoppingCart,
          },
          {
            title: 'Purchase Receipts',
            href: '/procurement/purchase-receipts',
            icon: ClipboardList,
          },
          {
            title: 'Supplier Comparison',
            href: '/procurement/supplier-comparison',
            icon: BarChart3,
          },
        ],
      },
      {
        title: 'Tendering',
        href: '/procurement/tendering',
        icon: Gavel,
        children: [
          { title: 'Tenders', href: '/procurement/tenders', icon: Gavel },
          {
            title: 'Tender Documents',
            href: '/procurement/tender-documents',
            icon: BookTemplate,
            permissions: ['procurement.records.read'],
          },
          {
            title: 'Prequalification',
            href: '/procurement/prequalification',
            icon: ShieldCheck,
            permissions: ['procurement.records.read'],
          },
          {
            title: 'My Assigned Tenders',
            href: '/procurement/my-assigned-tenders',
            icon: FileText,
          },
          { title: 'Bids', href: '/procurement/bids', icon: FileText },
          {
            title: 'Evaluations',
            href: '/procurement/evaluations',
            icon: Star,
          },
          { title: 'Awards', href: '/procurement/awards', icon: Award },
          {
            title: 'Contracts',
            href: '/procurement/contracts',
            icon: FileText,
          },
          {
            title: 'Contract Operations',
            href: '/procurement/contract-operations',
            icon: Activity,
            permissions: ['procurement.reports.read'],
          },
          {
            title: 'Framework Agreements',
            href: '/procurement/framework-agreements',
            icon: FileCheck,
            permissions: [
              'procurement.records.read',
              'procurement.contract.manage',
              'procurement.contract.approve',
              'procurement.audit.read',
            ],
            accessMode: 'any',
          },
          {
            title: 'Framework Call-offs',
            href: '/procurement/framework-call-offs',
            icon: PackageCheck,
            permissions: [
              'procurement.records.read',
              'procurement.purchase-order.create',
              'procurement.purchase-order.approve',
              'procurement.audit.read',
            ],
            accessMode: 'any',
          },
        ],
      },
      {
        title: 'Planning',
        href: '/procurement/planning',
        icon: Target,
        children: [
          {
            title: 'Overview',
            href: '/procurement/planning',
            icon: LayoutDashboard,
          },
          {
            title: 'Procurement Plans',
            href: '/procurement/planning/plans',
            icon: Target,
          },
          {
            title: 'APP Submissions',
            href: '/procurement/planning/app-submissions',
            icon: FileCheck,
          },
          {
            title: 'Specification Templates',
            href: '/procurement/planning/specification-templates',
            icon: BookTemplate,
            navigationSurface: 'settings',
          },
          {
            title: 'Budgets',
            href: '/procurement/planning/budgets',
            icon: DollarSign,
          },
          {
            title: 'Schedules',
            href: '/procurement/planning/schedules',
            icon: Calendar,
          },
          {
            title: 'Annual Calendar',
            href: '/procurement/planning/calendar',
            icon: CalendarClock,
          },
          {
            title: 'Market Analysis',
            href: '/procurement/planning/market-analysis',
            icon: TrendingUp,
          },
          {
            title: 'Supplier Consolidation',
            href: '/procurement/planning/supplier-consolidation',
            icon: Users,
          },
          {
            title: 'Emergency Plans',
            href: '/procurement/planning/emergency-plans',
            icon: AlertCircle,
          },
          {
            title: 'Reports',
            href: '/procurement/planning/reports',
            icon: FileText,
          },
        ],
      },
      {
        title: 'Procurement Documents',
        href: '/procurement/documents',
        icon: FileText,
        permissions: [
          'procurement.records.read',
          'procurement.inventory.read',
          'Finance.Read',
        ],
        accessMode: 'any',
      },
      {
        title: 'Governance & Controls',
        href: '/administration/procurement/master-data-changes',
        icon: ShieldCheck,
        children: [
          {
            title: 'Master Data Changes',
            href: '/administration/procurement/master-data-changes',
            icon: FileCheck,
            permissions: ['procurement.access.manage'],
          },
          {
            title: 'Control Events',
            href: '/administration/procurement/control-events',
            icon: Activity,
            permissions: ['procurement.audit.read', 'audit.read'],
            accessMode: 'any',
          },
        ],
      },
    ],
  },
  {
    title: 'Inventory',
    href: '/inventory',
    icon: Package,
    roles: INVENTORY_ROLES,
    // Employees already have server-authorized access to their own requests.
    // Show that route without exposing warehouse operations or granting roles.
    fallback: {
      title: 'Inventory',
      href: '/inventory',
      icon: Package,
      roles: ['Employee'],
      children: [
        { title: 'My requisitions', href: '/inventory/requisitions', icon: ClipboardList },
      ],
    },
    permissions: [
      'procurement.inventory.read',
      'procurement.inventory.master-data.manage',
    ],
    accessMode: 'any',
    children: [
      {
        title: 'Items & Catalogue',
        href: '/inventory/cards',
        icon: Package,
        permissions: [
          'procurement.inventory.read',
          'procurement.inventory.master-data.manage',
        ],
        accessMode: 'any',
        children: [
          { title: 'Inventory Items', href: '/inventory/items', icon: Package },
          {
            title: 'Warehouse Items',
            href: '/inventory/warehouse-items',
            icon: Building2,
          },
          {
            title: 'Item Identifiers',
            href: '/inventory/item-identifiers',
            icon: ScanLine,
          },
          {
            title: 'Item Suppliers',
            href: '/inventory/item-suppliers',
            icon: Users,
          },
          {
            title: 'Price Lists',
            href: '/inventory/price-lists',
            icon: DollarSign,
          },
        ],
      },
      {
        title: 'Transactions',
        href: '/inventory/transactions',
        icon: Activity,
        children: [
          {
            title: 'Stock Movements',
            href: '/inventory/stock-movements',
            icon: Activity,
          },
          {
            title: 'Inventory Requisitions',
            href: '/inventory/requisitions',
            icon: ClipboardList,
          },
          {
            title: 'Inventory Receipts',
            href: '/inventory/adjustments',
            icon: Package,
          },
          {
            title: 'Inventory Transfers',
            href: '/inventory/transfers',
            icon: Package,
          },
          {
            title: 'Supplier Returns',
            href: '/inventory/supplier-returns',
            icon: RotateCcw,
            permissions: [
              'procurement.inventory.issue',
              'procurement.inventory.adjust.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Bin Stock',
            href: '/inventory/bin-stock',
            icon: FolderTree,
          },
          {
            title: 'Physical Counts',
            href: '/inventory/physical-counts',
            icon: ClipboardCheck,
          },
          {
            title: 'Labels & Mobile Scanning',
            href: '/inventory/mobile-scanning',
            icon: ScanLine,
            permissions: [
              'procurement.inventory.receive',
              'procurement.inventory.issue',
              'procurement.inventory.transfer',
              'procurement.inventory.count',
            ],
            accessMode: 'any',
          },
          {
            title: 'Tracking Controls',
            href: '/inventory/tracking-controls',
            icon: ShieldCheck,
            permissions: ['procurement.inventory.read'],
          },
          {
            title: 'Negative Stock Controls',
            href: '/inventory/negative-stock-controls',
            icon: ShieldCheck,
            permissions: ['procurement.inventory.read'],
          },
          {
            title: 'Project Reservations',
            href: '/inventory/project-reservations',
            icon: ShieldCheck,
            permissions: ['procurement.inventory.read'],
          },
          {
            title: 'Replenishment',
            href: '/inventory/replenishment',
            icon: BellRing,
            permissions: ['procurement.inventory.read'],
          },
          {
            title: 'Valuation Reconciliation',
            href: '/inventory/valuation-reconciliation',
            icon: Scale,
            permissions: ['Finance.Read'],
          },
          {
            title: 'Ageing & Action Analytics',
            href: '/inventory/analytics',
            icon: BarChart3,
            permissions: ['procurement.inventory.read'],
          },
          {
            title: 'Inventory Disposal',
            href: '/inventory/disposals',
            icon: Gavel,
            permissions: [
              'procurement.inventory.read',
              'procurement.inventory.disposal.request',
              'procurement.inventory.disposal.approve',
            ],
            accessMode: 'any',
          },
          {
            title: 'Directed Operations',
            href: '/inventory/directed-operations',
            icon: GitBranch,
            permissions: [
              'procurement.inventory.receive',
              'procurement.inventory.issue',
              'procurement.inventory.transfer',
            ],
            accessMode: 'any',
          },
          {
            title: 'Valuation',
            href: '/inventory/valuation',
            icon: DollarSign,
          },
        ],
      },
    ],
  },
  {
    title: 'CRM',
    href: '/crm',
    icon: Users,
    roles: [
      ...ADMINISTRATION_ROLES,
      'CRM Manager',
      'CRM Officer',
      'Sales User',
      'Sales Manager',
      'Sales Officer',
    ],
    permissions: ['crm.access', 'crm.read', 'sales.access'],
    accessMode: 'any',
    children: [
      { title: 'Overview', href: '/crm', icon: LayoutDashboard },
      { title: 'Accounts', href: '/crm/accounts', icon: Building2 },
      { title: 'Collaboration', href: '/crm/collaboration', icon: Workflow },
      { title: 'Service', href: '/crm/service', icon: MessageSquare },
      { title: 'Risk', href: '/crm/risk', icon: AlertTriangle },
      { title: 'Readiness', href: '/crm/readiness', icon: ShieldCheck },
      { title: 'Contacts', href: '/crm/contacts', icon: Mail },
      { title: 'Leads', href: '/crm/leads', icon: Users },
      { title: 'Campaigns', href: '/crm/campaigns', icon: Megaphone },
      { title: 'Opportunities', href: '/crm/opportunities', icon: TrendingUp },
      { title: 'Conversions', href: '/crm/conversions', icon: FolderTree },
      { title: 'Renewals', href: '/crm/renewals', icon: AlertTriangle },
      { title: 'Activities', href: '/crm/activities', icon: Activity },
      { title: 'Quotes', href: '/crm/quotes', icon: FileText },
      { title: 'Projects', href: '/crm/projects', icon: Briefcase },
      { title: 'Contracts', href: '/crm/contracts', icon: FileCheck },
      { title: 'Tenders', href: '/crm/tenders', icon: Gavel },
      { title: 'Forecast', href: '/crm/forecast', icon: Target },
      { title: 'Reports', href: '/crm/reports', icon: BarChart3 },
    ],
  },
  {
    title: 'Sales',
    href: '/sales',
    icon: ShoppingCart,
    roles: [
      ...ADMINISTRATION_ROLES,
      'Sales User',
      'Marketing User',
      'Sales Manager',
      'Sales Officer',
      'Salesperson',
    ],
    permissions: ['sales.access', 'sales.read', 'sales.orders.read'],
    accessMode: 'any',
    children: [
      { title: 'Sales Overview', href: '/sales', icon: LayoutDashboard },
      { title: 'Property Enquiries', href: '/sales/property-enquiries', icon: MessageSquare, permissions: ['enquiry.property.access'] },
      { title: 'Sales Orders', href: '/sales/orders', icon: ShoppingCart },
      { title: 'Allocations', href: '/sales/allocations', icon: MapPin },
      { title: 'Delivery Notes', href: '/sales/deliveries', icon: Truck },
      { title: 'Sales Agreements', href: '/sales/agreements', icon: FileText },
      {
        title: 'CRM',
        href: '/sales/crm',
        icon: Users,
        children: [
          { title: 'Leads', href: '/sales/crm/leads', icon: Users },
          {
            title: 'Opportunities',
            href: '/sales/crm/opportunities',
            icon: Target,
          },
          { title: 'Quotes', href: '/sales/crm/quotes', icon: FileText },
          {
            title: 'Activities',
            href: '/sales/crm/activities',
            icon: Activity,
          },
        ],
      },
      { title: 'Return Orders', href: '/sales/return-orders', icon: RotateCcw },
      { title: 'Credit Notes', href: '/sales/credit-notes', icon: CreditCard },
      { title: 'Refunds', href: '/sales/refunds', icon: DollarSign },
      {
        title: 'Collections',
        href: '/sales/collections',
        icon: Phone,
        children: [
          { title: 'Activities', href: '/sales/collections', icon: Phone },
          {
            title: 'Payment Plans',
            href: '/sales/collections/plans',
            icon: CalendarClock,
          },
        ],
      },
      { title: 'Commissions', href: '/sales/commissions', icon: Award },
      { title: 'Forecasts', href: '/sales/forecasts', icon: TrendingUp },
      { title: 'Competitors', href: '/sales/competitors', icon: Swords },
      { title: 'Reports', href: '/sales/reports', icon: BarChart3 },
      {
        title: 'Journal Templates',
        href: '/sales/journal-templates',
        icon: BookTemplate,
        navigationSurface: 'settings',
      },
    ],
  },
  {
    // Estate/DMS integration: keep Central DMS visible as a shared workspace, not nested inside another module owner.
    title: 'Documents',
    href: '/document-management',
    icon: BookTemplate,
    roles: DOCUMENT_MANAGEMENT_ROLES,
    permissions: [
      'document-management.access',
      'document-management.read',
      'dms.access',
      'documents.read',
    ],
    accessMode: 'any',
    children: [
      {
        title: 'Dashboard',
        href: '/document-management',
        icon: LayoutDashboard,
      },
      {
        title: 'Document Register',
        href: '/document-management/records',
        icon: FileText,
      },
      {
        title: 'Metadata Templates',
        href: '/administration/document-management/metadata-templates',
        icon: BookTemplate,
      },
      {
        title: 'Metadata Template Records',
        href: '/document-management/CentralDocumentMetadataTemplate',
        icon: FileText,
      },
      {
        title: 'Document Templates',
        href: '/administration/document-management/document-templates',
        icon: BookTemplate,
      },
      {
        title: 'Version Control',
        href: '/document-management/CentralDocumentVersion',
        icon: Workflow,
      },
      {
        title: 'Access / Retention',
        href: '/document-management/CentralDocumentGovernance',
        icon: ShieldCheck,
      },
      {
        title: 'Module Queue',
        href: '/document-management/CentralDocumentIntegrationQueue',
        icon: Workflow,
      },
    ],
  },
  {
    // Estate/DMS integration: Estate owns these workspaces; other modules should link into them instead of duplicating pages.
    title: 'Estate',
    href: '/estate',
    icon: Home,
    roles: [
      ...ESTATE_CORE_ROLES,
      ...PROPERTY_MANAGEMENT_ROLES,
      ...FACILITIES_ROLES,
    ],
    permissions: [
      'estate.access',
      'property-management.access',
      'facilities.access',
    ],
    accessMode: 'any',
    children: [
      {
        title: 'Overview',
        href: '/estate',
        icon: ClipboardList,
        roles: ESTATE_CORE_ROLES,
        permissions: ['estate.access', 'estate.read'],
        accessMode: 'any',
      },
      {
        title: 'Land Acquisition',
        href: '/estate/land-acquisition',
        icon: Landmark,
        roles: ESTATE_CORE_ROLES,
        permissions: ['estate.access', 'estate.land.acquire'],
        accessMode: 'any',
      },
      {
        title: 'Land Management',
        href: '/estate/land-management',
        icon: MapPin,
        roles: ESTATE_CORE_ROLES,
        permissions: ['estate.access', 'estate.land.manage'],
        accessMode: 'any',
      },
      {
        title: 'GIS Integration',
        href: '/estate/gis',
        icon: Globe2,
        roles: ESTATE_CORE_ROLES,
        permissions: ['estate.access', 'estate.gis.read'],
        accessMode: 'any',
      },
      {
        title: 'Core Operations',
        href: '/estate',
        icon: ClipboardList,
        roles: ESTATE_CORE_ROLES,
        permissions: ['estate.access', 'estate.read'],
        accessMode: 'any',
        children: [
          {
            title: 'Registry',
            href: '/estate/EstateRegistrySecretariat',
            icon: ClipboardList,
          },
          {
            title: 'Records',
            href: '/estate/EstateRecordsManagement',
            icon: Database,
          },
          {
            title: 'Inspections',
            href: '/estate/EstateInspection',
            icon: MapPin,
          },
          {
            title: 'Searches',
            href: '/estate/EstateSearchApplication',
            icon: Search,
          },
          {
            title: 'Record Amendments',
            href: '/estate/EstateRecordAmendment',
            icon: FileCheck,
          },
          {
            title: 'Certified Copies',
            href: '/estate/EstateCertifiedTrueCopy',
            icon: FileCheck,
          },
          {
            title: 'Joint Ownership',
            href: '/estate/EstateJointOwnership',
            icon: Users,
          },
          {
            title: 'Transfers',
            href: '/estate/EstateTransfer',
            icon: ArrowRightLeft,
          },
          {
            title: 'Assignments',
            href: '/estate/EstateAssignment',
            icon: FileText,
          },
          {
            title: 'Mortgage Consent',
            href: '/estate/EstateMortgageConsent',
            icon: FileCheck,
          },
          {
            title: 'Lease Preparation',
            href: '/estate/EstateLeasePreparation',
            icon: FileText,
          },
          {
            title: 'Additional Land',
            href: '/estate/EstateAdditionalLand',
            icon: Landmark,
          },
          {
            title: 'Layout Revision',
            href: '/estate/EstateLayoutRevision',
            icon: MapPin,
          },
          {
            title: 'Change of Use',
            href: '/estate/EstateChangeOfUse',
            icon: FileCheck,
          },
          {
            title: 'Reminders / Rate Revision',
            href: '/estate/EstateReminderRateRevision',
            icon: ClipboardList,
          },
          {
            title: 'Lease Renewal',
            href: '/estate/EstateLeaseRenewal',
            icon: RotateCcw,
          },
          {
            title: 'Serviced Plots / HOS',
            href: '/estate/EstateServicedPlotAllocation',
            icon: Landmark,
          },
          {
            title: 'Partially Serviced',
            href: '/estate/EstateLandsPartiallyServiced',
            icon: Home,
          },
          {
            title: 'Housing / HOS',
            href: '/estate/EstateHousingHomeOwnership',
            icon: Building2,
          },
          {
            title: 'Traditional Lands',
            href: '/estate/EstateTraditionalLands',
            icon: Landmark,
          },
          {
            title: 'Regularisation',
            href: '/estate/EstateTenancyRegularisation',
            icon: Award,
          },
          {
            title: 'Reporting Controls',
            href: '/estate/EstateReportingControls',
            icon: BarChart3,
          },
          {
            title: 'Reports',
            href: '/reports?module=estate',
            icon: BarChart3,
          },
        ],
      },
      {
        title: 'Property Management',
        href: '/estate/property-management',
        icon: Home,
        roles: PROPERTY_MANAGEMENT_ROLES,
        permissions: ['property-management.access'],
        accessMode: 'any',
        children: [
          {
            title: 'Dashboard',
            href: '/estate/property-management/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Property & Units',
            href: '/estate/property-management/EstatePropertyManagementPropertyUnit',
            icon: Building2,
          },
          {
            title: 'Portal Listings',
            href: '/estate/property-management/listings',
            icon: MapPin,
          },
          {
            title: 'Property Requests',
            href: '/estate/property-management/EstatePropertyManagementListingApplication',
            icon: ClipboardList,
          },
          {
            title: 'Lease Management',
            href: '/estate/property-management/EstatePropertyManagementLease',
            icon: FileCheck,
          },
          {
            title: 'Tenants / Occupants',
            href: '/estate/property-management/EstatePropertyManagementTenantOccupant',
            icon: Users,
          },
          {
            title: 'Billing / Service Charge',
            href: '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
            icon: CreditCard,
          },
          {
            title: 'Ground Rent',
            href: '/estate/property-management/EstatePropertyManagementGroundRent',
            icon: Banknote,
          },
          {
            title: 'Occupancy / Availability',
            href: '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
            icon: Home,
          },
          {
            title: 'Move-in / Handover',
            href: '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
            icon: ClipboardCheck,
          },
          {
            title: 'Records Index',
            href: '/estate/property-management/EstatePropertyManagementDocumentRecordIndex',
            icon: FileText,
          },
        ],
      },
      {
        title: 'Facilities & Corporate Services',
        href: '/estate/facilities',
        icon: Building2,
        roles: FACILITIES_ROLES,
        permissions: ['facilities.access'],
        accessMode: 'any',
        children: [
          {
            title: 'Dashboard',
            href: '/estate/facilities/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Maintenance Intake',
            href: '/estate/facilities/EstateFacilityMaintenance',
            icon: Wrench,
          },
          {
            title: 'Complaints',
            href: '/estate/facilities/EstateFacilityComplaint',
            icon: MessageSquare,
          },
          {
            title: 'Service Providers',
            href: '/estate/facilities/EstateFacilityServiceProvider',
            icon: Briefcase,
          },
          {
            title: 'Staff / Cleaners',
            href: '/estate/facilities/EstateFacilityStaffCleaner',
            icon: ClipboardCheck,
          },
          {
            title: 'Facilities Assets',
            href: '/estate/facilities/EstateFacilityAssetRegister',
            icon: Database,
          },
        ],
      },
    ],
  },
  {
    // Estate/DMS integration: Legal procedure links support Estate-driven cases while keeping Legal under its own sidebar area.
    title: 'Legal',
    href: '/legal',
    icon: Gavel,
    roles: LEGAL_ROLES,
    permissions: ['legal.access', 'legal.read'],
    accessMode: 'any',
    children: [
      { title: 'Dashboard', href: '/legal/dashboard', icon: BarChart3 },
      { title: 'Procedures', href: '/legal', icon: ClipboardList },
      {
        title: 'Property Agreement Reviews',
        href: '/legal/LegalPropertyAgreementReview',
        icon: FileCheck,
      },
      {
        title: 'Procedure Manual',
        href: '/legal/LegalProcedure',
        icon: BookOpen,
      },
      {
        title: 'Legal Opinions / Advisory',
        href: '/legal/LegalOpinionAdvisory',
        icon: MessageSquare,
      },
      {
        title: 'External Counsel',
        href: '/legal/LegalExternalCounsel',
        icon: Briefcase,
      },
      { title: 'Mortgages', href: '/legal/LegalMortgage', icon: FileCheck },
      {
        title: 'Mortgage In Principle',
        href: '/legal/LegalMortgageInPrinciple',
        icon: FileCheck,
      },
      {
        title: 'Court Processes',
        href: '/legal/LegalCourtProcess',
        icon: Gavel,
      },
      {
        title: 'Other Court Processes',
        href: '/legal/LegalOtherCourtProcess',
        icon: Scale,
      },
      {
        title: 'Termination / Recognition',
        href: '/legal/LegalTerminationRecognition',
        icon: ShieldCheck,
      },
      {
        title: 'Assignment / Sublease / Vesting',
        href: '/legal/LegalAssignmentSubleaseVesting',
        icon: Landmark,
      },
      {
        title: 'Leases / Variation / Renewal',
        href: '/legal/LegalLeaseVariationRenewalSublease',
        icon: FileText,
      },
      { title: 'Transfers', href: '/legal/LegalTransfer', icon: GitBranch },
      {
        title: 'Workflow Setup',
        href: '/administration/workflow?q=Legal',
        icon: Workflow,
        navigationSurface: 'settings',
        roles: [
          'admin',
          'SystemAdmin',
          'SuperAdmin',
          'TenantAdmin',
          'WorkflowAdmin',
        ],
      },
    ],
  },
  {
    title: 'Enquiry',
    href: '/helpdesk/enquiry',
    icon: MessageSquare,
    children: [
      {
        title: 'Internal',
        href: '/helpdesk/enquiry/internal',
        icon: Building,
        permissions: ['enquiry.internal.access'],
        children: [
          {
            title: 'Dashboard',
            href: '/helpdesk/enquiry/internal/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Enquiries',
            href: '/helpdesk/enquiry/internal',
            icon: FileText,
          },
          {
            title: 'Queue',
            href: '/helpdesk/enquiry/internal/queue',
            icon: ClipboardList,
          },
          {
            title: 'New Enquiry',
            href: '/helpdesk/enquiry/internal/new',
            icon: MessageSquare,
          },
          {
            title: 'Problems',
            href: '/helpdesk/enquiry/internal/problems',
            icon: AlertCircle,
          },
          {
            title: 'Knowledge Base',
            href: '/helpdesk/enquiry/internal/knowledge-base',
            icon: FileText,
          },
          {
            title: 'FAQs',
            href: '/helpdesk/enquiry/internal/faq',
            icon: HelpCircle,
          },
        ],
      },
      {
        title: 'External',
        href: '/helpdesk/enquiry/external',
        icon: Users,
        permissions: ['enquiry.external.access'],
        children: [
          {
            title: 'Dashboard',
            href: '/helpdesk/enquiry/external/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Enquiries',
            href: '/helpdesk/enquiry/external',
            icon: FileText,
          },
          {
            title: 'Queue',
            href: '/helpdesk/enquiry/external/queue',
            icon: ClipboardList,
          },
          {
            title: 'New Enquiry',
            href: '/helpdesk/enquiry/external/new',
            icon: MessageSquare,
          },
          {
            title: 'Problems',
            href: '/helpdesk/enquiry/external/problems',
            icon: AlertCircle,
          },
          {
            title: 'Knowledge Base',
            href: '/helpdesk/enquiry/external/knowledge-base',
            icon: FileText,
          },
          {
            title: 'FAQs',
            href: '/helpdesk/enquiry/external/faq',
            icon: HelpCircle,
          },
        ],
      },
    ],
  },
  {
    title: 'Helpdesk',
    href: '/helpdesk/helpdesk-complaints',
    icon: HelpCircle,
    children: [
      {
        title: 'Internal',
        href: '/helpdesk/helpdesk-complaints/internal',
        icon: Building,
        permissions: ['support.internal.access'],
        children: [
          {
            title: 'Dashboard',
            href: '/helpdesk/helpdesk-complaints/internal/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Tickets',
            href: '/helpdesk/helpdesk-complaints/internal',
            icon: FileText,
          },
          {
            title: 'Queue',
            href: '/helpdesk/helpdesk-complaints/internal/queue',
            icon: ClipboardList,
          },
          {
            title: 'New Ticket',
            href: '/helpdesk/helpdesk-complaints/internal/new',
            icon: FileText,
          },
          {
            title: 'Approvals',
            href: '/helpdesk/helpdesk-complaints/internal/approvals',
            icon: Workflow,
          },
          {
            title: 'Service Requests',
            href: '/helpdesk/helpdesk-complaints/internal/requests',
            icon: ClipboardList,
          },
          {
            title: 'Problems',
            href: '/helpdesk/helpdesk-complaints/internal/problems',
            icon: AlertCircle,
          },
          {
            title: 'Knowledge Base',
            href: '/helpdesk/helpdesk-complaints/internal/knowledge-base',
            icon: FileText,
          },
          {
            title: 'FAQs',
            href: '/helpdesk/helpdesk-complaints/internal/faq',
            icon: HelpCircle,
          },
        ],
      },
      {
        title: 'External',
        href: '/helpdesk/helpdesk-complaints/external',
        icon: Users,
        permissions: ['support.external.access'],
        children: [
          {
            title: 'Dashboard',
            href: '/helpdesk/helpdesk-complaints/external/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Tickets',
            href: '/helpdesk/helpdesk-complaints/external',
            icon: FileText,
          },
          {
            title: 'Queue',
            href: '/helpdesk/helpdesk-complaints/external/queue',
            icon: ClipboardList,
          },
          {
            title: 'New Ticket',
            href: '/helpdesk/helpdesk-complaints/external/new',
            icon: FileText,
          },
          {
            title: 'Customer Support',
            href: '/helpdesk/helpdesk-complaints/external/support',
            icon: Users,
          },
          {
            title: 'Problems',
            href: '/helpdesk/helpdesk-complaints/external/problems',
            icon: AlertCircle,
          },
          {
            title: 'Knowledge Base',
            href: '/helpdesk/helpdesk-complaints/external/knowledge-base',
            icon: FileText,
          },
          {
            title: 'FAQs',
            href: '/helpdesk/helpdesk-complaints/external/faq',
            icon: HelpCircle,
          },
        ],
      },
    ],
  },
  {
    title: 'Reports',
    href: '/reports',
    icon: BarChart3,
    roles: ADMINISTRATION_ROLES,
    permissions: ['reports.read'],
    accessMode: 'any',
    children: [
      {
        title: 'Financial Reports',
        href: '/reports/financial',
        icon: CreditCard,
      },
      {
        title: 'Report Automation',
        href: '/reports/automation',
        icon: CalendarClock,
      },
      {
        title: 'Procurement Reports',
        href: '/reports/purchasing',
        icon: ShoppingCart,
      },
      { title: 'Inventory Reports', href: '/reports/inventory', icon: Package },
      {
        title: 'Quantity Survey Reports',
        href: '/reports/quantity-survey',
        icon: Scale,
      },
      {
        title: 'Audit & Compliance Reports',
        href: '/reports/audit-compliance',
        icon: ShieldCheck,
      },
      { title: 'Sales Reports', href: '/reports/sales', icon: ShoppingCart },
      {
        title: 'HR Reports',
        href: '/reports/human-resources',
        icon: UserCheck,
      },
      { title: 'Estate Reports', href: '/reports/estate', icon: Home },
      {
        title: 'Development Reports',
        href: '/reports/development',
        icon: Code,
      },
      {
        title: 'Operations Reports',
        href: '/reports/operations',
        icon: BarChart3,
      },
      {
        title: 'Property Management Reports',
        href: '/reports?module=property-management',
        icon: Building2,
      },
      {
        title: 'Facilities Reports',
        href: '/reports?module=facilities',
        icon: Building,
      },
      {
        title: 'Legal Reports',
        href: '/reports?module=legal',
        icon: Gavel,
      },
      {
        title: 'DMS Reports',
        href: '/reports?module=dms',
        icon: BookTemplate,
      },
      {
        title: 'Planning Reports',
        href: '/reports?module=planning',
        icon: MapPin,
      },
    ],
  },
  {
    title: 'Notifications',
    href: '/notifications',
    icon: Bell,
    navigationSurface: 'settings',
    children: [
      {
        title: 'Notification Center',
        href: '/notifications#center',
        icon: Bell,
      },
      { title: 'Email Campaigns', href: '/notifications#email', icon: Mail },
      { title: 'Templates', href: '/notifications#templates', icon: FileText },
      { title: 'Settings', href: '/notifications#settings', icon: Settings },
    ],
  },
  {
    title: 'Administration',
    href: '/administration',
    icon: Settings,
    navigationSurface: 'settings',
    roles: ADMINISTRATION_ROLES,
    // admin.hr: an HR practitioner holding the seeded admin.hr grant must see the parent
    // node, or the HR child below would be filtered out with it.
    // Reference.Geography.* is listed too: a reference-data curator from another module holds no
    // admin.hr, and without this the parent node would hide the child they do have.
    permissions: [
      'Finance.Admin',
      'admin.hr',
      'admin.she',
      'Reference.Geography.Write',
      'Reference.Geography.Admin',
    ],
    accessMode: 'any',
    children: [
      {
        title: 'Message Queue',
        href: '/administration/notifications',
        icon: Bell,
        roles: ADMINISTRATION_ROLES,
      },
      {
        title: 'System Logs',
        href: '/administration/system-exception-logs',
        icon: AlertTriangle,
        roles: ADMINISTRATION_ROLES,
      },
      {
        title: 'Document Management',
        href: '/administration/document-management',
        icon: BookTemplate,
        roles: ADMINISTRATION_ROLES,
        permissions: ['Finance.Admin'],
        accessMode: 'any',
        children: [
          {
            title: 'Integration Contract',
            href: '/administration/document-management/integration-contract',
            icon: GitBranch,
          },
          {
            title: 'Metadata Templates',
            href: '/administration/document-management/metadata-templates',
            icon: BookTemplate,
          },
          {
            title: 'Document Templates',
            href: '/administration/document-management/document-templates',
            icon: FileText,
          },
          {
            title: 'Access & Retention',
            href: '/administration/document-management/access-retention',
            icon: ShieldCheck,
          },
          {
            title: 'Workflow Setup',
            href: '/administration/workflow?q=Document%20Management',
            icon: Workflow,
          },
        ],
      },
      {
        title: 'Finance',
        href: '/administration/finance',
        icon: CreditCard,
        permissions: ['Finance.Admin'],
        children: [
          {
            title: 'Core Accounting',
            href: '/administration/finance/settings',
            icon: Settings,
            children: [
              {
                title: 'Finance Settings',
                href: '/administration/finance/settings',
                icon: Settings,
              },
              {
                title: 'Finance Access Scopes',
                href: '/administration/finance/access-scopes',
                icon: ShieldCheck,
                permissions: ['Finance.AccessScopes.Manage'],
              },
              {
                title: 'Chart of Accounts',
                href: '/finance/accounts',
                icon: CreditCard,
              },
              {
                title: 'Account Segments',
                href: '/administration/finance/account-segments',
                icon: FolderTree,
              },
              {
                title: 'Coding Dimensions',
                href: '/administration/finance/dimensions',
                icon: ListTree,
                permissions: ['Finance.Dimensions.Manage'],
              },
              {
                title: 'Dimension Route Readiness',
                href: '/administration/finance/dimensions/readiness',
                icon: ShieldCheck,
                permissions: ['Finance.Dimensions.Certification.Manage'],
              },
              {
                title: 'Account Generator',
                href: '/administration/finance/account-generator',
                icon: FileText,
              },
              {
                title: 'Fiscal Calendar Setup',
                href: '/administration/finance/fiscal-calendar',
                icon: Calendar,
              },
              {
                title: 'Close Templates',
                href: '/administration/finance/close-templates',
                icon: ClipboardList,
              },
            ],
          },
          {
            title: 'Tax & Currency',
            href: '/administration/finance/tax',
            icon: Globe,
            children: [
              {
                title: 'Tax Configuration',
                href: '/administration/finance/tax',
                icon: Settings,
              },
              {
                title: 'Currencies',
                href: '/administration/finance/currencies',
                icon: DollarSign,
              },
            ],
          },
          {
            title: 'Payments & Documents',
            href: '/administration/finance/payment-terms',
            icon: FileText,
            children: [
              {
                title: 'Payment Terms',
                href: '/administration/finance/payment-terms',
                icon: CreditCard,
              },
              {
                title: 'Payment Methods',
                href: '/administration/finance/payment-methods',
                icon: CreditCard,
              },
              {
                title: 'Document Numbering',
                href: '/administration/finance/document-numbering',
                icon: FileText,
              },
            ],
          },
          {
            title: 'Asset & Unit Accounting',
            href: '/administration/finance/fixed-asset-categories',
            icon: FolderTree,
            children: [
              {
                title: 'Fixed Asset Categories',
                href: '/administration/finance/fixed-asset-categories',
                icon: FolderTree,
              },
              {
                title: 'Unit Types',
                href: '/administration/finance/unit-types',
                icon: FileText,
              },
              {
                title: 'Ratio Definitions',
                href: '/administration/finance/ratio-definitions',
                icon: BarChart3,
              },
            ],
          },
        ],
      },
      {
        // SHE configuration is the safety function's own settings tree, beside
        // HR and gated on its own admin.she (SHE Manager + administrators — not officers, not
        // HR), the platform's one-admin-gate-per-module convention. Matches the administration
        // layout's /administration/safety route branch; the API's configuration writes are
        // HR.She.Admin to agree with it.
        title: 'Safety (SHE)',
        href: '/administration/safety',
        icon: HardHat,
        roles: ADMINISTRATION_ROLES,
        permissions: ['admin.she'],
        accessMode: 'any',
        children: [
          {
            title: 'Incident Types',
            href: '/administration/safety/incident-types',
            icon: AlertTriangle,
          },
          {
            title: 'Injury Types',
            href: '/administration/safety/injury-types',
            icon: Bandage,
          },
          {
            title: 'Body Parts',
            href: '/administration/safety/body-parts',
            icon: PersonStanding,
          },
          {
            title: 'CA Templates',
            href: '/administration/safety/corrective-action-templates',
            icon: ClipboardList,
          },
          {
            title: 'Regulatory Bodies',
            href: '/administration/safety/regulatory-bodies',
            icon: Landmark,
          },
          {
            title: 'PPE Types',
            href: '/administration/safety/ppe-types',
            icon: HardHat,
          },
          {
            title: 'PPE Requirements',
            href: '/administration/safety/ppe-requirements',
            icon: ClipboardCheck,
          },
          {
            title: 'Reminder Engine',
            href: '/administration/safety/reminders',
            icon: AlarmClock,
          },
        ],
      },
      {
        // Shared cross-module reference data. Deliberately NOT under Administration → HR: the
        // geography tree is consumed by Estate, Sales, Procurement and Inventory too, and an
        // administrator of any of those should not need an HR grant to curate a list of districts.
        title: 'Reference Data',
        href: '/administration/reference/geography',
        icon: Globe2,
        roles: ADMINISTRATION_ROLES,
        // Either tier admits — administrators hold both, the HR role holds Write. Matches the
        // /administration/reference route gate in the administration layout.
        permissions: ['Reference.Geography.Write', 'Reference.Geography.Admin'],
        accessMode: 'any',
        children: [
          {
            title: 'Geography',
            href: '/administration/reference/geography',
            icon: Globe2,
          },
        ],
      },
      {
        title: 'HR',
        href: '/administration/hr',
        icon: UserCheck,
        // Admins by role, HR practitioners by the seeded admin.hr grant — HR maintains its
        // own reference data (leave types, org structures). Matches the administration
        // layout's /administration/hr route gate.
        roles: ADMINISTRATION_ROLES,
        permissions: ['admin.hr'],
        accessMode: 'any',
        // Defined once, in config/hr-setup-nav.ts, because three surfaces render this list: this
        // node, the /administration/hr hub page, and /settings/modules/human-resources (which
        // derives its tiles from this node). They had drifted badly — Awards had a hub card and
        // no nav entry at all, the Discipline Catalogue sat behind a childless Discipline node,
        // and thirteen nav entries had no hub card.
        //
        // ⚠ Every leaf must live inside a group. The settings module page captions a tile with
        // its ancestor path, so a leaf hanging directly off this node renders with no subtext —
        // which is what left twenty-two tiles on that page blank.
        children: hrSetupNavChildren,
      },
      {
        title: 'Procurement',
        href: '/administration/procurement',
        icon: Briefcase,
        roles: ADMINISTRATION_ROLES,
        children: [
          // Procurement governance routes come from the target branch; retain them alongside
          // the existing partner/setup routes and the Finance branch's administration role gate.
          {
            title: 'Policy Profiles',
            href: '/administration/procurement/policy-profiles',
            icon: ShieldCheck,
          },
          {
            title: 'Policy Sets',
            href: '/administration/procurement/policy-sets',
            icon: ShieldCheck,
          },
          {
            title: 'Policy Simulator',
            href: '/administration/procurement/compliance-simulator',
            icon: ClipboardCheck,
          },
          {
            title: 'SOD Controls',
            href: '/administration/procurement/sod-controls',
            icon: Swords,
          },
          {
            title: 'Scopes & Committees',
            href: '/administration/procurement/access-controls',
            icon: Users,
          },
          {
            title: 'Purchase Order Settings',
            href: '/administration/procurement/purchase-order-settings',
            icon: Settings,
          },
          {
            title: 'Partner Categories',
            href: '/administration/procurement/partner-categories',
            icon: FolderTree,
          },
          {
            title: 'Contractor Specializations',
            href: '/administration/procurement/contractor-specializations',
            icon: Wrench,
          },
          {
            title: 'License Types',
            href: '/administration/procurement/license-types',
            icon: FileCheck,
          },
          {
            title: 'Approval Workflows',
            href: '/administration/procurement/approval-workflows',
            icon: Workflow,
          },
          // { title: 'Supplier Categories', href: '/administration/procurement/supplier-categories', icon: Briefcase },
          // { title: 'Purchase Categories', href: '/administration/procurement/purchase-categories', icon: Package },
          // { title: 'Terms & Conditions', href: '/administration/procurement/terms', icon: FileText },
          // { title: 'Tender Templates', href: '/administration/procurement/tender-templates', icon: Gavel },
          {
            title: 'Evaluation Criteria',
            href: '/administration/procurement/evaluation-criteria',
            icon: Star,
          },
          {
            title: 'Evaluation Templates',
            href: '/administration/procurement/evaluation-templates',
            icon: FileText,
          },
          {
            title: 'Document Types',
            href: '/administration/procurement/document-types',
            icon: FileText,
          },
          {
            title: 'Award Verification Checklists',
            href: '/administration/procurement/award-verification-checklists',
            icon: ClipboardCheck,
          },
        ],
      },
      {
        title: 'Inventory',
        href: '/administration/inventory',
        icon: Package,
        roles: ADMINISTRATION_ROLES,
        children: [
          {
            title: 'Units of Measure',
            href: '/administration/inventory/units-of-measure',
            icon: Package,
          },
          {
            title: 'UoM Schedules',
            href: '/administration/inventory/uom-schedules',
            icon: Package,
          },
          {
            title: 'Warehouses & Locations',
            href: '/administration/inventory/warehouses',
            icon: Building2,
          },
          {
            title: 'Issue Accounting & Asset Custody',
            href: '/administration/inventory/issue-accounting',
            icon: Landmark,
            permissions: ['procurement.inventory.master-data.manage'],
          },
          {
            title: 'Physical Count Decisions',
            href: '/administration/inventory/count-decisions',
            icon: Settings,
            permissions: ['procurement.inventory.master-data.manage'],
          },
        ],
      },
      {
        title: 'Sales',
        href: '/administration/sales',
        icon: ShoppingCart,
        roles: ADMINISTRATION_ROLES,
        children: [
          {
            title: 'Sales Setup',
            href: '/administration/sales',
            icon: Settings,
          },
          {
            title: 'Customer Categories',
            href: '/administration/sales/customer-categories',
            icon: Users,
          },
          {
            title: 'Sales Territories',
            href: '/administration/sales/territories',
            icon: Building,
          },
          {
            title: 'Price Lists',
            href: '/administration/sales/price-lists',
            icon: CreditCard,
          },
          {
            title: 'Sales Channels',
            href: '/administration/sales/channels',
            icon: ShoppingCart,
          },
          {
            title: 'Commission Rules',
            href: '/administration/sales/commission',
            icon: CreditCard,
          },
        ],
      },
      {
        title: 'Marketing',
        href: '/administration/marketing',
        icon: Megaphone,
        roles: ADMINISTRATION_ROLES,
        children: [
          {
            title: 'Campaign Templates',
            href: '/administration/marketing/templates',
            icon: Megaphone,
          },
          {
            title: 'Lead Sources',
            href: '/administration/marketing/lead-sources',
            icon: Users,
          },
          {
            title: 'Market Segments',
            href: '/administration/marketing/segments',
            icon: Users,
          },
          {
            title: 'Marketing Channels',
            href: '/administration/marketing/channels',
            icon: Megaphone,
          },
        ],
      },
      {
        title: 'Estate',
        href: '/administration/estate',
        icon: Home,
        roles: ADMINISTRATION_ROLES,
        children: [
          {
            title: 'Property Types',
            href: '/administration/estate/property-types',
            icon: Home,
          },
          {
            title: 'Lease Templates',
            href: '/administration/estate/lease-templates',
            icon: FileText,
          },
          {
            title: 'Maintenance Categories',
            href: '/administration/estate/maintenance-categories',
            icon: Wrench,
          },
          {
            title: 'Tenant Categories',
            href: '/administration/estate/tenant-categories',
            icon: Users,
          },
        ],
      },
      {
        title: 'Projects',
        href: '/administration/project-management',
        icon: Briefcase,
        permissions: [
          'admin.project-management',
          'quantity-survey.configuration.read',
          'quantity-survey.workspace.read',
          'civil-engineering.configuration.read',
        ],
        accessMode: 'any',
        children: [
          {
            title: 'Overview',
            href: '/administration/project-management',
            icon: Briefcase,
          },
          {
            title: 'Project Types',
            href: '/administration/project-management/types',
            icon: FileText,
          },
          {
            title: 'Priorities',
            href: '/administration/project-management/priorities',
            icon: BarChart3,
          },
          {
            title: 'BOQ Item Types',
            href: '/administration/project-management/item-types',
            icon: Package,
          },
          {
            title: 'Templates',
            href: '/administration/project-management/templates',
            icon: Users,
          },
          {
            title: 'Unit Types',
            href: '/administration/project-management/unit-types',
            icon: Building2,
          },
          {
            title: 'Settings',
            href: '/administration/project-management/settings',
            icon: Settings,
          },
          {
            title: 'Quantity Survey Policy',
            href: '/administration/project-management/quantity-survey-config',
            icon: ShieldCheck,
            permissions: ['quantity-survey.configuration.read'],
          },
          {
            title: 'Civil Engineering Policy',
            href: '/administration/project-management/civil-engineering-config',
            icon: ShieldCheck,
            permissions: ['civil-engineering.configuration.read'],
          },
          {
            title: 'Quantity Survey Catalogues',
            href: '/administration/project-management/quantity-survey-catalogues',
            icon: Package,
            permissions: ['quantity-survey.configuration.read'],
          },
          {
            title: 'Quantity Survey Rate Library',
            href: '/administration/project-management/quantity-survey-rate-library',
            icon: BarChart3,
            permissions: ['quantity-survey.workspace.read'],
          },
          {
            title: 'QS Escalation Formulas',
            href: '/administration/project-management/quantity-survey-escalation',
            icon: LineChart,
            permissions: ['quantity-survey.workspace.read'],
          },
        ],
      },
      {
        title: 'Maintenance',
        href: '/administration/maintenance',
        icon: Wrench,
        permissions: ['admin.maintenance'],
        children: [
          {
            title: 'Maintenance Settings',
            href: '/administration/maintenance/maintenance-settings',
            icon: Settings,
          },
          {
            title: 'Asset Categories',
            href: '/administration/maintenance/asset-categories',
            icon: Package,
          },
          {
            title: 'Work Order Types',
            href: '/administration/maintenance/work-order-types',
            icon: FileText,
          },
          {
            title: 'Maintenance Types',
            href: '/administration/maintenance/maintenance-types',
            icon: Wrench,
          },
          {
            title: 'Priority Levels',
            href: '/administration/maintenance/priorities',
            icon: AlertTriangle,
          },
          {
            title: 'Task Templates',
            href: '/administration/maintenance/task-templates',
            icon: FileText,
          },
          {
            title: 'Quality Checklists',
            href: '/administration/maintenance/quality-checklists',
            icon: CheckSquare,
          },
          {
            title: 'Admission Checklists',
            href: '/administration/maintenance/admission-checklists',
            icon: ClipboardCheck,
          },
          {
            title: 'Inspection Templates',
            href: '/administration/maintenance/inspection-templates',
            icon: ClipboardCheck,
          },
          // { title: 'Inspectors', href: '/administration/maintenance/inspectors', icon: Users },
          // { title: 'Maintenance Schedules', href: '/administration/maintenance/schedules', icon: Calendar },
          // { title: 'Technician Skills', href: '/administration/maintenance/skills', icon: Users },
          // { title: 'Safety Protocols', href: '/administration/maintenance/safety', icon: Shield },
        ],
      },
      {
        title: 'Fleet',
        href: '/administration/fleet-management',
        icon: Truck,
        permissions: ['admin.fleet-management'],
        children: [
          {
            title: 'Fleet Settings',
            href: '/administration/fleet-management/settings',
            icon: Settings,
          },
          {
            title: 'Trip Destinations',
            href: '/administration/fleet-management/trip-destinations',
            icon: MapPin,
          },
          {
            title: 'Compliance Templates',
            href: '/administration/fleet-management/compliance-templates',
            icon: ClipboardList,
          },
        ],
      },
      {
        title: 'Helpdesk',
        href: '/administration/helpdesk',
        icon: HelpCircle,
        roles: ADMINISTRATION_ROLES,
        children: [
          {
            title: 'Ticket Categories',
            href: '/administration/helpdesk/categories',
            icon: FileText,
          },
          {
            title: 'Root Causes',
            href: '/administration/helpdesk/root-causes',
            icon: Target,
          },
          {
            title: 'Canned Responses',
            href: '/administration/helpdesk/canned-responses',
            icon: MessageSquare,
          },
          {
            title: 'Knowledge Base',
            href: '/administration/helpdesk/knowledge-base',
            icon: BookOpen,
          },
          {
            title: 'FAQs',
            href: '/administration/helpdesk/faqs',
            icon: HelpCircle,
          },
          {
            title: 'Priority Levels',
            href: '/administration/helpdesk/priorities',
            icon: BarChart3,
          },
          {
            title: 'SLA Templates',
            href: '/administration/helpdesk/sla',
            icon: FileText,
          },
          {
            title: 'Escalations',
            href: '/administration/helpdesk/escalations',
            icon: AlertTriangle,
          },
          {
            title: 'Compliance',
            href: '/administration/helpdesk/compliance',
            icon: Shield,
          },
          {
            title: 'Workflow Routing',
            href: '/administration/helpdesk/workflows',
            icon: Workflow,
          },
          {
            title: 'Service Catalog',
            href: '/administration/helpdesk/service-catalog',
            icon: ClipboardList,
          },
          {
            title: 'Support Channels',
            href: '/administration/helpdesk/channels',
            icon: HelpCircle,
          },
        ],
      },
      {
        title: 'Workflow',
        href: '/administration/workflow',
        icon: Workflow,
        roles: ADMINISTRATION_ROLES,
      },
      {
        title: 'System',
        href: '/administration/system',
        icon: Settings,
        roles: ADMINISTRATION_ROLES,
        children: [
          {
            title: 'Security',
            href: '/administration/security/dashboard',
            icon: Shield,
          },
          {
            title: 'Reports Administration',
            href: '/administration/reports',
            icon: BarChart3,
          },
          {
            title: 'User Management',
            href: '/administration/identity-management/users',
            icon: Users,
          },
          {
            title: 'Role Management',
            href: '/administration/identity-management/roles',
            icon: Shield,
          },
          {
            title: 'User-Employee Links',
            href: '/administration/user-employee-links',
            icon: UserCheck,
          },
          {
            title: 'HR & Identity Reconciliation',
            href: '/administration/identity-management/hr-reconciliation',
            icon: RefreshCw,
          },
          {
            title: 'Tenant Management',
            href: '/administration/tenant-management',
            icon: Building,
          },
          {
            title: 'Email Settings',
            href: '/administration/settings/email',
            icon: Mail,
          },
          {
            title: 'SMS Settings',
            href: '/administration/settings/sms',
            icon: MessageSquare,
          },
          {
            title: 'File Uploads',
            href: '/administration/settings/file-uploads',
            icon: FileCheck,
          },
          {
            title: 'Field Labels',
            href: '/administration/settings/field-labels',
            icon: Tag,
          },
          {
            title: 'User-Tenant Mapping',
            href: '/administration/user-tenant-mapping',
            icon: Users,
          },
          {
            title: 'Security Logs',
            href: '/administration/identity-management/security-logs',
            icon: FileText,
          },
          {
            title: 'Audit Logs',
            href: '/administration/audit-logs',
            icon: FileText,
          },
          {
            title: 'Data Retention',
            href: '/administration/security/retention',
            icon: Clock,
          },
          { title: 'Data Sources', href: '/data-sources', icon: Database },
        ],
      },
    ],
  },
];

export function selectNavigationSurface(
  items: NavItem[],
  surface: 'operations' | 'settings',
  inheritedSurface: 'operations' | 'settings' = 'operations'
): NavItem[] {
  return items.reduce<NavItem[]>((selected, item) => {
    const itemSurface = item.navigationSurface ?? inheritedSurface;
    const children = item.children
      ? selectNavigationSurface(item.children, surface, itemSurface)
      : undefined;

    if (itemSurface !== surface && (!children || children.length === 0)) {
      return selected;
    }

    selected.push({
      ...item,
      children,
    });
    return selected;
  }, []);
}

export const sidebarNavigationItems = selectNavigationSurface(
  navigationItems,
  'operations'
);
export const settingsNavigationItems = selectNavigationSurface(
  navigationItems,
  'settings'
);

interface SidebarProps {
  className?: string;
  defaultCollapsed?: boolean;
}

export function Sidebar({ className, defaultCollapsed = false }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(defaultCollapsed);
  const [hoverExpanded, setHoverExpanded] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [expandedSections, setExpandedSections] = useState<Set<string>>(
    new Set()
  );
  const [hoveredItem, setHoveredItem] = useState<string | null>(null);
  const [hoveredChild, setHoveredChild] = useState<string | null>(null);
  const [hoveredGrandChild, setHoveredGrandChild] = useState<string | null>(
    null
  );
  const [menuPositions, setMenuPositions] = useState<{
    [key: string]: { x: number; y: number; height: number };
  }>({});
  const [activeMenuPath, setActiveMenuPath] = useState<string[]>([]);
  const sidebarRef = useRef<HTMLDivElement>(null);
  const closeTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const openTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const pathname = usePathname() ?? '';
  const { hasAnyRole, hasAnyPermission } = useAuth();
  const sidebarIsCollapsed = collapsed && !hoverExpanded;

  useEffect(() => {
    setMounted(true);

    const handlePointerDown = (event: MouseEvent) => {
      const target = event.target as HTMLElement | null;
      if (!target) {
        return;
      }

      if (
        sidebarRef.current?.contains(target) ||
        target.closest('[data-sidebar-flyout="true"]')
      ) {
        return;
      }

      clearMenus();
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        clearMenus();
      }
    };

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
      if (closeTimeoutRef.current) {
        clearTimeout(closeTimeoutRef.current);
      }
      if (openTimeoutRef.current) {
        clearTimeout(openTimeoutRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (!mounted) {
      return;
    }

    clearMenus();
  }, [pathname, mounted]);

  const cancelPendingClose = () => {
    if (closeTimeoutRef.current) {
      clearTimeout(closeTimeoutRef.current);
      closeTimeoutRef.current = null;
    }
  };

  const cancelPendingOpen = () => {
    if (openTimeoutRef.current) {
      clearTimeout(openTimeoutRef.current);
      openTimeoutRef.current = null;
    }
  };

  const scheduleMenuClose = (delay = 420) => {
    cancelPendingClose();
    closeTimeoutRef.current = setTimeout(() => {
      clearMenus();
      setHoverExpanded(false);
    }, delay);
  };

  const scheduleMenuOpen = (callback: () => void, delay = 90) => {
    cancelPendingOpen();
    openTimeoutRef.current = setTimeout(() => {
      callback();
      openTimeoutRef.current = null;
    }, delay);
  };

  const calculateMenuPosition = (
    rect: DOMRect,
    menuKey: string,
    estimatedHeight: number = 400
  ) => {
    const viewportHeight = window.innerHeight;
    const viewportWidth = window.innerWidth;
    const menuWidth = 240; // Approximate menu width
    const padding = 20; // Padding from viewport edges
    // The top bar is sticky, h-16 and z-50 — the same layer as the flyout, painted later — so a
    // panel that starts near the top of the window has its first rows hidden behind it.
    const topPadding = 64 + 8;

    let x = rect.right + 8;
    let y = rect.top;

    // Adjust horizontal position if menu would go off-screen
    if (x + menuWidth > viewportWidth - padding) {
      x = Math.max(padding, rect.left - menuWidth - 8); // Position to the left instead
    }

    // Adjust vertical position if menu would go off-screen
    const maxMenuHeight = viewportHeight - topPadding - padding;
    const actualMenuHeight = Math.min(estimatedHeight, maxMenuHeight);

    if (y + actualMenuHeight > viewportHeight - padding) {
      const availableSpaceBelow = viewportHeight - y - padding;
      const availableSpaceAbove = rect.top - topPadding;

      if (
        availableSpaceAbove > availableSpaceBelow &&
        availableSpaceAbove >= 150
      ) {
        // Position above if there's more space and at least 150px available
        y = Math.max(topPadding, rect.bottom - actualMenuHeight);
      } else {
        // Position to fit in viewport with padding
        y = Math.max(topPadding, viewportHeight - actualMenuHeight - padding);
      }
    }

    // Ensure minimum top position
    y = Math.max(topPadding, y);

    return { x, y };
  };

  const openMainItemMenu = (itemTitle: string, event: React.MouseEvent) => {
    if (!event.currentTarget) return;
    const rect = event.currentTarget.getBoundingClientRect();

    // Estimate menu height based on number of children
    const menuItem = filterNavItems(sidebarNavigationItems).find(
      (item) => item.title === itemTitle
    );
    const childCount = menuItem?.children?.length || 0;
    const estimatedHeight = childCount * 32 + 10; // 32px rows + panel padding and borders

    const position = calculateMenuPosition(rect, itemTitle, estimatedHeight);
    setMenuPositions({
      [itemTitle]: {
        ...position,
        height: estimatedHeight,
      },
    });
    setHoveredItem(itemTitle);
    setHoveredChild(null);
    setHoveredGrandChild(null);
    setActiveMenuPath([itemTitle]);
  };

  const handleMainItemHover = (itemTitle: string, event: React.MouseEvent) => {
    cancelPendingClose();
    if (hoveredItem === itemTitle && !hoveredChild && !hoveredGrandChild) {
      return;
    }
    scheduleMenuOpen(() => openMainItemMenu(itemTitle, event));
  };

  const openChildItemMenu = (
    parentTitle: string,
    childTitle: string,
    event: React.MouseEvent
  ) => {
    if (!event.currentTarget) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const menuKey = `${parentTitle}-${childTitle}`;

    // Estimate menu height based on number of grandchildren
    const parentItem = filterNavItems(sidebarNavigationItems).find(
      (item) => item.title === parentTitle
    );
    const childItem = parentItem?.children?.find(
      (child) => child.title === childTitle
    );
    const grandChildCount = childItem?.children?.length || 0;
    const estimatedHeight = grandChildCount * 32 + 10; // 32px rows + panel padding and borders

    const position = calculateMenuPosition(rect, menuKey, estimatedHeight);
    setMenuPositions((prev) => ({
      ...prev,
      [menuKey]: {
        ...position,
        height: estimatedHeight,
      },
    }));
    setHoveredChild(childTitle);
    setHoveredGrandChild(null);
    setActiveMenuPath([parentTitle, childTitle]);
  };

  const handleChildItemHover = (
    parentTitle: string,
    childTitle: string,
    event: React.MouseEvent
  ) => {
    cancelPendingClose();
    if (
      hoveredChild === childTitle &&
      hoveredItem === parentTitle &&
      !hoveredGrandChild
    ) {
      return;
    }
    scheduleMenuOpen(
      () => openChildItemMenu(parentTitle, childTitle, event),
      75
    );
  };

  const openGrandChildItemMenu = (
    parentTitle: string,
    childTitle: string,
    grandChildTitle: string,
    event: React.MouseEvent
  ) => {
    if (!event.currentTarget) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const menuKey = `${parentTitle}-${childTitle}-${grandChildTitle}`;

    // Estimate menu height based on number of great-grandchildren (if any)
    const parentItem = filterNavItems(sidebarNavigationItems).find(
      (item) => item.title === parentTitle
    );
    const childItem = parentItem?.children?.find(
      (child) => child.title === childTitle
    );
    const grandChildItem = childItem?.children?.find(
      (grandChild) => grandChild.title === grandChildTitle
    );
    const greatGrandChildCount = grandChildItem?.children?.length || 0;
    const estimatedHeight = greatGrandChildCount * 32 + 10; // 32px rows + panel padding and borders

    const position = calculateMenuPosition(rect, menuKey, estimatedHeight);
    setMenuPositions((prev) => ({
      ...prev,
      [menuKey]: {
        ...position,
        height: estimatedHeight,
      },
    }));
    setHoveredGrandChild(grandChildTitle);
    setActiveMenuPath([parentTitle, childTitle, grandChildTitle]);
  };

  const handleGrandChildItemHover = (
    parentTitle: string,
    childTitle: string,
    grandChildTitle: string,
    event: React.MouseEvent
  ) => {
    cancelPendingClose();
    if (
      hoveredGrandChild === grandChildTitle &&
      hoveredChild === childTitle &&
      hoveredItem === parentTitle
    ) {
      return;
    }
    scheduleMenuOpen(
      () =>
        openGrandChildItemMenu(parentTitle, childTitle, grandChildTitle, event),
      60
    );
  };

  const handleMenuMouseEnter = () => {
    cancelPendingClose();
    cancelPendingOpen();
  };

  const getBridgeStyle = (fromX: number, menuKey: string) => {
    const targetMenu = menuPositions[menuKey];
    if (!targetMenu) {
      return undefined;
    }

    return {
      left: Math.min(fromX, targetMenu.x) - 12,
      top: targetMenu.y - 32,
      width: Math.abs(targetMenu.x - fromX) + 24,
      // Clamp to what the panel can actually show; an uncapped estimate would grow the bridge
      // past the bottom of the screen and swallow clicks beside the panel.
      height: Math.max(Math.min(targetMenu.height, window.innerHeight - targetMenu.y - 20) + 64, 180),
      pointerEvents: 'auto' as const,
      backgroundColor: 'transparent',
    };
  };

  const clearMenus = () => {
    cancelPendingClose();
    cancelPendingOpen();
    setHoveredItem(null);
    setHoveredChild(null);
    setHoveredGrandChild(null);
    setActiveMenuPath([]);
    setMenuPositions({});
  };

  const handleMenuMouseLeave = () => {
    scheduleMenuClose();
  };

  const handleSidebarMouseLeave = () => {
    scheduleMenuClose();
  };

  const handleSidebarMouseEnter = () => {
    cancelPendingClose();
    if (collapsed) {
      setHoverExpanded(true);
    }
  };

  const toggleSidebar = () => {
    setCollapsed((previous) => !previous);
    setHoverExpanded(false);
    clearMenus();
  };

  const toggleSection = (title: string) => {
    const newExpanded = new Set(expandedSections);
    if (newExpanded.has(title)) {
      newExpanded.delete(title);
    } else {
      newExpanded.add(title);
    }
    setExpandedSections(newExpanded);
  };

  const isActive = (href: string) => {
    if (href === '/dashboard') {
      return pathname === href;
    }
    return pathname.startsWith(href);
  };

  // A row that owns children is active when any screen beneath it is — its own href is only a
  // landing page and need not be a prefix of every child's route.
  const isActiveDeep = (item: NavItem): boolean =>
    isActive(item.href) || (item.children?.some(isActiveDeep) ?? false);

  // Siblings can nest by address at any level — Safety (SHE) under /hr/safety, Fleet under
  // /maintenance/fleet, PPE Issuance under /hr/safety/ppe, every environmental screen under
  // /hr/safety/environmental — so within one list only the sibling with the longest matching
  // href is highlighted; otherwise its shorter-addressed neighbour lights up as well. A row
  // that is active only through a child (a group) ranks below any direct match.
  const activeSibling = (siblings: NavItem[]): NavItem | null => {
    let best: NavItem | null = null;
    let bestScore = -1;
    for (const item of siblings) {
      const score = isActive(item.href) ? item.href.length : isActiveDeep(item) ? 0 : -1;
      if (score > bestScore) {
        best = item;
        bestScore = score;
      }
    }
    return best;
  };

  const filterNavItems = (items: NavItem[]): NavItem[] => {
    // During SSR or before mount, show all items to avoid hydration mismatch
    if (!mounted) {
      return items;
    }

    return filterNavigationByAccess(items, hasAnyRole, hasAnyPermission);
  };

  return (
    <div className="relative">
      <div
        ref={sidebarRef}
        onMouseEnter={handleSidebarMouseEnter}
        onMouseLeave={handleSidebarMouseLeave}
        className={cn(
          'flex h-full flex-col bg-white/95 dark:bg-[#181818]/95 backdrop-blur-xl border-r border-slate-200/50 dark:border-neutral-800/70 transition-all duration-300',
          sidebarIsCollapsed ? 'w-16' : 'w-64',
          className
        )}
      >
        {/* Header */}
        <div className="flex h-16 items-center justify-between px-4 border-b border-slate-200/50 dark:border-neutral-800/70">
          {!sidebarIsCollapsed && (
            <div className="flex items-center space-x-3">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-r from-blue-600 to-indigo-600">
                <Building2 className="h-5 w-5 text-white" />
              </div>
              <div>
                <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                  ERP System
                </h2>
              </div>
            </div>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={toggleSidebar}
            aria-label={
              collapsed ? 'Keep sidebar expanded' : 'Collapse sidebar'
            }
            className="h-8 w-8 p-0"
          >
            {collapsed ? (
              <Menu className="h-4 w-4" />
            ) : (
              <X className="h-4 w-4" />
            )}
          </Button>
        </div>

        {/* Navigation */}
        <nav className={cn('flex-1 overflow-y-auto p-2', sidebarIsCollapsed ? 'space-y-0' : 'space-y-0.5')}>
          {filterNavItems(sidebarNavigationItems).map((item, _index, siblings) => {
            const Icon = item.icon;
            const hasChildren = item.children && item.children.length > 0;
            const itemIsActive = item === activeSibling(siblings);

            return (
              <div key={item.title} className="relative">
                {hasChildren ? (
                  <button
                    aria-label={item.title}
                    title={item.title}
                    onMouseEnter={(e) => handleMainItemHover(item.title, e)}
                    onClick={(e) => {
                      if (hoveredItem === item.title) {
                        clearMenus();
                        return;
                      }
                      cancelPendingOpen();
                      openMainItemMenu(item.title, e);
                    }}
                    className={cn(
                      'flex w-full items-center justify-between font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      sidebarIsCollapsed
                        ? 'rounded-xl px-4 py-3 text-sm'
                        : 'min-h-9 gap-2 rounded-lg border border-transparent px-3 py-1.5 text-sm leading-5',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <div className={cn('flex items-center', sidebarIsCollapsed ? 'space-x-4' : 'min-w-0 gap-2.5')}>
                      <Icon className={cn('flex-shrink-0', sidebarIsCollapsed ? 'h-6 w-6' : 'h-4 w-4')} />
                      {!sidebarIsCollapsed && <span className="truncate">{item.title}</span>}
                    </div>
                    {!sidebarIsCollapsed && hasChildren && (
                      <ChevronRight className="h-4 w-4 flex-shrink-0" />
                    )}
                  </button>
                ) : (
                  <Link
                    href={item.href}
                    aria-label={item.title}
                    title={item.title}
                    className={cn(
                      'flex items-center font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      sidebarIsCollapsed
                        ? 'space-x-4 rounded-xl px-4 py-3 text-sm'
                        : 'min-h-9 gap-2.5 rounded-lg border border-transparent px-3 py-1.5 text-sm leading-5',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <Icon className={cn('flex-shrink-0', sidebarIsCollapsed ? 'h-6 w-6' : 'h-4 w-4')} />
                    {!sidebarIsCollapsed && <span className="truncate">{item.title}</span>}
                  </Link>
                )}
              </div>
            );
          })}
        </nav>
      </div>

      {/* Invisible Bridge for First Level Menu */}
      {hoveredItem && menuPositions[hoveredItem] && (
        <div
          data-sidebar-flyout="true"
          className="fixed z-40"
          style={getBridgeStyle(sidebarIsCollapsed ? 64 : 256, hoveredItem)}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        />
      )}

      {/* First Level Floating Submenu */}
      {hoveredItem && menuPositions[hoveredItem] && (
        <div
          data-sidebar-flyout="true"
          className="fixed bg-white/95 dark:bg-[#202020]/95 backdrop-blur-xl border border-slate-200/50 dark:border-neutral-700/70 rounded-lg shadow-lg z-50 min-w-56 py-1 max-h-[calc(100vh-40px)] overflow-y-auto"
          style={{
            left: menuPositions[hoveredItem].x,
            top: menuPositions[hoveredItem].y,
            // The panel is clamped to the viewport from its own top, not from the top of the screen —
            // otherwise a long menu positioned part-way down runs off the bottom and the tail is unreachable.
            maxHeight: `calc(100vh - ${menuPositions[hoveredItem].y + 20}px)`,
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgb(148 163 184) transparent',
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        >
          {filterNavItems(sidebarNavigationItems)
            .find((item) => item.title === hoveredItem)
            ?.children?.map((child, _index, siblings) => {
              const ChildIcon = child.icon;
              const childIsActive = child === activeSibling(siblings);
              const childHasChildren =
                child.children && child.children.length > 0;

              return (
                <div key={child.title} className="relative">
                  {childHasChildren ? (
                    <button
                      onMouseEnter={(e) =>
                        handleChildItemHover(hoveredItem, child.title, e)
                      }
                      onClick={(e) => {
                        if (hoveredChild === child.title) {
                          setHoveredChild(null);
                          setHoveredGrandChild(null);
                          setActiveMenuPath([hoveredItem]);
                          return;
                        }
                        cancelPendingOpen();
                        openChildItemMenu(hoveredItem, child.title, e);
                      }}
                      className="w-full flex min-h-8 items-center gap-2.5 px-3 py-1.5 text-sm font-medium leading-5 text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-700 dark:text-slate-300 transition-colors"
                    >
                      <ChildIcon className="h-4 w-4 flex-shrink-0" />
                      <span className="flex-1">{child.title}</span>
                      <ChevronRight className="h-4 w-4 flex-shrink-0" />
                    </button>
                  ) : (
                    <Link
                      href={child.href}
                      onClick={clearMenus}
                      className={cn(
                        'flex min-h-8 items-center gap-2.5 px-3 py-1.5 text-sm font-medium leading-5 hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
                        childIsActive
                          ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                          : 'text-slate-700 dark:text-slate-300'
                      )}
                    >
                      <ChildIcon className="h-4 w-4 flex-shrink-0" />
                      <span>{child.title}</span>
                    </Link>
                  )}
                </div>
              );
            })}
        </div>
      )}

      {/* Invisible Bridge for Second Level Menu */}
      {hoveredChild &&
        hoveredItem &&
        menuPositions[hoveredItem] &&
        menuPositions[`${hoveredItem}-${hoveredChild}`] && (
          <div
            data-sidebar-flyout="true"
            className="fixed z-40"
            style={getBridgeStyle(
              menuPositions[hoveredItem].x + 224,
              `${hoveredItem}-${hoveredChild}`
            )}
            onMouseEnter={handleMenuMouseEnter}
            onMouseLeave={handleMenuMouseLeave}
          />
        )}

      {/* Second Level Floating Submenu */}
      {hoveredChild &&
        hoveredItem &&
        menuPositions[`${hoveredItem}-${hoveredChild}`] && (
          <div
            data-sidebar-flyout="true"
            className="fixed bg-white/95 dark:bg-[#202020]/95 backdrop-blur-xl border border-slate-200/50 dark:border-neutral-700/70 rounded-lg shadow-lg z-50 min-w-56 py-1 max-h-[calc(100vh-40px)] overflow-y-auto"
            style={{
              left: menuPositions[`${hoveredItem}-${hoveredChild}`].x,
              top: menuPositions[`${hoveredItem}-${hoveredChild}`].y,
              // The panel is clamped to the viewport from its own top, not from the top of the screen —
              // otherwise a long menu positioned part-way down runs off the bottom and the tail is unreachable.
              maxHeight: `calc(100vh - ${menuPositions[`${hoveredItem}-${hoveredChild}`].y + 20}px)`,
              scrollbarWidth: 'thin',
              scrollbarColor: 'rgb(148 163 184) transparent',
            }}
            onMouseEnter={handleMenuMouseEnter}
            onMouseLeave={handleMenuMouseLeave}
          >
            {filterNavItems(sidebarNavigationItems)
              .find((item) => item.title === hoveredItem)
              ?.children?.find((child) => child.title === hoveredChild)
              ?.children?.map((grandchild, _index, siblings) => {
                const GrandChildIcon = grandchild.icon;
                const grandchildIsActive = grandchild === activeSibling(siblings);
                const grandchildHasChildren =
                  grandchild.children && grandchild.children.length > 0;

                return (
                  <div key={grandchild.title} className="relative">
                    {grandchildHasChildren ? (
                      <button
                        onMouseEnter={(e) =>
                          handleGrandChildItemHover(
                            hoveredItem,
                            hoveredChild,
                            grandchild.title,
                            e
                          )
                        }
                        onClick={(e) => {
                          if (hoveredGrandChild === grandchild.title) {
                            setHoveredGrandChild(null);
                            setActiveMenuPath([hoveredItem, hoveredChild]);
                            return;
                          }
                          cancelPendingOpen();
                          openGrandChildItemMenu(
                            hoveredItem,
                            hoveredChild,
                            grandchild.title,
                            e
                          );
                        }}
                        className="w-full flex min-h-8 items-center gap-2.5 px-3 py-1.5 text-sm font-medium leading-5 text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-600 dark:text-slate-400 transition-colors"
                      >
                        <GrandChildIcon className="h-4 w-4 flex-shrink-0" />
                        <span className="flex-1">{grandchild.title}</span>
                        <ChevronRight className="h-4 w-4 flex-shrink-0" />
                      </button>
                    ) : (
                      <Link
                        href={grandchild.href}
                        onClick={clearMenus}
                        className={cn(
                          'flex min-h-8 items-center gap-2.5 px-3 py-1.5 text-sm font-medium leading-5 hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
                          grandchildIsActive
                            ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                            : 'text-slate-600 dark:text-slate-400'
                        )}
                      >
                        <GrandChildIcon className="h-4 w-4 flex-shrink-0" />
                        <span>{grandchild.title}</span>
                      </Link>
                    )}
                  </div>
                );
              })}
          </div>
        )}

      {/* Invisible Bridge for Third Level Menu */}
      {hoveredGrandChild &&
        hoveredChild &&
        hoveredItem &&
        menuPositions[`${hoveredItem}-${hoveredChild}`] &&
        menuPositions[
          `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
        ] && (
          <div
            data-sidebar-flyout="true"
            className="fixed z-40"
            style={getBridgeStyle(
              menuPositions[`${hoveredItem}-${hoveredChild}`].x + 224,
              `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
            )}
            onMouseEnter={handleMenuMouseEnter}
            onMouseLeave={handleMenuMouseLeave}
          />
        )}

      {/* Third Level Floating Submenu */}
      {hoveredGrandChild &&
        hoveredChild &&
        hoveredItem &&
        menuPositions[
          `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
        ] && (
          <div
            data-sidebar-flyout="true"
            className="fixed bg-white/95 dark:bg-[#202020]/95 backdrop-blur-xl border border-slate-200/50 dark:border-neutral-700/70 rounded-lg shadow-lg z-50 min-w-56 py-1 max-h-[calc(100vh-40px)] overflow-y-auto"
            style={{
              left: menuPositions[
                `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
              ].x,
              top: menuPositions[
                `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
              ].y,
              // The panel is clamped to the viewport from its own top, not from the top of the screen —
              // otherwise a long menu positioned part-way down runs off the bottom and the tail is unreachable.
              maxHeight: `calc(100vh - ${menuPositions[
                `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
              ].y + 20}px)`,
              scrollbarWidth: 'thin',
              scrollbarColor: 'rgb(148 163 184) transparent',
            }}
            onMouseEnter={handleMenuMouseEnter}
            onMouseLeave={handleMenuMouseLeave}
          >
            {filterNavItems(sidebarNavigationItems)
              .find((item) => item.title === hoveredItem)
              ?.children?.find((child) => child.title === hoveredChild)
              ?.children?.find(
                (grandchild) => grandchild.title === hoveredGrandChild
              )
              ?.children?.map((greatGrandchild, _index, siblings) => {
                const GreatGrandChildIcon = greatGrandchild.icon;
                const greatGrandchildIsActive = greatGrandchild === activeSibling(siblings);

                return (
                  <Link
                    key={greatGrandchild.title}
                    href={greatGrandchild.href}
                    onClick={clearMenus}
                    className={cn(
                      'flex min-h-8 items-center gap-2.5 px-3 py-1.5 text-sm font-medium leading-5 hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
                      greatGrandchildIsActive
                        ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                        : 'text-slate-500 dark:text-slate-500'
                    )}
                  >
                    <GreatGrandChildIcon className="h-4 w-4 flex-shrink-0" />
                    <span>{greatGrandchild.title}</span>
                  </Link>
                );
              })}
          </div>
        )}
    </div>
  );
}
