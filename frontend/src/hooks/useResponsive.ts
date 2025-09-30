import { useState, useEffect, useCallback } from 'react';

export type Breakpoint = 'xs' | 'sm' | 'md' | 'lg' | 'xl' | '2xl';
export type BreakpointValue = number;

// Tailwind CSS breakpoints
export const breakpoints: Record<Breakpoint, BreakpointValue> = {
  xs: 0,    // Extra small devices (phones)
  sm: 640,  // Small devices (tablets)
  md: 768,  // Medium devices (small laptops)
  lg: 1024, // Large devices (desktops)
  xl: 1280, // Extra large devices
  '2xl': 1536, // 2X large devices
};

export interface ResponsiveState {
  width: number;
  height: number;
  breakpoint: Breakpoint;
  isMobile: boolean;
  isTablet: boolean;
  isDesktop: boolean;
  isLandscape: boolean;
  isPortrait: boolean;
  isTouchDevice: boolean;
}

export function useResponsive(): ResponsiveState {
  const [state, setState] = useState<ResponsiveState>(() => {
    // Default state for SSR
    if (typeof window === 'undefined') {
      return {
        width: 1024,
        height: 768,
        breakpoint: 'lg',
        isMobile: false,
        isTablet: false,
        isDesktop: true,
        isLandscape: true,
        isPortrait: false,
        isTouchDevice: false,
      };
    }

    return getResponsiveState();
  });

  const updateState = useCallback(() => {
    setState(getResponsiveState());
  }, []);

  useEffect(() => {
    // Update state on mount to get actual dimensions
    updateState();

    // Listen for resize events
    const handleResize = () => updateState();
    window.addEventListener('resize', handleResize);
    
    // Listen for orientation changes
    const handleOrientationChange = () => {
      // Small delay to allow browser to complete the orientation change
      setTimeout(updateState, 100);
    };
    window.addEventListener('orientationchange', handleOrientationChange);

    return () => {
      window.removeEventListener('resize', handleResize);
      window.removeEventListener('orientationchange', handleOrientationChange);
    };
  }, [updateState]);

  return state;
}

// Helper function to get responsive state
function getResponsiveState(): ResponsiveState {
  const width = window.innerWidth;
  const height = window.innerHeight;
  const breakpoint = getCurrentBreakpoint(width);
  
  return {
    width,
    height,
    breakpoint,
    isMobile: breakpoint === 'xs' || breakpoint === 'sm',
    isTablet: breakpoint === 'sm' || breakpoint === 'md',
    isDesktop: breakpoint === 'lg' || breakpoint === 'xl' || breakpoint === '2xl',
    isLandscape: width > height,
    isPortrait: width <= height,
    isTouchDevice: 'ontouchstart' in window || navigator.maxTouchPoints > 0,
  };
}

// Get current breakpoint based on width
function getCurrentBreakpoint(width: number): Breakpoint {
  if (width >= breakpoints['2xl']) return '2xl';
  if (width >= breakpoints.xl) return 'xl';
  if (width >= breakpoints.lg) return 'lg';
  if (width >= breakpoints.md) return 'md';
  if (width >= breakpoints.sm) return 'sm';
  return 'xs';
}

// Hook for breakpoint-specific values
export function useBreakpointValue<T>(values: Partial<Record<Breakpoint, T>>): T | undefined {
  const { breakpoint } = useResponsive();
  
  // Find the appropriate value for the current breakpoint
  const orderedBreakpoints: Breakpoint[] = ['2xl', 'xl', 'lg', 'md', 'sm', 'xs'];
  const currentIndex = orderedBreakpoints.indexOf(breakpoint);
  
  // Look for a value at the current breakpoint or the largest smaller breakpoint
  for (let i = currentIndex; i < orderedBreakpoints.length; i++) {
    const bp = orderedBreakpoints[i];
    if (values[bp] !== undefined) {
      return values[bp];
    }
  }
  
  return undefined;
}

// Hook for responsive visibility
export function useResponsiveVisibility(
  showOn: Breakpoint[] | 'mobile' | 'tablet' | 'desktop' | 'all' = 'all'
): boolean {
  const { breakpoint, isMobile, isTablet, isDesktop } = useResponsive();
  
  if (showOn === 'all') return true;
  if (showOn === 'mobile') return isMobile;
  if (showOn === 'tablet') return isTablet;
  if (showOn === 'desktop') return isDesktop;
  
  return Array.isArray(showOn) ? showOn.includes(breakpoint) : false;
}

