"use client";

import React from 'react';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { ThemeToggle, AnimatedThemeToggle, ThemeIndicator } from '@/components/ui/ThemeToggle';
import { ResponsiveLayout, ResponsiveContainer, ResponsiveStack } from '@/components/layout/ResponsiveLayout';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { 
  HomeIcon, 
  UsersIcon, 
  SettingsIcon, 
  BarChart3Icon,
  FileTextIcon,
  BellIcon,
} from 'lucide-react';
import useUserTheme from '@/hooks/useUserTheme';
import { useDeviceType } from '@/hooks/useResponsive';
import { UserManagementTable } from './UserManagementTable';

// Example sidebar navigation
function SidebarNavigation() {
  const { isCompact } = useUserTheme();
  
  const navItems = [
    { icon: HomeIcon, label: 'Dashboard', active: true },
    { icon: UsersIcon, label: 'Users', active: false },
    { icon: BarChart3Icon, label: 'Analytics', active: false },
    { icon: FileTextIcon, label: 'Reports', active: false },
    { icon: SettingsIcon, label: 'Settings', active: false },
  ];

  return (
    <nav className="space-y-2">
      {navItems.map((item) => {
        const IconComponent = item.icon;
        return (
          <Button
            key={item.label}
            variant={item.active ? 'default' : 'ghost'}
            className={`w-full justify-start ${isCompact ? 'h-8 px-2' : 'h-10 px-3'}`}
            size={isCompact ? 'sm' : 'default'}
          >
            <IconComponent className={`${isCompact ? 'h-3 w-3' : 'h-4 w-4'} mr-2 flex-shrink-0`} />
            <span className="truncate">{item.label}</span>
          </Button>
        );
      })}
    </nav>
  );
}

// Example header component
function HeaderContent() {
  const { isDark, isCompact, preferences, toggleCompactMode } = useUserTheme();

  return (
    <div className="flex items-center justify-between w-full">
      <div className="flex items-center space-x-4">
        <h1 className={`font-semibold ${isCompact ? 'text-lg' : 'text-xl'} text-foreground`}>
          ERP Dashboard
        </h1>
        <Badge variant="outline" className="hidden md:inline-flex">
          {isDark ? 'Dark Mode' : 'Light Mode'}
        </Badge>
      </div>

      <div className="flex items-center space-x-2">
        {/* Notifications */}
        <Button variant="ghost" size={isCompact ? 'sm' : 'default'} className="relative">
          <BellIcon className={`${isCompact ? 'h-3 w-3' : 'h-4 w-4'}`} />
          <Badge 
            variant="destructive" 
            className="absolute -top-1 -right-1 h-4 w-4 p-0 flex items-center justify-center text-xs"
          >
            3
          </Badge>
        </Button>

        {/* Compact mode toggle */}
        <Button 
          variant="ghost" 
          size={isCompact ? 'sm' : 'default'}
          onClick={toggleCompactMode}
          title={`${preferences?.compactMode ? 'Disable' : 'Enable'} compact mode`}
        >
          <span className={`${isCompact ? 'text-xs' : 'text-sm'}`}>
            {preferences?.compactMode ? 'Expanded' : 'Compact'}
          </span>
        </Button>

        {/* Theme toggle */}
        <ThemeToggle variant="dropdown" size={isCompact ? 'sm' : 'default'} />
        
        {/* Alternative animated theme toggle */}
        {/* <AnimatedThemeToggle size={isCompact ? 'sm' : 'default'} showLabel /> */}
      </div>
    </div>
  );
}

// Example footer component
function FooterContent() {
  const { fontSize, colorScheme } = useUserTheme();

  return (
    <div className="flex items-center justify-between w-full text-sm text-muted-foreground">
      <div className="flex items-center space-x-4">
        <span>&copy; 2024 ERP System</span>
        <ThemeIndicator />
      </div>
      
      <div className="flex items-center space-x-2">
        <Badge variant="outline" className="text-xs">
          Font: {fontSize?.toUpperCase()}
        </Badge>
        <Badge variant="outline" className="text-xs">
          Theme: {colorScheme}
        </Badge>
      </div>
    </div>
  );
}

