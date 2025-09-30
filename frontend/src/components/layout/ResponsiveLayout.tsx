"use client";

import React, { useState } from 'react';
import { useDeviceType, useResponsive } from '@/hooks/useResponsive';
import { useThemeStyles } from '@/hooks/useUserTheme';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetTrigger } from '@/components/ui/sheet';
import {
  MenuIcon,
  XIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
} from 'lucide-react';

interface ResponsiveLayoutProps {
  children: React.ReactNode;
  sidebar?: React.ReactNode;
  header?: React.ReactNode;
  footer?: React.ReactNode;
  className?: string;
  sidebarWidth?: string;
  collapsible?: boolean;
  defaultCollapsed?: boolean;
}

export function ResponsiveLayout({
  children,
  sidebar,
  header,
  footer,
  className = '',
  sidebarWidth = 'w-64',
  collapsible = true,
  defaultCollapsed = false,
}: ResponsiveLayoutProps) {
  const { shouldUseMobileLayout, shouldUseDrawer, shouldShowSidebar } = useDeviceType();
  const { isCompact } = useThemeStyles();
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(defaultCollapsed);

  const toggleDrawer = () => setIsDrawerOpen(!isDrawerOpen);
  const toggleSidebar = () => setIsSidebarCollapsed(!isSidebarCollapsed);

  // Mobile layout with drawer
  if (shouldUseMobileLayout || shouldUseDrawer) {
    return (
      <div className={`flex flex-col h-screen ${className}`}>
        {/* Mobile Header */}
        {header && (
          <header className={`border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 ${isCompact ? 'h-12' : 'h-14'}`}>
            <div className="flex items-center justify-between h-full px-4">
              {sidebar && (
                <Sheet open={isDrawerOpen} onOpenChange={setIsDrawerOpen}>
                  <SheetTrigger asChild>
                    <Button
                      variant="ghost"
                      size={isCompact ? "sm" : "default"}
                      className="p-2"
                      onClick={toggleDrawer}
                    >
                      <MenuIcon className="h-5 w-5" />
                    </Button>
                  </SheetTrigger>
                  <SheetContent 
                    side="left" 
                    className="w-80 p-0"
                  >
                    <div className="flex flex-col h-full">
                      <div className="flex items-center justify-between p-4 border-b">
                        <h2 className="text-lg font-semibold">Menu</h2>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setIsDrawerOpen(false)}
                        >
                          <XIcon className="h-4 w-4" />
                        </Button>
                      </div>
                      <div className="flex-1 overflow-auto">
                        {sidebar}
                      </div>
                    </div>
                  </SheetContent>
                </Sheet>
              )}
              
              <div className="flex-1">
                {header}
              </div>
            </div>
          </header>
        )}

        {/* Mobile Content */}
        <main className="flex-1 overflow-auto">
          <div className={`${isCompact ? 'p-3' : 'p-4'}`}>
            {children}
          </div>
        </main>

        {/* Mobile Footer */}
        {footer && (
          <footer className={`border-t bg-background ${isCompact ? 'h-12' : 'h-14'}`}>
            <div className="flex items-center justify-center h-full px-4">
              {footer}
            </div>
          </footer>
        )}
      </div>
    );
  }

  // Desktop layout with sidebar
  return (
    <div className={`flex h-screen ${className}`}>
      {/* Desktop Sidebar */}
      {sidebar && shouldShowSidebar && (
        <aside className={`
          border-r bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 
          transition-all duration-300 ease-in-out
          ${isSidebarCollapsed ? 'w-16' : sidebarWidth}
          ${isCompact ? 'py-2' : 'py-4'}
        `}>
          <div className="flex flex-col h-full">
            {/* Sidebar Header */}
            <div className={`flex items-center justify-between ${isCompact ? 'px-2 pb-2' : 'px-4 pb-4'}`}>
              {!isSidebarCollapsed && (
                <h2 className="text-lg font-semibold truncate">Navigation</h2>
              )}
              
              {collapsible && (
                <Button
                  variant="ghost"
                  size={isCompact ? "sm" : "default"}
                  onClick={toggleSidebar}
                  className={`${isSidebarCollapsed ? 'w-full' : 'ml-auto'} p-2`}
                >
                  {isSidebarCollapsed ? (
                    <ChevronRightIcon className="h-4 w-4" />
                  ) : (
                    <ChevronLeftIcon className="h-4 w-4" />
                  )}
                </Button>
              )}
            </div>

            {/* Sidebar Content */}
            <div className="flex-1 overflow-auto">
              <div className={isSidebarCollapsed ? 'sidebar-collapsed' : 'sidebar-expanded'}>
                {sidebar}
              </div>
            </div>
          </div>
        </aside>
      )}

      {/* Main Content Area */}
      <div className="flex flex-col flex-1 overflow-hidden">
        {/* Desktop Header */}
        {header && (
          <header className={`border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 ${isCompact ? 'h-12' : 'h-14'}`}>
            <div className={`flex items-center h-full ${isCompact ? 'px-3' : 'px-6'}`}>
              {header}
            </div>
          </header>
        )}

        {/* Main Content */}
        <main className="flex-1 overflow-auto">
          <div className={`h-full ${isCompact ? 'p-3' : 'p-6'}`}>
            {children}
          </div>
        </main>

        {/* Desktop Footer */}
        {footer && (
          <footer className={`border-t bg-background ${isCompact ? 'h-10' : 'h-12'}`}>
            <div className={`flex items-center justify-center h-full ${isCompact ? 'px-3' : 'px-6'}`}>
              {footer}
            </div>
          </footer>
        )}
      </div>
    </div>
  );
}

