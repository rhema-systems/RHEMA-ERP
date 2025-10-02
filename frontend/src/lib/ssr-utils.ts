'use client';

import { useState, useEffect } from 'react';

/**
 * Hook to detect if we're running on the client side
 * Helps avoid hydration mismatches by returning false during SSR
 */
export function useIsClient(): boolean {
  const [isClient, setIsClient] = useState(false);

  useEffect(() => {
    setIsClient(true);
  }, []);

  return isClient;
}

/**
 * Generates consistent trend data for SSR vs random data for client
 * This prevents hydration mismatches while still providing dynamic data on the client
 */
export function generateTrendData(isClient: boolean = false): Array<{ period: string; value: number }> {
  const data: Array<{ period: string; value: number }> = [];

  if (!isClient) {
    // Provide consistent data for SSR
    const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    for (let i = 6; i >= 0; i--) {
      data.push({
        period: days[(6 - i) % 7], // Consistent day names
        value: 75 + (i * 5) % 30 // Deterministic values
      });
    }
    return data;
  }

  // Generate real dynamic data on client
  for (let i = 6; i >= 0; i--) {
    const date = new Date();
    date.setDate(date.getDate() - i);
    data.push({
      period: date.toLocaleDateString('en-US', { weekday: 'short' }),
      value: Math.floor(Math.random() * 100) + 50
    });
  }

  return data;
}

/**
 * Safe date formatter that provides consistent output during SSR
 * and real formatting on the client
 */
export function safeDateFormat(
  date: Date | string,
  options?: Intl.DateTimeFormatOptions,
  locale: string = 'en-US',
  isClient: boolean = false
): string {
  if (!isClient) {
    // Return a safe fallback for SSR
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toISOString().split('T')[0]; // Simple YYYY-MM-DD format
  }

  // Use full formatting on client
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  return dateObj.toLocaleDateString(locale, options);
}

/**
 * Safe random number generator that provides consistent values during SSR
 */
export function safeRandom(min: number = 0, max: number = 100, isClient: boolean = false): number {
  if (!isClient) {
    // Return deterministic value for SSR
    return Math.floor((min + max) / 2);
  }

  // Return actual random value on client
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

/**
 * Wrapper for browser APIs that might not exist during SSR
 */
export function safeBrowserAPI<T>(
  browserFn: () => T,
  fallback: T,
  isClient: boolean = false
): T {
  if (!isClient || typeof window === 'undefined') {
    return fallback;
  }

  try {
    return browserFn();
  } catch {
    return fallback;
  }
}

/**
 * Hook for safely using localStorage with SSR
 */
export function useSafeLocalStorage<T>(key: string, initialValue: T): [T, (value: T) => void] {
  const [storedValue, setStoredValue] = useState<T>(initialValue);
  const [isClient, setIsClient] = useState(false);

  useEffect(() => {
    setIsClient(true);
    if (typeof window !== 'undefined') {
      try {
        const item = window.localStorage.getItem(key);
        if (item) {
          setStoredValue(JSON.parse(item));
        }
      } catch (error) {
        console.error(`Error reading localStorage key "${key}":`, error);
      }
    }
  }, [key]);

  const setValue = (value: T) => {
    try {
      setStoredValue(value);
      if (isClient && typeof window !== 'undefined') {
        window.localStorage.setItem(key, JSON.stringify(value));
      }
    } catch (error) {
      console.error(`Error setting localStorage key "${key}":`, error);
    }
  };

  return [storedValue, setValue];
}