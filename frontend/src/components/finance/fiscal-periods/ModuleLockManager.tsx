import React, { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Clock3, Lock, ShieldCheck, Unlock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FiscalPeriod, ModuleDefinition, PeriodModuleLock } from '@/types/finance';

interface ModuleLockManagerProps {
    period: FiscalPeriod;
    modules: ModuleDefinition[];
    canLock: boolean;
    canReopen: boolean;
    onUpdate: () => void;
}

const DEFAULT_REOPEN_HOURS = 4;
const MAX_REOPEN_HOURS = 24;

function toLocalDateTimeInput(date: Date): string {
    const offset = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

function formatRemaining(expiresAt: string, now: number): string {
    const remaining = new Date(expiresAt).getTime() - now;
    if (remaining <= 0) return 'Expired - relocking';

    const totalMinutes = Math.ceil(remaining / 60_000);
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return hours > 0 ? `${hours}h ${minutes}m remaining` : `${minutes}m remaining`;
}

export function ModuleLockManager({
    period,
    modules,
    canLock,
    canReopen,
    onUpdate,
}: ModuleLockManagerProps) {
    const { toast } = useToast();
    const [processing, setProcessing] = useState<string | null>(null);
    const [reason, setReason] = useState('');
    const [reopenUntil, setReopenUntil] = useState('');
    const [selectedModule, setSelectedModule] = useState<ModuleDefinition | null>(null);
    const [actionType, setActionType] = useState<'lock' | 'reopen' | null>(null);
    const [isOpen, setIsOpen] = useState(false);
    const [now, setNow] = useState(() => Date.now());

    useEffect(() => {
        const timer = window.setInterval(() => setNow(Date.now()), 30_000);
        return () => window.clearInterval(timer);
    }, []);

    const minimumExpiry = useMemo(() => toLocalDateTimeInput(new Date(now + 2 * 60_000)), [now]);
    const maximumExpiry = useMemo(() => toLocalDateTimeInput(new Date(now + MAX_REOPEN_HOURS * 3_600_000)), [now]);

    const getLock = (moduleCode: string): PeriodModuleLock | undefined =>
        period.moduleLocks?.find(lock => lock.moduleCode.toUpperCase() === moduleCode.toUpperCase());

    const isEffectivelyLocked = (moduleCode: string): boolean => {
        if (period.isLocked) return true;
        const lock = getLock(moduleCode);
        if (lock?.isLocked) return true;
        if (lock?.reopenExpiresAtUtc && new Date(lock.reopenExpiresAtUtc).getTime() <= now) return true;
        return Boolean(period.isGlobalLockSuspended && !lock);
    };

    const prepareAction = (module: ModuleDefinition, action: 'lock' | 'reopen') => {
        setSelectedModule(module);
        setActionType(action);
        setReason('');
        setReopenUntil(action === 'reopen'
            ? toLocalDateTimeInput(new Date(Date.now() + DEFAULT_REOPEN_HOURS * 3_600_000))
            : '');
    };

    const handleAction = async () => {
        if (!selectedModule || !actionType) return;
        const normalizedReason = reason.trim();
        if (!normalizedReason) {
            toast({
                title: 'Reason required',
                description: `Enter a reason before ${actionType === 'lock' ? 'locking' : 'reopening'} this module.`,
                variant: 'destructive',
            });
            return;
        }

        try {
            setProcessing(selectedModule.moduleCode);
            if (actionType === 'lock') {
                await financeDataService.lockPeriodForModule(period.id, selectedModule.moduleCode, normalizedReason);
                toast({
                    title: 'Module locked',
                    description: `${selectedModule.moduleName} can no longer post to ${period.periodName}.`,
                });
            } else {
                if (!reopenUntil) {
                    throw new Error('Select when this temporary reopening should expire.');
                }

                const expiry = new Date(reopenUntil);
                if (Number.isNaN(expiry.getTime()) || expiry.getTime() <= Date.now()) {
                    throw new Error('The automatic relock time must be in the future.');
                }

                await financeDataService.unlockPeriodForModule(
                    period.id,
                    selectedModule.moduleCode,
                    normalizedReason,
                    expiry.toISOString(),
                );
                toast({
                    title: 'Module temporarily reopened',
                    description: `${selectedModule.moduleName} can post until ${expiry.toLocaleString()}, then it will relock automatically.`,
                });
            }

            await onUpdate();
            setReason('');
            setReopenUntil('');
            setSelectedModule(null);
            setActionType(null);
        } catch (error: any) {
            toast({
                title: 'Module lock update failed',
                description: error.message || 'Failed to update module lock status.',
                variant: 'destructive',
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
            <DialogContent className="max-w-3xl">
                <DialogHeader>
                    <DialogTitle>Module Locks - {period.periodName}</DialogTitle>
                    <DialogDescription>
                        Locks prevent final Finance posting only. Users can continue creating, editing, viewing, and approving operational documents.
                    </DialogDescription>
                </DialogHeader>

                {period.isGlobalLockSuspended && (
                    <div className="flex items-start gap-2 rounded-md border border-orange-200 bg-orange-50 p-3 text-sm text-orange-900">
                        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                        <div>
                            <div className="font-semibold">Partially locked period</div>
                            <div>The original global lock will resume automatically when the last temporary module reopening expires.</div>
                        </div>
                    </div>
                )}

                <div className="py-3">
                    <div className="grid grid-cols-12 gap-4 px-2 pb-2 text-sm font-medium text-muted-foreground">
                        <div className="col-span-4">Module</div>
                        <div className="col-span-5">Posting status</div>
                        <div className="col-span-3 text-right">Action</div>
                    </div>
                    <ScrollArea className="h-[340px] rounded-md border p-2">
                        <div className="space-y-2">
                            {modules.map(module => {
                                const lock = getLock(module.moduleCode);
                                const locked = isEffectivelyLocked(module.moduleCode);
                                const temporaryOpen = !locked && Boolean(lock?.reopenExpiresAtUtc);

                                return (
                                    <div key={module.id} className="grid grid-cols-12 items-center gap-4 rounded p-2 transition-colors hover:bg-muted/50">
                                        <div className="col-span-4">
                                            <div className="font-medium">{module.moduleName}</div>
                                            <div className="text-xs text-muted-foreground">{module.moduleCode}</div>
                                        </div>
                                        <div className="col-span-5 space-y-1">
                                            {locked ? (
                                                <Badge variant="destructive" className="gap-1">
                                                    <Lock className="h-3 w-3" /> Locked
                                                </Badge>
                                            ) : (
                                                <Badge variant="outline" className="gap-1 border-green-200 bg-green-50 text-green-700">
                                                    <Unlock className="h-3 w-3" /> {temporaryOpen ? 'Temporarily open' : 'Open'}
                                                </Badge>
                                            )}
                                            {temporaryOpen && lock?.reopenExpiresAtUtc && (
                                                <div className="flex items-center gap-1 text-xs text-orange-700">
                                                    <Clock3 className="h-3 w-3" />
                                                    {formatRemaining(lock.reopenExpiresAtUtc, now)} - relocks {new Date(lock.reopenExpiresAtUtc).toLocaleString()}
                                                </div>
                                            )}
                                            {locked && lock?.lockReason && (
                                                <div className="line-clamp-2 text-xs text-muted-foreground">{lock.lockReason}</div>
                                            )}
                                        </div>
                                        <div className="col-span-3 text-right">
                                            {locked && canReopen && (
                                                <Dialog>
                                                    <DialogTrigger asChild>
                                                        <Button variant="ghost" size="sm" className="text-orange-700" onClick={() => prepareAction(module, 'reopen')}>
                                                            Reopen
                                                        </Button>
                                                    </DialogTrigger>
                                                    <DialogContent>
                                                        <DialogHeader>
                                                            <DialogTitle>Temporarily reopen {module.moduleName}?</DialogTitle>
                                                            <DialogDescription>
                                                                Final postings from {module.moduleName} will be allowed until the selected expiry. Draft and approval work is unaffected.
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-4 py-4">
                                                            <div className="space-y-2">
                                                                <Label htmlFor={`reopen-reason-${module.id}`}>Reason</Label>
                                                                <Input id={`reopen-reason-${module.id}`} value={reason} onChange={event => setReason(event.target.value)} placeholder="e.g. Authorized correction to January postings" />
                                                            </div>
                                                            <div className="space-y-2">
                                                                <Label htmlFor={`reopen-expiry-${module.id}`}>Automatically relock at</Label>
                                                                <Input id={`reopen-expiry-${module.id}`} type="datetime-local" value={reopenUntil} min={minimumExpiry} max={maximumExpiry} onChange={event => setReopenUntil(event.target.value)} />
                                                                <p className="text-xs text-muted-foreground">Default: 4 hours. Maximum: 24 hours. Time is shown in your local timezone.</p>
                                                            </div>
                                                        </div>
                                                        <DialogFooter>
                                                            <Button onClick={handleAction} disabled={Boolean(processing) || !reason.trim() || !reopenUntil}>
                                                                {processing === module.moduleCode ? 'Reopening...' : 'Approve temporary reopening'}
                                                            </Button>
                                                        </DialogFooter>
                                                    </DialogContent>
                                                </Dialog>
                                            )}
                                            {!locked && canLock && (
                                                <Dialog>
                                                    <DialogTrigger asChild>
                                                        <Button variant="ghost" size="sm" className="text-destructive" onClick={() => prepareAction(module, 'lock')}>
                                                            Lock now
                                                        </Button>
                                                    </DialogTrigger>
                                                    <DialogContent>
                                                        <DialogHeader>
                                                            <DialogTitle>Lock {module.moduleName}?</DialogTitle>
                                                            <DialogDescription>
                                                                Final Finance postings from this module will be blocked for {period.periodName}.
                                                            </DialogDescription>
                                                        </DialogHeader>
                                                        <div className="space-y-2 py-4">
                                                            <Label htmlFor={`lock-reason-${module.id}`}>Reason</Label>
                                                            <Input id={`lock-reason-${module.id}`} value={reason} onChange={event => setReason(event.target.value)} placeholder="e.g. Period-end processing started" />
                                                        </div>
                                                        <DialogFooter>
                                                            <Button variant="destructive" onClick={handleAction} disabled={Boolean(processing) || !reason.trim()}>
                                                                {processing === module.moduleCode ? 'Locking...' : 'Confirm lock'}
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
