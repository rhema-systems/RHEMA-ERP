"use client";

import React from 'react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { 
  SunIcon, 
  MoonIcon, 
  MonitorIcon,
  CheckIcon,
  PaletteIcon,
} from 'lucide-react';
import { useTheme } from '@/contexts/ThemeContext';
import { ClientOnly } from './client-only';

interface ThemeToggleProps {
  variant?: 'button' | 'dropdown' | 'compact';
  size?: 'sm' | 'default' | 'lg';
  showLabel?: boolean;
  align?: 'start' | 'center' | 'end';
}

export function ThemeToggle({ 
  variant = 'dropdown',
  size = 'default',
  showLabel = false,
  align = 'end'
}: ThemeToggleProps) {
  const { theme, setTheme, actualTheme, systemTheme, toggleTheme, isSystemTheme } = useTheme();

  const getThemeIcon = (themeName: string) => {
    switch (themeName) {
      case 'light':
        return <SunIcon className="h-4 w-4" />;
      case 'dark':
        return <MoonIcon className="h-4 w-4" />;
      case 'system':
        return <MonitorIcon className="h-4 w-4" />;
      default:
        return <PaletteIcon className="h-4 w-4" />;
    }
  };

  const getThemeLabel = (themeName: string) => {
    switch (themeName) {
      case 'light':
        return 'Light';
      case 'dark':
        return 'Dark';
      case 'system':
        return 'System';
      default:
        return 'Theme';
    }
  };

  const getCurrentIcon = () => {
    if (isSystemTheme) {
      return <MonitorIcon className="h-4 w-4" />;
    }
    return actualTheme === 'dark' ? <MoonIcon className="h-4 w-4" /> : <SunIcon className="h-4 w-4" />;
  };

  const getCurrentLabel = () => {
    if (isSystemTheme) {
      return `System (${systemTheme})`;
    }
    return getThemeLabel(actualTheme);
  };

  // Simple toggle button (light/dark only)
  if (variant === 'button') {
    return (
    <ClientOnly fallback={
      <Button
        variant="ghost"
        size={size}
        className="px-2"
        disabled={true}
        aria-label="Theme control loading"
      >
        <SunIcon className="h-4 w-4" />
      </Button>
    }>
        <Button
          variant="ghost"
          size={size}
          onClick={toggleTheme}
          className="px-2"
          aria-label={`Switch to ${actualTheme === 'dark' ? 'light' : 'dark'} mode`}
          title={`Switch to ${actualTheme === 'dark' ? 'light' : 'dark'} mode`}
        >
          {getCurrentIcon()}
          {showLabel && <span className="ml-2">{getCurrentLabel()}</span>}
        </Button>
      </ClientOnly>
    );
  }

  // Compact icon-only toggle
  if (variant === 'compact') {
    return (
      <ClientOnly fallback={
      <Button
        variant="ghost"
        size="sm"
        className="h-8 w-8 p-0"
        disabled={true}
        aria-label="Theme control loading"
      >
          <SunIcon className="h-4 w-4" />
        </Button>
      }>
        <Button
          variant="ghost"
          size="sm"
          onClick={toggleTheme}
          className="h-8 w-8 p-0"
          aria-label={`Switch to ${actualTheme === 'dark' ? 'light' : 'dark'} mode`}
          title={`Switch to ${actualTheme === 'dark' ? 'light' : 'dark'} mode`}
        >
          {getCurrentIcon()}
        </Button>
      </ClientOnly>
    );
  }

  // Full dropdown with all options
  return (
    <ClientOnly fallback={
      <Button
        variant="ghost"
        size={size}
        className="px-2"
        disabled={true}
        aria-label="Theme control loading"
      >
        <SunIcon className="h-4 w-4" />
      </Button>
    }>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size={size}
            className="px-2"
            aria-label="Change theme"
            title="Change theme"
          >
            {getCurrentIcon()}
            {showLabel && <span className="ml-2">{getCurrentLabel()}</span>}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align={align} className="min-w-[130px]">
          <DropdownMenuItem
            onClick={() => setTheme('light')}
            className="cursor-pointer"
          >
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center">
                <SunIcon className="mr-2 h-4 w-4" />
                <span>Light</span>
              </div>
              {theme === 'light' && <CheckIcon className="h-3 w-3" />}
            </div>
          </DropdownMenuItem>
          
          <DropdownMenuItem
            onClick={() => setTheme('dark')}
            className="cursor-pointer"
          >
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center">
                <MoonIcon className="mr-2 h-4 w-4" />
                <span>Dark</span>
              </div>
              {theme === 'dark' && <CheckIcon className="h-3 w-3" />}
            </div>
          </DropdownMenuItem>
          
          <DropdownMenuItem
            onClick={() => setTheme('system')}
            className="cursor-pointer"
          >
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center">
                <MonitorIcon className="mr-2 h-4 w-4" />
                <span>System</span>
              </div>
              {theme === 'system' && <CheckIcon className="h-3 w-3" />}
            </div>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </ClientOnly>
  );
}

// Alternative theme toggle with animated icons
export function AnimatedThemeToggle({ 
  size = 'default',
  showLabel = false 
}: { 
  size?: 'sm' | 'default' | 'lg';
  showLabel?: boolean;
}) {
  const { actualTheme, toggleTheme } = useTheme();

  return (
    <ClientOnly fallback={
      <Button
        variant="ghost"
        size={size}
        className="px-2 relative overflow-hidden"
        disabled={true}
        aria-label="Theme control loading"
      >
        <div className="relative">
          <SunIcon className="h-4 w-4" />
        </div>
      </Button>
    }>
      <Button
        variant="ghost"
        size={size}
        onClick={toggleTheme}
        className="px-2 relative overflow-hidden"
        aria-label={`Switch to ${actualTheme === 'dark' ? 'light' : 'dark'} mode`}
        title={`Switch to ${actualTheme === 'dark' ? 'light' : 'dark'} mode`}
      >
        <div className="relative">
          <SunIcon 
            className={`h-4 w-4 transition-all duration-300 ${
              actualTheme === 'dark' 
                ? 'rotate-90 scale-0 opacity-0' 
                : 'rotate-0 scale-100 opacity-100'
            }`} 
          />
          <MoonIcon 
            className={`h-4 w-4 absolute inset-0 transition-all duration-300 ${
              actualTheme === 'dark' 
                ? 'rotate-0 scale-100 opacity-100' 
                : '-rotate-90 scale-0 opacity-0'
            }`} 
          />
        </div>
        {showLabel && (
          <span className="ml-2 transition-opacity duration-300">
            {actualTheme === 'dark' ? 'Dark' : 'Light'}
          </span>
        )}
      </Button>
    </ClientOnly>
  );
}

// Theme status indicator
export function ThemeIndicator({ 
  className = "" 
}: { 
  className?: string;
}) {
  const { theme, actualTheme, systemTheme, isSystemTheme } = useTheme();

  return (
    <div className={`flex items-center space-x-2 text-sm text-muted-foreground ${className}`}>
      {actualTheme === 'dark' ? (
        <MoonIcon className="h-4 w-4" />
      ) : actualTheme === 'light' ? (
        <SunIcon className="h-4 w-4" />
      ) : (
        <MonitorIcon className="h-4 w-4" />
      )}
      <span>
        {isSystemTheme ? (
          <>System <span className="text-xs">({systemTheme})</span></>
        ) : (
          actualTheme === 'dark' ? 'Dark' : 'Light'
        )}
      </span>
    </div>
  );
}

export default ThemeToggle;
