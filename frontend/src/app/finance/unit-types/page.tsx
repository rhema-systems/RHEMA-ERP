'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger
} from '@/components/ui/dropdown-menu';
import { Ruler, Plus, Search, MoreHorizontal, Pencil, Trash2, ToggleLeft, ToggleRight, Loader2 } from 'lucide-react';
import Link from 'next/link';
import type { UnitType } from '@/types/unit-accounts';
import { unitAccountsDataService } from '@/services/finance/unit-accounts-data.service';
import { toast } from 'sonner';

export default function UnitTypesPage() {
    const [unitTypes, setUnitTypes] = useState<UnitType[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');

    const loadData = useCallback(async () => {
        try {
            setLoading(true);
            const data = await unitAccountsDataService.getUnitTypes();
            setUnitTypes(data);
        } catch (error) {
            console.error('Failed to load unit types:', error);
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        loadData();
    }, [loadData]);

    const handleToggleActive = async (type: UnitType) => {
        try {
            await unitAccountsDataService.updateUnitType(type.id, { isActive: !type.isActive });
            await loadData(); // Refresh the list
        } catch (error) {
            console.error('Failed to toggle unit type status:', error);
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('Are you sure you want to delete this unit type?')) return;
        try {
            await unitAccountsDataService.deleteUnitType(id);
            await loadData(); // Refresh the list
        } catch (error) {
            console.error('Failed to delete unit type:', error);
            toast.error('The unit type was not deleted. It may still be assigned to one or more unit accounts; remove those assignments and retry.');
        }
    };

    const filteredTypes = unitTypes.filter((type) => {
        const matchesSearch =
            searchTerm === '' ||
            type.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
            type.name.toLowerCase().includes(searchTerm.toLowerCase());

        const matchesStatus =
            statusFilter === 'all' ||
            (statusFilter === 'active' && type.isActive) ||
            (statusFilter === 'inactive' && !type.isActive);

        return matchesSearch && matchesStatus;
    });

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
                        <Ruler className="h-8 w-8" />
                        Unit Types
                    </h1>
                    <p className="text-muted-foreground">
                        Manage unit types for tracking non-financial quantities
                    </p>

                </div>
                <Link href="/finance/unit-types/new">
                    <Button>
                        <Plus className="mr-2 h-4 w-4" />
                        New Unit Type
                    </Button>
                </Link>
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
                        <BreadcrumbPage>Unit Types</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Search and Filters */}
            <Card>
                <CardHeader className="pb-3">
                    <CardTitle className="text-base">Search & Filter</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="flex flex-col md:flex-row gap-4">
                        <div className="relative flex-1">
                            <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                            <Input
                                placeholder="Search by code or name..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="pl-10"
                            />
                        </div>
                        <div className="flex gap-2">
                            <Button
                                variant={statusFilter === 'all' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setStatusFilter('all')}
                            >
                                All
                            </Button>
                            <Button
                                variant={statusFilter === 'active' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setStatusFilter('active')}
                            >
                                Active
                            </Button>
                            <Button
                                variant={statusFilter === 'inactive' ? 'default' : 'outline'}
                                size="sm"
                                onClick={() => setStatusFilter('inactive')}
                            >
                                Inactive
                            </Button>
                        </div>
                    </div>
                </CardContent>
            </Card>

            {/* Unit Types Table */}
            <Card>
                <CardHeader>
                    <CardTitle>Unit Types ({filteredTypes.length})</CardTitle>
                    <CardDescription>
                        Define what quantities are being measured (e.g., employees, hours, square footage)
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead className="w-[100px]">Code</TableHead>
                                <TableHead>Name</TableHead>
                                <TableHead>Description</TableHead>
                                <TableHead className="text-center">Decimals</TableHead>
                                <TableHead className="text-center">Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredTypes.map((type) => (
                                <TableRow key={type.id}>
                                    <TableCell className="font-mono font-semibold text-blue-600">
                                        {type.code}
                                    </TableCell>
                                    <TableCell className="font-medium">{type.name}</TableCell>
                                    <TableCell className="text-muted-foreground max-w-xs truncate">
                                        {type.description || '-'}
                                    </TableCell>
                                    <TableCell className="text-center">{type.decimalPlaces}</TableCell>
                                    <TableCell className="text-center">
                                        <Badge variant={type.isActive ? 'default' : 'secondary'}>
                                            {type.isActive ? 'Active' : 'Inactive'}
                                        </Badge>
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <DropdownMenu>
                                            <DropdownMenuTrigger asChild>
                                                <Button variant="ghost" size="sm">
                                                    <MoreHorizontal className="h-4 w-4" />
                                                </Button>
                                            </DropdownMenuTrigger>
                                            <DropdownMenuContent align="end">
                                                <DropdownMenuItem asChild>
                                                    <Link href={`/finance/unit-types/${type.id}`}>
                                                        <Pencil className="mr-2 h-4 w-4" />
                                                        Edit
                                                    </Link>
                                                </DropdownMenuItem>
                                                <DropdownMenuItem onClick={() => handleToggleActive(type)}>
                                                    {type.isActive ? (
                                                        <>
                                                            <ToggleLeft className="mr-2 h-4 w-4" />
                                                            Deactivate
                                                        </>
                                                    ) : (
                                                        <>
                                                            <ToggleRight className="mr-2 h-4 w-4" />
                                                            Activate
                                                        </>
                                                    )}
                                                </DropdownMenuItem>
                                                <DropdownMenuItem className="text-destructive" onClick={() => handleDelete(type.id)}>
                                                    <Trash2 className="mr-2 h-4 w-4" />
                                                    Delete
                                                </DropdownMenuItem>
                                            </DropdownMenuContent>
                                        </DropdownMenu>
                                    </TableCell>
                                </TableRow>
                            ))}
                            {filteredTypes.length === 0 && (
                                <TableRow>
                                    <TableCell colSpan={6} className="text-center py-8 text-muted-foreground">
                                        No unit types found matching your criteria.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>
        </div>
    );
}