// Hook for responsive classes
export function useResponsiveClasses(
  classes: Partial<Record<Breakpoint, string>>
): string {
  const breakpointValue = useBreakpointValue(classes);
  return breakpointValue || '';
}

// Utility functions
export const responsive = {
  // Check if current breakpoint matches
  is: (bp: Breakpoint): boolean => {
    if (typeof window === 'undefined') return false;
    return getCurrentBreakpoint(window.innerWidth) === bp;
  },

  // Check if current width is at least the specified breakpoint
  isAtLeast: (bp: Breakpoint): boolean => {
    if (typeof window === 'undefined') return false;
    return window.innerWidth >= breakpoints[bp];
  },

  // Check if current width is below the specified breakpoint
  isBelow: (bp: Breakpoint): boolean => {
    if (typeof window === 'undefined') return false;
    return window.innerWidth < breakpoints[bp];
  },

  // Get responsive value based on current breakpoint
  getValue: <T>(values: Partial<Record<Breakpoint, T>>): T | undefined => {
    if (typeof window === 'undefined') return undefined;
    
    const currentBp = getCurrentBreakpoint(window.innerWidth);
    const orderedBreakpoints: Breakpoint[] = ['2xl', 'xl', 'lg', 'md', 'sm', 'xs'];
    const currentIndex = orderedBreakpoints.indexOf(currentBp);
    
    for (let i = currentIndex; i < orderedBreakpoints.length; i++) {
      const bp = orderedBreakpoints[i];
      if (values[bp] !== undefined) {
        return values[bp];
      }
    }
    
    return undefined;
  },
};

// Hook for device-specific behavior
export function useDeviceType() {
  const { isMobile, isTablet, isDesktop, isTouchDevice, isLandscape, isPortrait } = useResponsive();
  
  return {
    isMobile,
    isTablet,
    isDesktop,
    isTouchDevice,
    isLandscape,
    isPortrait,
    
    // Device categories
    isSmallDevice: isMobile,
    isMediumDevice: isTablet,
    isLargeDevice: isDesktop,
    
    // User interaction preferences
    prefersTouchInteraction: isTouchDevice && isMobile,
    prefersMouseInteraction: !isTouchDevice && isDesktop,
    needsLargerTapTargets: isTouchDevice,
    needsMoreSpacing: isMobile || isTablet,
    
    // Layout preferences
    shouldUseMobileLayout: isMobile,
    shouldUseTabletLayout: isTablet && !isMobile,
    shouldUseDesktopLayout: isDesktop,
    shouldShowSidebar: isDesktop,
    shouldUseDrawer: isMobile || (isTablet && isPortrait),
  };
}

// Hook for responsive component props
export function useResponsiveProps<T extends Record<string, any>>(
  props: Partial<Record<Breakpoint, Partial<T>>>
): Partial<T> {
  const currentProps = useBreakpointValue(props);
  return currentProps || {};
}

// Custom hook for media queries
export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() => {
    if (typeof window === 'undefined') return false;
    return window.matchMedia(query).matches;
  });

  useEffect(() => {
    const mediaQuery = window.matchMedia(query);
    
    const handleChange = (event: MediaQueryListEvent) => {
      setMatches(event.matches);
    };

    mediaQuery.addEventListener('change', handleChange);
    setMatches(mediaQuery.matches);

    return () => {
      mediaQuery.removeEventListener('change', handleChange);
    };
  }, [query]);

  return matches;
}

// Predefined media queries
export const useCommonMediaQueries = () => ({
  isMobile: useMediaQuery('(max-width: 639px)'),
  isTablet: useMediaQuery('(min-width: 640px) and (max-width: 1023px)'),
  isDesktop: useMediaQuery('(min-width: 1024px)'),
  isLandscape: useMediaQuery('(orientation: landscape)'),
  isPortrait: useMediaQuery('(orientation: portrait)'),
  prefersReducedMotion: useMediaQuery('(prefers-reduced-motion: reduce)'),
  prefersDarkMode: useMediaQuery('(prefers-color-scheme: dark)'),
  isHighDensity: useMediaQuery('(-webkit-min-device-pixel-ratio: 2), (min-resolution: 192dpi)'),
});

export default useResponsive;