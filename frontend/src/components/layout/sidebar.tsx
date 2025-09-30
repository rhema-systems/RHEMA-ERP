'use client';

import { useState, useEffect } from 'react';
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
    title: 'Sales',
    href: '/sales',
    icon: ShoppingCart,
    children: [
      { title: 'Customers', href: '/sales/customers', icon: Users },
      { title: 'Orders', href: '/sales/orders', icon: ShoppingCart },
      { title: 'Quotes', href: '/sales/quotes', icon: CreditCard },
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
    ],
  },
  {
    title: 'Finance',
    href: '/finance',
    icon: CreditCard,
    children: [
      { title: 'Accounts', href: '/finance/accounts', icon: CreditCard },
      { title: 'Invoices', href: '/finance/invoices', icon: CreditCard },
      { title: 'Financial Reports', href: '/finance/reports', icon: CreditCard },
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
    ],
  },
  {
    title: 'Procurement',
    href: '/procurement',
    icon: Briefcase,
    children: [
      { title: 'Suppliers', href: '/procurement/suppliers', icon: Briefcase },
      { title: 'Purchase Orders', href: '/procurement/purchase-orders', icon: ShoppingCart },
    ],
  },
  {
    title: 'Marketing',
    href: '/marketing',
    icon: Megaphone,
    children: [
      { title: 'Campaigns', href: '/marketing/campaigns', icon: Megaphone },
      { title: 'Leads', href: '/marketing/leads', icon: Users },
    ],
  },
  {
    title: 'Reports & Analytics',
    href: '/reports',
    icon: BarChart3,
    children: [
      { title: 'Report Builder', href: '/reports#builder', icon: FileText },
      { title: 'Analytics Dashboard', href: '/reports#analytics', icon: BarChart3 },
      { title: 'Report Templates', href: '/reports#templates', icon: FileText },
      { title: 'Data Export', href: '/reports#export', icon: FileText },
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
        title: 'Security',
        href: '/administration/security/dashboard',
        icon: Shield,
      },
      {
        title: 'Identity Management',
        href: '/administration/identity-management',
        icon: Shield,
        children: [
          { title: 'User Management', href: '/administration/identity-management/users', icon: Users },
          { title: 'Online Users', href: '/administration/identity-management/online-users', icon: UserCheck },
          { title: 'Role Management', href: '/administration/identity-management/roles', icon: Shield },
          { title: 'Security Logs', href: '/administration/identity-management/security-logs', icon: FileText },
        ],
      },
      { title: 'Tenant Management', href: '/administration/tenant-management', icon: Building },
      { title: 'User-Tenant Mapping', href: '/administration/user-tenant-mapping', icon: Users },
      {
        title: 'Settings',
        href: '/administration/settings',
        icon: Settings,
        children: [
          { title: 'Email Settings', href: '/administration/settings/email', icon: Mail },
        ],
      },
      { title: 'Audit Logs', href: '/administration/audit-logs', icon: FileText },
    ],
  },
];

interface SidebarProps {
  className?: string;
}

