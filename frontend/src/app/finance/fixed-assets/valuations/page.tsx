'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { TrendingUp, Loader2, Plus, BookOpen, CheckCircle, ChevronsUpDown, Check, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Checkbox } from '@/components/ui/checkbox';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { cn } from '@/lib/utils';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import type { FixedAsset, FixedAssetCategory, AssetValuation, AssetValuationCorrection, ValuationType, CreateAssetValuationDto, CreateBulkAssetValuationDto } from '@/types/fixed-assets';
import { useToast } from '@/components/ui/use-toast';

export default function AssetValuationsPage() {
    const [assets, setAssets] = useState<FixedAsset[]>([]);
    const [categories, setCategories] = useState<FixedAssetCategory[]>([]);
    const [valuations, setValuations] = useState<AssetValuation[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const { toast } = useToast();

    // Single valuation dialog
    const [singleDialogOpen, setSingleDialogOpen] = useState(false);
    const [assetComboOpen, setAssetComboOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [singleForm, setSingleForm] = useState<Partial<CreateAssetValuationDto>>({
        valuationDate: new Date().toISOString().split('T')[0],
        valuationType: 'Revaluation' as ValuationType,
        fairValue: 0,
    });

    // Bulk valuation dialog
    const [bulkDialogOpen, setBulkDialogOpen] = useState(false);
    const [selectedAssetIds, setSelectedAssetIds] = useState<string[]>([]);
    const [categoryFilter, setCategoryFilter] = useState<string>('all');
    const [bulkForm, setBulkForm] = useState<Partial<CreateBulkAssetValuationDto>>({
        valuationDate: new Date().toISOString().split('T')[0],
        valuationType: 'Revaluation' as ValuationType,
        indexPercentage: 0,
    });

    // A posted valuation is never edited in place. This workspace deliberately mirrors the
    // request -> independent review -> compensating-journal stages enforced by the API.
    const [correctionDialogOpen, setCorrectionDialogOpen] = useState(false);
    const [correctionValuation, setCorrectionValuation] = useState<AssetValuation | null>(null);
    const [corrections, setCorrections] = useState<AssetValuationCorrection[]>([]);
    const [correctionReason, setCorrectionReason] = useState('');
    const [correctionImpact, setCorrectionImpact] = useState('');
    const [correctionReview, setCorrectionReview] = useState('');

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        try {
            const [assetsData, catsData] = await Promise.all([
                fixedAssetsDataService.getAssets(),
                fixedAssetsDataService.getCategories(),
            ]);
            setAssets(assetsData || []);
            setCategories(catsData || []);
        } catch (error) {
            console.error('Failed to load data:', error);
        }
        setLoading(false);
    };

    const loadValuationsForAsset = async (assetId: string) => {
        try {
            const data = await fixedAssetsDataService.getAssetValuations(assetId);
            setValuations(prev => {
                const filtered = prev.filter(v => v.fixedAssetId !== assetId);
                return [...filtered, ...data];
            });
        } catch (error) {
            console.error('Failed to load valuations:', error);
        }
    };

    // Load valuations for selected single asset
    useEffect(() => {
        if (singleForm.fixedAssetId) {
            loadValuationsForAsset(singleForm.fixedAssetId);
        }
    }, [singleForm.fixedAssetId]);

    const activeAssets = useMemo(() =>
        assets.filter(a => a.status === 'Active' || a.status === 'FullyDepreciated'),
        [assets]
    );

    const filteredBulkAssets = useMemo(() => {
        let filtered = activeAssets;
        if (categoryFilter !== 'all') {
            filtered = filtered.filter(a => a.fixedAssetCategoryId === categoryFilter);
        }
        return filtered;
    }, [activeAssets, categoryFilter]);

    const recentValuations = useMemo(() => {
        return valuations
            .filter(v =>
                searchTerm === '' ||
                v.assetCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
                v.assetName?.toLowerCase().includes(searchTerm.toLowerCase())
            )
            .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    }, [valuations, searchTerm]);

    const eligibleSourceImpairments = useMemo(() => {
        if (!singleForm.fixedAssetId) return [];
        return valuations.filter(v =>
            v.fixedAssetId === singleForm.fixedAssetId &&
            v.valuationType === 'Impairment' &&
            v.isPostedToGL &&
            !v.isCorrected &&
            v.impairmentLoss > valuations
                .filter(r => r.sourceImpairmentValuationId === v.id && r.isPostedToGL && !r.isCorrected)
                .reduce((sum, r) => sum + r.impairmentReversal, 0)
        );
    }, [singleForm.fixedAssetId, valuations]);

    const handleSingleValuation = async () => {
        if (!singleForm.fixedAssetId || !singleForm.valuationDate || !singleForm.fairValue) {
            toast({ title: 'Validation', description: 'Please fill all required fields.', variant: 'destructive' });
            return;
        }
        if (singleForm.valuationType === 'ImpairmentReversal' &&
            (!singleForm.sourceImpairmentValuationId || !singleForm.unimpairedCarryingAmountCap ||
             !singleForm.valuationMethod || !singleForm.valuationReportReference)) {
            toast({
                title: 'Impairment reversal evidence required',
                description: 'Select the source impairment and provide the no-impairment cap, method, and report reference.',
                variant: 'destructive'
            });
            return;
        }
        try {
            setIsSubmitting(true);
            const result = await fixedAssetsDataService.createValuation(singleForm as CreateAssetValuationDto);
            setValuations(prev => [result, ...prev]);
            toast({ title: 'Success', description: 'Valuation recorded successfully.' });
            setSingleDialogOpen(false);
            setSingleForm({ valuationDate: new Date().toISOString().split('T')[0], valuationType: 'Revaluation', fairValue: 0 });
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Failed to create valuation.', variant: 'destructive' });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleBulkValuation = async () => {
        if (selectedAssetIds.length === 0 || !bulkForm.indexPercentage) {
            toast({ title: 'Validation', description: 'Select assets and enter an index percentage.', variant: 'destructive' });
            return;
        }
        try {
            setIsSubmitting(true);
            const result = await fixedAssetsDataService.createBulkValuation({
                ...bulkForm as CreateBulkAssetValuationDto,
                fixedAssetIds: selectedAssetIds,
            });
            toast({
                title: 'Bulk Valuation Complete',
                description: `${result.successCount} of ${result.totalCount} assets revalued. ${result.failureCount > 0 ? `${result.failureCount} failed.` : ''}`,
            });
            setBulkDialogOpen(false);
            setSelectedAssetIds([]);
            setBulkForm({ valuationDate: new Date().toISOString().split('T')[0], valuationType: 'Revaluation', indexPercentage: 0 });
            // Add successful items to display
            if (result.successfulItems) {
                setValuations(prev => [...result.successfulItems, ...prev]);
            }
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Bulk valuation failed.', variant: 'destructive' });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handlePostToGL = async (valuationId: string) => {
        try {
            const result = await fixedAssetsDataService.postValuationToGL(valuationId);
            setValuations(prev => prev.map(v => v.id === valuationId ? result : v));
            toast({ title: 'Posted', description: 'Valuation journal posted to GL.' });
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'GL posting failed.', variant: 'destructive' });
        }
    };

    const openCorrectionWorkspace = async (valuation: AssetValuation) => {
        setCorrectionValuation(valuation);
        setCorrectionDialogOpen(true);
        try {
            setCorrections(await fixedAssetsDataService.getValuationCorrections(valuation.id));
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Could not load corrections.', variant: 'destructive' });
        }
    };

    const requestCorrection = async () => {
        if (!correctionValuation || correctionReason.trim().length < 10 || correctionImpact.trim().length < 20) {
            toast({ title: 'More evidence required', description: 'Provide a substantive reason and an impact assessment of at least 20 characters.', variant: 'destructive' });
            return;
        }
        try {
            setIsSubmitting(true);
            const item = await fixedAssetsDataService.requestValuationCorrection(correctionValuation.id, {
                reason: correctionReason,
                impactAssessment: correctionImpact,
            });
            setCorrections(prev => [item, ...prev]);
            setCorrectionReason('');
            setCorrectionImpact('');
            toast({ title: 'Submitted', description: 'Valuation correction submitted for independent review.' });
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Correction request failed.', variant: 'destructive' });
        } finally { setIsSubmitting(false); }
    };

    const reviewCorrection = async (item: AssetValuationCorrection, approved: boolean) => {
        if (!correctionValuation || correctionReview.trim().length < 20) {
            toast({ title: 'Review evidence required', description: 'Enter a review comment of at least 20 characters.', variant: 'destructive' });
            return;
        }
        try {
            const updated = await fixedAssetsDataService.reviewValuationCorrection(
                correctionValuation.id, item.id, { approved, reviewComment: correctionReview }
            );
            setCorrections(prev => prev.map(c => c.id === item.id ? updated : c));
            setCorrectionReview('');
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Review failed.', variant: 'destructive' });
        }
    };

    const postCorrection = async (item: AssetValuationCorrection) => {
        if (!correctionValuation) return;
        try {
            const updated = await fixedAssetsDataService.postValuationCorrection(correctionValuation.id, item.id);
            setCorrections(prev => prev.map(c => c.id === item.id ? updated : c));
            setValuations(prev => prev.map(v => v.id === correctionValuation.id
                ? { ...v, isCorrected: true, correctionId: item.id, correctedAt: updated.postedAt }
                : v));
            toast({ title: 'Corrected', description: 'Compensating journal posted and the prior book snapshot restored.' });
        } catch (error: unknown) {
            toast({ title: 'Error', description: error instanceof Error ? error.message : 'Correction posting failed.', variant: 'destructive' });
        }
    };

    const toggleAssetSelection = (assetId: string) => {
        setSelectedAssetIds(prev =>
            prev.includes(assetId) ? prev.filter(id => id !== assetId) : [...prev, assetId]
        );
    };

    const toggleAllFiltered = () => {
        const allIds = filteredBulkAssets.map(a => a.id);
        const allSelected = allIds.every(id => selectedAssetIds.includes(id));
        if (allSelected) {
            setSelectedAssetIds(prev => prev.filter(id => !allIds.includes(id)));
        } else {
            setSelectedAssetIds(prev => [...new Set([...prev, ...allIds])]);
        }
    };

    const formatMoney = (amount: number) =>
        new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(amount || 0);

    const getTypeBadge = (type: ValuationType) => {
        const m: Record<ValuationType, { variant: 'default' | 'destructive' | 'secondary'; label: string }> = {
            Revaluation: { variant: 'default', label: 'Revaluation' },
            Impairment: { variant: 'destructive', label: 'Impairment' },
            ImpairmentReversal: { variant: 'secondary', label: 'Reversal' },
        };
        const { variant, label } = m[type] || { variant: 'default' as const, label: type };
        return <Badge variant={variant}>{label}</Badge>;
    };

    if (loading) {
        return <div className="flex items-center justify-center min-h-[400px]"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
    }

    return (
        <div className="p-6 space-y-6">
            <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
                        <TrendingUp className="h-7 w-7" />
                        Asset Valuations & Impairment
                    </h1>
                    <p className="text-slate-500 dark:text-slate-400 text-sm">Revalue assets, record impairments, and post to GL.</p>
                </div>
                <div className="flex gap-2">
                    {/* Single Valuation Dialog */}
                    <Dialog open={singleDialogOpen} onOpenChange={setSingleDialogOpen}>
                        <DialogTrigger asChild>
                            <Button variant="outline"><Plus className="h-4 w-4 mr-2" />Single Valuation</Button>
                        </DialogTrigger>
                        <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
                            <DialogHeader>
                                <DialogTitle>Record Asset Valuation</DialogTitle>
                                <DialogDescription>Revalue a single asset or record an impairment loss.</DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="space-y-2">
                                        <Label>Asset *</Label>
                                        <Popover open={assetComboOpen} onOpenChange={setAssetComboOpen}>
                                            <PopoverTrigger asChild>
                                                <Button variant="outline" role="combobox" className="w-full justify-between font-normal">
                                                    {singleForm.fixedAssetId
                                                        ? (() => { const a = activeAssets.find(a => a.id === singleForm.fixedAssetId); return a ? `${a.assetCode} — ${a.name}` : 'Select'; })()
                                                        : 'Select asset'}
                                                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                                                </Button>
                                            </PopoverTrigger>
                                            <PopoverContent className="w-[350px] p-0" align="start">
                                                <Command>
                                                    <CommandInput placeholder="Search..." />
                                                    <CommandList>
                                                        <CommandEmpty>No assets found.</CommandEmpty>
                                                        <CommandGroup>
                                                            {activeAssets.map(a => (
                                                                <CommandItem key={a.id} value={`${a.assetCode} ${a.name}`} onSelect={() => { setSingleForm({ ...singleForm, fixedAssetId: a.id }); setAssetComboOpen(false); }}>
                                                                    <Check className={cn("mr-2 h-4 w-4", singleForm.fixedAssetId === a.id ? "opacity-100" : "opacity-0")} />
                                                                    <div className="flex flex-col"><span className="font-medium">{a.assetCode} — {a.name}</span><span className="text-xs text-muted-foreground">NBV: {formatMoney(a.netBookValue)}</span></div>
                                                                </CommandItem>
                                                            ))}
                                                        </CommandGroup>
                                                    </CommandList>
                                                </Command>
                                            </PopoverContent>
                                        </Popover>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Valuation Type *</Label>
                                        <Select value={singleForm.valuationType} onValueChange={(v) => setSingleForm({
                                            ...singleForm,
                                            valuationType: v as ValuationType,
                                            // Source and cap belong only to an IAS 36 reversal; clearing them
                                            // prevents stale evidence leaking into an ordinary valuation request.
                                            sourceImpairmentValuationId: undefined,
                                            unimpairedCarryingAmountCap: undefined,
                                        })}>
                                            <SelectTrigger><SelectValue /></SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="Revaluation">Revaluation (Upward/Downward)</SelectItem>
                                                <SelectItem value="Impairment">Impairment Loss</SelectItem>
                                                <SelectItem value="ImpairmentReversal">Impairment Reversal</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Valuation Date *</Label>
                                        <Input type="date" value={singleForm.valuationDate} onChange={(e) => setSingleForm({ ...singleForm, valuationDate: e.target.value })} />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Fair Value *</Label>
                                        <Input type="number" min={0} step={0.01} value={singleForm.fairValue} onChange={(e) => setSingleForm({ ...singleForm, fairValue: parseFloat(e.target.value) || 0 })} />
                                    </div>
                                    {singleForm.valuationType === 'ImpairmentReversal' && (
                                        <>
                                            <div className="space-y-2">
                                                <Label>Source Posted Impairment *</Label>
                                                <Select
                                                    value={singleForm.sourceImpairmentValuationId}
                                                    onValueChange={(value) => setSingleForm({ ...singleForm, sourceImpairmentValuationId: value })}
                                                >
                                                    <SelectTrigger><SelectValue placeholder="Select impairment to reverse" /></SelectTrigger>
                                                    <SelectContent>
                                                        {eligibleSourceImpairments.map(v => (
                                                            <SelectItem key={v.id} value={v.id}>
                                                                {new Date(v.valuationDate).toLocaleDateString()} — {formatMoney(v.impairmentLoss)}
                                                            </SelectItem>
                                                        ))}
                                                    </SelectContent>
                                                </Select>
                                                <p className="text-xs text-muted-foreground">Only posted impairments with an outstanding balance are shown.</p>
                                            </div>
                                            <div className="space-y-2">
                                                <Label>No-prior-impairment NBV cap *</Label>
                                                <Input
                                                    type="number" min={0} step={0.01}
                                                    value={singleForm.unimpairedCarryingAmountCap || ''}
                                                    onChange={(e) => setSingleForm({ ...singleForm, unimpairedCarryingAmountCap: parseFloat(e.target.value) || undefined })}
                                                />
                                                <p className="text-xs text-muted-foreground">Documented carrying amount had the impairment never occurred, net of depreciation.</p>
                                            </div>
                                        </>
                                    )}
                                    <div className="space-y-2">
                                        <Label>Revised Useful Life (Months)</Label>
                                        <Input type="number" min={0} value={singleForm.revisedUsefulLifeMonths || ''} onChange={(e) => setSingleForm({ ...singleForm, revisedUsefulLifeMonths: parseInt(e.target.value) || undefined })} placeholder="Leave blank if unchanged" />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Valuer Name</Label>
                                        <Input value={singleForm.valuerName || ''} onChange={(e) => setSingleForm({ ...singleForm, valuerName: e.target.value })} />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Valuation Method</Label>
                                        <Input value={singleForm.valuationMethod || ''} onChange={(e) => setSingleForm({ ...singleForm, valuationMethod: e.target.value })} placeholder="e.g. Market Value, DCF" />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Report Reference</Label>
                                        <Input value={singleForm.valuationReportReference || ''} onChange={(e) => setSingleForm({ ...singleForm, valuationReportReference: e.target.value })} />
                                    </div>
                                </div>
                                <div className="space-y-2">
                                    <Label>Reason / Notes</Label>
                                    <Textarea rows={2} value={singleForm.reason || ''} onChange={(e) => setSingleForm({ ...singleForm, reason: e.target.value })} />
                                </div>
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setSingleDialogOpen(false)}>Cancel</Button>
                                <Button onClick={handleSingleValuation} disabled={isSubmitting}>
                                    {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <CheckCircle className="h-4 w-4 mr-2" />}
                                    Record Valuation
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>

                    {/* Bulk Valuation Dialog */}
                    <Dialog open={bulkDialogOpen} onOpenChange={setBulkDialogOpen}>
                        <DialogTrigger asChild>
                            <Button><BookOpen className="h-4 w-4 mr-2" />Bulk Revaluation</Button>
                        </DialogTrigger>
                        <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
                            <DialogHeader>
                                <DialogTitle>Bulk Asset Revaluation</DialogTitle>
                                <DialogDescription>Apply an index percentage adjustment to multiple assets at once.</DialogDescription>
                            </DialogHeader>
                            <div className="space-y-4 py-4">
                                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                    <div className="space-y-2">
                                        <Label>Index % *</Label>
                                        <Input type="number" step={0.1} placeholder="e.g. 5 for +5%, -10 for -10%" value={bulkForm.indexPercentage} onChange={(e) => setBulkForm({ ...bulkForm, indexPercentage: parseFloat(e.target.value) || 0 })} />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Type *</Label>
                                        <Select value={bulkForm.valuationType} onValueChange={(v) => setBulkForm({ ...bulkForm, valuationType: v as ValuationType })}>
                                            <SelectTrigger><SelectValue /></SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="Revaluation">Revaluation</SelectItem>
                                                <SelectItem value="Impairment">Impairment</SelectItem>
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Valuation Date *</Label>
                                        <Input type="date" value={bulkForm.valuationDate} onChange={(e) => setBulkForm({ ...bulkForm, valuationDate: e.target.value })} />
                                    </div>
                                </div>
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div className="space-y-2"><Label>Valuer</Label><Input value={bulkForm.valuerName || ''} onChange={(e) => setBulkForm({ ...bulkForm, valuerName: e.target.value })} /></div>
                                    <div className="space-y-2"><Label>Reason</Label><Input value={bulkForm.reason || ''} onChange={(e) => setBulkForm({ ...bulkForm, reason: e.target.value })} /></div>
                                </div>

                                <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-2">
                                        <Label>Filter by Category:</Label>
                                        <Select value={categoryFilter} onValueChange={setCategoryFilter}>
                                            <SelectTrigger className="w-[200px]"><SelectValue /></SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value="all">All Categories</SelectItem>
                                                {categories.map(c => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <Button variant="outline" size="sm" onClick={toggleAllFiltered}>
                                        {filteredBulkAssets.every(a => selectedAssetIds.includes(a.id)) ? 'Deselect All' : 'Select All'}
                                    </Button>
                                </div>
                                <Card className="max-h-[250px] overflow-y-auto">
                                    <Table>
                                        <TableHeader>
                                            <TableRow>
                                                <TableHead className="w-[40px]" />
                                                <TableHead>Code</TableHead>
                                                <TableHead>Name</TableHead>
                                                <TableHead>Category</TableHead>
                                                <TableHead className="text-right">NBV</TableHead>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {filteredBulkAssets.map(a => (
                                                <TableRow key={a.id} className="cursor-pointer" onClick={() => toggleAssetSelection(a.id)}>
                                                    <TableCell>
                                                        <Checkbox checked={selectedAssetIds.includes(a.id)} onCheckedChange={() => toggleAssetSelection(a.id)} />
                                                    </TableCell>
                                                    <TableCell className="font-mono text-sm">{a.assetCode}</TableCell>
                                                    <TableCell>{a.name}</TableCell>
                                                    <TableCell>{a.fixedAssetCategoryName}</TableCell>
                                                    <TableCell className="text-right">{formatMoney(a.netBookValue)}</TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                </Card>
                                <p className="text-sm text-muted-foreground">{selectedAssetIds.length} asset(s) selected</p>
                            </div>
                            <DialogFooter>
                                <Button variant="outline" onClick={() => setBulkDialogOpen(false)}>Cancel</Button>
                                <Button onClick={handleBulkValuation} disabled={isSubmitting || selectedAssetIds.length === 0}>
                                    {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <BookOpen className="h-4 w-4 mr-2" />}
                                    Revalue {selectedAssetIds.length} Asset(s)
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/fixed-assets/dashboard">Fixed Assets</BreadcrumbLink></BreadcrumbItem>
                    <BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>Valuations</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            {/* Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Card>
                    <CardHeader className="pb-2"><CardDescription className="text-xs uppercase font-semibold">Total Valuations</CardDescription><CardTitle className="text-2xl">{recentValuations.length}</CardTitle></CardHeader>
                </Card>
                <Card>
                    <CardHeader className="pb-2"><CardDescription className="text-xs uppercase font-semibold text-amber-500">Pending GL Post</CardDescription><CardTitle className="text-2xl text-amber-600">{recentValuations.filter(v => !v.isPostedToGL).length}</CardTitle></CardHeader>
                </Card>
                <Card>
                    <CardHeader className="pb-2"><CardDescription className="text-xs uppercase font-semibold text-emerald-500">Posted to GL</CardDescription><CardTitle className="text-2xl text-emerald-600">{recentValuations.filter(v => v.isPostedToGL).length}</CardTitle></CardHeader>
                </Card>
            </div>

            {/* Valuation History Table */}
            <Card>
                <CardHeader>
                    <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                        <div>
                            <CardTitle className="text-lg">Valuation History</CardTitle>
                            <CardDescription>All recorded valuations and impairments</CardDescription>
                        </div>
                        <div className="relative w-full md:w-64">
                            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                            <Input placeholder="Search valuations..." className="pl-9" value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} />
                        </div>
                    </div>
                </CardHeader>
                <CardContent>
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>Date</TableHead>
                                <TableHead>Asset</TableHead>
                                <TableHead>Type</TableHead>
                                <TableHead className="text-right">Before</TableHead>
                                <TableHead className="text-right">Fair Value</TableHead>
                                <TableHead className="text-right">After</TableHead>
                                <TableHead className="text-right">Surplus / (Loss)</TableHead>
                                <TableHead>GL Status</TableHead>
                                <TableHead />
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {recentValuations.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={9} className="text-center py-12 text-muted-foreground">
                                        <TrendingUp className="h-10 w-10 mx-auto mb-2 opacity-20" />
                                        <p>No valuations recorded yet. Use the buttons above to record your first valuation.</p>
                                    </TableCell>
                                </TableRow>
                            ) : (
                                recentValuations.map(v => {
                                    const netEffect = v.revaluationSurplus - v.revaluationDeficit - v.impairmentLoss + v.impairmentReversal;
                                    return (
                                        <TableRow key={v.id}>
                                            <TableCell>{new Date(v.valuationDate).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })}</TableCell>
                                            <TableCell>
                                                <div className="flex flex-col">
                                                    <span className="font-medium">{v.assetName}</span>
                                                    <span className="text-xs text-muted-foreground font-mono">{v.assetCode}</span>
                                                </div>
                                            </TableCell>
                                            <TableCell>{getTypeBadge(v.valuationType)}</TableCell>
                                            <TableCell className="text-right">{formatMoney(v.carryingAmountBefore)}</TableCell>
                                            <TableCell className="text-right">{formatMoney(v.fairValue)}</TableCell>
                                            <TableCell className="text-right font-medium">{formatMoney(v.carryingAmountAfter)}</TableCell>
                                            <TableCell className={`text-right font-semibold ${netEffect >= 0 ? 'text-emerald-600' : 'text-red-600'}`}>
                                                {netEffect >= 0 ? '' : '('}{formatMoney(Math.abs(netEffect))}{netEffect < 0 ? ')' : ''}
                                            </TableCell>
                                            <TableCell>
                                                {v.isCorrected ? (
                                                    <Badge variant="outline" className="bg-slate-100 text-slate-700">Corrected</Badge>
                                                ) : v.isPostedToGL ? (
                                                    <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200 gap-1"><CheckCircle className="h-3 w-3" />Posted</Badge>
                                                ) : (
                                                    <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200">Pending</Badge>
                                                )}
                                            </TableCell>
                                            <TableCell>
                                                {!v.isPostedToGL && (
                                                    <Button size="sm" variant="outline" onClick={() => handlePostToGL(v.id)}>
                                                        Post to GL
                                                    </Button>
                                                )}
                                                {v.isPostedToGL && !v.isCorrected && (
                                                    <Button size="sm" variant="ghost" onClick={() => openCorrectionWorkspace(v)}>
                                                        Correct
                                                    </Button>
                                                )}
                                            </TableCell>
                                        </TableRow>
                                    );
                                })
                            )}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>

            <Dialog open={correctionDialogOpen} onOpenChange={setCorrectionDialogOpen}>
                <DialogContent className="sm:max-w-[760px] max-h-[90vh] overflow-y-auto">
                    <DialogHeader>
                        <DialogTitle>Controlled Valuation Correction</DialogTitle>
                        <DialogDescription>
                            {correctionValuation?.assetCode} — the original valuation and journal remain visible; posting creates a linked compensating entry.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-5">
                        {corrections.length === 0 && (
                            <Card>
                                <CardHeader><CardTitle className="text-base">Request correction</CardTitle></CardHeader>
                                <CardContent className="space-y-3">
                                    <div className="space-y-2">
                                        <Label>Reason *</Label>
                                        <Textarea value={correctionReason} onChange={e => setCorrectionReason(e.target.value)} placeholder="Explain the posting error and required correction." />
                                    </div>
                                    <div className="space-y-2">
                                        <Label>Impact assessment *</Label>
                                        <Textarea value={correctionImpact} onChange={e => setCorrectionImpact(e.target.value)} placeholder="Describe affected reports, depreciation, disposal work, and the intended repost." />
                                    </div>
                                    <Button onClick={requestCorrection} disabled={isSubmitting}>Submit for independent review</Button>
                                </CardContent>
                            </Card>
                        )}
                        {corrections.map(item => (
                            <Card key={item.id}>
                                <CardHeader className="pb-3">
                                    <div className="flex items-center justify-between gap-2">
                                        <CardTitle className="text-base">{item.requestedByUserName}</CardTitle>
                                        <Badge variant="outline">{item.status}</Badge>
                                    </div>
                                    <CardDescription>{item.reason}</CardDescription>
                                </CardHeader>
                                <CardContent className="space-y-3">
                                    <p className="text-sm">{item.impactAssessment}</p>
                                    {item.failureReason && <p className="text-sm text-red-600">Last posting error: {item.failureReason}</p>}
                                    {item.status === 'PendingApproval' && (
                                        <>
                                            <Textarea value={correctionReview} onChange={e => setCorrectionReview(e.target.value)} placeholder="Independent review comment (minimum 20 characters)" />
                                            <div className="flex gap-2">
                                                <Button size="sm" onClick={() => reviewCorrection(item, true)}>Approve</Button>
                                                <Button size="sm" variant="destructive" onClick={() => reviewCorrection(item, false)}>Reject</Button>
                                            </div>
                                        </>
                                    )}
                                    {item.status === 'Approved' && (
                                        <Button size="sm" onClick={() => postCorrection(item)}>Post compensating journal</Button>
                                    )}
                                    {item.reversalJournalEntryId && (
                                        <p className="text-xs text-muted-foreground font-mono">Reversal journal: {item.reversalJournalEntryId}</p>
                                    )}
                                </CardContent>
                            </Card>
                        ))}
                    </div>
                </DialogContent>
            </Dialog>
        </div>
    );
}
