import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Lock, Unlock, AlertTriangle, ShieldCheck } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FiscalPeriod, ModuleDefinition, PeriodModuleLock } from '@/types/finance';

interface ModuleLockManagerProps {
    period: FiscalPeriod;
    modules: ModuleDefinition[];
    onUpdate: () => void;
}

export function ModuleLockManager({ period, modules, onUpdate }: ModuleLockManagerProps) {
    const { toast } = useToast();
    const [processing, setProcessing] = useState<string | null>(null);
    const [reason, setReason] = useState('');
    const [selectedModule, setSelectedModule] = useState<ModuleDefinition | null>(null);
    const [actionType, setActionType] = useState<'lock' | 'unlock' | null>(null);
    const [isOpen, setIsOpen] = useState(false);

    const getLockStatus = (moduleCode: string): PeriodModuleLock | undefined => {
        return period.moduleLocks?.find(l => l.moduleCode === moduleCode && l.isLocked);
    };

    const handleAction = async () => {
        if (!selectedModule || !actionType) return;

        try {
            setProcessing(selectedModule.moduleCode);

            if (actionType === 'lock') {
                if (!reason) {
                    toast({
                        title: "Reason required",
                        description: "Please provide a reason for locking this module.",
                        variant: "destructive"
                    });
                    setProcessing(null);
                    return;
                }
                await financeDataService.lockPeriodForModule(period.id, selectedModule.moduleCode, reason);
                toast({ title: "Module Locked", description: `${selectedModule.moduleName} has been locked for this period.` });
            } else {
                await financeDataService.unlockPeriodForModule(period.id, selectedModule.moduleCode, reason || 'Manual unlock');
                toast({ title: "Module Unlocked", description: `${selectedModule.moduleName} has been unlocked.` });
            }

            onUpdate();
            setReason('');
            setSelectedModule(null);
            setActionType(null);
        } catch (error: any) {
            toast({
                title: "Error",
                description: error.message || "Failed to update module lock status",
                variant: "destructive"
            });
        } finally {
            setProcessing(null);
        }
    };

    return (
        <Dialog open={isOpen} onOpenChange={setIsOpen}>
            <DialogTrigger asChild>
                <Button variant="outline" size="sm" className="gap-2">
                    <ShieldCheck className="h-4 w-4" />
                    Manage Module Locks
                </Button>
            </DialogTrigger>
            <DialogContent className="max-w-2xl">
                <DialogHeader>
                    <DialogTitle>Module Locks - {period.periodName}</DialogTitle>
                    <DialogDescription>
                        Manage access to specific modules for this fiscal period.
                        Global period status: <Badge variant={period.isLocked ? "destructive" : "secondary"}>{period.status}</Badge>
                    </DialogDescription>
                </DialogHeader>

                <div className="py-4">
                    <div className="grid grid-cols-12 gap-4 font-medium text-sm text-muted-foreground mb-2 px-2">
                        <div className="col-span-5">Module</div>
                        <div className="col-span-3">Status</div>
                        <div className="col-span-4 text-right">Action</div>
                    </div>
                    <ScrollArea className="h-[300px] border rounded-md p-2">
                        <div className="space-y-2">
                            {modules.map((module) => {
                                const lock = getLockStatus(module.moduleCode);
                                const isLocked = !!lock;

                                return (
                                    <div key={module.id} className="grid grid-cols-12 gap-4 items-center p-2 rounded hover:bg-muted/50 transition-colors">
                                        <div className="col-span-5">
                                            <div className="font-medium">{module.moduleName}</div>
                                            <div className="text-xs text-muted-foreground">{module.moduleCode}</div>
                                        </div>
                                        <div className="col-span-3">
                                            {isLocked ? (
                                                <Badge variant="destructive" className="gap-1">
                                                    <Lock className="h-3 w-3" /> Locked
                                                </Badge>
                                            ) : (
                                                <Badge variant="outline" className="gap-1 bg-green-50 text-green-700 border-green-200">
                                                    <Unlock className="h-3 w-3" /> Open
                                                </Badge>
                                            )}
                                        </div>
                                        <div className="col-span-4 text-right">
                                            {isLocked ? (
                                                <Dialog>
                                                    <DialogTrigger asChild>
                                                        <Button
                                                            variant="ghost"
                                                            size="sm"
                                                            className="h-8 text-orange-600 hover:text-orange-700 hover:bg-orange-50"
                                                            onClick={() => {
                                                                setSelectedModule(module);
                                                                setActionType('unlock');
                                                                setReason('');
                                                            }}
                                                        >
                                                            Unlock
                                                        </Button>
                                                    </DialogTrigger>
                                                    <DialogContent>
                                                        <DialogHeader>
                                                            <DialogTitle>Unlock {module.moduleName}?</DialogTitle>
                                                            <DialogDescription>
                                                                This will allow transactions from {module.moduleName} to be posted to this period again.
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-4 py-4">
                                                            <div className="space-y-2">
                                                                <Label>Reason for unlocking (Optional)</Label>
                                                                <Input
                                                                    placeholder="e.g. Corrections needed"
                                                                    value={reason}
                                                                    onChange={(e) => setReason(e.target.value)}
                                                                />
                                                            </div>
                                                            {lock?.lockReason && (
                                                                <div className="text-sm bg-muted p-2 rounded">
                                                                    <span className="font-semibold">Original Lock Reason:</span> {lock.lockReason}
                                                                </div>
                                                            )}
                                                        </div>
                                                        <DialogFooter>
                                                            <Button onClick={handleAction} disabled={!!processing}>
                                                                {processing === module.moduleCode ? 'Unlocking...' : 'Approve Unlock'}
                                                            </Button>
                                                        </DialogFooter>
                                                    </DialogContent>
                                                </Dialog>
                                            ) : (
                                                <Dialog>
                                                    <DialogTrigger asChild>
                                                        <Button
                                                            variant="ghost"
                                                            size="sm"
                                                            className="h-8 text-destructive hover:text-destructive hover:bg-red-50"
                                                            onClick={() => {
                                                                setSelectedModule(module);
                                                                setActionType('lock');
                                                                setReason('');
                                                            }}
                                                        >
                                                            Lock
                                                        </Button>
                                                    </DialogTrigger>
                                                    <DialogContent>
                                                        <DialogHeader>
                                                            <DialogTitle>Lock {module.moduleName}?</DialogTitle>
                                                            <DialogDescription>
                                                                Prevent further transactions from {module.moduleName} for this period.
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-4 py-4">
                                                            <div className="space-y-2">
                                                                <Label>Reason for locking <span className="text-destructive">*</span></Label>
                                                                <Input
                                                                    placeholder="e.g. Period end close initiated"
                                                                    value={reason}
                                                                    onChange={(e) => setReason(e.target.value)}
                                                                />
                                                            </div>
                                                        </div>
                                                        <DialogFooter>
                                                            <Button variant="destructive" onClick={handleAction} disabled={!!processing || !reason}>
                                                                {processing === module.moduleCode ? 'Locking...' : 'Confirm Lock'}
                                                            </Button>
                                                        </DialogFooter>
                                                    </DialogContent>
                                                </Dialog>
                                            )}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </ScrollArea>
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={() => setIsOpen(false)}>Close</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
