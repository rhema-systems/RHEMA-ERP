'use client';

import React, { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { AlertCircle, ArrowLeft, Loader2, RefreshCw, Tags } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FinanceDimensionDefinition } from '@/types/finance';
import { getDimensionSettingsAccess } from '@/components/finance/dimensions/dimension-settings-access';

export default function TransactionDimensionSettingsPage() {
    const { hasPermission, isLoading: authLoading } = useAuth();
    const { canRead, canManage } = getDimensionSettingsAccess(hasPermission);
    const [definitions, setDefinitions] = useState<FinanceDimensionDefinition[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        if (!canRead) return;
        setIsLoading(true);
        setError(null);
        try {
            const result = await financeDataService.getFinanceDimensions(true);
            setDefinitions([...result].sort((left, right) =>
                left.displayOrder - right.displayOrder || left.code.localeCompare(right.code)));
        } catch (loadError) {
            setError(loadError instanceof Error ? loadError.message : 'Unable to load transaction coding dimensions.');
        } finally {
            setIsLoading(false);
        }
    }, [canRead]);

    useEffect(() => {
        if (!authLoading && canRead) void load();
        if (!authLoading && !canRead) setIsLoading(false);
    }, [authLoading, canRead, load]);

    if (authLoading || isLoading) {
        return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" aria-label="Loading transaction dimensions" /></div>;
    }

    if (!canRead) {
        return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Permission required</AlertTitle><AlertDescription>Finance.Read is required to browse transaction coding dimensions.</AlertDescription></Alert>;
    }

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <Button asChild variant="ghost" className="mb-2 px-0"><Link href="/finance/settings/segments"><ArrowLeft className="mr-2 h-4 w-4" />Account number structure</Link></Button>
                    <h1 className="text-2xl font-semibold">Transaction coding dimensions</h1>
                    <p className="text-muted-foreground">Analysis fields applied to postings. They do not form part of a GL account number.</p>
                </div>
                <Badge variant={canManage ? 'default' : 'secondary'}>{canManage ? 'Configuration access' : 'Read only'}</Badge>
            </div>

            <Alert><Tags className="h-4 w-4" /><AlertTitle>Separate from account identity</AlertTitle><AlertDescription>Department, Project, Estate, Contract, Funding Source and Activity remain transaction dimensions. Company and Natural Account are the account-number identity segments.</AlertDescription></Alert>

            {error ? (
                <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Could not load dimensions</AlertTitle><AlertDescription className="flex items-center justify-between gap-3"><span>{error}</span><Button size="sm" variant="outline" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert>
            ) : definitions.length === 0 ? (
                <Card><CardContent className="py-12 text-center text-muted-foreground">No transaction dimensions are configured. Run the governed Finance manifest seeder or configure them through the Finance dimension administration boundary.</CardContent></Card>
            ) : (
                <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {definitions.map(definition => (
                        <Card key={definition.id}>
                            <CardHeader>
                                <div className="flex items-center justify-between gap-3"><CardTitle className="text-base">{definition.name}</CardTitle><Badge variant={definition.isActive ? 'default' : 'secondary'}>{definition.isActive ? 'Active' : 'Inactive'}</Badge></div>
                                <CardDescription>{definition.code}</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-2 text-sm">
                                <div><span className="text-muted-foreground">Classification:</span> {definition.classification}</div>
                                <div><span className="text-muted-foreground">Value source:</span> {definition.valueSourceType}</div>
                                <div><span className="text-muted-foreground">Configured values:</span> {definition.values.length}</div>
                                {definition.description && <p className="text-muted-foreground">{definition.description}</p>}
                            </CardContent>
                        </Card>
                    ))}
                </div>
            )}
        </div>
    );
}
