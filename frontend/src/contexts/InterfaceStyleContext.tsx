'use client';

import React, { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authService } from '../services/auth';

export type InterfaceStyle = 'executive' | 'immersive';

const storagePrefix = 'erp-interface-style';
const normalizeStyle = (value: string | null): InterfaceStyle => value === 'immersive' ? 'immersive' : 'executive';

export const getInterfaceStyleStorageKey = () => {
  const user = authService.getStoredUser();
  const identity = user?.id || user?.username || 'anonymous';
  return `${storagePrefix}:${identity}`;
};

interface InterfaceStyleContextValue {
  interfaceStyle: InterfaceStyle;
  setInterfaceStyle: (style: InterfaceStyle) => void;
}

const InterfaceStyleContext = createContext<InterfaceStyleContextValue>({
  interfaceStyle: 'executive',
  setInterfaceStyle: () => undefined,
});

export function InterfaceStyleProvider({ children }: { children: ReactNode }) {
  const [interfaceStyle, setStyle] = useState<InterfaceStyle>('executive');

  const applyStyle = useCallback((style: InterfaceStyle) => {
    document.documentElement.dataset.interfaceStyle = style;
    setStyle(style);
  }, []);

  useEffect(() => {
    const storageKey = getInterfaceStyleStorageKey();
    try {
      applyStyle(normalizeStyle(localStorage.getItem(storageKey)));
    } catch {
      applyStyle('executive');
    }

    const synchronize = (event: StorageEvent) => {
      if (event.key === storageKey || event.key === null) {
        applyStyle(normalizeStyle(event.newValue));
      }
    };
    window.addEventListener('storage', synchronize);
    return () => window.removeEventListener('storage', synchronize);
  }, [applyStyle]);

  const setInterfaceStyle = useCallback((style: InterfaceStyle) => {
    applyStyle(style);
    try {
      localStorage.setItem(getInterfaceStyleStorageKey(), style);
    } catch {
      // Keep the selected style for this session when storage is unavailable.
    }
  }, [applyStyle]);

  const value = useMemo(() => ({ interfaceStyle, setInterfaceStyle }), [interfaceStyle, setInterfaceStyle]);
  return <InterfaceStyleContext.Provider value={value}>{children}</InterfaceStyleContext.Provider>;
}

export const useInterfaceStyle = () => useContext(InterfaceStyleContext);
