"use client";

import React, { createContext, useContext, useEffect, useState } from 'react';

type Theme = 'dark' | 'light' | 'system';

type ThemeProviderProps = {
  children: React.ReactNode;
  defaultTheme?: Theme;
  storageKey?: string;
  attribute?: string;
  enableSystem?: boolean;
  disableTransitionOnChange?: boolean;
};

type ThemeProviderState = {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  actualTheme: 'dark' | 'light'; // The actual resolved theme (system preference resolved)
  systemTheme: 'dark' | 'light';
  toggleTheme: () => void;
  isSystemTheme: boolean;
};

const initialState: ThemeProviderState = {
  theme: 'system',
  setTheme: () => null,
  actualTheme: 'light',
  systemTheme: 'light',
  toggleTheme: () => null,
  isSystemTheme: true,
};

const ThemeProviderContext = createContext<ThemeProviderState>(initialState);

export function ThemeProvider({
  children,
  defaultTheme = 'system',
  storageKey = 'erp-theme',
  attribute = 'class',
  enableSystem = true,
  disableTransitionOnChange = false,
  ...props
}: ThemeProviderProps) {
  const [theme, setTheme] = useState<Theme>(() => {
    // Check if we're on the client side
    if (typeof window === 'undefined') {
      return defaultTheme;
    }

    // Try to get theme from localStorage
    try {
      const stored = localStorage.getItem(storageKey);
      if (stored && (stored === 'dark' || stored === 'light' || stored === 'system')) {
        return stored as Theme;
      }
    } catch (error) {
      console.warn('Failed to read theme from localStorage:', error);
    }

    return defaultTheme;
  });

  const [systemTheme, setSystemTheme] = useState<'dark' | 'light'>(() => {
    if (typeof window === 'undefined') {
      return 'light';
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  });

  const [mounted, setMounted] = useState(false);

  // Get the actual theme to apply (resolve 'system' to actual preference)
  const actualTheme = theme === 'system' ? systemTheme : theme;
  const isSystemTheme = theme === 'system';

  // Update system theme when system preference changes
  useEffect(() => {
    if (!enableSystem) return;

    const media = window.matchMedia('(prefers-color-scheme: dark)');
    
    const updateSystemTheme = (e: MediaQueryListEvent) => {
      setSystemTheme(e.matches ? 'dark' : 'light');
    };

    media.addEventListener('change', updateSystemTheme);
    return () => media.removeEventListener('change', updateSystemTheme);
  }, [enableSystem]);

  // Apply theme to document
  useEffect(() => {
    if (typeof window === 'undefined') return;
    
    const root = window.document.documentElement;
    
    // Temporarily disable transitions to prevent flash
    if (disableTransitionOnChange && mounted) {
      root.style.setProperty('transition', 'none');
    }

    // Remove previous theme classes
    root.classList.remove('light', 'dark');
    
    // Add new theme class
    if (attribute === 'class') {
      root.classList.add(actualTheme);
    } else {
      root.setAttribute(attribute, actualTheme);
    }

    // Re-enable transitions
    if (disableTransitionOnChange && mounted) {
      // Force reflow
      void root.offsetHeight;
      root.style.removeProperty('transition');
    }
  }, [actualTheme, attribute, disableTransitionOnChange, mounted]);

  // Save theme to localStorage
  useEffect(() => {
    if (!mounted) return;
    
    try {
      localStorage.setItem(storageKey, theme);
    } catch (error) {
      console.warn('Failed to save theme to localStorage:', error);
    }
  }, [theme, storageKey, mounted]);

  // Set mounted flag after hydration
  useEffect(() => {
    setMounted(true);
  }, []);

  const setThemeHandler = (newTheme: Theme) => {
    setTheme(newTheme);
  };

  const toggleTheme = () => {
    if (theme === 'system') {
      // If on system, switch to the opposite of current system theme
      setTheme(systemTheme === 'dark' ? 'light' : 'dark');
    } else if (theme === 'light') {
      setTheme('dark');
    } else {
      setTheme('light');
    }
  };

  const value: ThemeProviderState = {
    theme,
    setTheme: setThemeHandler,
    actualTheme,
    systemTheme,
    toggleTheme,
    isSystemTheme,
  };

  return (
    <ThemeProviderContext.Provider {...props} value={value}>
      {children}
    </ThemeProviderContext.Provider>
  );
}

export const useTheme = () => {
  const context = useContext(ThemeProviderContext);

  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }

  return context;
};

// Hook for theme-aware components
export const useThemeAware = () => {
  const { actualTheme, theme, systemTheme } = useTheme();
  
  return {
    isDark: actualTheme === 'dark',
    isLight: actualTheme === 'light',
    isSystemTheme: theme === 'system',
    actualTheme,
    systemTheme,
  };
};

// HOC for theme-aware components
export function withTheme<P extends object>(
  Component: React.ComponentType<P & { theme?: 'dark' | 'light' }>
) {
  return function ThemedComponent(props: P) {
    const { actualTheme } = useTheme();
    return <Component {...props} theme={actualTheme} />;
  };
}
