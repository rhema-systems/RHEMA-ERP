'use client';

import React, { useEffect, useState, useMemo } from 'react';
import { FileCheck, Activity, Plus, Loader2, CheckCircle2, Clock, XCircle, Search, Filter, Calendar, BarChart3, ChevronRight } from 'lucide-react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { format } from 'date-fns';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { AssetVerificationSession, VerificationSessionStatus, CreateAssetVerificationSessionDto } from '@/types/fixed-assets';
import { useToast } from "@/components/ui/use-toast";
import Link from 'next/link';
import { Progress } from "@/components/ui/progress";

export default function AssetVerificationPage() {
    const [sessions, setSessions] = useState<AssetVerificationSession[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchTerm, setSearchTerm] = useState('');
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const { toast } = useToast();

    // Form state
    const [formData, setFormData] = useState<Partial<CreateAssetVerificationSessionDto>>({
        sessionName: '',
        scheduledDate: new Date().toISOString().split('T')[0],
        description: ''
    });

    useEffect(() => {
        loadSessions();
    }, []);

    const loadSessions = async () => {
        try {
            setLoading(true);
            const data = await fixedAssetsDataService.getVerificationSessions();
            setSessions(data || []);
        } catch (error) {
            console.error('Failed to load verification sessions:', error);
            toast({
                title: "Error",
                description: "Failed to load verification sessions.",
                variant: "destructive",
            });
        } finally {
            setLoading(false);
        }
    };

    const handleCreateSession = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!formData.sessionName || !formData.scheduledDate) {
            toast({
                title: "Validation Error",
                description: "Please provide a name and date for the audit session.",
                variant: "destructive",
            });
            return;
        }

        try {
            setIsSubmitting(true);
            await fixedAssetsDataService.createVerificationSession(formData as CreateAssetVerificationSessionDto);
            toast({
                title: "Success",
                description: "New verification session created successfully.",
            });
            setIsDialogOpen(false);
            setFormData({
                sessionName: '',
                scheduledDate: new Date().toISOString().split('T')[0],
                description: ''
            });
            loadSessions();
        } catch (error) {
            console.error('Failed to create session:', error);
            toast({
                title: "Error",
                description: "Failed to create verification session.",
                variant: "destructive",
            });
        } finally {
            setIsSubmitting(false);
        }
    };

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

    const filteredSessions = useMemo(() => {
        return sessions.filter(s =>
            s.sessionName.toLowerCase().includes(searchTerm.toLowerCase()) ||
            s.referenceNumber.toLowerCase().includes(searchTerm.toLowerCase())
        );
    }, [sessions, searchTerm]);

    if (loading && sessions.length === 0) {
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
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Physical Verification</h1>
                    <p className="text-slate-500 dark:text-slate-400 text-sm">Schedule and record physical audits of organization assets.</p>
                </div>
                <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
                    <DialogTrigger asChild>
                        <Button className="bg-indigo-600 hover:bg-indigo-700 text-white shadow-md active:scale-95">
                            <Plus className="h-4 w-4 mr-2" />
                            New Audit Cycle
                        </Button>
                    </DialogTrigger>
                    <DialogContent className="sm:max-w-[500px]">
                        <DialogHeader>
                            <DialogTitle>Create Audit Cycle</DialogTitle>
                            <DialogDescription>
                                Set up a new physical verification session for assets.
                            </DialogDescription>
                        </DialogHeader>
                        <form onSubmit={handleCreateSession} className="space-y-4 py-4">
                            <div className="space-y-2">
                                <Label htmlFor="name">Cycle Name <span className="text-red-500">*</span></Label>
                                <Input
                                    id="name"
                                    placeholder="e.g. 2024 Q3 General Audit"
                                    value={formData.sessionName}
                                    onChange={(e) => setFormData({ ...formData, sessionName: e.target.value })}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="date">Scheduled Date <span className="text-red-500">*</span></Label>
                                <Input
                                    id="date"
                                    type="date"
                                    value={formData.scheduledDate}
                                    onChange={(e) => setFormData({ ...formData, scheduledDate: e.target.value })}
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="description">Description</Label>
                                <Textarea
                                    id="description"
                                    placeholder="Scope and objectives of this audit..."
                                    value={formData.description}
                                    onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                />
                            </div>
                            <DialogFooter>
                                <Button type="button" variant="outline" onClick={() => setIsDialogOpen(false)}>Cancel</Button>
                                <Button type="submit" className="bg-indigo-600 hover:bg-indigo-700" disabled={isSubmitting}>
                                    {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <FileCheck className="h-4 w-4 mr-2" />}
                                    Create Cycle
                                </Button>
                            </DialogFooter>
                        </form>
                    </DialogContent>
                </Dialog>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Card className="border-slate-200">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-slate-500">Active Cycles</CardDescription>
                        <CardTitle className="text-2xl">{sessions.filter(s => s.status === 'InProgress').length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-emerald-500">Completed Sessions</CardDescription>
                        <CardTitle className="text-2xl text-emerald-600">{sessions.filter(s => s.status === 'Completed').length}</CardTitle>
                    </CardHeader>
                </Card>
                <Card className="border-slate-200">
                    <CardHeader className="pb-2">
                        <CardDescription className="text-xs uppercase font-semibold text-indigo-500">Verification Rate</CardDescription>
                        <CardTitle className="text-2xl text-indigo-600">
                            {sessions.length > 0
                                ? Math.round((sessions.reduce((a, b) => a + b.verifiedItems, 0) / sessions.reduce((a, b) => a + b.totalItems, 0)) * 100) || 0
                                : 0}%
                        </CardTitle>
                    </CardHeader>
                </Card>
            </div>

            <Card className="border-slate-200 shadow-sm overflow-hidden">
                <CardHeader className="bg-slate-50 border-b pb-4">
                    <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                        <CardTitle className="text-lg font-semibold">Audit Sessions</CardTitle>
                        <div className="flex w-full md:w-auto gap-2">
                            <div className="relative w-full md:w-64">
                                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                                <Input
                                    placeholder="Search cycles..."
                                    className="pl-9 bg-white"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                />
                            </div>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="p-0">
                    {filteredSessions.length === 0 ? (
                        <div className="flex flex-col items-center justify-center py-24 text-slate-400">
                            <Activity className="h-12 w-12 mb-2 opacity-20" />
                            <p>No verification cycles found.</p>
                            <Button variant="ghost" className="mt-2 text-indigo-600" onClick={() => setIsDialogOpen(true)}>Create your first cycle</Button>
                        </div>
                    ) : (
                        <Table>
                            <TableHeader>
                                <TableRow className="bg-slate-50/50">
                                    <TableHead className="w-[120px] font-semibold">Ref #</TableHead>
                                    <TableHead className="font-semibold">Session Name</TableHead>
                                    <TableHead className="font-semibold text-center">Date</TableHead>
                                    <TableHead className="font-semibold">Progress</TableHead>
                                    <TableHead className="font-semibold">Status</TableHead>
                                    <TableHead className="text-right font-semibold">Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {filteredSessions.map((session) => (
                                    <TableRow key={session.id} className="group hover:bg-slate-50 transition-colors">
                                        <TableCell className="font-mono text-xs font-semibold text-indigo-600">{session.referenceNumber}</TableCell>
                                        <TableCell className="font-medium">{session.sessionName}</TableCell>
                                        <TableCell className="text-center text-sm">
                                            <div className="flex flex-col items-center">
                                                <span className="flex items-center gap-1 text-slate-600"><Calendar className="h-3 w-3" /> {format(new Date(session.scheduledDate), 'MMM dd, yyyy')}</span>
                                            </div>
                                        </TableCell>
                                        <TableCell>
                                            <div className="w-full max-w-[150px] space-y-1">
                                                <div className="flex justify-between text-[10px] font-medium text-slate-500 uppercase">
                                                    <span>{session.verifiedItems}/{session.totalItems} verified</span>
                                                    <span>{Math.round((session.verifiedItems / session.totalItems) * 100) || 0}%</span>
                                                </div>
                                                <Progress value={(session.verifiedItems / session.totalItems) * 100} className="h-1.5" />
                                            </div>
                                        </TableCell>
                                        <TableCell>{getStatusBadge(session.status)}</TableCell>
                                        <TableCell className="text-right">
                                            <Link href={`/finance/fixed-assets/verification/${session.id}`}>
                                                <Button variant="outline" size="sm" className="h-8 border-indigo-200 text-indigo-600 hover:bg-indigo-50 hover:text-indigo-700 font-semibold group-hover:scale-105 transition-transform">
                                                    Manage Audit
                                                    <ChevronRight className="h-4 w-4 ml-1" />
                                                </Button>
                                            </Link>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    )}
                </CardContent>
            </Card>
        </div>
    );
}
