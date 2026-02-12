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
  Ruler,
  Divide,
  Layers,
  Calculator,
  Wallet,
  ArrowLeftRight,
  CheckCircle2,
  Wand2,
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
          { title: 'Chart of Accounts', href: '/finance/accounts', icon: CreditCard },
          { title: 'Journal Entries', href: '/finance/journal-entries', icon: FileText },
        ],
      },
      {
        title: 'Unit Accounting',
        href: '/finance/unit-accounting',
        icon: Calculator,
        children: [
          { title: 'Unit Types', href: '/finance/unit-types', icon: Ruler },
          { title: 'Unit Accounts', href: '/finance/unit-accounts', icon: Calculator },
          { title: 'Unit Journal Entries', href: '/finance/unit-journal-entries', icon: FileText },
          { title: 'Ratio Definitions', href: '/finance/ratio-definitions', icon: Divide },
          { title: 'Unit Budgets', href: '/finance/unit-budgets', icon: BarChart3 },
          { title: 'Allocations', href: '/finance/allocations', icon: Calculator },
        ],
      },
      {
        title: 'Segmented Accounts',
        href: '/finance/segmented-accounts',
        icon: FolderTree,
        children: [
          { title: 'Segment Structure', href: '/finance/settings/segments', icon: FolderTree },
          { title: 'Segment Values', href: '/finance/settings/segments?tab=values', icon: Database },
          { title: 'Account Generator', href: '/finance/accounts/generate', icon: Wand2 },
        ],
      },
      {
        title: 'Fiscal Management',
        href: '/finance/fiscal',
        icon: Calendar,
        children: [
          { title: 'Fiscal Years', href: '/finance/fiscal-years', icon: Calendar },
          { title: 'Fiscal Periods', href: '/finance/fiscal-periods', icon: Calendar },
        ],
      },
      {
        title: 'Budgeting',
        href: '/finance/budgeting',
        icon: BarChart3,
        children: [
          { title: 'Scenarios', href: '/finance/budgeting/scenarios', icon: Calculator },
          { title: 'My Returns', href: '/finance/budgeting/my-returns', icon: FileText },
        ],
      },
      {
        title: 'Multi-Currency',
        href: '/finance/multi-currency',
        icon: CreditCard,
        children: [
          { title: 'Currencies', href: '/finance/currencies', icon: CreditCard },
          { title: 'Exchange Rates', href: '/finance/exchange-rates', icon: BarChart3 },
          { title: 'Revaluation', href: '/finance/revaluation', icon: BarChart3 },
        ],
      },
      {
        title: 'Taxation',
        href: '/finance/tax',
        icon: Calculator,
        children: [
          { title: 'Tax Configuration', href: '/finance/tax/configuration', icon: Settings },
          { title: 'Taxes', href: '/finance/tax/configuration/taxes', icon: FileText },
          { title: 'Tax Groups', href: '/finance/tax/configuration/groups', icon: Layers },
          { title: 'Tax Calculator', href: '/finance/tax/calculator', icon: Calculator },
          { title: 'Tax Reports', href: '/finance/tax/reports', icon: FileText },
        ],
      },
      {
        title: 'Accounts Receivable',
        href: '/finance/ar',
        icon: Users,
        children: [
          { title: 'Dashboard', href: '/finance/ar/dashboard', icon: LayoutDashboard },
          { title: 'Customers', href: '/finance/ar/customers', icon: Users },
          { title: 'Invoices', href: '/finance/ar/invoices', icon: FileText },
          { title: 'Payments', href: '/finance/ar/payments', icon: Wallet },
          { title: 'Reports', href: '/finance/ar/reports', icon: BarChart3 },
        ],
      },
      {
        title: 'Cash Management',
        href: '/finance/cash',
        icon: Wallet,
        children: [
          { title: 'Bank Accounts', href: '/finance/cash/accounts', icon: Building2 },
          { title: 'Cash Transactions', href: '/finance/cash/transactions', icon: ArrowLeftRight },
          { title: 'Bank Reconciliation', href: '/finance/cash/reconciliation', icon: CheckCircle2 },
          { title: 'Cash Reports', href: '/finance/cash/reports', icon: FileText },
        ],
      },
      {
        title: 'Fixed Assets',
        href: '/finance/fixed-assets',
        icon: Building2,
        children: [
          { title: 'Dashboard', href: '/finance/fixed-assets/dashboard', icon: LayoutDashboard },
          { title: 'Asset Register', href: '/finance/fixed-assets/register', icon: Package },
          { title: 'Categories', href: '/finance/fixed-assets/categories', icon: ClipboardCheck },
          { title: 'Depreciation', href: '/finance/fixed-assets/depreciation', icon: Activity },
          { title: 'Transfers', href: '/finance/fixed-assets/transfers', icon: ArrowLeftRight },
          { title: 'Disposals', href: '/finance/fixed-assets/disposals', icon: Settings },
          { title: 'Verification', href: '/finance/fixed-assets/verification', icon: FileCheck },
          { title: 'Reports', href: '/finance/fixed-assets/reports', icon: BarChart3 },
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
      { title: 'Payroll', href: '/hr/payroll', icon: CreditCard },
      { title: 'Attendance', href: '/hr/attendance', icon: UserCheck },
      { title: 'Leave Management', href: '/hr/leave', icon: UserCheck },
      { title: 'Performance', href: '/hr/performance', icon: BarChart3 },
    ],
  },
  {
    title: 'Procurement',
    href: '/procurement',
    icon: Briefcase,
    children: [
      { title: 'Business Partners', href: '/procurement/business-partners', icon: Users },
      { title: 'Registrations', href: '/administration/procurement/registrations', icon: FileText },
      { title: 'Suppliers', href: '/procurement/suppliers', icon: Briefcase },
      { title: 'Purchase Orders', href: '/procurement/purchase-orders', icon: ShoppingCart },
      { title: 'Purchase Requests', href: '/procurement/requests', icon: FileText },
      { title: 'Vendor Management', href: '/procurement/vendors', icon: Users },
    ],
  },
  {
    title: 'Inventory',
    href: '/inventory',
    icon: Package,
    children: [
      { title: 'Products', href: '/inventory/products', icon: Package },
      { title: 'Stock Management', href: '/inventory/stock', icon: Package },
      { title: 'Warehouses', href: '/inventory/warehouses', icon: Building2 },
      { title: 'Stock Movements', href: '/inventory/movements', icon: Package },
    ],
  },
  {
    title: 'Sales',
    href: '/sales',
    icon: ShoppingCart,
    children: [
      { title: 'Customers', href: '/sales/customers', icon: Users },
      { title: 'Orders', href: '/sales/orders', icon: ShoppingCart },
      { title: 'Quotes', href: '/sales/quotes', icon: FileText },
      { title: 'Invoicing', href: '/sales/invoicing', icon: CreditCard },
      { title: 'Sales Pipeline', href: '/sales/pipeline', icon: BarChart3 },
    ],
  },
  {
    title: 'Marketing',
    href: '/marketing',
    icon: Megaphone,
    children: [
      { title: 'Campaigns', href: '/marketing/campaigns', icon: Megaphone },
      { title: 'Leads', href: '/marketing/leads', icon: Users },
      { title: 'Contacts', href: '/marketing/contacts', icon: Users },
      { title: 'Analytics', href: '/marketing/analytics', icon: BarChart3 },
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
      { title: 'Rent Collection', href: '/estate/rent-collection', icon: CreditCard },
    ],
  },
  {
    title: 'Development',
    href: '/development',
    icon: Building,
    children: [
      {
        title: 'Project Mngt',
        href: '/development/project-management',
        icon: Briefcase,
        children: [
          { title: 'Projects', href: '/development/projects', icon: Briefcase },
          { title: 'Tasks', href: '/development/tasks', icon: FileText },
          { title: 'Resources', href: '/development/resources', icon: Users },
          { title: 'Timeline', href: '/development/timeline', icon: BarChart3 },
        ],
      },
      {
        title: 'Maintenance Mngt',
        href: '/maintenance',
        icon: Wrench,
        children: [
          { title: 'Dashboard', href: '/maintenance/dashboard', icon: LayoutDashboard },
          { title: 'Job Cards', href: '/maintenance/job-cards', icon: FileText },
          { title: 'Work Orders', href: '/maintenance/work-orders', icon: FileText },
          { title: 'Assets', href: '/maintenance/assets', icon: Package },
          { title: 'Technicians', href: '/maintenance/technicians', icon: Users },
          { title: 'Quality Control', href: '/maintenance/quality-control', icon: ClipboardCheck },
          { title: 'Scheduled Maintenance', href: '/maintenance/scheduled', icon: Calendar },
          { title: 'Condition Monitoring', href: '/maintenance/condition-monitoring', icon: Activity },
          // Emergency Maintenance temporarily hidden
          // { title: 'Emergency Maintenance', href: '/maintenance/emergency', icon: AlertTriangle },
          // Inspections temporarily hidden
          // { title: 'Inspections', href: '/maintenance/inspections', icon: ClipboardCheck },
          { title: 'Maintenance History', href: '/maintenance/history', icon: Clock },
          { title: 'Tool Management', href: '/maintenance/tools', icon: Wrench },
          // Reports temporarily hidden
          // { title: 'Reports', href: '/maintenance/reports', icon: BarChart3 },
          // Asset Analytics temporarily hidden
          // { title: 'Asset Analytics', href: '/maintenance/analytics', icon: BarChart3 },
        ],
      },
    ],
  },
  {
    title: 'Enquiry & Helpdesk',
    href: '/helpdesk',
    icon: HelpCircle,
    children: [
      { title: 'Dashboard', href: '/helpdesk/dashboard', icon: BarChart3 },
      { title: 'Tickets', href: '/helpdesk/tickets', icon: FileText },
      { title: 'Knowledge Base', href: '/helpdesk/knowledge-base', icon: FileText },
      { title: 'Customer Support', href: '/helpdesk/support', icon: Users },
      { title: 'FAQ Management', href: '/helpdesk/faq', icon: HelpCircle },
    ],
  },
  {
    title: 'Workflow',
    href: '/workflow',
    icon: Workflow,
    children: [
      { title: 'Workflow Demo', href: '/workflow-demo', icon: Workflow },
      { title: 'Process Designer', href: '/workflow/designer', icon: Code },
      { title: 'Running Processes', href: '/workflow/running', icon: Workflow },
      { title: 'Process History', href: '/workflow/history', icon: FileText },
      { title: 'Task Management', href: '/workflow/tasks', icon: FileText },
    ],
  },
  {
    title: 'Reports',
    href: '/reports',
    icon: BarChart3,
    children: [
      { title: 'Financial Reports', href: '/reports?module=financial', icon: CreditCard },
      { title: 'Sales Reports', href: '/reports?module=sales', icon: ShoppingCart },
      { title: 'HR Reports', href: '/reports?module=hr', icon: UserCheck },
      { title: 'Inventory Reports', href: '/reports?module=inventory', icon: Package },
      { title: 'Estate Reports', href: '/reports?module=estate', icon: Home },
      { title: 'Development Reports', href: '/reports?module=development', icon: Code },
      { title: 'Operations Reports', href: '/reports?module=operations', icon: BarChart3 },
    ],
  },
  {
    title: 'Notifications',
    href: '/notifications',
    icon: Bell,
    children: [
      { title: 'Notification Center', href: '/notifications#center', icon: Bell },
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
        title: 'Finance',
        href: '/administration/finance',
        icon: CreditCard,
        children: [
          { title: 'Chart of Accounts', href: '/administration/finance/accounts', icon: CreditCard },
          { title: 'Tax Configuration', href: '/administration/finance/tax', icon: CreditCard },
          { title: 'Currency Settings', href: '/administration/finance/currency', icon: CreditCard },
          { title: 'Payment Methods', href: '/administration/finance/payment-methods', icon: CreditCard },
          { title: 'Financial Periods', href: '/administration/finance/periods', icon: CreditCard },
        ],
      },
      {
        title: 'HR',
        href: '/administration/hr',
        icon: UserCheck,
        children: [
          { title: 'Job Positions', href: '/administration/hr/positions', icon: Users },
          { title: 'Departments', href: '/administration/hr/departments', icon: Building },
          { title: 'Employee Categories', href: '/administration/hr/categories', icon: Users },
          { title: 'Leave Types', href: '/administration/hr/leave-types', icon: UserCheck },
          { title: 'Payroll Components', href: '/administration/hr/payroll-components', icon: CreditCard },
        ],
      },
      {
        title: 'Procurement',
        href: '/administration/procurement',
        icon: Briefcase,
        children: [
          { title: 'Partner Categories', href: '/administration/procurement/partner-categories', icon: FolderTree },
          { title: 'Contractor Specializations', href: '/administration/procurement/contractor-specializations', icon: Wrench },
          { title: 'License Types', href: '/administration/procurement/license-types', icon: FileCheck },
          { title: 'Approval Workflows', href: '/administration/procurement/approval-workflows', icon: Workflow },
          { title: 'Supplier Categories', href: '/administration/procurement/supplier-categories', icon: Briefcase },
          { title: 'Purchase Categories', href: '/administration/procurement/purchase-categories', icon: Package },
          { title: 'Terms & Conditions', href: '/administration/procurement/terms', icon: FileText },
        ],
      },
      {
        title: 'Inventory',
        href: '/administration/inventory',
        icon: Package,
        children: [
          { title: 'Product Categories', href: '/administration/inventory/categories', icon: Package },
          { title: 'Units of Measure', href: '/administration/inventory/units', icon: Package },
          { title: 'Storage Locations', href: '/administration/inventory/locations', icon: Building2 },
          { title: 'Stock Levels', href: '/administration/inventory/stock-levels', icon: Package },
        ],
      },
      {
        title: 'Sales',
        href: '/administration/sales',
        icon: ShoppingCart,
        children: [
          { title: 'Customer Categories', href: '/administration/sales/customer-categories', icon: Users },
          { title: 'Sales Territories', href: '/administration/sales/territories', icon: Building },
          { title: 'Price Lists', href: '/administration/sales/price-lists', icon: CreditCard },
          { title: 'Sales Channels', href: '/administration/sales/channels', icon: ShoppingCart },
          { title: 'Commission Rules', href: '/administration/sales/commission', icon: CreditCard },
        ],
      },
      {
        title: 'Marketing',
        href: '/administration/marketing',
        icon: Megaphone,
        children: [
          { title: 'Campaign Templates', href: '/administration/marketing/templates', icon: Megaphone },
          { title: 'Lead Sources', href: '/administration/marketing/lead-sources', icon: Users },
          { title: 'Market Segments', href: '/administration/marketing/segments', icon: Users },
          { title: 'Marketing Channels', href: '/administration/marketing/channels', icon: Megaphone },
        ],
      },
      {
        title: 'Estate',
        href: '/administration/estate',
        icon: Home,
        children: [
          { title: 'Property Types', href: '/administration/estate/property-types', icon: Home },
          { title: 'Lease Templates', href: '/administration/estate/lease-templates', icon: FileText },
          { title: 'Maintenance Categories', href: '/administration/estate/maintenance-categories', icon: Wrench },
          { title: 'Tenant Categories', href: '/administration/estate/tenant-categories', icon: Users },
        ],
      },
      {
        title: 'Development',
        href: '/administration/development',
        icon: Building,
        children: [
          {
            title: 'Project Mngt',
            href: '/administration/development/project-management',
            icon: Briefcase,
            children: [
              { title: 'Project Templates', href: '/administration/development/templates', icon: Briefcase },
              { title: 'Task Categories', href: '/administration/development/task-categories', icon: FileText },
              { title: 'Development Stages', href: '/administration/development/stages', icon: BarChart3 },
              { title: 'Resource Types', href: '/administration/development/resource-types', icon: Users },
            ],
          },
          {
            title: 'Maintenance Mngt',
            href: '/administration/maintenance',
            icon: Wrench,
            children: [
              { title: 'Asset Categories', href: '/administration/maintenance/asset-categories', icon: Package },
              { title: 'Work Order Types', href: '/administration/maintenance/work-order-types', icon: FileText },
              { title: 'Maintenance Types', href: '/administration/maintenance/maintenance-types', icon: Wrench },
              { title: 'Priority Levels', href: '/administration/maintenance/priorities', icon: AlertTriangle },
              { title: 'Task Templates', href: '/administration/maintenance/task-templates', icon: FileText },
              { title: 'Quality Checklists', href: '/administration/maintenance/quality-checklists', icon: CheckSquare },
              // The following advanced maintenance admin menus are temporarily hidden:
              // { title: 'Inspection Templates', href: '/administration/maintenance/inspection-templates', icon: ClipboardCheck },
              // { title: 'Inspectors', href: '/administration/maintenance/inspectors', icon: Users },
              // { title: 'Maintenance Schedules', href: '/administration/maintenance/schedules', icon: Calendar },
              // { title: 'Technician Skills', href: '/administration/maintenance/skills', icon: Users },
              // { title: 'Safety Protocols', href: '/administration/maintenance/safety', icon: Shield },
            ],
          },
        ],
      },
      {
        title: 'Helpdesk',
        href: '/administration/helpdesk',
        icon: HelpCircle,
        children: [
          { title: 'Ticket Categories', href: '/administration/helpdesk/categories', icon: FileText },
          { title: 'Priority Levels', href: '/administration/helpdesk/priorities', icon: BarChart3 },
          { title: 'SLA Templates', href: '/administration/helpdesk/sla', icon: FileText },
          { title: 'Workflow Routing', href: '/administration/helpdesk/workflows', icon: Workflow },
          { title: 'Support Channels', href: '/administration/helpdesk/channels', icon: HelpCircle },
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
          { title: 'User Management', href: '/administration/identity-management/users', icon: Users },
          { title: 'Role Management', href: '/administration/identity-management/roles', icon: Shield },
          { title: 'User-Employee Links', href: '/administration/user-employee-links', icon: UserCheck },
          { title: 'Tenant Management', href: '/administration/tenant-management', icon: Building },
          { title: 'Email Settings', href: '/administration/settings/email', icon: Mail },
          { title: 'User-Tenant Mapping', href: '/administration/user-tenant-mapping', icon: Users },
          { title: 'Security Logs', href: '/administration/identity-management/security-logs', icon: FileText },
          { title: 'Audit Logs', href: '/administration/audit-logs', icon: FileText },
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
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set());
  const [hoveredItem, setHoveredItem] = useState<string | null>(null);
  const [hoveredChild, setHoveredChild] = useState<string | null>(null);
  const [hoveredGrandChild, setHoveredGrandChild] = useState<string | null>(null);
  const [menuPositions, setMenuPositions] = useState<{ [key: string]: { x: number; y: number } }>({});
  const [activeMenuPath, setActiveMenuPath] = useState<string[]>([]);
  const [mouseInMenu, setMouseInMenu] = useState(false);
  const [lastMousePosition, setLastMousePosition] = useState({ x: 0, y: 0 });
  const [isMovingToChild, setIsMovingToChild] = useState(false);
  const sidebarRef = useRef<HTMLDivElement>(null);
  const timeoutRef = useRef<NodeJS.Timeout | null>(null);
  const moveTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const pathname = usePathname();
  const { hasAnyRole } = useAuth();

  useEffect(() => {
    setMounted(true);

    // Track global mouse movement for safe zone detection
    const handleMouseMove = (e: MouseEvent) => {
      setLastMousePosition({ x: e.clientX, y: e.clientY });
    };

    document.addEventListener('mousemove', handleMouseMove);
    return () => document.removeEventListener('mousemove', handleMouseMove);
  }, []);

  const isMouseMovingTowardsMenu = (menuPosition: { x: number; y: number }, currentMouse: { x: number; y: number }, previousMouse: { x: number; y: number }) => {
    // Calculate if mouse is moving in the general direction of the menu
    const menuVector = {
      x: menuPosition.x - previousMouse.x,
      y: menuPosition.y - previousMouse.y
    };

    const mouseVector = {
      x: currentMouse.x - previousMouse.x,
      y: currentMouse.y - previousMouse.y
    };

    // Dot product to check if vectors are pointing in similar direction
    const dotProduct = menuVector.x * mouseVector.x + menuVector.y * mouseVector.y;
    const menuMagnitude = Math.sqrt(menuVector.x * menuVector.x + menuVector.y * menuVector.y);
    const mouseMagnitude = Math.sqrt(mouseVector.x * mouseVector.x + mouseVector.y * mouseVector.y);

    if (menuMagnitude === 0 || mouseMagnitude === 0) return false;

    // Cosine similarity - if > 0.3, mouse is moving roughly toward menu
    const similarity = dotProduct / (menuMagnitude * mouseMagnitude);
    return similarity > 0.3;
  };

  const calculateMenuPosition = (rect: DOMRect, menuKey: string, estimatedHeight: number = 400) => {
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
    const maxMenuHeight = viewportHeight - (2 * padding);
    const actualMenuHeight = Math.min(estimatedHeight, maxMenuHeight);

    if (y + actualMenuHeight > viewportHeight - padding) {
      const availableSpaceBelow = viewportHeight - y - padding;
      const availableSpaceAbove = rect.top - padding;

      if (availableSpaceAbove > availableSpaceBelow && availableSpaceAbove >= 150) {
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

  const handleMainItemHover = (itemTitle: string, event: React.MouseEvent) => {
    const rect = event.currentTarget.getBoundingClientRect();

    // Estimate menu height based on number of children
    const menuItem = filterNavItems(navigationItems).find(item => item.title === itemTitle);
    const childCount = menuItem?.children?.length || 0;
    const estimatedHeight = Math.min(600, (childCount * 40) + 16); // 40px per item + padding

    const position = calculateMenuPosition(rect, itemTitle, estimatedHeight);
    setMenuPositions({ [itemTitle]: position });
    setHoveredItem(itemTitle);
    setHoveredChild(null);
    setHoveredGrandChild(null);
    setActiveMenuPath([itemTitle]);
    setMouseInMenu(true);
  };

  const handleChildItemHover = (parentTitle: string, childTitle: string, event: React.MouseEvent) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const menuKey = `${parentTitle}-${childTitle}`;

    // Estimate menu height based on number of grandchildren
    const parentItem = filterNavItems(navigationItems).find(item => item.title === parentTitle);
    const childItem = parentItem?.children?.find(child => child.title === childTitle);
    const grandChildCount = childItem?.children?.length || 0;
    const estimatedHeight = Math.min(600, (grandChildCount * 40) + 16); // 40px per item + padding

    const position = calculateMenuPosition(rect, menuKey, estimatedHeight);
    setMenuPositions(prev => ({
      ...prev,
      [menuKey]: position
    }));
    setHoveredChild(childTitle);
    setHoveredGrandChild(null);
    setActiveMenuPath([parentTitle, childTitle]);
    setMouseInMenu(true);
  };

  const handleGrandChildItemHover = (parentTitle: string, childTitle: string, grandChildTitle: string, event: React.MouseEvent) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const menuKey = `${parentTitle}-${childTitle}-${grandChildTitle}`;

    // Estimate menu height based on number of great-grandchildren (if any)
    const parentItem = filterNavItems(navigationItems).find(item => item.title === parentTitle);
    const childItem = parentItem?.children?.find(child => child.title === childTitle);
    const grandChildItem = childItem?.children?.find(grandChild => grandChild.title === grandChildTitle);
    const greatGrandChildCount = grandChildItem?.children?.length || 0;
    const estimatedHeight = Math.min(600, (greatGrandChildCount * 40) + 16); // 40px per item + padding

    const position = calculateMenuPosition(rect, menuKey, estimatedHeight);
    setMenuPositions(prev => ({
      ...prev,
      [menuKey]: position
    }));
    setHoveredGrandChild(grandChildTitle);
    setActiveMenuPath([parentTitle, childTitle, grandChildTitle]);
    setMouseInMenu(true);
  };

  const handleMenuMouseEnter = () => {
    setMouseInMenu(true);
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  };

  const clearMenus = () => {
    setHoveredItem(null);
    setHoveredChild(null);
    setHoveredGrandChild(null);
    setActiveMenuPath([]);
    setMenuPositions({});
    setMouseInMenu(false);
  };

  const handleMenuMouseLeave = (event: React.MouseEvent) => {
    setMouseInMenu(false);

    // Check if mouse is moving toward a child menu
    const currentMouse = { x: event.clientX, y: event.clientY };

    // Check if moving toward first level child menu
    if (hoveredItem && menuPositions[hoveredItem]) {
      const isMovingToChild = isMouseMovingTowardsMenu(
        menuPositions[hoveredItem],
        currentMouse,
        lastMousePosition
      );

      if (isMovingToChild) {
        setIsMovingToChild(true);
        // Give more time when moving toward child
        if (timeoutRef.current) clearTimeout(timeoutRef.current);
        timeoutRef.current = setTimeout(() => {
          if (!mouseInMenu) clearMenus();
          setIsMovingToChild(false);
        }, 1200);
        return;
      }
    }

    // Check if moving toward second level child menu
    if (hoveredChild && hoveredItem && menuPositions[`${hoveredItem}-${hoveredChild}`]) {
      const isMovingToChild = isMouseMovingTowardsMenu(
        menuPositions[`${hoveredItem}-${hoveredChild}`],
        currentMouse,
        lastMousePosition
      );

      if (isMovingToChild) {
        setIsMovingToChild(true);
        if (timeoutRef.current) clearTimeout(timeoutRef.current);
        timeoutRef.current = setTimeout(() => {
          if (!mouseInMenu) clearMenus();
          setIsMovingToChild(false);
        }, 800);
        return;
      }
    }

    // Default behavior - longer timeout for better UX
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
    }
    timeoutRef.current = setTimeout(() => {
      clearMenus();
    }, 900);
  };

  const handleSidebarMouseLeave = () => {
    setMouseInMenu(false);
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
    }
    timeoutRef.current = setTimeout(() => {
      clearMenus();
    }, 1000);
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

    return items.filter(item => {
      if (item.roles && !hasAnyRole(item.roles)) {
        return false;
      }
      return true;
    });
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
                <h2 className="text-lg font-bold text-slate-900 dark:text-white">ERP System</h2>
              </div>
            </div>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setCollapsed(!collapsed)}
            className="h-8 w-8 p-0"
          >
            {collapsed ? <Menu className="h-4 w-4" /> : <X className="h-4 w-4" />}
          </Button>
        </div>

        {/* Navigation */}
        <nav className="flex-1 space-y-0 p-2">
          {filterNavItems(navigationItems).map((item) => {
            const Icon = item.icon;
            const hasChildren = item.children && item.children.length > 0;
            const itemIsActive = isActive(item.href);

            return (
              <div key={item.title} className="relative">
                {hasChildren ? (
                  <button
                    onMouseEnter={(e) => handleMainItemHover(item.title, e)}
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
          className="fixed z-40"
          style={{
            left: Math.min(collapsed ? 64 : 288, menuPositions[hoveredItem].x),
            top: menuPositions[hoveredItem].y - 20,
            width: Math.abs(menuPositions[hoveredItem].x - (collapsed ? 64 : 288)) + 16,
            height: 120,
            pointerEvents: 'auto',
            backgroundColor: 'transparent'
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        />
      )}

      {/* First Level Floating Submenu */}
      {hoveredItem && menuPositions[hoveredItem] && (
        <div
          className="fixed bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-slate-200/50 dark:border-slate-800/50 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
          style={{
            left: menuPositions[hoveredItem].x,
            top: menuPositions[hoveredItem].y,
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgb(148 163 184) transparent'
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        >
          {filterNavItems(navigationItems)
            .find(item => item.title === hoveredItem)
            ?.children?.map((child) => {
              const ChildIcon = child.icon;
              const childIsActive = isActive(child.href);
              const childHasChildren = child.children && child.children.length > 0;

              return (
                <div key={child.title} className="relative">
                  {childHasChildren ? (
                    <button
                      onMouseEnter={(e) => handleChildItemHover(hoveredItem, child.title, e)}
                      className="w-full flex items-center gap-3 px-4 py-2 text-sm text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-700 dark:text-slate-300 transition-colors"
                    >
                      <ChildIcon className="h-4 w-4 flex-shrink-0" />
                      <span className="flex-1">{child.title}</span>
                      <ChevronRight className="h-4 w-4" />
                    </button>
                  ) : (
                    <Link
                      href={child.href}
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
      {hoveredChild && hoveredItem && menuPositions[hoveredItem] && menuPositions[`${hoveredItem}-${hoveredChild}`] && (
        <div
          className="fixed z-40"
          style={{
            left: Math.min(menuPositions[hoveredItem].x + 224, menuPositions[`${hoveredItem}-${hoveredChild}`].x),
            top: menuPositions[`${hoveredItem}-${hoveredChild}`].y - 20,
            width: Math.abs(menuPositions[`${hoveredItem}-${hoveredChild}`].x - (menuPositions[hoveredItem].x + 224)) + 16,
            height: 120,
            pointerEvents: 'auto',
            backgroundColor: 'transparent'
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        />
      )}

      {/* Second Level Floating Submenu */}
      {hoveredChild && hoveredItem && menuPositions[`${hoveredItem}-${hoveredChild}`] && (
        <div
          className="fixed bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-slate-200/50 dark:border-slate-800/50 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
          style={{
            left: menuPositions[`${hoveredItem}-${hoveredChild}`].x,
            top: menuPositions[`${hoveredItem}-${hoveredChild}`].y,
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgb(148 163 184) transparent'
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        >
          {filterNavItems(navigationItems)
            .find(item => item.title === hoveredItem)
            ?.children?.find(child => child.title === hoveredChild)
            ?.children?.map((grandchild) => {
              const GrandChildIcon = grandchild.icon;
              const grandchildIsActive = isActive(grandchild.href);
              const grandchildHasChildren = grandchild.children && grandchild.children.length > 0;

              return (
                <div key={grandchild.title} className="relative">
                  {grandchildHasChildren ? (
                    <button
                      onMouseEnter={(e) => handleGrandChildItemHover(hoveredItem, hoveredChild, grandchild.title, e)}
                      className="w-full flex items-center gap-3 px-4 py-2 text-sm text-left hover:bg-slate-100 dark:hover:bg-slate-800/50 text-slate-600 dark:text-slate-400 transition-colors"
                    >
                      <GrandChildIcon className="h-3 w-3 flex-shrink-0" />
                      <span className="flex-1">{grandchild.title}</span>
                      <ChevronRight className="h-3 w-3" />
                    </button>
                  ) : (
                    <Link
                      href={grandchild.href}
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
      {hoveredGrandChild && hoveredChild && hoveredItem && menuPositions[`${hoveredItem}-${hoveredChild}`] && menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`] && (
        <div
          className="fixed z-40"
          style={{
            left: Math.min(menuPositions[`${hoveredItem}-${hoveredChild}`].x + 224, menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`].x),
            top: menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`].y - 20,
            width: Math.abs(menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`].x - (menuPositions[`${hoveredItem}-${hoveredChild}`].x + 224)) + 16,
            height: 120,
            pointerEvents: 'auto',
            backgroundColor: 'transparent'
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        />
      )}

      {/* Third Level Floating Submenu */}
      {hoveredGrandChild && hoveredChild && hoveredItem && menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`] && (
        <div
          className="fixed bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border border-slate-200/50 dark:border-slate-800/50 rounded-lg shadow-lg z-50 min-w-56 py-2 max-h-[calc(100vh-40px)] overflow-y-auto"
          style={{
            left: menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`].x,
            top: menuPositions[`${hoveredItem}-${hoveredChild}-${hoveredGrandChild}`].y,
            scrollbarWidth: 'thin',
            scrollbarColor: 'rgb(148 163 184) transparent'
          }}
          onMouseEnter={handleMenuMouseEnter}
          onMouseLeave={handleMenuMouseLeave}
        >
          {filterNavItems(navigationItems)
            .find(item => item.title === hoveredItem)
            ?.children?.find(child => child.title === hoveredChild)
            ?.children?.find(grandchild => grandchild.title === hoveredGrandChild)
            ?.children?.map((greatGrandchild) => {
              const GreatGrandChildIcon = greatGrandchild.icon;
              const greatGrandchildIsActive = isActive(greatGrandchild.href);

              return (
                <Link
                  key={greatGrandchild.title}
                  href={greatGrandchild.href}
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
