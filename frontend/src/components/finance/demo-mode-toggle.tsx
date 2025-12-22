'use client';

import React, { useState, useEffect } from 'react';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertTriangle, Server, Database } from 'lucide-react';

interface DemoModeState {
    finance: boolean;
}

const STORAGE_KEY = 'rhema-erp-demo-mode';

function getDemoModeState(): DemoModeState {
    if (typeof window === 'undefined') {
        return { finance: true }; // Default to demo mode on server
    }
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            return JSON.parse(stored);
        }
    } catch (error) {
        console.error('Failed to read demo mode state:', error);
    }
    return { finance: true }; // Default to demo mode
}

function setDemoModeState(state: DemoModeState): void {
    if (typeof window === 'undefined') return;
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
        // Dispatch event for other components to react
        window.dispatchEvent(new CustomEvent('demo-mode-changed', { detail: state }));
    } catch (error) {
        console.error('Failed to save demo mode state:', error);
    }
}

/**
 * Demo Mode Toggle Component for Finance Module
 * 
 * When enabled (default): Uses localStorage for data persistence
 * When disabled: Connects to real backend API
 */
export function FinanceDemoModeToggle() {
    const [isDemo, setIsDemo] = useState(true);
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        setMounted(true);
        const state = getDemoModeState();
        setIsDemo(state.finance);
    }, []);

    const handleToggle = (checked: boolean) => {
        setIsDemo(checked);
        setDemoModeState({ ...getDemoModeState(), finance: checked });

        // Notify user about the mode change
        if (!checked) {
            alert('Switching to Live API mode. Make sure the backend is running on localhost:53484 and you are logged in.');
        }
    };

    // Avoid hydration mismatch
    if (!mounted) {
        return null;
    }

    return (
        <div className="space-y-2">
            <div className="flex items-center space-x-3 p-3 rounded-lg border bg-muted/50">
                <div className="flex items-center space-x-2">
                    {isDemo ? (
                        <Database className="h-5 w-5 text-yellow-600" />
                    ) : (
                        <Server className="h-5 w-5 text-green-600" />
                    )}
                </div>
                <div className="flex-1">
                    <Label htmlFor="demo-mode" className="text-sm font-medium">
                        {isDemo ? 'Demo Mode (LocalStorage)' : 'Live API Mode'}
                    </Label>
                    <p className="text-xs text-muted-foreground">
                        {isDemo
                            ? 'Data is stored in your browser'
                            : 'Connected to backend API'
                        }
                    </p>
                </div>
                <Switch
                    id="demo-mode"
                    checked={isDemo}
                    onCheckedChange={handleToggle}
                />
            </div>
        </div>
    );
}

/**
 * Simple Demo Mode Banner for display at the top of Finance pages
 */
export function FinanceDemoModeBanner() {
    const [isDemo, setIsDemo] = useState(true);
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        setMounted(true);
        const state = getDemoModeState();
        setIsDemo(state.finance);

        // Listen for changes
        const handleChange = (e: CustomEvent<DemoModeState>) => {
            setIsDemo(e.detail.finance);
        };
        window.addEventListener('demo-mode-changed', handleChange as EventListener);
        return () => {
            window.removeEventListener('demo-mode-changed', handleChange as EventListener);
        };
    }, []);

    if (!mounted || !isDemo) {
        return null;
    }

    return (
        <Alert className="mb-4 border-yellow-500 bg-yellow-50">
            <AlertTriangle className="h-4 w-4 text-yellow-600" />
            <AlertDescription className="text-yellow-700">
                <strong>⚠️ DEMO MODE</strong> - Data is stored in your browser's localStorage
            </AlertDescription>
        </Alert>
    );
}

export default FinanceDemoModeToggle;
