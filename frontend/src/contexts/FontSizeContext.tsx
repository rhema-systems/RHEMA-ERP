'use client';

import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';

export type FontSize = 'small' | 'medium' | 'large';
const storageKey = 'erp-font-size';
const normalize = (value: string | null): FontSize =>
  value === 'small' || value === 'large' ? value : 'medium';

const FontSizeContext = createContext({
  fontSize: 'medium' as FontSize,
  setFontSize: (_size: FontSize) => {},
});

export function FontSizeProvider({ children }: { children: ReactNode }) {
  // Keep the server and initial browser render identical.
  const [fontSize, setSize] = useState<FontSize>('medium');
  const applySize = useCallback((size: FontSize) => {
    document.documentElement.dataset.fontSize = size;
    setSize(size);
  }, []);

  useEffect(() => {
    try {
      applySize(normalize(localStorage.getItem(storageKey)));
    } catch {
      applySize('medium');
    }
    const syncSize = (event: StorageEvent) => {
      if (event.key === storageKey || event.key === null) {
        applySize(normalize(event.newValue));
      }
    };
    window.addEventListener('storage', syncSize);
    return () => window.removeEventListener('storage', syncSize);
  }, [applySize]);

  const setFontSize = useCallback((size: FontSize) => {
    applySize(size);
    try {
      localStorage.setItem(storageKey, size);
    } catch {
      // The current session can still use the preference if storage is blocked.
    }
  }, [applySize]);

  return <FontSizeContext.Provider value={{ fontSize, setFontSize }}>{children}</FontSizeContext.Provider>;
}

export const useFontSize = () => useContext(FontSizeContext);
