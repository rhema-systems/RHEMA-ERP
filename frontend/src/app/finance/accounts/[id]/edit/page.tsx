'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { ArrowLeft, Save } from 'lucide-react';
import { useRouter } from 'next/navigation';
import type { AccountType, AccountStatus } from '@/types/finance';

// MOCK DATA - In real app, this would come from API based on params.id
const MOCK_ACCOUNT = {
    id: 'acc-1',
    accountCode: '1000',
    accountNumber: '1000',
    accountName: 'Cash and Cash Equivalents',
    accountType: 'Asset' as AccountType,
    description: 'Bank accounts, petty cash, and other liquid assets',
    currencyCode: 'GHS',
    isMultiCurrency: false,
    isIFRSClassified: true,
    isBaseClassified: true,
    isLocalClassified: true,
    allowDirectPosting: true,
    isControlAccount: false,
    budgetTrackingEnabled: false,
    status: 'Active' as AccountStatus,
};

export default function EditAccountPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const [formData, setFormData] = useState(MOCK_ACCOUNT);

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        console.log('Updating account:', formData);
        // Simulate save
        setTimeout(() => {
            router.push(`/finance/accounts/${params.id}`);
        }, 500);
    };

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">
                        Edit Account: {formData.accountCode}
                    </h1>
                    <p className="text-muted-foreground">
                        Update account details
                    </p>
                    <p className="text-sm text-orange-600 mt-1">
                        ⚠️ DEMO MODE - Using mock data (backend not connected)
                    </p>
                </div>
                <Button variant="outline" onClick={() => router.back()}>
                    <ArrowLeft className="mr-2 h-4 w-4" />
                    Back
                </Button>
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
                        <BreadcrumbLink href="/finance/accounts">Chart of Accounts</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbLink href={`/finance/accounts/${params.id}`}>{formData.accountCode}</BreadcrumbLink>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem>
                        <BreadcrumbPage>Edit</BreadcrumbPage>
                    </BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Form */}
            <form onSubmit={handleSubmit}>
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                    {/* Main Form */}
                    <div className="lg:col-span-2 space-y-6">
                        {/* Basic Information */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Basic Information</CardTitle>
                                <CardDescription>Account identification and classification</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label htmlFor="accountCode">Account Code *</Label>
                                        <Input
                                            id="accountCode"
                                            value={formData.accountCode}
                                            onChange={(e) => setFormData({ ...formData, accountCode: e.target.value })}
                                            required
                                            disabled
                                        />
                                        <p className="text-xs text-muted-foreground">Account code cannot be changed</p>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor="accountNumber">Account Number *</Label>
                                        <Input
                                            id="accountNumber"
                                            value={formData.accountNumber}
                                            onChange={(e) => setFormData({ ...formData, accountNumber: e.target.value })}
                                            required
                                            disabled
                                        />
                                        <p className="text-xs text-muted-foreground">Account number cannot be changed</p>
                                    </div>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="accountName">Account Name *</Label>
                                    <Input
                                        id="accountName"
                                        value={formData.accountName}
                                        onChange={(e) => setFormData({ ...formData, accountName: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="accountType">Account Type *</Label>
                                    <Select
                                        value={formData.accountType}
                                        onValueChange={(value: AccountType) => setFormData({ ...formData, accountType: value })}
                                        disabled
                                    >
                                        <SelectTrigger id="accountType">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Asset">Asset</SelectItem>
                                            <SelectItem value="Liability">Liability</SelectItem>
                                            <SelectItem value="Equity">Equity</SelectItem>
                                            <SelectItem value="Revenue">Revenue</SelectItem>
                                            <SelectItem value="Expense">Expense</SelectItem>
                                        </SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">Account type cannot be changed</p>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="description">Description</Label>
                                    <Textarea
                                        id="description"
                                        value={formData.description}
                                        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                        rows={3}
                                    />
                                </div>
                            </CardContent>
                        </Card>

                        {/* Currency Settings */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Currency Settings</CardTitle>
                                <CardDescription>Configure currency handling for this account</CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-4">
                                <div className="space-y-2">
                                    <Label htmlFor="currencyCode">Primary Currency *</Label>
                                    <Select
                                        value={formData.currencyCode}
                                        onValueChange={(value) => setFormData({ ...formData, currencyCode: value })}
                                        disabled
                                    >
                                        <SelectTrigger id="currencyCode">
                                            <SelectValue />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="GHS">GHS - Ghana Cedi</SelectItem>
                                            <SelectItem value="USD">USD - US Dollar</SelectItem>
                                            <SelectItem value="EUR">EUR - Euro</SelectItem>
                                            <SelectItem value="GBP">GBP - British Pound</SelectItem>
                                        </SelectContent>
                                    </Select>
                                    <p className="text-xs text-muted-foreground">Currency cannot be changed after creation</p>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isMultiCurrency"
                                        checked={formData.isMultiCurrency}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isMultiCurrency: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isMultiCurrency" className="cursor-pointer">
                                        Enable multi-currency transactions
                                    </Label>
                                </div>
                            </CardContent>
                        </Card>
                    </div>

                    {/* Sidebar */}
                    <div className="space-y-6">
                        {/* Account Features */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Account Features</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="allowDirectPosting"
                                        checked={formData.allowDirectPosting}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, allowDirectPosting: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="allowDirectPosting" className="cursor-pointer text-sm">
                                        Allow direct posting
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isControlAccount"
                                        checked={formData.isControlAccount}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isControlAccount: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isControlAccount" className="cursor-pointer text-sm">
                                        Control account
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="budgetTrackingEnabled"
                                        checked={formData.budgetTrackingEnabled}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, budgetTrackingEnabled: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="budgetTrackingEnabled" className="cursor-pointer text-sm">
                                        Enable budget tracking
                                    </Label>
                                </div>
                            </CardContent>
                        </Card>

                        {/* Classification */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Classification</CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isIFRSClassified"
                                        checked={formData.isIFRSClassified}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isIFRSClassified: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isIFRSClassified" className="cursor-pointer text-sm">
                                        IFRS Classification
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isBaseClassified"
                                        checked={formData.isBaseClassified}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isBaseClassified: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isBaseClassified" className="cursor-pointer text-sm">
                                        Base Classification
                                    </Label>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <Checkbox
                                        id="isLocalClassified"
                                        checked={formData.isLocalClassified}
                                        onCheckedChange={(checked) =>
                                            setFormData({ ...formData, isLocalClassified: checked as boolean })
                                        }
                                    />
                                    <Label htmlFor="isLocalClassified" className="cursor-pointer text-sm">
                                        Local Classification
                                    </Label>
                                </div>
                            </CardContent>
                        </Card>

                        {/* Status */}
                        <Card>
                            <CardHeader>
                                <CardTitle>Status</CardTitle>
                            </CardHeader>
                            <CardContent>
                                <Select
                                    value={formData.status}
                                    onValueChange={(value: AccountStatus) => setFormData({ ...formData, status: value })}
                                >
                                    <SelectTrigger>
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Active">Active</SelectItem>
                                        <SelectItem value="Inactive">Inactive</SelectItem>
                                        <SelectItem value="Closed">Closed</SelectItem>
                                    </SelectContent>
                                </Select>
                            </CardContent>
                        </Card>

                        {/* Actions */}
                        <div className="space-y-2">
                            <Button type="submit" className="w-full">
                                <Save className="mr-2 h-4 w-4" />
                                Save Changes
                            </Button>
                            <Button type="button" variant="outline" className="w-full" onClick={() => router.back()}>
                                Cancel
                            </Button>
                        </div>
                    </div>
                </div>
            </form>
        </div>
    );
}
