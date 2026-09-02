'use client';

import React, { useEffect, useState, useMemo } from 'react';
import { ArrowLeftRight, Plus, Loader2, CheckCircle2, XCircle, Clock, Info, Search, Filter, ChevronsUpDown, Check } from 'lucide-react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { cn } from "@/lib/utils";
import { format } from 'date-fns';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { maintenanceDataService, Employee } from '@/services/maintenanceDataService';
import { AssetTransfer, AssetTransferStatus, AssetTransferType, RequestAssetTransferDto, FixedAsset, FixedAssetCategory } from '@/types/fixed-assets';
import { useToast } from "@/components/ui/use-toast";
import { SourceDocumentDimensionDefaultsPanel } from '@/components/finance/dimensions/source-document-dimension-panel';
import { toFinancePostingDimensionValues } from '@/lib/finance/source-document-dimensions';
import { AssetLocationCombobox } from '@/components/finance/fixed-assets/AssetLocationCombobox';
import type { FixedAssetLocationOption } from '@/types/fixed-assets';

export default function AssetTransfersPage() {
    const [transfers, setTransfers] = useState<AssetTransfer[]>([]);
    const [assets, setAssets] = useState<FixedAsset[]>([]);
    const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
    const [locationOptions, setLocationOptions] = useState<FixedAssetLocationOption[]>([]);
    const [employees, setEmployees] = useState<Employee[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [assetComboOpen, setAssetComboOpen] = useState(false);
    const { toast } = useToast();
    const [dimensionDefaults, setDimensionDefaults] = useState<Record<string, string>>({});

    // Form state
    const [formData, setFormData] = useState<Partial<RequestAssetTransferDto>>({
        transferType: 'Internal',
        transferDate: new Date().toISOString().split('T')[0],
        bookClassification: 'IFRS',
    });

    const selectedAsset = useMemo(
        () => assets.find(asset => asset.id === formData.fixedAssetId),
        [assets, formData.fixedAssetId]
    );
    const isGlReclassification = formData.transferType === 'GlReclassification';

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        // Load each data source independently so one failure doesn't block others
        try {
            const transfersData = await fixedAssetsDataService.getTransfers();
            setTransfers(transfersData || []);
        } catch (error) {
            console.error('Failed to load transfers:', error);
        }
        try {
            const assetsData = await fixedAssetsDataService.getAssets();
            // Include all non-disposed assets
            setAssets((assetsData || []).filter(a => a.status !== 'Disposed' && a.status !== 'WrittenOff'));
        } catch (error) {
            console.error('Failed to load assets:', error);
        }
        try {
            const categoriesData = await fixedAssetsDataService.getCategories();
            setCategories(categoriesData || []);
        } catch (error) {
            console.error('Failed to load fixed asset categories:', error);
        }
        try {
            const locationsData = await fixedAssetsDataService.getLocationOptions();
            setLocationOptions(locationsData || []);
        } catch (error) {
            console.error('Failed to load organization locations:', error);
        }
        try {
            const employeesData = await maintenanceDataService.getEmployees();
            setEmployees(employeesData || []);
        } catch (error) {
            console.error('Failed to load employees:', error);
        }
        setLoading(false);
    };

    const handleRequestTransfer = async (e: React.FormEvent) => {
        e.preventDefault();
        const reclassificationTargetMissing = isGlReclassification &&
            !formData.toFixedAssetCategoryId &&
            !formData.toSegmentString;
        const shortReclassificationReason = isGlReclassification &&
            (!formData.reason || formData.reason.trim().length < 20);
        if (!formData.fixedAssetId || !formData.transferDate ||
            (!isGlReclassification && !formData.toLocation) ||
            reclassificationTargetMissing || shortReclassificationReason) {
            toast({
                title: "Validation Error",
                description: isGlReclassification
                    ? "Choose a target category or segment and provide an accounting reason of at least 20 characters."
                    : "Please fill in all required fields.",
                variant: "destructive",
            });
            return;
        }

        try {
            setIsSubmitting(true);
            await fixedAssetsDataService.requestTransfer({
                ...formData,
                financeDimensions: isGlReclassification ? {
                    defaultDimensions: toFinancePostingDimensionValues(dimensionDefaults),
                    lines: [],
                    applyDefaultToEligibleLines: true,
                } : undefined,
            } as RequestAssetTransferDto);
            toast({
                title: "Success",
                description: "Asset transfer request submitted successfully.",
            });
            setIsDialogOpen(false);
            setFormData({
                transferType: 'Internal',
                transferDate: new Date().toISOString().split('T')[0],
                bookClassification: 'IFRS',
            });
            setDimensionDefaults({});
            loadData();
        } catch (error) {
            console.error('Failed to submit transfer request:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to submit transfer request.",
                variant: "destructive",
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleApprove = async (id: string) => {
        try {
            await fixedAssetsDataService.approveTransfer(id, { comments: 'Approved via UI' });
            toast({
                title: "Approved",
                description: "Transfer request has been approved and completed.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to approve transfer:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to approve transfer.",
                variant: "destructive",
            });
        }
    };

    const handleReject = async (id: string) => {
        try {
            await fixedAssetsDataService.rejectTransfer(id, { comments: 'Rejected via UI' });
            toast({
                title: "Rejected",
                description: "Transfer request has been rejected.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to reject transfer:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to reject transfer.",
                variant: "destructive",
            });
        }
    };

    const filteredTransfers = useMemo(() => {
        return (transfers || []).filter(t =>
            t.referenceNumber?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            t.fixedAssetName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            t.assetCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            t.toLocation?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            t.fromFixedAssetCategoryName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            t.toFixedAssetCategoryName?.toLowerCase().includes(searchTerm.toLowerCase())
        );
    }, [transfers, searchTerm]);

    const getStatusBadge = (status: AssetTransferStatus) => {
        switch (status) {
            case 'PendingApproval':
                return <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200 gap-1"><Clock className="h-3 w-3" /> Pending</Badge>;
            case 'Approved':
                return <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200 gap-1"><CheckCircle2 className="h-3 w-3" /> Approved</Badge>;
            case 'Completed':
                return <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200 gap-1"><CheckCircle2 className="h-3 w-3" /> Completed</Badge>;
            case 'Rejected':
                return <Badge variant="destructive" className="gap-1"><XCircle className="h-3 w-3" /> Rejected</Badge>;
            case 'Cancelled':
                return <Badge variant="secondary" className="gap-1">Cancelled</Badge>;
            default:
                return <Badge variant="outline">{status}</Badge>;
        }
    };

    if (loading && transfers.length === 0) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
            </div>
        );
    }

    return (
        <div className="p-6 space-y-6">
            <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Asset Transfers</h1>
                    <p className="text-slate-500 dark:text-slate-400 text-sm">Control physical, custody, segment, and GL classification changes through one auditable workflow.</p>
                </div>
                <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
                    <DialogTrigger asChild>
                        <Button className="bg-blue-600 hover:bg-blue-700 transition-all shadow-md active:scale-95">
                            <Plus className="h-4 w-4 mr-2" />
                            Request Transfer
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
                        <DialogHeader>
                            <DialogTitle className="text-xl">Request Asset Transfer or Reclassification</DialogTitle>
                            <DialogDescription>
                                Physical moves update custody details; GL reclassifications move current balances only after independent approval.
                            </DialogDescription>
                        </DialogHeader>
                        <form onSubmit={handleRequestTransfer} className="space-y-4 py-4">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label htmlFor="asset">Asset <span className="text-red-500">*</span></Label>
                                    <Popover open={assetComboOpen} onOpenChange={setAssetComboOpen}>
                                        <PopoverTrigger asChild>
                                            <Button
                                                variant="outline"
                                                role="combobox"
                                                aria-expanded={assetComboOpen}
                                                className="w-full justify-between font-normal"
                                            >
                                                {formData.fixedAssetId
                                                    ? (() => { const a = assets.find(a => a.id === formData.fixedAssetId); return a ? `${a.assetCode} - ${a.name}` : 'Select asset'; })()
                                                    : 'Select asset'}
                                                <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                            </Button>
                                        </PopoverTrigger>
                                        <PopoverContent className="w-[350px] p-0" align="start">
                                            <Command>
                                                <CommandInput placeholder="Search by code or name..." />
                                                <CommandList>
                                                    <CommandEmpty>No assets found.</CommandEmpty>
                                                    <CommandGroup>
                                                        {assets.map(asset => (
                                                            <CommandItem
                                                                key={asset.id}
                                                                value={`${asset.assetCode} ${asset.name}`}
                                                                onSelect={() => {
                                                                    const defaultBook = asset.bookValues?.find(book => book.bookClassification === 'IFRS') ?? asset.bookValues?.[0];
                                                                    setFormData({
                                                                        ...formData,
                                                                        fixedAssetId: asset.id,
                                                                        accountingBookId: defaultBook?.accountingBookId,
                                                                        bookClassification: defaultBook?.bookClassification ?? 'IFRS',
                                                                    });
                                                                    setAssetComboOpen(false);
                                                                }}
                                                            >
                                                                <Check className={cn("mr-2 h-4 w-4", formData.fixedAssetId === asset.id ? "opacity-100" : "opacity-0")} />
                                                                <div className="flex flex-col">
                                                                    <span className="font-medium">{asset.assetCode} - {asset.name}</span>
                                                                    <span className="text-xs text-muted-foreground">NBV: ₵ {asset.netBookValue?.toLocaleString()} | {asset.status}</span>
                                                                </div>
                                                            </CommandItem>
                                                        ))}
                                                    </CommandGroup>
                                                </CommandList>
                                            </Command>
                                        </PopoverContent>
                                    </Popover>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="transferDate">Transfer Date <span className="text-red-500">*</span></Label>
                                    <Input
                                        id="transferDate"
                                        type="date"
                                        value={formData.transferDate}
                                        onChange={(e) => setFormData({ ...formData, transferDate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="transferType">Transfer Type</Label>
                                    <Select value={formData.transferType} onValueChange={(value) => {
                                        const transferType = value as AssetTransferType;
                                        setFormData({
                                            ...formData,
                                            transferType,
                                            // Financial reclassification must not accidentally carry over physical fields
                                            // entered for a previous form mode. The API independently preserves current custody.
                                            ...(transferType === 'GlReclassification' ? { toLocation: '', toCustodianId: undefined, transferCost: undefined } : {}),
                                        });
                                    }}>
                                        <SelectTrigger id="transferType">
                                            <SelectValue placeholder="Select type" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Internal">Internal (Same Org)</SelectItem>
                                            <SelectItem value="External">External (Outside Org)</SelectItem>
                                            <SelectItem value="Custodial">Custodial (Owner Change)</SelectItem>
                                            <SelectItem value="GlReclassification">GL Reclassification (Finance)</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                                {isGlReclassification ? (
                                    <>
                                        <div className="space-y-2">
                                            <Label htmlFor="targetCategory">Target Asset Category</Label>
                                            <Select value={formData.toFixedAssetCategoryId} onValueChange={(value) => setFormData({ ...formData, toFixedAssetCategoryId: value === 'same' ? undefined : value })}>
                                                <SelectTrigger id="targetCategory"><SelectValue placeholder="Keep current category" /></SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="same">Keep current category (segment-only)</SelectItem>
                                                    {categories.map(category => (
                                                        <SelectItem key={category.id} value={category.id} disabled={category.id === selectedAsset?.fixedAssetCategoryId}>
                                                            {category.code} - {category.name}
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="accountingBook">Accounting Book</Label>
                                            <Select value={formData.accountingBookId} onValueChange={(value) => {
                                                const book = selectedAsset?.bookValues?.find(item => item.accountingBookId === value);
                                                setFormData({ ...formData, accountingBookId: value, bookClassification: book?.bookClassification ?? 'IFRS' });
                                            }}>
                                                <SelectTrigger id="accountingBook"><SelectValue placeholder="Select posting book" /></SelectTrigger>
                                                <SelectContent>
                                                    {(selectedAsset?.bookValues || []).map(book => (
                                                        <SelectItem key={book.id} value={book.accountingBookId}>
                                                            {book.accountingBookName || book.bookClassification} ({book.bookClassification})
                                                        </SelectItem>
                                                    ))}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="accountingDate">Accounting Date</Label>
                                            <Input id="accountingDate" type="date" value={formData.accountingDate || formData.transferDate} onChange={(e) => setFormData({ ...formData, accountingDate: e.target.value })} />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="targetSegment">Target Segment</Label>
                                            <Input id="targetSegment" placeholder={selectedAsset?.currentSegmentString || 'e.g. DEPT-ESTATES'} value={formData.toSegmentString || ''} onChange={(e) => setFormData({ ...formData, toSegmentString: e.target.value })} />
                                        </div>
                                        <div className="md:col-span-2 rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900">
                                            Current category: <strong>{selectedAsset?.fixedAssetCategoryName || 'Select an asset'}</strong>. The approved journal moves gross cost and the related accumulated depreciation, impairment, and revaluation reserve without rewriting historical entries.
                                        </div>
                                    </>
                                ) : (
                                    <>
                                        <div className="space-y-2">
                                            <Label htmlFor="toLocation">Destination Location <span className="text-red-500">*</span></Label>
                                            <AssetLocationCombobox
                                                id="toLocation"
                                                options={locationOptions}
                                                value={locationOptions.find(option => option.displayName === formData.toLocation)?.id}
                                                allowClear={false}
                                                onValueChange={(location) => setFormData({ ...formData, toLocation: location?.displayName || '' })}
                                            />
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="custodian">New Custodian</Label>
                                            <Select value={formData.toCustodianId} onValueChange={(val) => setFormData({ ...formData, toCustodianId: val === "none" ? undefined : val })}>
                                                <SelectTrigger id="custodian"><SelectValue placeholder="Select Employee" /></SelectTrigger>
                                                <SelectContent>
                                                    <SelectItem value="none">None</SelectItem>
                                                    {employees.map(emp => <SelectItem key={emp.id} value={emp.id}>{emp.firstName} {emp.lastName}</SelectItem>)}
                                                </SelectContent>
                                            </Select>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="cost">Estimated Cost (GHS)</Label>
                                            <Input id="cost" type="number" placeholder="0.00" value={formData.transferCost || ''} onChange={(e) => setFormData({ ...formData, transferCost: parseFloat(e.target.value) })} />
                                        </div>
                                    </>
                                )}
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="reason">Reason {isGlReclassification && <span className="text-red-500">* (minimum 20 characters)</span>}</Label>
                                <Textarea
                                    id="reason"
                                    placeholder={isGlReclassification ? "Explain the accounting classification correction and business rationale..." : "Explain why this transfer is needed..."}
                                    rows={3}
                                    value={formData.reason || ''}
                                    onChange={(e) => setFormData({ ...formData, reason: e.target.value })}
                                />
                            </div>
                            {isGlReclassification && (
                                <SourceDocumentDimensionDefaultsPanel
                                    effectiveDate={formData.accountingDate || formData.transferDate || new Date().toISOString().slice(0, 10)}
                                    values={dimensionDefaults}
                                    onChange={setDimensionDefaults}
                                    disabled={isSubmitting}
                                />
                            )}
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                                <Button type="submit" className="bg-blue-600 hover:bg-blue-700" disabled={isSubmitting}>
                                    {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <ArrowLeftRight className="h-4 w-4 mr-2" />}
                                    Submit Request
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Card className="border-slate-200 dark:border-slate-800 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-slate-500">Total Transfers</CardDescription>
                        <CardTitle className="text-2xl">{transfers.length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200 dark:border-slate-800 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-amber-500">Pending Approval</CardDescription>
                        <CardTitle className="text-2xl text-amber-600">{transfers.filter(t => t.status === 'PendingApproval').length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200 dark:border-slate-800 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-blue-500">Approved</CardDescription>
                        <CardTitle className="text-2xl text-blue-600">{transfers.filter(t => t.status === 'Approved' || t.status === 'Completed').length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200 dark:border-slate-800 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-slate-500">Total Transit Cost</CardDescription>
                        <CardTitle className="text-2xl">₵ {transfers.reduce((acc, curr) => acc + (curr.transferCost || 0), 0).toFixed(2)}</CardTitle>
                    </CardHeader>
                </Card>
            </div>

            <Card className="border-slate-200 dark:border-slate-800 shadow-sm overflow-hidden">
                <CardHeader className="bg-slate-50/50 dark:bg-slate-800/20 border-b dark:border-slate-800 pb-4">
                    <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                        <CardTitle className="text-lg font-semibold">Transfer History</CardTitle>
                        <div className="flex w-full md:w-auto gap-2">
                            <div className="relative w-full md:w-64">
                                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                                <Input
                                    placeholder="Search transfers..."
                                    className="pl-9 bg-white dark:bg-slate-900 border-slate-200 dark:border-slate-800"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                />
                            </div>
                            <Button variant="outline" size="icon">
                                <Filter className="h-4 w-4 text-slate-400" />
                            </Button>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="p-0">
                    <Table>
                        <TableHeader>
                            <TableRow className="hover:bg-transparent bg-slate-50/50 dark:bg-slate-800/50 border-b dark:border-slate-800">
                                <TableHead className="w-[120px] font-semibold">Ref #</TableHead>
                                <TableHead className="w-[120px] font-semibold">Date</TableHead>
                                <TableHead className="font-semibold">Asset</TableHead>
                                <TableHead className="font-semibold">Move Details</TableHead>
                                <TableHead className="font-semibold">Status</TableHead>
                                <TableHead className="text-right font-semibold">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredTransfers.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={6} className="h-48 text-center text-slate-400">
                                        <div className="flex flex-col items-center justify-center">
                                            <ArrowLeftRight className="h-10 w-10 mb-2 opacity-20" />
                                            <p>No asset transfers found matching your search.</p>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ) : (
                                filteredTransfers.map((transfer) => (
                                    <TableRow key={transfer.id} className="group hover:bg-slate-50/80 dark:hover:bg-slate-800/50 transition-colors border-b dark:border-slate-800">
                                        <TableCell className="font-mono text-xs font-semibold text-blue-600 dark:text-blue-400">{transfer.referenceNumber}</TableCell>
                                        <TableCell className="text-sm">{format(new Date(transfer.transferDate), 'MMM dd, yyyy')}</TableCell>
                                        <TableCell>
                                            <div className="flex flex-col">
                                                <span className="font-medium text-slate-900 dark:text-white">{transfer.fixedAssetName}</span>
                                                <span className="text-xs text-slate-500">{transfer.assetCode}</span>
                                            </div>
                                        </TableCell>
                                        <TableCell className="text-sm">
                                            <div className="flex items-center gap-2">
                                                <div className="flex flex-col min-w-[100px]">
                                                    <span className="text-xs text-slate-400 uppercase">From</span>
                                                    <span className="font-medium truncate max-w-[150px]">
                                                        {transfer.transferType === 'GlReclassification'
                                                            ? (transfer.fromFixedAssetCategoryName || transfer.fromSegmentString || 'Current')
                                                            : (transfer.fromLocation || '—')}
                                                    </span>
                                                </div>
                                                <ArrowLeftRight className="h-3 w-3 text-slate-300" />
                                                <div className="flex flex-col min-w-[100px]">
                                                    <span className="text-xs text-slate-400 uppercase">To</span>
                                                    <span className="font-medium truncate max-w-[150px]">
                                                        {transfer.transferType === 'GlReclassification'
                                                            ? (transfer.toFixedAssetCategoryName || transfer.toSegmentString || 'Reclassified')
                                                            : transfer.toLocation}
                                                    </span>
                                                    {transfer.transferType === 'GlReclassification' && (
                                                        <span className="text-xs text-slate-500">
                                                            Gross {transfer.reclassificationAssetCarryingAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                                                        </span>
                                                    )}
                                                </div>
                                            </div>
                                        </TableCell>
                                        <TableCell>{getStatusBadge(transfer.status)}</TableCell>
                                        <TableCell className="text-right">
                                            {transfer.status === 'PendingApproval' ? (
                                                <div className="flex justify-end gap-1 opacity-0 md:opacity-20 group-hover:opacity-100 transition-opacity">
                                                    <Button variant="ghost" size="sm" className="text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50 h-8 font-semibold" onClick={() => handleApprove(transfer.id)}>Approve</Button>
                                                    <Button variant="ghost" size="sm" className="text-red-600 hover:text-red-700 hover:bg-red-50 h-8 font-semibold" onClick={() => handleReject(transfer.id)}>Reject</Button>
                                                </div>
                                            ) : transfer.status === 'Approved' && transfer.transferType === 'GlReclassification' && transfer.failureReason ? (
                                                <Button variant="outline" size="sm" className="text-blue-700" title={transfer.failureReason} onClick={() => handleApprove(transfer.id)}>Retry posting</Button>
                                            ) : (
                                                <Button variant="ghost" size="icon" className="h-8 w-8 text-slate-400 hover:text-blue-600">
                                                    <Info className="h-4 w-4" />
                                                </Button>
                                            )}
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
