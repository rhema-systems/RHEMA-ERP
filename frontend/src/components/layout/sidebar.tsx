'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  AlarmClock,
  Building2,
  Sparkles,
  DoorOpen,
  Flag,
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
  Tags,
  CalendarDays,
  Globe2,
  ListTree,
  Mail,
  Building,
  BarChart3,
  LineChart,
  Bell,
  Database,
  Home,
  Code,
  HelpCircle,
  LayoutList,
  Workflow,
  Wrench,
  Calendar,
  CalendarRange,
  Check,
  ClipboardCheck,
  ListChecks,
  OctagonX,
  FileSearch,
  FolderArchive,
  Medal,
  SlidersHorizontal,
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
  TriangleAlert,
  Users2,
  UserRoundCheck,
  Grid3x3,
  Award,
  Star,
  Target,
  DollarSign,
  TrendingUp,
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
  FileStack,
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

export interface NavItem {
  title: string;
  href: string;
  icon: React.ComponentType<any>;
  children?: NavItem[];
  roles?: string[];
  permissions?: string[];
  accessMode?: 'all' | 'any';
  navigationSurface?: 'operations' | 'settings';
}

const ADMINISTRATION_ROLES = ['admin', 'SuperAdmin', 'TenantAdmin'];

export const navigationItems: NavItem[] = [
  {
    title: 'Dashboard',
    href: '/dashboard',
    icon: LayoutDashboard,
  },
  {
    // Area 25: the two-way switcher's desk side — /me has its own chrome ("Back to ERP"
    // lives there). The /me layout handles unlinked users with a friendly page.
    title: 'My Self-Service',
    href: '/me',
    icon: Sparkles,
  },
  {
    title: 'Finance',
    href: '/finance',
    icon: CreditCard,
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
          'Finance Manager',
          'Financial Controller',
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
            href: '/procurement/business-partners?partnerType=Supplier',
            icon: Users,
            permissions: [
              'Finance.Read',
              'Finance.Admin',
              'procurement.records.read',
              'procurement.supplier.manage',
              'procurement.supplier.review',
            ],
            accessMode: 'any',
          },
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
          { title: 'Invoices', href: '/finance/ap/invoices', icon: FileText },
          {
            title: 'Supplier Returns',
            href: '/finance/ap/returns',
            icon: RotateCcw,
          },
          {
            title: 'Supplier Debit Notes',
            href: '/finance/ap/supplier-debit-notes',
            icon: Receipt,
            permissions: ['Finance.Read'],
          },
          { title: 'Payments', href: '/finance/ap/payments', icon: CreditCard },
          {
            title: 'Adjustment Journal',
            href: '/finance/subledger-adjustments/new?module=AP',
            icon: FileText,
          },
          {
            title: 'Journals',
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
          { title: 'Refunds', href: '/sales/refunds', icon: DollarSign },
          {
            title: 'Adjustment Journal',
            href: '/finance/subledger-adjustments/new?module=AR',
            icon: FileText,
          },
          {
            title: 'Journals',
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
    // W3: mirrors the /hr layout's AuthGuard. hr.access is granted to every general internal
    // role by the seeder — the gate exists to exclude external (candidate/business-partner)
    // accounts, not to hide HR from staff. SuperAdmin/TenantAdmin auto-pass.
    permissions: ['hr.access'],
    children: [
      // Area 25 slice 12c. Operational, not configuration — written and published continuously,
      // so it sits at the top of HR rather than in administration settings. HR.Company, because
      // an announcement is a communication from the organisation, not an act on a record.
      {
        title: 'Announcements',
        href: '/hr/announcements',
        icon: Megaphone,
        permissions: ['HR.Company.Read'],
      },
      // Area 25 slice 12d — the policy library and its acknowledgements. Same HR.Company
      // family: a policy is issued by the organisation, not performed on a record.
      {
        title: 'Policy Library',
        href: '/hr/policies',
        icon: BookText,
        permissions: ['HR.Company.Read'],
      },
      // W3 slice 11: the register's detail/profile/sub-record reads are HR.Employee.Read on the
      // API now (they carry salary, identifiers and bank data), so the desk entry follows. The
      // shared employee picker rides the open paged read and does not need this.
      { title: 'Employees', href: '/hr/employees', icon: Users, permissions: ['HR.Employee.Read'] },
      // Area 25 slice 12a — approving one of these WRITES onto the employee record, which is
      // why it sits with the register and carries the same permission.
      {
        title: 'Change Requests',
        href: '/hr/employees/change-requests',
        icon: UserCheck,
        permissions: ['HR.Employee.Read'],
      },
      // Area 25 slice 12b — issuing a letter is making a statement about an employee record,
      // so it carries the same permission and sits with the register.
      {
        title: 'Letter Requests',
        href: '/hr/employees/letter-requests',
        icon: Mail,
        permissions: ['HR.Employee.Read'],
      },
      // Read-only view of the org's own records. Lives here rather than under Administration
      // because it is looked at daily, not configured — and the administration tree is gated to
      // admin roles, which would hide it from the HR officers the API gate lets in.
      { title: 'Organogram', href: '/hr/organogram', icon: Network },
      {
        title: 'Payroll',
        href: '/hr/payroll',
        icon: CreditCard,
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
        // W3 slice 5: the org-wide surfaces authorize on HR.Leave.* (SuperAdmin/TenantAdmin
        // auto-pass). Requests stays open — it is per-employee and the API's self-or-permission
        // check lets anyone work their own history — and Approvals is the workflow assignee's
        // queue, deliberately not an HR permission. Year-End is the admin tier: it rewrites
        // every balance in the tenant, so plain HR staff do not see it.
        title: 'Leave Management',
        href: '/hr/leave',
        icon: CalendarDays,
        children: [
          { title: 'Requests', href: '/hr/leave/requests', icon: CalendarDays },
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
        title: 'Emoluments',
        href: '/hr/emoluments',
        icon: Coins,
        // W3 slice 7: org-wide pay data — the compensation read tier. An employee's own pay
        // makeup is an ownership check on the API and arrives as a screen with area 25.
        permissions: ['HR.Compensation.Read'],
      },
      {
        title: 'Benefits',
        href: '/hr/benefits',
        icon: ShieldPlus,
        // W3 slice 7: the enrollment desk — same tier. Own benefits/beneficiaries = area 25.
        permissions: ['HR.Compensation.Read'],
      },
      {
        // W3 slice 6: every child here is a desk surface (org-wide search, monitoring, exports,
        // biometrics), so the whole section rides on the attendance read tier. Employee
        // self-service (punch, own records, own regularizations) arrives with area 25's portal
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
        // W3 slice 6: management registers (clients, engagements, invoicing) — attendance read
        // tier. A consultant's own timesheet surface is self-service and comes with area 25.
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
        // Cycles, the goal cascade, and the appraisal run itself. Ordered by who acts: the
        // employee's own work first, then the manager's, then HR's — then what happens around
        // the sign-off (calibration before it, appeals after it, outcomes off the back of it),
        // with the deadline override last as the exception path.
        title: 'Performance',
        href: '/hr/performance',
        icon: Target,
        // W3 slice 10: the desk registers ride on HR.Performance.Read and the deadline override
        // on Write; everything an employee, peer or manager does as themselves stays open — the
        // API holds those to the token actor, not to a permission.
        children: [
          // Area 25 slice 5: the my-shaped screens (My Appraisals, Peer Reviews, Check-ins,
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
          // Appraisal notifications were a self surface on the desk; area 25 slice 11 folded
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
        // W3 slice 8: desk registers gate on HR.Training.Read (leave-slice shape — the parent stays
        // open so the self-service children remain reachable). Schedules is the published calendar,
        // Requests is the desk register + on-behalf create, and Nomination Approvals is the approver
        // queue — workflow-validated per request.
        //
        // Area 25 slice 6: the my-* screens (My Training, My Learning Paths, my mentoring pairs)
        // re-homed to the portal (/me/training, /me/learning, /me/mentoring); Mentoring and the new
        // Enrollments entry are the desk registers that remained.
        title: 'Training',
        href: '/hr/training',
        icon: GraduationCap,
        children: [
          { title: 'Enrollments', href: '/hr/training/enrollments', icon: Route, permissions: ['HR.Training.Read'] },
          { title: 'Mentoring', href: '/hr/training/mentoring', icon: Handshake, permissions: ['HR.Training.Read'] },
          { title: 'Analytics', href: '/hr/training/analytics', icon: TrendingUp, permissions: ['HR.Training.Read'] },
          { title: 'Schedules', href: '/hr/training/schedules', icon: CalendarClock },
          { title: 'Requests', href: '/hr/training/requests', icon: ClipboardList },
          { title: 'Nomination Approvals', href: '/hr/training/approvals', icon: UserCheck },
          { title: 'Completions', href: '/hr/training/completions', icon: Award, permissions: ['HR.Training.Read'] },
          { title: 'Certificates', href: '/hr/training/certificates', icon: Stamp },
          { title: 'Employee Certificates', href: '/hr/training/employee-certificates', icon: IdCard },
          { title: 'Compliance', href: '/hr/training/compliance', icon: ShieldAlert, permissions: ['HR.Training.Read'] },
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
          // Area 25 slice 7: My Orientation re-homed to the portal (/me/orientation).
          { title: 'Onboarding Plans', href: '/hr/orientation/onboarding', icon: ListChecks, permissions: ['HR.Orientation.Read'] },
          { title: 'Task Queues', href: '/hr/orientation/onboarding/queues', icon: ClipboardCheck, permissions: ['HR.Orientation.Read'] },
        ],
      },
      {
        // Ordered the way a hire happens rather than alphabetically: a gap in the establishment
        // becomes a requisition, an approved requisition becomes a vacancy, publishing the vacancy
        // raises the adverts, and the adverts bring in candidates and applications.
        //
        // The pipeline board and the screening workspace are not listed: both belong to a single
        // vacancy and are reached from it, so a top-level link would have nowhere to go.
        // W3 slice 9: desk registers gate on HR.Recruitment.Read (the leave-slice shape — the
        // parent stays open so the self-service children remain reachable). Requisitions stays
        // open as the manager's entry point (raising one is self-service; the register tab needs
        // the desk read), and My Panel is the panelist's own surface, validated per record.
        title: 'Recruitment',
        href: '/hr/recruitment',
        icon: UserPlus,
        children: [
          { title: 'Establishment', href: '/hr/recruitment/establishment', icon: Building2, permissions: ['HR.Recruitment.Read'] },
          { title: 'Requisitions', href: '/hr/recruitment/requisitions', icon: ClipboardList },
          { title: 'Vacancies', href: '/hr/recruitment/vacancies', icon: Briefcase, permissions: ['HR.Recruitment.Read'] },
          { title: 'Adverts', href: '/hr/recruitment/adverts', icon: Megaphone, permissions: ['HR.Recruitment.Read'] },
          { title: 'Candidates', href: '/hr/recruitment/candidates', icon: Users, permissions: ['HR.Recruitment.Read'] },
          { title: 'Talent Pool', href: '/hr/recruitment/talent-pool', icon: Star, permissions: ['HR.Recruitment.Read'] },
          { title: 'Applications', href: '/hr/recruitment/applications', icon: FileText, permissions: ['HR.Recruitment.Read'] },
          { title: 'Interviews', href: '/hr/recruitment/interviews', icon: CalendarClock, permissions: ['HR.Recruitment.Read'] },
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
        ],
      },
      {
        // The enforcement side of a sponsored training nomination. It sits outside Performance
        // because the obligation is a training commitment, not an appraisal outcome — Training
        // links here too once that area is built.
        //
        // W3 slice 8: the screen is the desk register (getAll + on-behalf/waive/settle actions),
        // so it rides on the desk read. The employee's own bonds surface through My Training /
        // the API's /mine read; a self-service accept screen is still owed.
        title: 'Service Bonds',
        href: '/hr/service-bonds',
        icon: HandCoins,
        permissions: ['HR.Training.Read'],
      },
      {
        // Area 16. Company property in a named employee's hands: the register, the requisition that
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
          // Area 25 slice 9: "My Assets" moved to the portal (/me/assets), via "My Self-Service".
        ],
      },
      {
        // Area 8. One record covers promotion, transfer, demotion, secondment, acting appointment,
        // lateral move and redesignation — they share an approval route, a checklist and a set of
        // documents, and differ only in their subtype detail.
        title: 'Staff Movements',
        href: '/hr/movements',
        icon: ArrowRightLeft,
        children: [
          { title: 'Register', href: '/hr/movements', icon: ArrowRightLeft, permissions: ['HR.Movements.Read'] },
          // Standalone: most acting appointments never come from a movement at all.
          { title: 'Acting Appointments', href: '/hr/movements/acting', icon: UserCheck, permissions: ['HR.Movements.Read'] },
          // Ledger D-37: answering a demotion notice removes it from the pending queue, so a filed
          // appeal was visible on no list at all. Both queues live here.
          { title: 'Demotion Appeals', href: '/hr/movements/appeals', icon: Gavel, permissions: ['HR.Movements.Read'] },
          // Area 25 slice 7: My Movements re-homed to the portal (/me/movements).
        ],
      },
      {
        // Area 12. One request covers the whole trip — approval, itinerary, bookings, the advance
        // and the expenses claimed against it. Approval runs on the generic workflow engine, so a
        // screen reads the live step from the workflow record rather than from `status`.
        title: 'Staff Travel',
        href: '/hr/travel',
        icon: Plane,
        children: [
          { title: 'Register', href: '/hr/travel', icon: Plane },
          // Area 25 slice 7: My Travel re-homed to the portal (/me/travel).
          // Claims get their own entry because the finance desk works a queue ACROSS trips —
          // "approved and unpaid" — which no single travel request can show.
          { title: 'Group Travel', href: '/hr/travel/groups', icon: Users2 },
          { title: 'Expense Claims', href: '/hr/travel/claims', icon: Receipt },
          { title: 'Dashboard', href: '/hr/travel/dashboard', icon: LayoutDashboard },
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
        ],
      },
      {
        // Area 18. FR-HR-135 names the chain: Department Head → HR → Managing Director. An approved
        // budget is not only a plan — it sets the establishment (D-2), which then gates whether a
        // vacancy may be opened at all (FR-HR-136).
        title: 'Manpower Budgets',
        href: '/hr/manpower-budgets',
        icon: Banknote,
      },
      {
        // Area 17. The gap view is the operational screen: what positions require against what
        // people have been assessed at. "Below requirement" and "not assessed" stay separate all
        // the way to the tiles — an unknown is not a training need.
        title: 'Competencies',
        href: '/hr/competencies',
        icon: GraduationCap,
        children: [
          { title: 'Organisation Gaps', href: '/hr/competencies', icon: TrendingDown },
          { title: 'Assess Competencies', href: '/hr/competencies/assess', icon: ClipboardCheck },
          // The only screen in this area most employees will ever open. It reads `me/*`, because
          // the client User object carries no employee link to address the by-employee routes with.
          { title: 'My Competencies', href: '/hr/competencies/me', icon: UserCheck },
        ],
      },
      {
        // Area 17. FR-HR-134: a position is measured against its APPROVED job description, and only
        // one stands at a time — approving a new version retires the one before it. Coverage is its
        // own entry because the useful question is not how many descriptions exist but how many
        // positions still have none.
        title: 'Job Descriptions',
        href: '/hr/job-descriptions',
        icon: FileText,
        children: [
          { title: 'Register', href: '/hr/job-descriptions', icon: FileText },
          { title: 'Coverage Gaps', href: '/hr/job-descriptions/gaps', icon: ClipboardList },
        ],
      },
      {
        // Area 15b. The probation is per EMPLOYMENT TERM, not per person: a rehire gets a new one,
        // and an employee may accumulate several over a career. Length comes from the staff
        // category (FR-HR-031, senior 6 / junior 3), not from whoever fills in the form.
        title: 'Probation & Confirmation',
        href: '/hr/probation',
        icon: UserCheck,
        children: [
          { title: 'Register', href: '/hr/probation', icon: UserCheck },
          // The reviewer queue is deliberately its own entry: probation reviews are conducted by
          // LINE MANAGERS, who hold no HR permission at all, so this is the one screen in the area
          // most of its users will ever open. (Area 25 slice 7: the employee's own reviews and
          // the oath affirmation re-homed to the portal — /me/probation and /me/oath.)
          { title: 'Reviews to Conduct', href: '/hr/probation/reviews', icon: ClipboardCheck },
          // FR-HR-030. Not probation, but the same FRD section (onboarding), and it is the only
          // other place an oath would sensibly live.
          { title: 'Oaths of Secrecy', href: '/hr/probation/oaths', icon: ScrollText },
        ],
      },
      {
        // Area 9b. ONE exit register for every route out — resignation, retirement, contract
        // expiry, redundancy, dismissal, death. Before it, the only way to leave was a disciplinary
        // case, so the other routes had enum members and nothing that could reach them. The
        // disciplinary route writes here too: area 9 keeps the decision, the exit lives here.
        // Area 14. NOTE the title: '/procurement/awards' already exists and also uses the Award
        // icon, so a bare "Awards" here would give the sidebar two entries a user cannot tell
        // apart. Medal is deliberately a different glyph for the same reason.
        title: 'Awards & Recognition',
        href: '/hr/awards',
        icon: Medal,
        children: [
          { title: 'Register', href: '/hr/awards', icon: Medal },
          // Area 25 slice 9: the self surface (My Awards, Nominate, Vote, Score) moved to the
          // portal under /me/awards — reachable via "My Self-Service". Only the desk stays here.
          { title: 'Who Qualifies', href: '/hr/awards/eligibility', icon: Users },
          { title: 'Results', href: '/hr/awards/results', icon: Trophy },
          { title: 'Long Service', href: '/hr/awards/long-service', icon: Medal },
        ],
      },
      {
        title: 'Separations & Exit',
        href: '/hr/separations',
        icon: DoorOpen,
        children: [
          { title: 'Register', href: '/hr/separations', icon: DoorOpen },
          { title: 'Raise a separation', href: '/hr/separations/new', icon: Plus },
        ],
      },
      {
        // Area 13. The plan is per POSITION, not per person, and it is versioned: approving a new
        // plan archives the one the post had before and points it at the successor. Only an
        // approved plan is the position's active version — a draft deliberately holds no slot.
        title: 'Succession Planning',
        href: '/hr/succession',
        icon: Network,
        children: [
          { title: 'Plans', href: '/hr/succession', icon: Network },
          // Pools are NOT tied to a post, which is what separates them from a plan — so they get
          // their own entry rather than living inside one. Note that area 5 writes into the
          // "Appraisal Nominations" pool on its own, without anyone opening this screen.
          { title: 'Talent Pools', href: '/hr/succession/pools', icon: Users2 },
          // Calibration sessions and the nine box. Separate from plans and pools because a session
          // is an EVENT with a close, not a register: finalizing freezes it for good.
          { title: 'Talent Reviews', href: '/hr/succession/reviews', icon: Grid3x3 },
          { title: 'Dashboard', href: '/hr/succession/dashboard', icon: LayoutDashboard },
        ],
        // No "my succession" entry, and there must never be one: readiness, retention risk and
        // nine-box placement are assessments made ABOUT a candidate, not records belonging to them.
        // Succession inverts the self-service rule the rest of HR follows (decision D-2).
      },
      {
        // Area 9. The case is the unit of work: one record carries the allegation, the
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
          // Area 25 slice 9: "My Record" moved to the portal (/me/discipline).
        ],
      },
      {
        // Area 9 slice 7, FR-HR-181. Separate from Discipline on purpose: a disciplinary case is
        // raised ABOUT an employee and a grievance BY one, which gives them opposite permissions —
        // HR cannot file, escalate or withdraw a grievance at all.
        // Area 25 slice 9: the employee surface (mine, filing, the detail) moved to the portal
        // under /me/grievances.
        // Area 9c slice 10: renamed and re-homed to /hr/employee-relations. The register has held
        // mediations, welfare matters and union consultations since slice 1, so "Grievances" had
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
      {
        // Slice 1 of area 10: the live dashboard and the hand-reported KPI snapshots. Each later
        // slice adds its register here in lifecycle order (incidents, hazards, inspections,
        // permits, PPE, …). Reference data lives under Administration → HR → Safety.
        title: 'Safety (SHE)',
        href: '/hr/safety',
        icon: HardHat,
        children: [
          { title: 'Dashboard', href: '/hr/safety/dashboard', icon: LayoutDashboard, permissions: ['HR.She.Read'] },
          // Area 25 slice 8: the open employee actions (report incident/hazard/environmental,
          // raise stop-work) and My PPE moved to the self-service portal under /me/safety —
          // reachable via "My Self-Service". Only the desk registers stay here.
          { title: 'Incidents', href: '/hr/safety/incidents', icon: AlertTriangle, permissions: ['HR.She.Read'] },
          { title: 'Hazards', href: '/hr/safety/hazards', icon: ShieldAlert, permissions: ['HR.She.Read'] },
          { title: 'Risk Assessments', href: '/hr/safety/risk-assessments', icon: ClipboardList, permissions: ['HR.She.Read'] },
          { title: 'Inspections', href: '/hr/safety/inspections', icon: ClipboardCheck, permissions: ['HR.She.Read'] },
          { title: 'Permits to Work', href: '/hr/safety/permits', icon: FileCheck, permissions: ['HR.She.Read'] },
          { title: 'PPE Stock', href: '/hr/safety/ppe', icon: Package, permissions: ['HR.She.Read'] },
          { title: 'PPE Issuance', href: '/hr/safety/ppe/issuances', icon: Users, permissions: ['HR.She.Read'] },
          { title: 'Safety Equipment', href: '/hr/safety/equipment', icon: FireExtinguisher, permissions: ['HR.She.Read'] },
          { title: 'Emergency Plans', href: '/hr/safety/emergency', icon: Siren, permissions: ['HR.She.Read'] },
          // The SHE training record — deliberately separate from corporate Training (area 7).
          { title: 'Safety Training', href: '/hr/safety/training', icon: GraduationCap, permissions: ['HR.She.Read'] },
          { title: 'Contractors', href: '/hr/safety/contractors', icon: Handshake, permissions: ['HR.She.Read'] },
          // Occ-health + RTW ride the HR.Medical.* policies, not the SHE HR-role gate.
          { title: 'Occupational Health', href: '/hr/safety/occupational-health', icon: Stethoscope, permissions: ['HR.Medical.Read'] },
          { title: 'Return to Work', href: '/hr/safety/return-to-work', icon: HeartPulse, permissions: ['HR.Medical.Read'] },
          { title: 'Environmental', href: '/hr/safety/environmental', icon: Leaf, permissions: ['HR.She.Read'] },
          { title: 'Waste Management', href: '/hr/safety/waste', icon: Recycle, permissions: ['HR.She.Read'] },
          // Slice 17 — Part D environmental core (FR-ENV): the permit/licence register with the
          // statutory renewal ladder, monitoring schedules, the regulatory-updates register,
          // sustainability, compliance reviews/clearance and the monthly environmental report.
          { title: 'Environmental Permits', href: '/hr/safety/environmental/permits', icon: FileCheck, permissions: ['HR.She.Read'] },
          { title: 'Monitoring Schedules', href: '/hr/safety/environmental/monitoring-schedules', icon: CalendarClock, permissions: ['HR.She.Read'] },
          { title: 'Regulatory Updates', href: '/hr/safety/environmental/regulatory-updates', icon: Scale, permissions: ['HR.She.Read'] },
          { title: 'Sustainability', href: '/hr/safety/environmental/sustainability', icon: Sprout, permissions: ['HR.She.Read'] },
          { title: 'Environmental Reviews', href: '/hr/safety/environmental/reviews', icon: ClipboardCheck, permissions: ['HR.She.Read'] },
          { title: 'Monthly Env. Reports', href: '/hr/safety/environmental/monthly-reports', icon: FileBarChart, permissions: ['HR.She.Read'] },
          { title: 'Safety Committees', href: '/hr/safety/committees', icon: Users, permissions: ['HR.She.Read'] },
          { title: 'Regulatory Compliance', href: '/hr/safety/regulatory', icon: Scale, permissions: ['HR.She.Read'] },
          { title: 'Safety Signage', href: '/hr/safety/signs', icon: Signpost, permissions: ['HR.She.Read'] },
          // Slice 15: audits + the stop-work register (raise lives in the open block above).
          { title: 'SHE Audits', href: '/hr/safety/audits', icon: FileSearch, permissions: ['HR.She.Read'] },
          { title: 'Stop-Work Orders', href: '/hr/safety/stop-work', icon: OctagonX, permissions: ['HR.She.Read'] },
          // Slice 16: the controlled document register (versions ride the central DMS).
          { title: 'Document Register', href: '/hr/safety/documents', icon: FolderArchive, permissions: ['HR.She.Read'] },
          // Slice 14: the unified CA tracker (one queue over all five action stores) and the
          // computed-KPI layer (snapshot compute + the analytics screen).
          { title: 'Corrective Actions', href: '/hr/safety/corrective-actions', icon: ListChecks, permissions: ['HR.She.Read'] },
          { title: 'Performance Snapshots', href: '/hr/safety/performance', icon: Gauge, permissions: ['HR.She.Read'] },
          { title: 'SHE Analytics', href: '/hr/safety/performance/analytics', icon: BarChart3, permissions: ['HR.She.Read'] },
        ],
      },
      {
        // Area 11 — Medical & Health. Everything here is gated on the HR.Medical.* permission
        // policies rather than a role, so an HR user without medical permissions sees 403s.
        // Slice 4 ships the reference registers; health records, claims and the clinical
        // surface follow in slices 5–7. Note occupational health and return-to-work live under
        // Safety (SHE owns them) but ride the same medical policies.
        title: 'Medical & Health',
        href: '/hr/medical',
        icon: HeartPulse,
        children: [
          { title: 'Dashboard', href: '/hr/medical/dashboard', icon: LayoutDashboard },
          { title: 'Medical Claims', href: '/hr/medical/claims', icon: Receipt },
          // Area 25 slice 8: "My Medical Claims" moved to the portal (/me/medical/claims).
          { title: 'NHIS Claims', href: '/hr/medical/nhis', icon: Landmark },
          { title: 'Clinical', href: '/hr/medical/clinical', icon: ClipboardCheck },
          { title: 'Health Records', href: '/hr/medical/health', icon: FileHeart },
          { title: 'Healthcare Facilities', href: '/hr/medical/facilities', icon: Hospital },
          { title: 'Physicians', href: '/hr/medical/physicians', icon: Stethoscope },
          { title: 'Insurance Providers', href: '/hr/medical/insurance', icon: ShieldPlus },
          { title: 'Benefit Schemes', href: '/hr/medical/schemes', icon: Layers },
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
    permissions: ['project.access'],
    children: [
      { title: 'Projects', href: '/development/projects', icon: Briefcase },
      {
        title: 'Town Planning',
        href: '/development/planning',
        icon: MapPin,
        children: [
          { title: 'Planning Dashboard', href: '/development/planning/dashboard', icon: BarChart3 },
          { title: 'SOP Procedures', href: '/development/planning', icon: ClipboardList },
          { title: 'Workflow Setup', href: '/administration/workflow?q=Planning', icon: Workflow },
          { title: 'Documents', href: '/document-management?module=Planning', icon: FileText },
          { title: 'Reports', href: '/reports?module=planning', icon: FileCheck },
        ],
      },
      {
        title: 'Operations',
        href: '/development/project-operations',
        icon: Activity,
      },
      {
        title: 'Portfolios',
        href: '/development/portfolios',
        icon: FolderTree,
      },
      { title: 'Programs', href: '/development/programs', icon: Target },
      {
        title: 'Dependencies',
        href: '/development/project-dependencies',
        icon: AlertCircle,
      },
      {
        title: 'Analytics',
        href: '/development/project-analytics',
        icon: TrendingUp,
      },
      {
        title: 'Reports',
        href: '/development/project-reports',
        icon: FileCheck,
      },
      {
        title: 'Approvals',
        href: '/development/project-approvals',
        icon: ClipboardCheck,
      },
      {
        title: 'Billing',
        href: '/development/project-billing',
        icon: DollarSign,
      },
      {
        title: 'Materials',
        href: '/development/project-materials',
        icon: Package,
      },
      {
        title: 'Mobile',
        href: '/development/project-mobile',
        icon: Smartphone,
      },
      { title: 'Tasks', href: '/development/tasks', icon: FileText },
      { title: 'Timesheets', href: '/development/timesheets', icon: Clock },
      { title: 'Expenses', href: '/development/expenses', icon: DollarSign },
      { title: 'Resources', href: '/development/resources', icon: Users },
      { title: 'Timeline', href: '/development/timeline', icon: BarChart3 },
    ],
  },
  {
    title: 'Procurement',
    href: '/procurement',
    icon: Briefcase,
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
            permissions: ['procurement.inventory.issue', 'procurement.inventory.adjust.approve'],
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
    children: [
      { title: 'Sales Overview', href: '/sales', icon: LayoutDashboard },
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
        href: '/document-management/CentralDocumentMetadataTemplate',
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
    children: [
      { title: 'Overview', href: '/estate', icon: ClipboardList },
      {
        title: 'Property Dashboard',
        href: '/estate/property-management/dashboard',
        icon: BarChart3,
      },
      {
        title: 'Facilities Dashboard',
        href: '/estate/facilities/dashboard',
        icon: BarChart3,
      },
      {
        title: 'Land Acquisition',
        href: '/estate/land-acquisition',
        icon: Landmark,
      },
      {
        title: 'Land Management',
        href: '/estate/land-management',
        icon: MapPin,
      },
      { title: 'GIS Integration', href: '/estate/gis', icon: Globe2 },
      {
        title: 'Core Operations',
        href: '/estate',
        icon: ClipboardList,
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
        title: 'Facilities Management',
        href: '/estate/facilities',
        icon: Building2,
        children: [
          {
            title: 'Dashboard',
            href: '/estate/facilities/dashboard',
            icon: BarChart3,
          },
          {
            title: 'Property / Site Operating View',
            href: '/estate/facilities/EstateFacilityPropertySite',
            icon: Building2,
          },
          {
            title: 'Lease / Occupancy Coordination',
            href: '/estate/facilities/EstateFacilityLease',
            icon: FileCheck,
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
            title: 'Asset Operating View',
            href: '/estate/facilities/EstateFacilityAssetRegister',
            icon: Database,
          },
          {
            title: 'Billing / Service Charge',
            href: '/estate/facilities/EstateFacilityBillingServiceCharge',
            icon: CreditCard,
          },
          {
            title: 'Documents Index',
            href: '/estate/facilities/EstateFacilityDocument',
            icon: FileText,
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
    children: [
      { title: 'Dashboard', href: '/legal/dashboard', icon: BarChart3 },
      { title: 'Procedures', href: '/legal', icon: ClipboardList },
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
      { title: 'Quantity Survey Reports', href: '/reports/quantity-survey', icon: Scale },
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
    // admin.hr (W3): an HR practitioner holding the seeded admin.hr grant must see the parent
    // node, or the HR child below would be filtered out with it.
    permissions: ['Finance.Admin', 'admin.hr'],
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
                title: 'Chart of Accounts Setup',
                href: '/administration/finance/accounts',
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
        title: 'HR',
        href: '/administration/hr',
        icon: UserCheck,
        // W3: admins by role, HR practitioners by the seeded admin.hr grant — HR maintains its
        // own reference data (leave types, org structures). Matches the administration
        // layout's /administration/hr route gate.
        roles: ADMINISTRATION_ROLES,
        permissions: ['admin.hr'],
        accessMode: 'any',
        children: [
          {
            title: 'Organization',
            href: '/administration/hr/organization',
            icon: Building2,
            children: [
              { title: 'Structures', href: '/administration/hr/organization/structures', icon: Building2 },
              { title: 'Levels', href: '/administration/hr/organization/levels', icon: ListTree },
              { title: 'Units', href: '/administration/hr/organization/units', icon: FolderTree },
              // Slice 4b. Teams are the working groups, as distinct from the units people are
              // formally posted into — the entities existed since the port with no way to write them.
              { title: 'Teams', href: '/administration/hr/organization/teams', icon: Users2 },
              // Slice 5. Read-only, and gated on the HR/admin roles server-side — the log names the
              // employees who have led each unit.
              { title: 'Unit Change Log', href: '/administration/hr/organization/unit-history', icon: History },
            ],
          },
          {
            title: 'Location',
            href: '/administration/hr/location',
            icon: MapPin,
            children: [
              {
                title: 'Structures',
                href: '/administration/hr/location/structures',
                icon: MapPin,
              },
              {
                title: 'Levels',
                href: '/administration/hr/location/levels',
                icon: ListTree,
              },
              {
                title: 'Locations',
                href: '/administration/hr/location/locations',
                icon: MapPin,
              },
            ],
          },
          { title: 'Job Positions', href: '/administration/hr/positions', icon: Users },
          // Slice 6. Master data behind the job description's bargaining-unit clause, which
          // until now could never be filled in: there was no register and no picker.
          { title: 'Unions', href: '/administration/hr/unions', icon: Users2 },
          // Slice 8. Eleven endpoints that nothing had ever called; the panel picker in
          // recruitment was the only reader, and it could only search a register no screen
          // could add to.
          { title: 'External Associates', href: '/administration/hr/external-associates', icon: UserRoundCheck },
          { title: 'Staff Levels', href: '/administration/hr/staff-levels', icon: ListTree },
          { title: 'Skills', href: '/administration/hr/skills', icon: Wrench },
          // Area 17 setup. A competency is what the organisation expects someone to be able to do;
          // a skill is the finer-grained thing an indicator points at, which is why both exist.
          { title: 'Competencies', href: '/administration/hr/competencies', icon: Layers },
          // Area 17/18. The exception path: most positions should be established by an approved
          // manpower budget, and this is the one place that chain can be bypassed.
          { title: 'Establishment', href: '/administration/hr/establishment', icon: ShieldCheck },
          { title: 'Qualifications', href: '/administration/hr/qualifications', icon: GraduationCap },
          { title: 'Identification Types', href: '/administration/hr/identification-types', icon: IdCard },
          { title: 'Reason Codes', href: '/administration/hr/reason-codes', icon: Tags },
          { title: 'Countries', href: '/administration/hr/countries', icon: Globe },
          // Ten write endpoints with no screen anywhere until 2026-08-31: a bank could not be
          // added, renamed, retired or removed from the product at all.
          { title: 'Banks', href: '/administration/hr/banks', icon: Landmark },
          { title: 'Departments', href: '/administration/hr/departments', icon: Building },
          // Area 16. The categories the company-asset register is built on — nothing can be
          // added to that register until at least one exists, so it is setup, not casework.
          { title: 'Asset Types', href: '/administration/hr/asset-types', icon: Boxes },
          { title: 'Leave Types', href: '/administration/hr/leave-types', icon: CalendarDays },
          {
            title: 'Compensation & Benefits',
            href: '/administration/hr/compensation',
            icon: Coins,
            children: [
              {
                title: 'Pay Components',
                href: '/administration/hr/compensation/pay-components',
                icon: Coins,
              },
              {
                title: 'Position Emoluments',
                href: '/administration/hr/compensation/position-emoluments',
                icon: Briefcase,
              },
              {
                title: 'Benefit Policies',
                href: '/administration/hr/compensation/benefit-policies',
                icon: ShieldPlus,
              },
            ],
          },
          {
            title: 'Attendance & Time',
            href: '/administration/hr/attendance',
            icon: Clock,
            children: [
              {
                title: 'Work Schedules',
                href: '/administration/hr/attendance/work-schedules',
                icon: CalendarClock,
              },
              {
                title: 'Shift Rotations',
                href: '/administration/hr/attendance/shift-rotations',
                icon: Repeat2,
              },
              {
                title: 'Holiday Calendars',
                href: '/administration/hr/attendance/holiday-calendars',
                icon: CalendarDays,
              },
              {
                title: 'Pay Periods',
                href: '/administration/hr/attendance/pay-periods',
                icon: DollarSign,
              },
              {
                title: 'Geofence Zones',
                href: '/administration/hr/attendance/geofence-zones',
                icon: MapPin,
              },
              {
                title: 'Devices',
                href: '/administration/hr/attendance/devices',
                icon: Fingerprint,
              },
              {
                title: 'Alert Rules',
                href: '/administration/hr/attendance/alert-rules',
                icon: BellRing,
              },
              {
                title: 'Overtime Policies',
                href: '/administration/hr/attendance/overtime-policies',
                icon: Timer,
              },
            ],
          },
          {
            title: 'Performance',
            href: '/administration/hr/performance',
            icon: Target,
            children: [
              { title: 'Appraisal Settings', href: '/administration/hr/performance/settings', icon: SlidersHorizontal },
              { title: 'Appraisal Templates', href: '/administration/hr/performance/templates', icon: ClipboardCheck },
              { title: 'Appraisal Criteria', href: '/administration/hr/performance/criteria', icon: ListChecks },
              { title: 'Grade Definitions', href: '/administration/hr/performance/grade-definitions', icon: Medal },
              { title: 'Strategic Goals', href: '/administration/hr/performance/strategic-goals', icon: Target },
              { title: 'Goal Library', href: '/administration/hr/performance/goal-library', icon: Library },
              { title: 'KPI Definitions', href: '/administration/hr/performance/kpi-definitions', icon: Gauge },
              { title: 'Goal Risk Thresholds', href: '/administration/hr/performance/goal-risk-settings', icon: AlertTriangle },
            ],
          },
          {
            title: 'Recruitment',
            href: '/administration/hr/recruitment',
            icon: Workflow,
            children: [
              { title: 'Pipelines', href: '/administration/hr/recruitment/pipelines', icon: Workflow },
              { title: 'Question Bank', href: '/administration/hr/recruitment/question-bank', icon: HelpCircle },
              { title: 'Interview Presets', href: '/administration/hr/recruitment/question-presets', icon: LayoutList },
              {
                title: 'Check Templates',
                href: '/administration/hr/recruitment/check-templates',
                icon: ClipboardCheck,
              },
            ],
          },
          {
            title: 'Training & Learning',
            href: '/administration/hr/training',
            icon: GraduationCap,
            children: [
              { title: 'Categories', href: '/administration/hr/training/categories', icon: Tag },
              { title: 'Program Groups', href: '/administration/hr/training/program-groups', icon: Layers },
              { title: 'Vendors', href: '/administration/hr/training/vendors', icon: Building2 },
              { title: 'Trainers', href: '/administration/hr/training/trainers', icon: GraduationCap },
              { title: 'Programs', href: '/administration/hr/training/programs', icon: BookOpen },
              { title: 'Needs Assessments', href: '/administration/hr/training/needs-assessments', icon: ClipboardPen },
              { title: 'Training Plans', href: '/administration/hr/training/plans', icon: CalendarRange },
              { title: 'Training Budgets', href: '/administration/hr/training/budgets', icon: Coins },
              { title: 'Compliance Requirements', href: '/administration/hr/training/compliance', icon: ShieldAlert },
              { title: 'Learning Paths', href: '/administration/hr/training/learning-paths', icon: Route },
              { title: 'Mentoring Programmes', href: '/administration/hr/training/mentoring', icon: Handshake },
            ],
          },
          {
            // Set up once, then referenced by the operational screens under /hr/company-schedule.
            // Closures sit here rather than with events because the question they answer — "is this
            // a working day?" — is the same one holiday calendars answer, and that is setup.
            title: 'Company Schedule',
            href: '/administration/hr/company-schedule',
            icon: CalendarDays,
            children: [
              { title: 'Meeting Rooms', href: '/administration/hr/company-schedule/rooms', icon: DoorOpen },
              { title: 'Business Closures', href: '/administration/hr/company-schedule/closures', icon: CalendarClock },
              { title: 'Milestones', href: '/administration/hr/company-schedule/milestones', icon: Flag },
              { title: 'Fiscal Years', href: '/administration/hr/company-schedule/fiscal-years', icon: CalendarRange },
            ],
          },
          {
            title: 'Orientation & Onboarding',
            href: '/administration/hr/orientation',
            icon: GraduationCap,
            children: [
              { title: 'Programmes', href: '/administration/hr/orientation/programs', icon: BookOpen },
              { title: 'Categories', href: '/administration/hr/orientation/categories', icon: Tags },
              {
                title: 'Onboarding Templates',
                href: '/administration/hr/orientation/onboarding-templates',
                icon: FileStack,
              },
            ],
          },
          {
            // The movement reminder sweep: run-now, run history and the dispatch log. Operational
            // movement screens live under HR; this is the engine's admin surface.
            title: 'Movements',
            href: '/administration/hr/movements/reminders',
            icon: ArrowRightLeft,
          },
          {
            // The discipline reminder sweep: run-now, the preview, run history and the dispatch log.
            // Operational case and grievance screens live under HR; this is the engine's admin
            // surface — and muting a reminder means deactivating a notification topic, which is
            // administration's to do rather than the notified party's.
            title: 'Discipline',
            href: '/administration/hr/discipline/reminders',
            icon: Gavel,
          },
          {
            // Slice 10's route sweep found this one. Movements, discipline and safety each had
            // their reminder sweep in this nav and probation's did not, so a screen that behaves
            // exactly like its three neighbours had no way in — and `confirming-authorities`,
            // which only the reminders page links to, was unreachable behind it.
            title: 'Probation',
            href: '/administration/hr/probation/reminders',
            icon: UserPlus,
            children: [
              { title: 'Reminders', href: '/administration/hr/probation/reminders', icon: AlarmClock },
              {
                title: 'Confirming Authorities',
                href: '/administration/hr/probation/confirming-authorities',
                icon: UserRoundCheck,
              },
            ],
          },
          {
            // Both children were orphans. ⚠ The policy register lists and approves but cannot
            // create or edit: its "Draft a policy" button and its per-row link both pointed at
            // pages that were never built, so they are gone until they are. The API behind them
            // is complete — see the slice 10 entry in the areas 19–23 plan.
            title: 'Travel',
            href: '/administration/hr/travel/policies',
            icon: Plane,
            children: [
              { title: 'Travel Policies', href: '/administration/hr/travel/policies', icon: ShieldCheck },
              { title: 'Destination Alerts', href: '/administration/hr/travel/alerts', icon: TriangleAlert },
              { title: 'Reminders', href: '/administration/hr/travel/reminders', icon: AlarmClock },
            ],
          },
          {
            // The clearance form an exiting employee is walked through, by department. Built,
            // and reachable from nothing until the sweep.
            title: 'Separation',
            href: '/administration/hr/separation/clearance-form',
            icon: DoorOpen,
          },
          {
            title: 'Safety (SHE)',
            href: '/administration/hr/safety',
            icon: HardHat,
            children: [
              {
                title: 'Incident Types',
                href: '/administration/hr/safety/incident-types',
                icon: AlertTriangle,
              },
              {
                title: 'Injury Types',
                href: '/administration/hr/safety/injury-types',
                icon: Bandage,
              },
              {
                title: 'Body Parts',
                href: '/administration/hr/safety/body-parts',
                icon: PersonStanding,
              },
              {
                title: 'CA Templates',
                href: '/administration/hr/safety/corrective-action-templates',
                icon: ClipboardList,
              },
              {
                title: 'Regulatory Bodies',
                href: '/administration/hr/safety/regulatory-bodies',
                icon: Landmark,
              },
              {
                title: 'PPE Types',
                href: '/administration/hr/safety/ppe-types',
                icon: HardHat,
              },
              {
                title: 'PPE Requirements',
                href: '/administration/hr/safety/ppe-requirements',
                icon: ClipboardCheck,
              },
              {
                title: 'Reminder Engine',
                href: '/administration/hr/safety/reminders',
                icon: AlarmClock,
              },
            ],
          },
          // Employee Categories has no backing controller at all, so it stays dropped.
          {
            title: 'Payroll',
            href: '/administration/hr/payroll',
            icon: CreditCard,
          },
          {
            // Tenant-wide HR configuration. The company profile is the letterhead every offer and
            // confirmation letter is written on; policy settings follows in the next slice.
            title: 'HR Settings',
            href: '/administration/hr/settings',
            icon: Settings,
            children: [
              {
                title: 'Company Profile',
                href: '/administration/hr/settings/company-profile',
                icon: Building2,
              },
              {
                title: 'Policy Settings',
                href: '/administration/hr/settings/policy',
                icon: SlidersHorizontal,
              },
            ],
          },
        ],
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
            title: 'Executable Policies',
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

    let x = rect.right + 8;
    let y = rect.top;

    // Adjust horizontal position if menu would go off-screen
    if (x + menuWidth > viewportWidth - padding) {
      x = Math.max(padding, rect.left - menuWidth - 8); // Position to the left instead
    }

    // Adjust vertical position if menu would go off-screen
    const maxMenuHeight = viewportHeight - 2 * padding;
    const actualMenuHeight = Math.min(estimatedHeight, maxMenuHeight);

    if (y + actualMenuHeight > viewportHeight - padding) {
      const availableSpaceBelow = viewportHeight - y - padding;
      const availableSpaceAbove = rect.top - padding;

      if (
        availableSpaceAbove > availableSpaceBelow &&
        availableSpaceAbove >= 150
      ) {
        // Position above if there's more space and at least 150px available
        y = Math.max(padding, rect.bottom - actualMenuHeight);
      } else {
        // Position to fit in viewport with padding
        y = Math.max(padding, viewportHeight - actualMenuHeight - padding);
      }
    }

    // Ensure minimum top position
    y = Math.max(padding, y);

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
    const estimatedHeight = Math.min(600, childCount * 40 + 16); // 40px per item + padding

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
    const estimatedHeight = Math.min(600, grandChildCount * 40 + 16); // 40px per item + padding

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
    const estimatedHeight = Math.min(600, greatGrandChildCount * 40 + 16); // 40px per item + padding

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
      height: Math.max(targetMenu.height + 64, 180),
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

  const filterNavItems = (items: NavItem[]): NavItem[] => {
    // During SSR or before mount, show all items to avoid hydration mismatch
    if (!mounted) {
      return items;
    }

    return items.reduce<NavItem[]>((acc, item) => {
      const hasRoleAccess = !item.roles || hasAnyRole(item.roles);
      const hasPermissionAccess =
        !item.permissions || hasAnyPermission(item.permissions);
      const hasAccess =
        item.accessMode === 'any'
          ? hasRoleAccess || hasPermissionAccess
          : hasRoleAccess && hasPermissionAccess;

      if (!hasAccess) {
        return acc;
      }

      const children = item.children
        ? filterNavItems(item.children)
        : undefined;
      if (item.children && (!children || children.length === 0)) {
        return acc;
      }

      acc.push({
        ...item,
        children,
      });

      return acc;
    }, []);
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
        <nav className="flex-1 space-y-0 overflow-y-auto p-2">
          {filterNavItems(sidebarNavigationItems).map((item) => {
            const Icon = item.icon;
            const hasChildren = item.children && item.children.length > 0;
            const itemIsActive = isActive(item.href);

            return (
              <div key={item.title} className="relative">
                {hasChildren ? (
                  <button
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
                      'flex w-full items-center justify-between rounded-xl px-4 py-3 text-sm font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <div className="flex items-center space-x-4">
                      <Icon className="h-6 w-6 flex-shrink-0" />
                      {!sidebarIsCollapsed && <span>{item.title}</span>}
                    </div>
                    {!sidebarIsCollapsed && hasChildren && (
                      <ChevronRight className="h-5 w-5" />
                    )}
                  </button>
                ) : (
                  <Link
                    href={item.href}
                    className={cn(
                      'flex items-center space-x-4 rounded-xl px-4 py-3 text-sm font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <Icon className="h-6 w-6 flex-shrink-0" />
                    {!sidebarIsCollapsed && <span>{item.title}</span>}
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
          className="fixed bg-white/95 dark:bg-[#202020]/95 backdrop-blur-xl border border-slate-200/50 dark:border-neutral-700/70 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
          style={{
            left: menuPositions[hoveredItem].x,
            top: menuPositions[hoveredItem].y,
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgb(148 163 184) transparent',
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        >
          {filterNavItems(sidebarNavigationItems)
            .find((item) => item.title === hoveredItem)
            ?.children?.map((child) => {
              const ChildIcon = child.icon;
              const childIsActive = isActive(child.href);
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
                      className="w-full flex items-center gap-3 px-4 py-2 text-[13px] text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-700 dark:text-slate-300 transition-colors"
                    >
                      <ChildIcon className="h-4 w-4 flex-shrink-0" />
                      <span className="flex-1">{child.title}</span>
                      <ChevronRight className="h-4 w-4" />
                    </button>
                  ) : (
                    <Link
                      href={child.href}
                      onClick={clearMenus}
                      className={cn(
                        'flex items-center gap-3 px-4 py-2 text-[13px] hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
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
            className="fixed bg-white/95 dark:bg-[#202020]/95 backdrop-blur-xl border border-slate-200/50 dark:border-neutral-700/70 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
            style={{
              left: menuPositions[`${hoveredItem}-${hoveredChild}`].x,
              top: menuPositions[`${hoveredItem}-${hoveredChild}`].y,
              scrollbarWidth: 'thin',
              scrollbarColor: 'rgb(148 163 184) transparent',
            }}
            onMouseEnter={handleMenuMouseEnter}
            onMouseLeave={handleMenuMouseLeave}
          >
            {filterNavItems(sidebarNavigationItems)
              .find((item) => item.title === hoveredItem)
              ?.children?.find((child) => child.title === hoveredChild)
              ?.children?.map((grandchild) => {
                const GrandChildIcon = grandchild.icon;
                const grandchildIsActive = isActive(grandchild.href);
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
                        className="w-full flex items-center gap-3 px-4 py-2 text-[13px] text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-600 dark:text-slate-400 transition-colors"
                      >
                        <GrandChildIcon className="h-3 w-3 flex-shrink-0" />
                        <span className="flex-1">{grandchild.title}</span>
                        <ChevronRight className="h-3 w-3" />
                      </button>
                    ) : (
                      <Link
                        href={grandchild.href}
                        onClick={clearMenus}
                        className={cn(
                          'flex items-center gap-3 px-4 py-2 text-[13px] hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
                          grandchildIsActive
                            ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                            : 'text-slate-600 dark:text-slate-400'
                        )}
                      >
                        <GrandChildIcon className="h-3 w-3 flex-shrink-0" />
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
            className="fixed bg-white/95 dark:bg-[#202020]/95 backdrop-blur-xl border border-slate-200/50 dark:border-neutral-700/70 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
            style={{
              left: menuPositions[
                `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
              ].x,
              top: menuPositions[
                `${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`
              ].y,
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
              ?.children?.map((greatGrandchild) => {
                const GreatGrandChildIcon = greatGrandchild.icon;
                const greatGrandchildIsActive = isActive(greatGrandchild.href);

                return (
                  <Link
                    key={greatGrandchild.title}
                    href={greatGrandchild.href}
                    onClick={clearMenus}
                    className={cn(
                      'flex items-center gap-3 px-4 py-2 text-[13px] hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
                      greatGrandchildIsActive
                        ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                        : 'text-slate-500 dark:text-slate-500'
                    )}
                  >
                    <GreatGrandChildIcon className="h-3 w-3 flex-shrink-0" />
                    <span>{greatGrandchild.title}</span>
                  </Link>
                );
              })}
          </div>
        )}
    </div>
  );
}
