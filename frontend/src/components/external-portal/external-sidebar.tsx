'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import {
  Building2,
  FileText,
  MapPin,
  Home,
  User,
  Bell,
  Settings,
  LogOut,
  ChevronDown,
  Menu,
} from 'lucide-react';
import { useState } from 'react';
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
import { useRouter } from 'next/navigation';

interface MenuItem {
  title: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
  comingSoon?: boolean;
}

const menuItems: MenuItem[] = [
  {
    title: 'Dashboard',
    href: '/external-portal',
    icon: Home,
  },
  {
    title: 'Business Partner Registration',
    href: '/external-portal/business-partner',
    icon: Building2,
  },
  {
    title: 'Permit Applications',
    href: '/external-portal/permits',
    icon: FileText,
    comingSoon: true,
  },
  {
    title: 'Land Registration',
    href: '/external-portal/land-registration',
    icon: MapPin,
    comingSoon: true,
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
    badge: '3',
  },
];

export function ExternalSidebar() {
  const pathname = usePathname();
  const router = useRouter();
  const [isCollapsed, setIsCollapsed] = useState(false);
  const user = authService.getStoredUser();

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
            <Building2 className="h-6 w-6 text-blue-400" />
            <span className="font-semibold text-lg">External Portal</span>
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

      {/* User Info */}
      {!isCollapsed && user && (
        <div className="p-4 border-b border-slate-700">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                className="w-full justify-start text-white hover:bg-slate-800"
              >
                <div className="flex items-center space-x-3 w-full">
                  <div className="h-8 w-8 rounded-full bg-blue-500 flex items-center justify-center">
                    <span className="text-sm font-medium">
                      {user.firstName?.[0]}{user.lastName?.[0]}
                    </span>
                  </div>
                  <div className="flex-1 text-left">
                    <p className="text-sm font-medium">
                      {user.firstName} {user.lastName}
                    </p>
                    <p className="text-xs text-slate-400">{user.email}</p>
                  </div>
                  <ChevronDown className="h-4 w-4" />
                </div>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuLabel>My Account</DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => router.push('/external-portal/profile')}>
                <User className="mr-2 h-4 w-4" />
                Profile
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => router.push('/external-portal/settings')}>
                <Settings className="mr-2 h-4 w-4" />
                Settings
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={handleLogout} className="text-red-600">
                <LogOut className="mr-2 h-4 w-4" />
                Logout
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto p-4 space-y-2">
        {menuItems.map((item) => {
          const Icon = item.icon;
          const isActive = pathname === item.href || pathname?.startsWith(item.href + '/');

          return (
            <Link
              key={item.href}
              href={item.comingSoon ? '#' : item.href}
              className={cn(
                'flex items-center space-x-3 px-3 py-2 rounded-lg transition-colors relative',
                isActive
                  ? 'bg-blue-600 text-white'
                  : 'text-slate-300 hover:bg-slate-800 hover:text-white',
                item.comingSoon && 'opacity-50 cursor-not-allowed'
              )}
              onClick={(e) => item.comingSoon && e.preventDefault()}
            >
              <Icon className={cn('h-5 w-5', isCollapsed ? 'mx-auto' : '')} />
              {!isCollapsed && (
                <>
                  <span className="flex-1">{item.title}</span>
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
        })}
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