export function Sidebar({ className }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(false);
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set(['Administration', 'Administration-Identity Management', 'Administration-Security']));
  const [mounted, setMounted] = useState(false);
  const pathname = usePathname();
  const { hasAnyRole } = useAuth();

  useEffect(() => {
    setMounted(true);
  }, []);

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
    <div className={cn(
      'flex h-full flex-col bg-white/95 dark:bg-slate-900/95 backdrop-blur-xl border-r border-slate-200/50 dark:border-slate-800/50 transition-all duration-300',
      collapsed ? 'w-16' : 'w-72',
      className
    )}>
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
      <nav className="flex-1 space-y-1 p-3 overflow-y-auto">
        {filterNavItems(navigationItems).map((item) => {
          const Icon = item.icon;
          const hasChildren = item.children && item.children.length > 0;
          const isExpanded = expandedSections.has(item.title);
          const itemIsActive = isActive(item.href);

          return (
            <div key={item.title}>
              <div className="relative">
                {hasChildren ? (
                  <button
                    onClick={() => toggleSection(item.title)}
                    className={cn(
                      'flex w-full items-center justify-between rounded-xl px-3 py-3 text-sm font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <div className="flex items-center space-x-3">
                      <Icon className="h-5 w-5 flex-shrink-0" />
                      {!collapsed && <span>{item.title}</span>}
                    </div>
                    {!collapsed && hasChildren && (
                      <div className={cn(
                        'transition-transform duration-200',
                        isExpanded ? 'rotate-90' : ''
                      )}>
                        <ChevronRight className="h-4 w-4" />
                      </div>
                    )}
                  </button>
                ) : (
                  <Link
                    href={item.href}
                    className={cn(
                      'flex items-center space-x-3 rounded-xl px-3 py-3 text-sm font-medium transition-all hover:bg-slate-100 dark:hover:bg-slate-800/50',
                      itemIsActive
                        ? 'bg-gradient-to-r from-blue-50 to-indigo-50 dark:from-blue-900/20 dark:to-indigo-900/20 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-800/50'
                        : 'text-slate-700 dark:text-slate-300'
                    )}
                  >
                    <Icon className="h-5 w-5 flex-shrink-0" />
                    {!collapsed && <span>{item.title}</span>}
                  </Link>
                )}
              </div>

              {/* Children */}
              {hasChildren && isExpanded && !collapsed && (
                <div className="ml-6 mt-1 space-y-1 border-l-2 border-slate-100 dark:border-slate-800 pl-4">
                  {(item.children || []).map((child) => {
                    const ChildIcon = child.icon;
                    const childIsActive = isActive(child.href);
                    const childHasChildren = child.children && child.children.length > 0;
                    const childIsExpanded = expandedSections.has(`${item.title}-${child.title}`);

                    return (
                      <div key={child.title}>
                        {childHasChildren ? (
                          <button
                            onClick={() => toggleSection(`${item.title}-${child.title}`)}
                            className={cn(
                              'flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm transition-all hover:bg-slate-50 dark:hover:bg-slate-800/30',
                              childIsActive
                                ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                                : 'text-slate-600 dark:text-slate-400'
                            )}
                          >
                            <div className="flex items-center space-x-3">
                              <ChildIcon className="h-4 w-4 flex-shrink-0" />
                              <span>{child.title}</span>
                            </div>
                            <div className={cn(
                              'transition-transform duration-200',
                              childIsExpanded ? 'rotate-90' : ''
                            )}>
                              <ChevronRight className="h-3 w-3" />
                            </div>
                          </button>
                        ) : (
                          <Link
                            href={child.href}
                            className={cn(
                              'flex items-center space-x-3 rounded-lg px-3 py-2 text-sm transition-all hover:bg-slate-50 dark:hover:bg-slate-800/30',
                              childIsActive
                                ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                                : 'text-slate-600 dark:text-slate-400'
                            )}
                          >
                            <ChildIcon className="h-4 w-4 flex-shrink-0" />
                            <span>{child.title}</span>
                          </Link>
                        )}
                        
                        {/* Nested Children (3rd level) */}
                        {childHasChildren && childIsExpanded && (
                          <div className="ml-6 mt-1 space-y-1 border-l-2 border-slate-100 dark:border-slate-800 pl-4">
                            {(child.children || []).map((grandChild) => {
                              const GrandChildIcon = grandChild.icon;
                              const grandChildIsActive = isActive(grandChild.href);

                              return (
                                <Link
                                  key={grandChild.title}
                                  href={grandChild.href}
                                  className={cn(
                                    'flex items-center space-x-3 rounded-lg px-3 py-2 text-sm transition-all hover:bg-slate-50 dark:hover:bg-slate-800/30',
                                    grandChildIsActive
                                      ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 font-medium'
                                      : 'text-slate-500 dark:text-slate-500'
                                  )}
                                >
                                  <GrandChildIcon className="h-3 w-3 flex-shrink-0" />
                                  <span>{grandChild.title}</span>
                                </Link>
                              );
                            })}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </nav>
    </div>
  );
}