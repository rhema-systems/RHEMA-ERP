'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ChevronRight, FileText, Clock, CheckCircle, XCircle } from 'lucide-react'; // Import icons
import { useToast } from '@/components/ui/use-toast';
import { budgetDataService } from '@/services/finance/budget-data.service'; // Import budget service
import type { BudgetReturn } from '@/types/budget';

export default function MyBudgetReturnsPage() {
    const { toast } = useToast();
    const [returns, setReturns] = useState<BudgetReturn[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        try {
            setIsLoading(true);
            const returnsData = await budgetDataService.getMyReturns();
            setReturns(returnsData);
        } catch (error) {
            console.error('Failed to load my returns:', error);
            toast({
                title: 'Error',
                description: 'Failed to load your budget returns.',
                variant: 'destructive',
            });
        } finally {
            setIsLoading(false);
        }
    };

    const getStatusBadge = (status: string) => {
        const variants: Record<string, string> = {
            Draft: 'bg-gray-100 text-gray-800',
            Submitted: 'bg-blue-100 text-blue-800',
            Approved: 'bg-green-100 text-green-800',
            Rejected: 'bg-red-100 text-red-800',
        };
        const icons: Record<string, any> = {
            Draft: FileText,
            Submitted: Clock,
            Approved: CheckCircle,
            Rejected: XCircle,
        };
        const Icon = icons[status] || FileText;

        return (
            <span className={`flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium ${variants[status] || 'bg-gray-100'}`}>
                <Icon className="w-3 h-3" />
                {status}
            </span>
        );
    };

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">My Budget Returns</h1>
                    <p className="text-muted-foreground">
                        Manage budget worksheets assigned to you
                    </p>
                </div>
            </div>

            {/* Breadcrumbs */}
            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>My Returns</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Returns List */}
            <Card>
                <CardHeader>
                    <CardTitle>Assigned Worksheets</CardTitle>
                    <CardDescription>
                        Budget returns pending your input or review
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Department / Unit</TableHead>
                                <TableHead>Scenario</TableHead>
                                <TableHead>Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {returns.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={4} className="text-center py-8 text-muted-foreground">
                                        No budget returns assigned to you.
                                    </TableCell>
                                </TableRow>
                            ) : (
                                returns.map((ret) => (
                                    <TableRow key={ret.id}>
                                        <TableCell className="font-medium">
                                            {ret.distributionDimensionName || ret.segmentValueName || 'Unassigned distribution'}
                                        </TableCell>
                                        <TableCell>
                                            {ret.budgetScenarioName || ret.budgetScenarioId}
                                        </TableCell>
                                        <TableCell>{getStatusBadge(ret.status)}</TableCell>
                                        <TableCell className="text-right">
                                            <Link href={`/finance/budgeting/returns/${ret.id}`}>
                                                <Button size="sm" variant="outline">
                                                    Open
                                                    <ChevronRight className="ml-2 h-4 w-4" />
                                                </Button>
                                            </Link>
                                        </TableCell>
                                    </TableRow>
                                ))
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </div>
    );
}
