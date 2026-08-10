'use client';

import React, { useEffect, useState, useMemo } from 'react';
import { Trash2, Plus, Loader2, CheckCircle2, XCircle, Clock, Info, Search, Filter, TrendingDown, ChevronsUpDown, Check } from 'lucide-react';
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
import { AssetDisposal, AssetDisposalStatus, RequestAssetDisposalDto, FixedAsset } from '@/types/fixed-assets';
import { useToast } from "@/components/ui/use-toast";

export default function AssetDisposalsPage() {
    const [disposals, setDisposals] = useState<AssetDisposal[]>([]);
    const [assets, setAssets] = useState<FixedAsset[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [assetComboOpen, setAssetComboOpen] = useState(false);
    const { toast } = useToast();

    // Form state
    const [formData, setFormData] = useState<Partial<RequestAssetDisposalDto>>({
        disposalType: 'Sale',
        disposalDate: new Date().toISOString().split('T')[0],
        saleProceeds: 0,
        disposalCost: 0
    });

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        // Load each data source independently so one failure doesn't block others
        try {
            const disposalsData = await fixedAssetsDataService.getDisposals();
            setDisposals(disposalsData || []);
        } catch (error) {
            console.error('Failed to load disposals:', error);
        }
        try {
            const assetsData = await fixedAssetsDataService.getAssets();
            // Include all non-disposed assets (Draft, Active, FullyDepreciated, etc.)
            setAssets((assetsData || []).filter(a => a.status !== 'Disposed' && a.status !== 'WrittenOff'));
        } catch (error) {
            console.error('Failed to load assets:', error);
        }
        setLoading(false);
    };

    const handleRequestDisposal = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!formData.fixedAssetId || !formData.disposalDate) {
            toast({
                title: "Validation Error",
                description: "Please fill in all required fields.",
                variant: "destructive",
            });
            return;
        }
        const selectedAssetForRequest = assets.find(asset => asset.id === formData.fixedAssetId);
        if (selectedAssetForRequest?.depreciationMethod === 'UnitsOfProduction' &&
            (!formData.finalDepreciationProductionUnits || !formData.finalDepreciationEvidenceReference?.trim())) {
            toast({
                title: "Usage evidence required",
                description: "Enter disposal-period production units and a meter reading or production-report reference.",
                variant: "destructive",
            });
            return;
        }

        try {
            setIsSubmitting(true);
            await fixedAssetsDataService.requestDisposal(formData as RequestAssetDisposalDto);
            toast({
                title: "Success",
                description: "Asset disposal request submitted successfully.",
            });
            setIsDialogOpen(false);
            setFormData({
                disposalType: 'Sale',
                disposalDate: new Date().toISOString().split('T')[0],
                saleProceeds: 0,
                disposalCost: 0
            });
            loadData();
        } catch (error) {
            console.error('Failed to submit disposal request:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to submit disposal request.",
                variant: "destructive",
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const handleApprove = async (id: string) => {
        try {
            await fixedAssetsDataService.approveDisposal(id, { comments: 'Approved via UI' });
            toast({
                title: "Approved",
                description: "Disposal request has been approved and completed.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to approve disposal:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to approve disposal.",
                variant: "destructive",
            });
        }
    };

    const handleReject = async (id: string) => {
        try {
            await fixedAssetsDataService.rejectDisposal(id, { comments: 'Rejected via UI' });
            toast({
                title: "Rejected",
                description: "Disposal request has been rejected.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to reject disposal:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to reject disposal.",
                variant: "destructive",
            });
        }
    };

    const handleComplete = async (id: string) => {
        try {
            await fixedAssetsDataService.completeDisposal(id);
            toast({
                title: "Completed",
                description: "Disposal has been completed and GL entries posted.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to complete disposal:', error);
            toast({
                title: "Error",
                description: error instanceof Error ? error.message : "Failed to complete disposal.",
                variant: "destructive",
            });
        }
    };

    const filteredDisposals = useMemo(() => {
        return (disposals || []).filter(d =>
            d.referenceNumber?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            d.fixedAssetName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            d.assetCode?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            d.buyerName?.toLowerCase().includes(searchTerm.toLowerCase())
        );
    }, [disposals, searchTerm]);

    const getStatusBadge = (status: AssetDisposalStatus) => {
        switch (status) {
            case 'PendingApproval':
                return <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200 gap-1"><Clock className="h-3 w-3" /> Pending</Badge>;
            case 'Approved':
                return <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200 gap-1"><CheckCircle2 className="h-3 w-3" /> Approved</Badge>;
            case 'Completed':
                return <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200 gap-1"><CheckCircle2 className="h-3 w-3" /> Completed</Badge>;
            case 'Rejected':
                return <Badge variant="destructive" className="gap-1"><XCircle className="h-3 w-3" /> Rejected</Badge>;
            default:
                return <Badge variant="outline">{status}</Badge>;
        }
    };

    const selectedAssetNBV = useMemo(() => {
        if (!formData.fixedAssetId) return 0;
        const asset = assets.find(a => a.id === formData.fixedAssetId);
        return asset?.netBookValue || 0;
    }, [formData.fixedAssetId, assets]);

    const selectedAsset = useMemo(
        () => assets.find(asset => asset.id === formData.fixedAssetId),
        [formData.fixedAssetId, assets]
    );

    const estimatedGainLoss = useMemo(() => {
        const proceeds = formData.saleProceeds || 0;
        const cost = formData.disposalCost || 0;
        return (proceeds - cost) - selectedAssetNBV;
    }, [formData.saleProceeds, formData.disposalCost, selectedAssetNBV]);

    if (loading && disposals.length === 0) {
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
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Asset Disposals</h1>
                    <p className="text-slate-500 dark:text-slate-400 text-sm">Control depreciation through the disposal date, derecognition, gain/loss, proceeds, and direct revaluation-reserve transfer within equity.</p>
                </div>
                <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
                    <DialogTrigger asChild>
                        <Button className="bg-red-600 hover:bg-red-700 text-white shadow-md active:scale-95">
                            <Trash2 className="h-4 w-4 mr-2" />
                            Dispose Asset
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
                        <DialogHeader>
                            <DialogTitle className="text-xl">Request Asset Disposal</DialogTitle>
                            <DialogDescription>
                                Initiate the retirement or sale of a fixed asset. Finance calculates final depreciation through the disposal date and includes it in the same approved posting as derecognition.
                            </DialogDescription>
                        </DialogHeader>
                        <form onSubmit={handleRequestDisposal} className="space-y-4 py-4">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div className="space-y-2">
                                    <Label htmlFor="asset">Asset to Dispose <span className="text-red-500">*</span></Label>
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
                                                                    setFormData({ ...formData, fixedAssetId: asset.id });
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
                                    {formData.fixedAssetId && (
                                        <p className="text-xs text-slate-500">Current NBV: ₵ {selectedAssetNBV.toLocaleString()}</p>
                                    )}
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="disposalDate">Disposal Date <span className="text-red-500">*</span></Label>
                                    <Input
                                        id="disposalDate"
                                        type="date"
                                        value={formData.disposalDate}
                                        onChange={(e) => setFormData({ ...formData, disposalDate: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="disposalType">Disposal Method</Label>
                                    <Select value={formData.disposalType} onValueChange={(val: any) => setFormData({ ...formData, disposalType: val })}>
                                        <SelectTrigger id="disposalType">
                                            <SelectValue placeholder="Select method" />
                                        </SelectTrigger>
                                        <SelectContent>
                                            <SelectItem value="Sale">Sale</SelectItem>
                                            <SelectItem value="Scrap">Scrap</SelectItem>
                                            <SelectItem value="Donation">Donation</SelectItem>
                                            <SelectItem value="DamageTheft">Damage / Theft</SelectItem>
                                        </SelectContent>
                                    </Select>
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="buyer">Buyer / Recipient Name</Label>
                                    <Input
                                        id="buyer"
                                        placeholder="Company or Person"
                                        value={formData.buyerName || ''}
                                        onChange={(e) => setFormData({ ...formData, buyerName: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="proceeds">Sale Proceeds (GHS)</Label>
                                    <Input
                                        id="proceeds"
                                        type="number"
                                        placeholder="0.00"
                                        value={formData.saleProceeds}
                                        onChange={(e) => setFormData({ ...formData, saleProceeds: parseFloat(e.target.value) || 0 })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="cost">Disposal Cost (GHS)</Label>
                                    <Input
                                        id="cost"
                                        type="number"
                                        placeholder="0.00"
                                        value={formData.disposalCost}
                                        onChange={(e) => setFormData({ ...formData, disposalCost: parseFloat(e.target.value) || 0 })}
                                    />
                                </div>
                            </div>

                            {selectedAsset?.depreciationMethod === 'UnitsOfProduction' ? (
                                <Card className="border-blue-200 bg-blue-50/70">
                                    <CardContent className="pt-4 space-y-3">
                                        <p className="text-sm text-blue-900">
                                            This asset uses units of production. The checker approves the verified usage and final charge together with the disposal.
                                        </p>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                                            <div className="space-y-2">
                                                <Label htmlFor="finalProductionUnits">Disposal-period units <span className="text-red-500">*</span></Label>
                                                <Input id="finalProductionUnits" type="number" min="0" step="0.0001"
                                                    value={formData.finalDepreciationProductionUnits ?? ''}
                                                    onChange={(e) => setFormData({ ...formData, finalDepreciationProductionUnits: Number(e.target.value) })} />
                                            </div>
                                            <div className="space-y-2">
                                                <Label htmlFor="finalEvidenceReference">Evidence reference <span className="text-red-500">*</span></Label>
                                                <Input id="finalEvidenceReference" placeholder="Meter reading / production report"
                                                    value={formData.finalDepreciationEvidenceReference ?? ''}
                                                    onChange={(e) => setFormData({ ...formData, finalDepreciationEvidenceReference: e.target.value })} />
                                            </div>
                                        </div>
                                        <div className="space-y-2">
                                            <Label htmlFor="finalEvidenceNotes">Evidence notes</Label>
                                            <Textarea id="finalEvidenceNotes" rows={2}
                                                value={formData.finalDepreciationEvidenceNotes ?? ''}
                                                onChange={(e) => setFormData({ ...formData, finalDepreciationEvidenceNotes: e.target.value })} />
                                        </div>
                                    </CardContent>
                                </Card>
                            ) : (
                                <p className="text-xs text-slate-500">
                                    Finance prorates the current period&apos;s depreciation by actual inclusive days through the selected disposal date. The approved amount appears in Disposal History.
                                </p>
                            )}

                            <Card className={estimatedGainLoss >= 0 ? "bg-emerald-50 border-emerald-100" : "bg-red-50 border-red-100"}>
                                <CardContent className="py-3 flex justify-between items-center">
                                    <div className="text-sm font-medium text-slate-700">Estimated {estimatedGainLoss >= 0 ? 'Gain' : 'Loss'}:</div>
                                    <div className={`text-lg font-bold ${estimatedGainLoss >= 0 ? 'text-emerald-700' : 'text-red-700'}`}>
                                        ₵ {Math.abs(estimatedGainLoss).toLocaleString()}
                                    </div>
                                </CardContent>
                            </Card>

                            <div className="space-y-2">
                                <Label htmlFor="reason">Reason for Disposal</Label>
                                <Textarea
                                    id="reason"
                                    placeholder="Provide justification for disposing of this asset..."
                                    rows={3}
                                    value={formData.reason || ''}
                                    onChange={(e) => setFormData({ ...formData, reason: e.target.value })}
                                />
                            </div>

                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                                <Button type="submit" className="bg-red-600 hover:bg-red-700" disabled={isSubmitting}>
                                    {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <TrendingDown className="h-4 w-4 mr-2" />}
                                    Submit Request
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-slate-500">Total Retired</CardDescription>
                        <CardTitle className="text-2xl">{disposals.filter(d => d.status === 'Completed').length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-amber-500">Pending Approval</CardDescription>
                        <CardTitle className="text-2xl text-amber-600">{disposals.filter(d => d.status === 'PendingApproval').length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-emerald-500">Total Proceeds</CardDescription>
                        <CardTitle className="text-2xl text-emerald-600">₵ {disposals.reduce((acc, curr) => acc + (curr.saleProceeds || 0), 0).toLocaleString()}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-blue-500">Net Gain/Loss</CardDescription>
                        <CardTitle className={`text-2xl ${disposals.reduce((a, c) => a + c.gainOrLoss, 0) >= 0 ? 'text-emerald-600' : 'text-red-600'}`}>
                            ₵ {disposals.reduce((acc, curr) => acc + curr.gainOrLoss, 0).toLocaleString()}
                        </CardTitle>
                    </CardHeader>
                </Card>
            </div>

            <Card className="border-slate-200 shadow-sm overflow-hidden">
                <CardHeader className="bg-slate-50 border-b pb-4">
                    <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                        <CardTitle className="text-lg font-semibold">Disposal History</CardTitle>
                        <div className="flex w-full md:w-auto gap-2">
                            <div className="relative w-full md:w-64">
                                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                                <Input
                                    placeholder="Search disposals..."
                                    className="pl-9 bg-white"
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
                            <TableRow className="bg-slate-50/50">
                                <TableHead className="w-[120px] font-semibold">Ref #</TableHead>
                                <TableHead className="w-[120px] font-semibold">Date</TableHead>
                                <TableHead className="font-semibold">Asset</TableHead>
                                <TableHead className="font-semibold">Method</TableHead>
                                <TableHead className="font-semibold text-right">NBV</TableHead>
                                <TableHead className="font-semibold text-right">Final Depreciation</TableHead>
                                <TableHead className="font-semibold text-right">Proceeds</TableHead>
                                <TableHead className="font-semibold text-right">Gain/Loss</TableHead>
                                <TableHead className="font-semibold text-right">Equity Transfer</TableHead>
                                <TableHead className="font-semibold">Status</TableHead>
                                <TableHead className="text-right font-semibold">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredDisposals.length === 0 ? (
                                <TableRow>
                                    <TableCell colSpan={11} className="h-48 text-center text-slate-400">
                                        <div className="flex flex-col items-center justify-center">
                                            <Trash2 className="h-10 w-10 mb-2 opacity-20" />
                                            <p>No asset disposals found matching your search.</p>
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ) : (
                                filteredDisposals.map((disposal) => (
                                    <TableRow key={disposal.id} className="group hover:bg-slate-50 transition-colors">
                                        <TableCell className="font-mono text-xs font-semibold text-red-600">{disposal.referenceNumber}</TableCell>
                                        <TableCell className="text-sm">{format(new Date(disposal.disposalDate), 'MMM dd, yyyy')}</TableCell>
                                        <TableCell>
                                            <div className="flex flex-col">
                                                <span className="font-medium">{disposal.fixedAssetName}</span>
                                                <span className="text-xs text-slate-500">{disposal.assetCode}</span>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <Badge variant="secondary" className="font-normal capitalize">{disposal.disposalType.toLowerCase()}</Badge>
                                        </TableCell>
                                        <TableCell className="text-right text-sm">₵ {disposal.netBookValueAtDisposal.toLocaleString()}</TableCell>
                                        <TableCell className="text-right text-sm">
                                            {disposal.finalDepreciationAmount > 0 ? (
                                                <div className="flex flex-col">
                                                    <span className="font-semibold text-blue-700">₵ {disposal.finalDepreciationAmount.toLocaleString()}</span>
                                                    <span className="text-[11px] text-slate-500">{disposal.finalDepreciationProrationBasis}</span>
                                                </div>
                                            ) : '—'}
                                        </TableCell>
                                        <TableCell className="text-right text-sm">₵ {disposal.saleProceeds.toLocaleString()}</TableCell>
                                        <TableCell className={`text-right text-sm font-semibold ${disposal.gainOrLoss >= 0 ? 'text-emerald-600' : 'text-red-600'}`}>
                                            ₵ {Math.abs(disposal.gainOrLoss).toLocaleString()}
                                        </TableCell>
                                        <TableCell className="text-right text-sm">
                                            {disposal.revaluationSurplusTransferAmount > 0 ? (
                                                <div className="flex flex-col">
                                                    <span className="font-semibold text-indigo-700">₵ {disposal.revaluationSurplusTransferAmount.toLocaleString()}</span>
                                                    <span className="text-[11px] text-slate-500">reserve → retained earnings</span>
                                                </div>
                                            ) : '—'}
                                        </TableCell>
                                        <TableCell>{getStatusBadge(disposal.status)}</TableCell>
                                        <TableCell className="text-right">
                                            {disposal.status === 'PendingApproval' ? (
                                                <div className="flex justify-end gap-1 opacity-100 transition-opacity">
                                                    <Button variant="ghost" size="sm" className="text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50 h-8 font-semibold" onClick={() => handleApprove(disposal.id)}>Approve</Button>
                                                    <Button variant="ghost" size="sm" className="text-red-600 hover:text-red-700 hover:bg-red-50 h-8 font-semibold" onClick={() => handleReject(disposal.id)}>Reject</Button>
                                                </div>
                                            ) : disposal.status === 'Approved' ? (
                                                <Button variant="outline" size="sm" className="text-blue-600 hover:text-blue-700 hover:bg-blue-50 h-8 font-semibold" onClick={() => handleComplete(disposal.id)}>Complete</Button>
                                            ) : (
                                                <Button variant="ghost" size="icon" className="h-8 w-8 text-slate-400">
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
