'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  Building2,
  LayoutDashboard,
  Users,
  ShoppingCart,
  Package,
  CreditCard,
  UserCheck,
  Briefcase,
  Megaphone,
  Settings,
  ChevronDown,
  ChevronRight,
  Menu,
  X,
  Shield,
  FileText,
  Mail,
  Building,
  BarChart3,
  Bell,
  Database,
  Home,
  Code,
  HelpCircle,
  Workflow,
  Wrench,
  Calendar,
  ClipboardCheck,
  AlertTriangle,
  Clock,
  CheckSquare,
  Activity,
  FolderTree,
  FileCheck,
  Gavel,
  Award,
  Star,
  Target,
  DollarSign,
  TrendingUp,
  AlertCircle,
  ClipboardList,
  Tag,
  MapPin,
  Truck,
  ShieldCheck,
  Droplet,
  BookOpen,
  MessageSquare,
  Smartphone,
  RotateCcw,
  CalendarClock,
  Phone,
  Swords,
  BookTemplate,
} from 'lucide-react';

import { cn } from '../../lib/utils';
import { Button } from '../ui/button';
import { useAuth } from '../../hooks/use-auth';

interface NavItem {
  title: string;
  href: string;
  icon: React.ComponentType<any>;
  children?: NavItem[];
  roles?: string[];
  permissions?: string[];
}

