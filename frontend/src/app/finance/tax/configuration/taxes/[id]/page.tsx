'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Loader2, Save, MoreHorizontal } from 'lucide-react';
import { toast } from '@/components/ui/use-toast';
import { useRouter } from 'next/navigation';
import { taxDataService } from '@/services/finance/tax-data.service';
import { TaxCategory, TaxApplicability } from '@/types/tax';
import type { Tax } from '@/types/tax';

interface PageProps {
    params: {
        id: string;
    };
}

export default function TaxDetailPage({ params }: PageProps) {
    const router = useRouter();
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [tax, setTax] = useState<Tax | null>(null);

    useEffect(() => {
        loadTax();
    }, [params.id]);

    const loadTax = async () => {
        try {
            setIsLoading(true);
            const data = await taxDataService.getTaxById(params.id);
            setTax(data);
        } catch (error) {
            console.error('Failed to load tax:', error);
            toast({
                title: 'Error',
                description: 'Failed to load tax details.',
                variant: 'destructive',
            });
            // If 404, redirect to list? Or just show error state
        } finally {
            setIsLoading(false);
        }
    };

    if (isLoading) {
        return (
            <div className="flex h-[400px] items-center justify-center">
                <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
        );
    }

    if (!tax) {
        return (
            <div className="flex flex-col items-center justify-center h-[400px] gap-4">
                <p className="text-muted-foreground">Tax not found</p>
                <Button variant="outline" onClick={() => router.push('/finance/tax/configuration/taxes')}>
                    Back to Taxes
                </Button>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <Button variant="ghost" size="icon" onClick={() => router.back()}>
                        <ArrowLeft className="h-4 w-4" />
                    </Button>
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">{tax.name}</h1>
                        <p className="text-muted-foreground">
                            {tax.code} • {tax.category}
                        </p>
                    </div>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" onClick={() => loadTax()}>
                        Refresh
                    </Button>
                    <Button variant="default" onClick={() => { /* TODO: Edit logic */ }}>
                        Edit
                    </Button>
                </div>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance/tax/configuration/taxes">Taxes</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>{tax.code}</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Content */}
            <div className="grid gap-6 md:grid-cols-2">
                <Card>
                    <CardHeader>
                        <CardTitle>General Information</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label className="text-muted-foreground">Tax Code</Label>
                                <p className="font-medium">{tax.code}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Rate</Label>
                                <p className="font-medium">{tax.rate}%</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Category</Label>
                                <p className="font-medium">{tax.category}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Applicability</Label>
                                <p className="font-medium">{tax.applicability}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Recoverable</Label>
                                <p className="font-medium">{tax.isInputTaxDeductible ? 'Yes' : 'No'}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Status</Label>
                                <Badge variant={tax.isActive ? 'default' : 'secondary'}>
                                    {tax.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                            </div>
                        </div>
                        <div>
                            <Label className="text-muted-foreground">Description</Label>
                            <p className="mt-1 text-sm">{tax.description || '-'}</p>
                        </div>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle>Configuration</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div>
                            <Label className="text-muted-foreground">GL Accounts</Label>
                            <div className="mt-2 space-y-2 text-sm">
                                <div className="flex justify-between border-b pb-2">
                                    <span>Payable Account</span>
                                    <span className="font-mono text-muted-foreground">{tax.taxPayableAccountId || 'Not Set'}</span>
                                </div>
                                <div className="flex justify-between border-b pb-2">
                                    <span>Receivable Account</span>
                                    <span className="font-mono text-muted-foreground">{tax.taxReceivableAccountId || 'Not Set'}</span>
                                </div>
                            </div>
                        </div>
                        {tax.thresholdAmount && (
                            <div>
                                <Label className="text-muted-foreground">Threshold Amount</Label>
                                <p className="font-medium">{tax.thresholdAmount}</p>
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>
        </div>
    );
}
