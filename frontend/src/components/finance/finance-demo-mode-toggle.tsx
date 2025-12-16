'use client';

import React from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useDemoMode } from '@/contexts/demo-mode-context';
import { Database, TestTube } from 'lucide-react';

export function FinanceDemoModeToggle() {
    const context = useDemoMode();
    const isDemo = context.isDemoMode('finance');

    return (
        <Card className={`border-2 transition-colors ${isDemo ? 'border-orange-400 bg-orange-50/50' : 'border-green-400 bg-green-50/50'}`}>
            <CardContent className="p-4">
                <div className="flex items-center justify-between gap-4">
                    <div className="flex items-center gap-3">
                        {isDemo ? (
                            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-orange-100">
                                <TestTube className="h-5 w-5 text-orange-600" />
                            </div>
                        ) : (
                            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-green-100">
                                <Database className="h-5 w-5 text-green-600" />
                            </div>
                        )}
                        <div>
                            <Label className={`text-base font-semibold ${isDemo ? 'text-orange-900' : 'text-green-900'}`}>
                                {isDemo ? 'Demo Mode' : 'Live Mode'}
                            </Label>
                            <p className={`text-sm ${isDemo ? 'text-orange-700' : 'text-green-700'}`}>
                                {isDemo ? 'Using mock data for demonstrations' : 'Connected to API and database'}
                            </p>
                        </div>
                    </div>
                    <Switch
                        checked={!isDemo}
                        onCheckedChange={() => context.toggleDemoMode('finance')}
                        className={`${isDemo ? 'data-[state=unchecked]:bg-orange-400' : 'data-[state=checked]:bg-green-500'}`}
                    />
                </div>
                {isDemo && (
                    <div className="mt-3 rounded-md bg-orange-100 px-3 py-2 text-xs text-orange-800">
                        <strong>Note:</strong> Demo mode only affects the Finance module. Other modules continue using live data.
                    </div>
                )}
            </CardContent>
        </Card>
    );
}
