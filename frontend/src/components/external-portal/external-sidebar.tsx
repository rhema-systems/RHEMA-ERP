'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import {
  Building2,
  FileText,
  Home,
  User,
  Bell,
  Settings,
  LogOut,
  ChevronDown,
  Menu,
  Briefcase,
  ClipboardList,
  Users,
  ListTodo,
  LifeBuoy,
  PackageCheck,
  KeyRound,
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { authService } from '@/services/auth';
import { isCandidateUser, isConsultantClientUser } from '@/lib/auth-routing';
import { useRouter } from 'next/navigation';
import type { Tenant } from '@/types';

interface MenuItem {
  title: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
  comingSoon?: boolean;
}

interface MenuGroup {
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  items: MenuItem[];
}

const portalMenuItems: MenuItem[] = [
  {
    title: 'Dashboard',
    href: '/external-portal',
    icon: Home,
  },
  {
    title: 'Business Partner',
    href: '/external-portal/business-partner',
    icon: Building2,
  },
  {
    title: 'Available Tenders',
    href: '/external-portal/tenders',
    icon: Briefcase,
  },
  {
    title: 'My Tender Bids',
    href: '/external-portal/my-bids',
    icon: ClipboardList,
  },
  {
    title: 'My RFQs',
    href: '/external-portal/rfqs',
    icon: FileText,
  },
  {
    title: 'PO Amendments',
    href: '/external-portal/purchase-order-amendments',
    icon: FileText,
  },
  {
    title: 'Receipt Inspections',
    href: '/external-portal/receipt-inspections',
    icon: PackageCheck,
  },
  {
    title: 'My Tasks',
    href: '/external-portal/task-list',
    icon: ListTodo,
  },
  {
    title: 'My Projects',
    href: '/external-portal/projects',
    icon: Briefcase,
  },
  {
    title: 'User Management',
    href: '/external-portal/user-management',
    icon: Users,
  },
  // Estate/customer portal integration: expose only public Estate listings, service requests, and dispatched documents.
  {
    title: 'Property Listings',
    href: '/external-portal/property-listings',
    icon: Home,
  },
  {
    title: 'My Properties',
    href: '/external-portal/my-properties',
    icon: KeyRound,
  },
  {
    title: 'Estate Services',
    href: '/external-portal/estate-services',
    icon: ClipboardList,
  },
  {
    title: 'Estate Documents',
    href: '/external-portal/estate-documents',
    icon: FileText,
  },
  {
    title: 'My Profile',
    href: '/external-portal/profile',
    icon: User,
  },
  {
    title: 'Notifications',
    href: '/external-portal/notifications',
    icon: Bell,
  },
  {
    title: 'Support Tickets',
    href: '/support/tickets',
    icon: LifeBuoy,
  },
  {
    title: 'Service Requests',
    href: '/support/requests',
    icon: ClipboardList,
  },
];

const externalMenuGroups: MenuGroup[] = [
  {
    title: 'Procurement',
    icon: Briefcase,
    items: portalMenuItems.filter((item) => [
      '/external-portal/business-partner',
      '/external-portal/tenders',
      '/external-portal/my-bids',
      '/external-portal/rfqs',
      '/external-portal/purchase-order-amendments',
      '/external-portal/receipt-inspections',
    ].includes(item.href)),
  },
  {
    title: 'Estate',
    icon: Home,
    items: portalMenuItems.filter((item) => [
      '/external-portal/property-listings',
      '/external-portal/my-properties',
      '/external-portal/estate-services',
      '/external-portal/estate-documents',
    ].includes(item.href)),
  },
  {
    title: 'Workspace',
    icon: ClipboardList,
    items: portalMenuItems.filter((item) => [
      '/external-portal/task-list',
      '/external-portal/projects',
      '/external-portal/user-management',
    ].includes(item.href)),
  },
  {
    title: 'Support',
    icon: LifeBuoy,
    items: portalMenuItems.filter((item) => [
      '/support/tickets',
      '/support/requests',
    ].includes(item.href)),
  },
  {
    title: 'Account',
    icon: User,
    items: portalMenuItems.filter((item) => [
      '/external-portal/profile',
      '/external-portal/notifications',
    ].includes(item.href)),
  },
];

// The careers candidate's menu (Candidate role). Candidates share this shell but
// never see the partner sections — the server's CandidateAccessMiddleware would 403 every one
// of them, and a menu of dead links is worse than a short menu.
const candidateMenuItems: MenuItem[] = [
  {
    title: 'My Applications',
    href: '/external-portal/careers',
    icon: Home,
  },
  {
    title: 'Browse Jobs',
    href: '/careers',
    icon: Briefcase,
  },
  {
    title: 'My Candidate Profile',
    href: '/external-portal/careers/profile',
    icon: User,
  },
  {
    title: 'My Documents',
    href: '/external-portal/careers/documents',
    icon: FileText,
  },
  {
    title: 'Notifications',
    href: '/external-portal/notifications',
    icon: Bell,
  },
];

// The consultant-client contact's menu (ConsultantClient role). Contacts share
// this shell but never see the partner sections — the server's ConsultantClientAccessMiddleware
// would 403 every one of them, and a menu of dead links is worse than a short menu.
const consultantClientMenuItems: MenuItem[] = [
  {
    title: 'Timesheets',
    href: '/external-portal/client-timesheets',
    icon: ClipboardList,
  },
  {
    title: 'Notifications',
    href: '/external-portal/notifications',
    icon: Bell,
  },
];

