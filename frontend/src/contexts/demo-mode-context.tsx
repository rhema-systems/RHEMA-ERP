'use client';

import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

// Module-specific demo mode state
interface DemoModeState {
    finance: boolean;
    inventory: boolean;
    fixedAssets: boolean;
    ar: boolean;
    ap: boolean;
    // Add more modules as needed
}

interface DemoModeContextType {
    demoModeState: DemoModeState;
    isDemoMode: (module: keyof DemoModeState) => boolean;
    toggleDemoMode: (module: keyof DemoModeState) => void;
    setDemoMode: (module: keyof DemoModeState, enabled: boolean) => void;
}

const DemoModeContext = createContext<DemoModeContextType | undefined>(undefined);

const DEFAULT_DEMO_MODE_STATE: DemoModeState = {
    finance: true,        // Default to demo mode for safer development
    inventory: false,
    fixedAssets: false,
    ar: false,
    ap: false,
};

const STORAGE_KEY = 'rhema-erp-demo-mode';

export function DemoModeProvider({ children }: { children: ReactNode }) {
    const [demoModeState, setDemoModeState] = useState<DemoModeState>(DEFAULT_DEMO_MODE_STATE);
    const [isInitialized, setIsInitialized] = useState(false);

    // Load state from localStorage on mount
    useEffect(() => {
        if (typeof window !== 'undefined') {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                if (stored) {
                    const parsed = JSON.parse(stored) as Partial<DemoModeState>;
                    setDemoModeState({ ...DEFAULT_DEMO_MODE_STATE, ...parsed });
                }
            } catch (error) {
                console.error('Failed to load demo mode state from localStorage:', error);
            } finally {
                setIsInitialized(true);
            }
        }
    }, []);

    // Save state to localStorage whenever it changes
    useEffect(() => {
        if (isInitialized && typeof window !== 'undefined') {
            try {
                localStorage.setItem(STORAGE_KEY, JSON.stringify(demoModeState));
            } catch (error) {
                console.error('Failed to save demo mode state to localStorage:', error);
            }
        }
    }, [demoModeState, isInitialized]);

    const isDemoMode = (module: keyof DemoModeState): boolean => {
        return demoModeState[module] ?? false;
    };

    const toggleDemoMode = (module: keyof DemoModeState) => {
        setDemoModeState((prev) => ({
            ...prev,
            [module]: !prev[module],
        }));
    };

    const setDemoMode = (module: keyof DemoModeState, enabled: boolean) => {
        setDemoModeState((prev) => ({
            ...prev,
            [module]: enabled,
        }));
    };

    const value: DemoModeContextType = {
        demoModeState,
        isDemoMode,
        toggleDemoMode,
        setDemoMode,
    };

    return <DemoModeContext.Provider value={value}>{children}</DemoModeContext.Provider>;
}

// Function overloads for useDemoMode
export function useDemoMode(): DemoModeContextType;
export function useDemoMode(module: keyof DemoModeState): boolean;
export function useDemoMode(module?: keyof DemoModeState): boolean | DemoModeContextType {
    const context = useContext(DemoModeContext);

    if (context === undefined) {
        throw new Error('useDemoMode must be used within a DemoModeProvider');
    }

    // If module is specified, return just the boolean for that module
    if (module !== undefined) {
        return context.isDemoMode(module);
    }

    // Otherwise return the full context
    return context;
}

// Convenience hook for Finance module
export function useFinanceDemoMode() {
    return useDemoMode('finance');
}