// Responsive container component
interface ResponsiveContainerProps {
  children: React.ReactNode;
  className?: string;
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | '2xl' | 'full';
  padding?: 'none' | 'sm' | 'md' | 'lg';
}

export function ResponsiveContainer({
  children,
  className = '',
  maxWidth = '2xl',
  padding = 'md',
}: ResponsiveContainerProps) {
  const { isMobile } = useDeviceType();
  const { isCompact } = useThemeStyles();

  const maxWidthClasses = {
    sm: 'max-w-sm',
    md: 'max-w-md',
    lg: 'max-w-lg',
    xl: 'max-w-xl',
    '2xl': 'max-w-2xl',
    full: 'max-w-full',
  };

  const paddingClasses = {
    none: '',
    sm: isCompact ? 'p-2' : 'p-3',
    md: isCompact ? 'p-3' : (isMobile ? 'p-4' : 'p-6'),
    lg: isCompact ? 'p-4' : (isMobile ? 'p-6' : 'p-8'),
  };

  return (
    <div className={`
      w-full mx-auto 
      ${maxWidthClasses[maxWidth]} 
      ${paddingClasses[padding]} 
      ${className}
    `}>
      {children}
    </div>
  );
}

// Responsive grid component
interface ResponsiveGridProps {
  children: React.ReactNode;
  className?: string;
  cols?: {
    xs?: number;
    sm?: number;
    md?: number;
    lg?: number;
    xl?: number;
    '2xl'?: number;
  };
  gap?: 'sm' | 'md' | 'lg';
}

export function ResponsiveGrid({
  children,
  className = '',
  cols = { xs: 1, sm: 2, md: 3, lg: 4, xl: 5, '2xl': 6 },
  gap = 'md',
}: ResponsiveGridProps) {
  const { breakpoint } = useResponsive();
  const { isCompact } = useThemeStyles();

  const gapClasses = {
    sm: isCompact ? 'gap-2' : 'gap-3',
    md: isCompact ? 'gap-3' : 'gap-4',
    lg: isCompact ? 'gap-4' : 'gap-6',
  };

  // Get columns for current breakpoint
  const getCurrentCols = () => {
    if (cols[breakpoint]) return cols[breakpoint];
    
    // Fallback logic
    const orderedBreakpoints: (keyof typeof cols)[] = ['2xl', 'xl', 'lg', 'md', 'sm', 'xs'];
    const currentIndex = orderedBreakpoints.indexOf(breakpoint);
    
    for (let i = currentIndex; i < orderedBreakpoints.length; i++) {
      const bp = orderedBreakpoints[i];
      if (cols[bp]) return cols[bp];
    }
    
    return 1;
  };

  const currentCols = getCurrentCols();
  const gridColsClass = `grid-cols-${currentCols}`;

  return (
    <div className={`
      grid 
      ${gridColsClass}
      ${gapClasses[gap]}
      ${className}
    `}>
      {children}
    </div>
  );
}

// Responsive stack component (vertical layout on mobile, horizontal on desktop)
interface ResponsiveStackProps {
  children: React.ReactNode;
  className?: string;
  direction?: 'row' | 'column' | 'responsive';
  align?: 'start' | 'center' | 'end' | 'stretch';
  justify?: 'start' | 'center' | 'end' | 'between' | 'around';
  gap?: 'sm' | 'md' | 'lg';
  wrap?: boolean;
}

export function ResponsiveStack({
  children,
  className = '',
  direction = 'responsive',
  align = 'start',
  justify = 'start',
  gap = 'md',
  wrap = false,
}: ResponsiveStackProps) {
  const { isMobile } = useDeviceType();
  const { isCompact } = useThemeStyles();

  const gapClasses = {
    sm: isCompact ? 'gap-2' : 'gap-3',
    md: isCompact ? 'gap-3' : 'gap-4',
    lg: isCompact ? 'gap-4' : 'gap-6',
  };

  const alignClasses = {
    start: 'items-start',
    center: 'items-center',
    end: 'items-end',
    stretch: 'items-stretch',
  };

  const justifyClasses = {
    start: 'justify-start',
    center: 'justify-center',
    end: 'justify-end',
    between: 'justify-between',
    around: 'justify-around',
  };

  const getDirectionClass = () => {
    if (direction === 'responsive') {
      return isMobile ? 'flex-col' : 'flex-row';
    }
    return direction === 'column' ? 'flex-col' : 'flex-row';
  };

  return (
    <div className={`
      flex 
      ${getDirectionClass()}
      ${alignClasses[align]}
      ${justifyClasses[justify]}
      ${gapClasses[gap]}
      ${wrap ? 'flex-wrap' : ''}
      ${className}
    `}>
      {children}
    </div>
  );
}

export default ResponsiveLayout;