export function ExternalSidebar() {
  const pathname = usePathname() ?? '';
  const router = useRouter();
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const user = authService.getStoredUser();
  const [tenant, setTenant] = useState<Tenant | null>(() => authService.getCurrentTenant());
  const isCandidate = isCandidateUser(user);
  const isConsultantClient = isConsultantClientUser(user);
  const items = isCandidate
    ? candidateMenuItems
    : isConsultantClient
      ? consultantClientMenuItems
      : portalMenuItems;

  const isActiveItem = (item: MenuItem) =>
    item.href === '/support/tickets'
      ? pathname === '/support/tickets' ||
        pathname.startsWith('/support/tickets/') ||
        pathname === '/external-portal/support/tickets' ||
        pathname.startsWith('/external-portal/support/tickets/')
      : item.href === '/support/requests'
        ? pathname === '/support/requests' ||
          pathname.startsWith('/support/requests/') ||
          pathname === '/external-portal/support/requests' ||
          pathname.startsWith('/external-portal/support/requests/')
        : pathname === item.href || pathname.startsWith(item.href + '/');

  const renderItem = (item: MenuItem, nested = false) => {
    const Icon = item.icon;
    const isActive = isActiveItem(item);

    return (
      <Link
        key={item.href}
        href={item.comingSoon ? '#' : item.href}
        className={cn(
          'flex items-center gap-3 rounded-md px-3 py-1.5 text-sm transition-colors relative',
          nested && 'ml-3 pl-4',
          isActive
            ? 'bg-blue-600 text-white'
            : 'text-slate-300 hover:bg-slate-800 hover:text-white',
          item.comingSoon && 'opacity-50 cursor-not-allowed'
        )}
        onClick={(event) => item.comingSoon && event.preventDefault()}
        title={isCollapsed ? item.title : undefined}
      >
        <Icon className={cn('h-4 w-4 shrink-0', isCollapsed ? 'mx-auto' : '')} />
        {!isCollapsed && (
          <>
            <span className="flex-1 leading-5">{item.title}</span>
            {item.badge && (
              <span className="bg-red-500 text-white text-xs px-2 py-0.5 rounded-full">
                {item.badge}
              </span>
            )}
            {item.comingSoon && (
              <span className="bg-yellow-500 text-xs px-2 py-0.5 rounded-full text-black">
                Soon
              </span>
            )}
          </>
        )}
      </Link>
    );
  };

  useEffect(() => {
    const handler = (e: any) => setTenant(e?.detail || authService.getCurrentTenant());
    window.addEventListener('tenant-changed', handler as any);
    return () => window.removeEventListener('tenant-changed', handler as any);
  }, []);

  const handleLogout = () => {
    authService.logout();
    router.push('/login');
  };

  return (
    <div
      className={cn(
        'flex flex-col h-screen bg-slate-900 text-white transition-all duration-300',
        isCollapsed ? 'w-16' : 'w-64'
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-slate-700">
        {!isCollapsed && (
          <div className="flex items-center space-x-2">
            {tenant?.logoUrl ? (
              <img src={tenant.logoUrl} alt={tenant.name || 'Tenant'} className="h-7 w-7 rounded object-contain bg-white p-1" />
            ) : (
              <Building2 className="h-6 w-6 text-blue-400" />
            )}
            <span className="font-semibold text-base">{tenant?.name || 'External Portal'}</span>
          </div>
        )}
        <Button
          variant="ghost"
          size="icon"
          onClick={() => setIsCollapsed(!isCollapsed)}
          className="text-white hover:bg-slate-800"
        >
          <Menu className="h-5 w-5" />
        </Button>
      </div>



      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto p-3 space-y-1">
        {isCandidate || isConsultantClient || isCollapsed ? (
          items.map((item) => renderItem(item))
        ) : (
          <>
            {renderItem(portalMenuItems[0])}
            {externalMenuGroups.map((group) => {
              const GroupIcon = group.icon;
              const containsActiveItem = group.items.some(isActiveItem);
              const isOpen = openGroups[group.title] ?? containsActiveItem;
              return (
                <div key={group.title} className="pt-1">
                  <button
                    type="button"
                    onClick={() => setOpenGroups((current) => ({ ...current, [group.title]: !isOpen }))}
                    className={cn(
                      'flex w-full items-center gap-3 rounded-md px-3 py-1.5 text-left text-sm transition-colors',
                      containsActiveItem ? 'text-white' : 'text-slate-300 hover:bg-slate-800 hover:text-white'
                    )}
                    aria-expanded={isOpen}
                  >
                    <GroupIcon className="h-4 w-4 shrink-0" />
                    <span className="flex-1 font-medium">{group.title}</span>
                    <ChevronDown className={cn('h-4 w-4 transition-transform', !isOpen && '-rotate-90')} />
                  </button>
                  {isOpen && <div className="mt-0.5 space-y-0.5">{group.items.map((item) => renderItem(item, true))}</div>}
                </div>
              );
            })}
          </>
        )}
      </nav>

      {/* Footer */}
      {!isCollapsed && (
        <div className="p-4 border-t border-slate-700">
          <p className="text-xs text-slate-400 text-center">
            External Portal v1.0
          </p>
        </div>
      )}
    </div>
  );
}