// Main dashboard content
function DashboardContent() {
  const { isCompact, isDark, preferences } = useUserTheme();

  return (
    <ResponsiveContainer maxWidth="full" padding="none">
      <div className="space-y-6">
        {/* Welcome section */}
        <Card>
          <CardHeader className={isCompact ? 'pb-3' : 'pb-4'}>
            <CardTitle className={isCompact ? 'text-lg' : 'text-xl'}>
              Welcome to Your ERP Dashboard
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveStack direction="responsive" gap="md" className="mb-4">
              <Card className="flex-1">
                <CardContent className={`${isCompact ? 'p-3' : 'p-4'} text-center`}>
                  <div className={`text-${isDark ? '2xl' : '3xl'} font-bold text-primary`}>
                    1,234
                  </div>
                  <div className="text-sm text-muted-foreground">Total Users</div>
                </CardContent>
              </Card>
              
              <Card className="flex-1">
                <CardContent className={`${isCompact ? 'p-3' : 'p-4'} text-center`}>
                  <div className={`text-${isDark ? '2xl' : '3xl'} font-bold text-green-600`}>
                    89%
                  </div>
                  <div className="text-sm text-muted-foreground">System Health</div>
                </CardContent>
              </Card>
              
              <Card className="flex-1">
                <CardContent className={`${isCompact ? 'p-3' : 'p-4'} text-center`}>
                  <div className={`text-${isDark ? '2xl' : '3xl'} font-bold text-blue-600`}>
                    456
                  </div>
                  <div className="text-sm text-muted-foreground">Active Sessions</div>
                </CardContent>
              </Card>
            </ResponsiveStack>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Card>
                <CardHeader className={isCompact ? 'pb-2' : 'pb-3'}>
                  <CardTitle className={isCompact ? 'text-base' : 'text-lg'}>
                    Theme Settings
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-2">
                  <div className="text-sm">
                    <strong>Current Theme:</strong> {isDark ? 'Dark' : 'Light'}
                  </div>
                  <div className="text-sm">
                    <strong>Compact Mode:</strong> {preferences?.compactMode ? 'Enabled' : 'Disabled'}
                  </div>
                  <div className="text-sm">
                    <strong>Font Size:</strong> {preferences?.fontSize?.toUpperCase()}
                  </div>
                  <div className="text-sm">
                    <strong>Color Scheme:</strong> {preferences?.colorScheme}
                  </div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader className={isCompact ? 'pb-2' : 'pb-3'}>
                  <CardTitle className={isCompact ? 'text-base' : 'text-lg'}>
                    Device Info
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-2 text-sm">
                  <ResponsiveDeviceInfo />
                </CardContent>
              </Card>
            </div>
          </CardContent>
        </Card>

        {/* Data Table Example */}
        <Card>
          <CardContent className="p-0">
            <UserManagementTable />
          </CardContent>
        </Card>
      </div>
    </ResponsiveContainer>
  );
}

// Component to show responsive device information
function ResponsiveDeviceInfo() {
  const { 
    isMobile, 
    isTablet, 
    isTouchDevice,
    needsLargerTapTargets,
    shouldUseMobileLayout,
  } = useDeviceType();

  return (
    <>
      <div><strong>Device Type:</strong> {isMobile ? 'Mobile' : isTablet ? 'Tablet' : 'Desktop'}</div>
      <div><strong>Touch Device:</strong> {isTouchDevice ? 'Yes' : 'No'}</div>
      <div><strong>Mobile Layout:</strong> {shouldUseMobileLayout ? 'Yes' : 'No'}</div>
      <div><strong>Large Tap Targets:</strong> {needsLargerTapTargets ? 'Yes' : 'No'}</div>
    </>
  );
}

// Main themed layout component
export function ThemedLayout() {
  return (
    <ThemeProvider defaultTheme="system" storageKey="erp-theme" enableSystem>
      <ResponsiveLayout
        sidebar={<SidebarNavigation />}
        header={<HeaderContent />}
        footer={<FooterContent />}
        collapsible={true}
        defaultCollapsed={false}
      >
        <DashboardContent />
      </ResponsiveLayout>
    </ThemeProvider>
  );
}

export default ThemedLayout;
