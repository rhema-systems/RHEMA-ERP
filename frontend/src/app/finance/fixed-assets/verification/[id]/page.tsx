'use client';

import React, { useEffect, useState, useMemo } from 'react';
import { useParams, useRouter } from 'next/navigation';
import {
    ChevronLeft, Loader2, CheckCircle2, Clock, XCircle, Search, Filter,
    Activity, MapPin, ClipboardList, Camera, AlertTriangle, PlayCircle, CheckCheck
} from 'lucide-react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { format } from 'date-fns';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import {
    AssetVerificationSession,
    AssetVerificationItem,
    AssetCondition,
    VerifyAssetDto,
    VerificationSessionStatus
} from '@/types/fixed-assets';
import { useToast } from "@/components/ui/use-toast";
import { Progress } from "@/components/ui/progress";
import { AssetLocationCombobox } from '@/components/finance/fixed-assets/AssetLocationCombobox';
import type { FixedAssetLocationOption } from '@/types/fixed-assets';

export default function AuditExecutionPage() {
    const params = useParams();
    const router = useRouter();
    const sessionId = params.id as string;
    const { toast } = useToast();

    const [session, setSession] = useState<AssetVerificationSession | null>(null);
    const [items, setItems] = useState<AssetVerificationItem[]>([]);
    const [locationOptions, setLocationOptions] = useState<FixedAssetLocationOption[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [isVerifyDialogOpen, setIsVerifyDialogOpen] = useState(false);
    const [selectedItem, setSelectedItem] = useState<AssetVerificationItem | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

    // Verify form state
    const [verifyData, setVerifyData] = useState<VerifyAssetDto>({
        condition: 'Good',
        currentLocation: '',
        notes: ''
    });

    useEffect(() => {
        if (sessionId) {
            loadData();
        }
    }, [sessionId]);

    const loadData = async () => {
        try {
            setLoading(true);
            const [sessionData, itemsData, locationsData] = await Promise.all([
                fixedAssetsDataService.getVerificationSession(sessionId),
                fixedAssetsDataService.getSessionItems(sessionId),
                fixedAssetsDataService.getLocationOptions()
            ]);
            setSession(sessionData);
            setItems(itemsData || []);
            setLocationOptions(locationsData || []);
        } catch (error) {
            console.error('Failed to load audit data:', error);
            toast({
                title: "Error",
                description: "Failed to load audit session details.",
                variant: "destructive",
            });
        } finally {
            setLoading(false);
        }
    };

    const handleStartSession = async () => {
        try {
            setLoading(true);
            await fixedAssetsDataService.startVerificationSession(sessionId);
            toast({
                title: "Audit Started",
                description: "The verification cycle is now active.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to start session:', error);
            toast({
                title: "Error",
                description: "Failed to start audit session.",
                variant: "destructive",
            });
        } finally {
            setLoading(false);
        }
    };

    const handleCompleteSession = async () => {
        try {
            setLoading(true);
            await fixedAssetsDataService.completeVerificationSession(sessionId);
            toast({
                title: "Audit Completed",
                description: "The verification cycle has been closed.",
            });
            loadData();
        } catch (error) {
            console.error('Failed to complete session:', error);
            toast({
                title: "Error",
                description: "Failed to complete audit session.",
                variant: "destructive",
            });
        } finally {
            setLoading(false);
        }
    };

    const openVerifyDialog = (item: AssetVerificationItem) => {
        setSelectedItem(item);
        setVerifyData({
            condition: item.condition || 'Good',
            currentLocation: item.currentLocation || '',
            notes: item.notes || ''
        });
        setIsVerifyDialogOpen(true);
    };

    const handleVerifySubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!selectedItem) return;

        try {
            setIsSubmitting(true);
            await fixedAssetsDataService.verifyAsset(sessionId, selectedItem.fixedAssetId, verifyData);
            toast({
                title: "Asset Verified",
                description: `Verification recorded for ${selectedItem.assetCode}.`,
            });
            setIsVerifyDialogOpen(false);
            loadData();
        } catch (error) {
            console.error('Failed to verify asset:', error);
            toast({
                title: "Error",
                description: "Failed to record asset verification.",
                variant: "destructive",
            });
        } finally {
            setIsSubmitting(false);
        }
    };

    const filteredItems = useMemo(() => {
        return items.filter(i =>
            i.fixedAssetName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            i.assetCode?.toLowerCase().includes(searchTerm.toLowerCase())
        );
    }, [items, searchTerm]);

    const getStatusBadge = (status: VerificationSessionStatus) => {
        switch (status) {
            case 'Draft':
                return <Badge variant="outline" className="bg-slate-50 text-slate-700 border-slate-200">Draft</Badge>;
            case 'InProgress':
                return <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200 gap-1"><Activity className="h-3 w-3" /> In Progress</Badge>;
            case 'Completed':
                return <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200 gap-1"><CheckCircle2 className="h-3 w-3" /> Completed</Badge>;
            case 'Cancelled':
                return <Badge variant="destructive" className="gap-1"><XCircle className="h-3 w-3" /> Cancelled</Badge>;
            default:
                return <Badge variant="outline">{status}</Badge>;
        }
    };

    const getConditionBadge = (condition: AssetCondition) => {
        switch (condition) {
            case 'Excellent':
                return <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200">Excellent</Badge>;
            case 'Good':
                return <Badge variant="outline" className="bg-blue-50 text-blue-700 border-blue-200">Good</Badge>;
            case 'Fair':
                return <Badge variant="outline" className="bg-amber-50 text-amber-700 border-amber-200">Fair</Badge>;
            case 'Poor':
                return <Badge variant="outline" className="bg-orange-50 text-orange-700 border-orange-200">Poor</Badge>;
            case 'Broken':
                return <Badge variant="destructive">Broken</Badge>;
            case 'Missing':
                return <Badge variant="destructive" className="bg-red-800">Missing</Badge>;
            default:
                return <Badge variant="outline">{condition}</Badge>;
        }
    };

    if (loading && !session) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <Loader2 className="h-8 w-8 animate-spin text-slate-400" />
            </div>
        );
    }

    if (!session) return <div>Session not found</div>;

    const progressValue = (session.verifiedItems / session.totalItems) * 100 || 0;

    return (
        <div className="p-6 space-y-6">
            <div className="flex items-center gap-4">
                <Button variant="ghost" size="icon" onClick={() => router.back()}>
                    <ChevronLeft className="h-5 w-5" />
                </Button>
                <div>
                    <div className="flex items-center gap-2">
                        <h1 className="text-2xl font-bold text-slate-900 dark:text-white">{session.sessionName}</h1>
                        {getStatusBadge(session.status)}
                    </div>
                    <p className="text-slate-500 dark:text-slate-400 text-sm">Session Ref: <span className="font-mono text-indigo-600 font-semibold">{session.referenceNumber}</span> • Scheduled: {format(new Date(session.scheduledDate), 'PPP')}</p>
                </div>
                <div className="ml-auto flex gap-2">
                    {session.status === 'Draft' && (
                        <Button className="bg-amber-600 hover:bg-amber-700 text-white" onClick={handleStartSession}>
                            <PlayCircle className="h-4 w-4 mr-2" />
                            Start Audit Cycle
                        </Button>
                    )}
                    {session.status === 'InProgress' && (
                        <Button className="bg-emerald-600 hover:bg-emerald-700 text-white" onClick={handleCompleteSession}>
                            <CheckCheck className="h-4 w-4 mr-2" />
                            Complete Audit
                        </Button>
                    )}
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <Card className="md:col-span-3">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-slate-500 uppercase tracking-wider">Audit Progress</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="flex items-center gap-4">
                            <Progress value={progressValue} className="h-4 flex-1" />
                            <span className="text-xl font-bold text-indigo-600 w-16 text-right">{Math.round(progressValue)}%</span>
                        </div>
                        <div className="mt-2 flex justify-between text-sm text-slate-500">
                            <span>{session.verifiedItems} of {session.totalItems} assets verified</span>
                            {session.completionDate && (
                                <span className="flex items-center gap-1 uppercase text-[10px] font-bold text-emerald-600">
                                    <CheckCircle2 className="h-3 w-3" /> Completed on {format(new Date(session.completionDate), 'MMM dd, yyyy')}
                                </span>
                            )}
                        </div>
                    </CardContent>
                </Card>
                <Card>
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-slate-500 uppercase tracking-wider">Session Info</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="flex items-start gap-3">
                            <ClipboardList className="h-4 w-4 text-slate-400 mt-0.5" />
                            <div>
                                <p className="text-xs font-semibold uppercase text-slate-400">Total Items</p>
                                <p className="text-lg font-bold">{session.totalItems}</p>
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>

            <Card className="border-slate-200 shadow-sm">
                <CardHeader className="bg-slate-50 border-b pb-4">
                    <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                        <CardTitle className="text-lg font-semibold">Verification Ledger</CardTitle>
                        <div className="flex w-full md:w-auto gap-2">
                            <div className="relative w-full md:w-64">
                                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                                <Input
                                    placeholder="Search by code or name..."
                                    className="pl-9 bg-white"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                />
                            </div>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="p-0">
                    <Table>
                        <TableHeader>
                            <TableRow className="bg-slate-50/50">
                                <TableHead className="w-[150px] font-semibold">Asset Code</TableHead>
                                <TableHead className="font-semibold">Asset Name</TableHead>
                                <TableHead className="font-semibold">Condition</TableHead>
                                <TableHead className="font-semibold">Last Location</TableHead>
                                <TableHead className="w-[180px] font-semibold">Status</TableHead>
                                <TableHead className="text-right font-semibold">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {filteredItems.map((item) => (
                                <TableRow key={item.id} className={`group hover:bg-slate-50 transition-colors ${item.isVerified ? 'bg-emerald-50/10' : ''}`}>
                                    <TableCell className="font-mono text-xs font-bold text-slate-600">{item.assetCode}</TableCell>
                                    <TableCell>
                                        <div className="font-medium text-slate-900">{item.fixedAssetName}</div>
                                        {item.notes && <div className="text-[10px] text-slate-400 italic line-clamp-1 truncate max-w-[200px]">{item.notes}</div>}
                                    </TableCell>
                                    <TableCell>{item.isVerified ? getConditionBadge(item.condition) : <span className="text-slate-300 italic text-sm">-</span>}</TableCell>
                                    <TableCell>
                                        <div className="flex items-center gap-1.5 text-sm text-slate-600">
                                            <MapPin className="h-3.5 w-3.5 text-slate-400" />
                                            {item.currentLocation || <span className="text-slate-300 italic">Not set</span>}
                                        </div>
                                    </TableCell>
                                    <TableCell>
                                        {item.isVerified ? (
                                            <div className="flex flex-col">
                                                <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200 font-semibold w-fit">Verified</Badge>
                                                <span className="text-[10px] text-slate-400 mt-1">{item.verificationDate ? format(new Date(item.verificationDate), 'MMM dd, HH:mm') : ''}</span>
                                            </div>
                                        ) : (
                                            <Badge variant="outline" className="text-slate-400 border-slate-200">Pending</Badge>
                                        )}
                                    </TableCell>
                                    <TableCell className="text-right">
                                        <Button
                                            variant={item.isVerified ? "outline" : "default"}
                                            size="sm"
                                            className={`${!item.isVerified && session.status === 'InProgress' ? 'bg-indigo-600 hover:bg-indigo-700 active:scale-95' : ''} h-8 transition-all`}
                                            disabled={session.status !== 'InProgress'}
                                            onClick={() => openVerifyDialog(item)}
                                        >
                                            {item.isVerified ? 'Edit' : 'Verify Now'}
                                        </Button>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </CardContent>
            </Card>

            <Dialog open={isVerifyDialogOpen} onOpenChange={setIsVerifyDialogOpen}>
                <DialogContent className="sm:max-w-[450px]">
                    <DialogHeader>
                        <DialogTitle className="flex items-center gap-2">
                            <ClipboardList className="h-5 w-5 text-indigo-600" />
                            Verify Asset Condition
                        </DialogTitle>
                        <DialogDescription>
                            Recording physical state for <span className="font-bold text-slate-900">{selectedItem?.assetCode}</span>
                        </DialogDescription>
                    </DialogHeader>
                    {selectedItem && (
                        <form onSubmit={handleVerifySubmit} className="space-y-4 py-4">
                            <div className="space-y-2">
                                <Label htmlFor="condition">Current Condition <span className="text-red-500">*</span></Label>
                                <Select value={verifyData.condition} onValueChange={(val: any) => setVerifyData({ ...verifyData, condition: val })}>
                                    <SelectTrigger>
                                        <SelectValue placeholder="Select condition" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="Excellent">Excellent</SelectItem>
                                        <SelectItem value="Good">Good</SelectItem>
                                        <SelectItem value="Fair">Fair</SelectItem>
                                        <SelectItem value="Poor">Poor</SelectItem>
                                        <SelectItem value="Broken">Broken</SelectItem>
                                        <SelectItem value="Missing">Missing</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="location">Verification Location</Label>
                                <AssetLocationCombobox
                                    id="location"
                                    options={locationOptions}
                                    value={locationOptions.find(option => option.displayName === verifyData.currentLocation)?.id}
                                    placeholder={verifyData.currentLocation ? `Legacy: ${verifyData.currentLocation}` : undefined}
                                    onValueChange={(location) => setVerifyData({ ...verifyData, currentLocation: location?.displayName })}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="notes">Audit Notes</Label>
                                <Textarea
                                    id="notes"
                                    placeholder="Any observations, tags missing, or damages found..."
                                    rows={3}
                                    value={verifyData.notes || ''}
                                    onChange={(e) => setVerifyData({ ...verifyData, notes: e.target.value })}
                                />
                            </div>

                            {verifyData.condition === 'Missing' && (
                                <div className="p-3 bg-red-50 border border-red-100 rounded-lg flex gap-3 text-red-700 text-xs">
                                    <AlertTriangle className="h-4 w-4 flex-shrink-0" />
                                    <p>Marking an asset as missing will flag it for security review and potential write-off in the next financial period.</p>
                                </div>
                            )}

                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsVerifyDialogOpen(false)}>Close</Button>
                                <Button type="submit" className="bg-indigo-600 hover:bg-indigo-700" disabled={isSubmitting}>
                                    {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <CheckCircle2 className="h-4 w-4 mr-2" />}
                                    Save Verification
                                </Button>
                            </DialogFooter>
                        </form>
                    )}
                </DialogContent>
            </Dialog>
        </div>
    );
}