const navigationItems: NavItem[] = [
  {
    title: 'Dashboard',
    href: '/dashboard',
    icon: LayoutDashboard,
  },
  {
    title: 'Finance',
    href: '/finance',
    icon: CreditCard,
    children: [
      { title: 'Dashboard', href: '/finance/dashboard', icon: LayoutDashboard },
      {
        title: 'General Ledger',
        href: '/finance/general-ledger',
        icon: FileText,
        children: [
          {
            title: 'Chart of Accounts',
            href: '/finance/accounts',
            icon: CreditCard,
          },
          {
            title: 'Journal Entries',
            href: '/finance/journal-entries',
            icon: FileText,
          },
        ],
      },
      {
        title: 'Segmented Accounts',
        href: '/finance/segmented-accounts',
        icon: FolderTree,
        children: [
          {
            title: 'Segment Structure',
            href: '/finance/settings/segments',
            icon: FolderTree,
          },
          {
            title: 'Segment Values',
            href: '/finance/settings/segments?tab=values',
            icon: Database,
          },
          {
            title: 'Account Generator',
            href: '/finance/accounts/generate',
            icon: FileText,
          },
        ],
      },
      {
        title: 'Fiscal Management',
        href: '/finance/fiscal',
        icon: Calendar,
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
            title: 'Categories',
            href: '/finance/fixed-assets/categories',
            icon: FolderTree,
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
          {
            title: 'Valuations',
            href: '/finance/fixed-assets/valuations',
            icon: TrendingUp,
          },
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
          { title: 'Invoices', href: '/finance/ap/invoices', icon: FileText },
          { title: 'Payments', href: '/finance/ap/payments', icon: CreditCard },
          { title: 'Reports', href: '/finance/ap/reports', icon: BarChart3 },
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
          { title: 'Customers', href: '/finance/ar/customers', icon: Users },
          { title: 'Invoices', href: '/finance/ar/invoices', icon: FileText },
          { title: 'Payments', href: '/finance/ar/payments', icon: CreditCard },
          { title: 'Reports', href: '/finance/ar/reports', icon: BarChart3 },
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
        ],
      },
      {
        title: 'Unit Accounting',
        href: '/finance/unit-accounting',
        icon: BarChart3,
        children: [
          { title: 'Unit Types', href: '/finance/unit-types', icon: FileText },
          {
            title: 'Unit Accounts',
            href: '/finance/unit-accounts',
            icon: BarChart3,
          },
          {
            title: 'Unit Journal Entries',
            href: '/finance/unit-journal-entries',
            icon: FileText,
          },
          {
            title: 'Ratio Definitions',
            href: '/finance/ratio-definitions',
            icon: BarChart3,
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
            title: 'Currencies',
            href: '/finance/currencies',
            icon: CreditCard,
          },
          {
            title: 'Exchange Rates',
            href: '/finance/exchange-rates',
            icon: BarChart3,
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
            title: 'Tax Configuration',
            href: '/finance/tax/configuration',
            icon: Settings,
          },
          {
            title: 'Taxes',
            href: '/finance/tax/configuration/taxes',
            icon: FileText,
          },
          {
            title: 'Tax Groups',
            href: '/finance/tax/configuration/groups',
            icon: FileText,
          },
          {
            title: 'Tax Calculator',
            href: '/finance/tax/calculator',
            icon: BarChart3,
          },
          {
            title: 'Tax Reports',
            href: '/finance/tax/reports',
            icon: FileText,
          },
        ],
      },
      { title: 'Financial Reports', href: '/finance/reports', icon: BarChart3 },
      { title: 'Settings', href: '/finance/settings', icon: Settings },
    ],
  },
  {
    title: 'Human Resources',
    href: '/hr',
    icon: UserCheck,
    children: [
      { title: 'Employees', href: '/hr/employees', icon: Users },
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
      { title: 'Attendance', href: '/hr/attendance', icon: UserCheck },
      { title: 'Leave Management', href: '/hr/leave', icon: UserCheck },
      { title: 'Performance', href: '/hr/performance', icon: BarChart3 },
    ],
  },
  {
    title: 'Maintenance Mngt',
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
    title: 'Fleet Management',
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
    title: 'Project Mngt',
    href: '/development/projects',
    icon: Briefcase,
    permissions: ['project.access'],
    children: [
      { title: 'Projects', href: '/development/projects', icon: Briefcase },
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
        title: 'Registrations',
        href: '/administration/procurement/registrations',
        icon: FileText,
      },
      {
        title: 'Business Partners',
        href: '/procurement/business-partners',
        icon: Users,
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
      // { title: 'Suppliers', href: '/procurement/suppliers', icon: Briefcase },
      // { title: 'Vendor Management', href: '/procurement/vendors', icon: Users },
    ],
  },
  {
    title: 'Inventory',
    href: '/inventory',
    icon: Package,
    children: [
      {
        title: 'Cards',
        href: '/inventory/cards',
        icon: Package,
        children: [
          { title: 'Inventory Items', href: '/inventory/items', icon: Package },
          {
            title: 'Warehouse Items',
            href: '/inventory/warehouse-items',
            icon: Building2,
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
      },
    ],
  },
  {
    title: 'Estate',
    href: '/estate',
    icon: Home,
    children: [
      { title: 'Properties', href: '/estate/properties', icon: Home },
      { title: 'Tenants', href: '/estate/tenants', icon: Users },
      { title: 'Leases', href: '/estate/leases', icon: FileText },
      { title: 'Maintenance', href: '/estate/maintenance', icon: Wrench },
      {
        title: 'Rent Collection',
        href: '/estate/rent-collection',
        icon: CreditCard,
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
    title: 'Helpdesk & Complaints',
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
        href: '/reports?module=financial',
        icon: CreditCard,
      },
      {
        title: 'Sales Reports',
        href: '/reports?module=sales',
        icon: ShoppingCart,
      },
      { title: 'HR Reports', href: '/reports/hr', icon: UserCheck },
      {
        title: 'Inventory Reports',
        href: '/reports?module=inventory',
        icon: Package,
      },
      { title: 'Estate Reports', href: '/reports?module=estate', icon: Home },
      {
        title: 'Development Reports',
        href: '/reports?module=development',
        icon: Code,
      },
      {
        title: 'Operations Reports',
        href: '/reports?module=operations',
        icon: BarChart3,
      },
    ],
  },
  {
    title: 'Notifications',
    href: '/notifications',
    icon: Bell,
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
    roles: ['admin', 'SuperAdmin', 'TenantAdmin'],
    children: [
      {
        title: 'Message Queue',
        href: '/administration/notifications',
        icon: Bell,
      },
      {
        title: 'System Logs',
        href: '/administration/system-exception-logs',
        icon: AlertTriangle,
      },
      {
        title: 'Finance',
        href: '/administration/finance',
        icon: CreditCard,
        children: [
          {
            title: 'Chart of Accounts',
            href: '/administration/finance/accounts',
            icon: CreditCard,
          },
          {
            title: 'Tax Configuration',
            href: '/administration/finance/tax',
            icon: CreditCard,
          },
          {
            title: 'Payment Terms',
            href: '/administration/finance/payment-terms',
            icon: CreditCard,
          },
          {
            title: 'Currencies',
            href: '/administration/finance/currencies',
            icon: DollarSign,
          },
          {
            title: 'Currency Settings',
            href: '/administration/finance/currency',
            icon: CreditCard,
          },
          {
            title: 'Payment Methods',
            href: '/administration/finance/payment-methods',
            icon: CreditCard,
          },
          {
            title: 'Financial Periods',
            href: '/administration/finance/periods',
            icon: CreditCard,
          },
        ],
      },
      {
        title: 'HR',
        href: '/administration/hr',
        icon: UserCheck,
        children: [
          {
            title: 'Job Positions',
            href: '/administration/hr/positions',
            icon: Users,
          },
          {
            title: 'Departments',
            href: '/administration/hr/departments',
            icon: Building,
          },
          {
            title: 'Employee Categories',
            href: '/administration/hr/categories',
            icon: Users,
          },
          {
            title: 'Leave Types',
            href: '/administration/hr/leave-types',
            icon: UserCheck,
          },
          {
            title: 'Payroll',
            href: '/administration/hr/payroll',
            icon: CreditCard,
          },
        ],
      },
      {
        title: 'Procurement',
        href: '/administration/procurement',
        icon: Briefcase,
        children: [
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
            title: 'Access & Committees',
            href: '/administration/procurement/access-controls',
            icon: Users,
          },
          {
            title: 'Master Data Changes',
            href: '/administration/procurement/master-data-changes',
            icon: FileCheck,
          },
          {
            title: 'Control Events',
            href: '/administration/procurement/control-events',
            icon: Activity,
          },
          {
            title: 'Pending Partners',
            href: '/administration/procurement/business-partners/pending',
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
        ],
      },
      {
        title: 'Sales',
        href: '/administration/sales',
        icon: ShoppingCart,
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
        title: 'Project Mngt',
        href: '/administration/project-management',
        icon: Briefcase,
        permissions: ['admin.project-management'],
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
        ],
      },
      {
        title: 'Maintenance Mngt',
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
        title: 'Fleet Management',
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
      },
      {
        title: 'System',
        href: '/administration/system',
        icon: Settings,
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

interface SidebarProps {
  className?: string;
}

export function Sidebar({ className }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(false);
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
    const menuItem = filterNavItems(navigationItems).find(
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
    const parentItem = filterNavItems(navigationItems).find(
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
    const parentItem = filterNavItems(navigationItems).find(
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
      if (item.roles && !hasAnyRole(item.roles)) {
        return acc;
      }

      if (item.permissions && !hasAnyPermission(item.permissions)) {
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
        onMouseLeave={handleSidebarMouseLeave}
        className={cn(
          'flex h-full flex-col bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border-r border-slate-200/50 dark:border-slate-800/50 transition-all duration-300',
          collapsed ? 'w-16' : 'w-72',
          className
        )}
      >
        {/* Header */}
        <div className="flex h-16 items-center justify-between px-4 border-b border-slate-200/50 dark:border-slate-800/50">
          {!collapsed && (
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
            onClick={() => setCollapsed(!collapsed)}
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
          {filterNavItems(navigationItems).map((item) => {
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
                      'flex w-full items-center justify-between rounded-xl px-4 py-3 text-base font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <div className="flex items-center space-x-4">
                      <Icon className="h-6 w-6 flex-shrink-0" />
                      {!collapsed && <span>{item.title}</span>}
                    </div>
                    {!collapsed && hasChildren && (
                      <ChevronRight className="h-5 w-5" />
                    )}
                  </button>
                ) : (
                  <Link
                    href={item.href}
                    className={cn(
                      'flex items-center space-x-4 rounded-xl px-4 py-3 text-base font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <Icon className="h-6 w-6 flex-shrink-0" />
                    {!collapsed && <span>{item.title}</span>}
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
          style={getBridgeStyle(collapsed ? 64 : 288, hoveredItem)}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        />
      )}

      {/* First Level Floating Submenu */}
      {hoveredItem && menuPositions[hoveredItem] && (
        <div
          data-sidebar-flyout="true"
          className="fixed bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-slate-200/50 dark:border-slate-800/50 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
          style={{
            left: menuPositions[hoveredItem].x,
            top: menuPositions[hoveredItem].y,
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgb(148 163 184) transparent',
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        >
          {filterNavItems(navigationItems)
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
                      className="w-full flex items-center gap-3 px-4 py-2 text-sm text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-700 dark:text-slate-300 transition-colors"
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
                        'flex items-center gap-3 px-4 py-2 text-sm hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
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
            className="fixed bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-slate-200/50 dark:border-slate-800/50 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
            style={{
              left: menuPositions[`${hoveredItem}-${hoveredChild}`].x,
              top: menuPositions[`${hoveredItem}-${hoveredChild}`].y,
              scrollbarWidth: 'thin',
              scrollbarColor: 'rgb(148 163 184) transparent',
            }}
            onMouseEnter={handleMenuMouseEnter}
            onMouseLeave={handleMenuMouseLeave}
          >
            {filterNavItems(navigationItems)
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
                        className="w-full flex items-center gap-3 px-4 py-2 text-sm text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-600 dark:text-slate-400 transition-colors"
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
                          'flex items-center gap-3 px-4 py-2 text-sm hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
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
            className="fixed bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-slate-200/50 dark:border-slate-800/50 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
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
            {filterNavItems(navigationItems)
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
                      'flex items-center gap-3 px-4 py-2 text-sm hover:bg-slate-100 dark:hover:bg-slate-800/50 transition-colors',
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
