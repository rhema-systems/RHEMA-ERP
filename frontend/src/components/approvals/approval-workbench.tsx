'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
    AlertCircle,
    CheckCircle2,
    ClipboardCheck,
    Clock,
    Loader2,
    RefreshCw,
    Search,
    XCircle,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { useToast } from '@/hooks/use-toast';

export interface ApprovalBreadcrumb {
    label: string;
    href?: string;
}

export interface ApprovalQueueItem {
    id: string;
    reference: string;
    title: string;
    detailHref: string;
    documentType: string;
    module?: string;
    statusLabel?: string;
    date?: string | Date | null;
    amount?: number | null;
    currencyCode?: string | null;
    submittedBy?: string | null;
    canApprove?: boolean;
    canReject?: boolean;
    decisionOnDetailPage?: boolean;
    approveDisabledReason?: string | null;
    rejectDisabledReason?: string | null;
    metadata?: Array<{
        label: string;
        value: React.ReactNode;
    }>;
}

export interface ApprovalQueueDefinition {
    id: string;
    title: string;
    documentLabel: string;
    description?: string;
    emptyMessage?: string;
    accessDeniedMessage?: string;
    accentClassName?: string;
    icon?: React.ComponentType<{ className?: string }>;
    load: () => Promise<ApprovalQueueItem[]>;
    approve: (item: ApprovalQueueItem, comments?: string) => Promise<void>;
    reject?: (item: ApprovalQueueItem, reason: string) => Promise<void>;
}

interface QueueState {
    items: ApprovalQueueItem[];
    loading: boolean;
    error?: string;
    accessDenied?: boolean;
}

interface ApprovalWorkbenchProps {
    title: string;
    description: string;
    definitions: ApprovalQueueDefinition[];
    breadcrumbs?: ApprovalBreadcrumb[];
    maxWidthClassName?: string;
}

type ApprovalRow = ApprovalQueueItem & {
    queueId: string;
};

function isAccessDenied(error: any): boolean {
    const status = Number(error?.status ?? error?.response?.status);
    if (status === 401 || status === 403) return true;

    const message = String(error?.message || '').toLowerCase();
    return message.includes('forbid') || message.includes('unauthoriz') || message.includes('403') || message.includes('401');
}

function getErrorMessage(error: any, fallback: string): string {
    return error?.message || fallback;
}

function formatDate(value?: string | Date | null): string {
    if (!value) return 'No date';

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return 'No date';

    return date.toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
    });
}

function formatAmount(amount?: number | null, currencyCode?: string | null): string | null {
    if (amount === undefined || amount === null) return null;

    return new Intl.NumberFormat('en-GH', {
        style: 'currency',
        currency: currencyCode || 'GHS',
    }).format(amount || 0);
}

export function ApprovalWorkbench({
    title,
    description,
    definitions,
    breadcrumbs,
    maxWidthClassName = 'max-w-[1400px]',
}: ApprovalWorkbenchProps) {
    const { toast } = useToast();
    const [queues, setQueues] = useState<Record<string, QueueState>>({});
    const [activeTab, setActiveTab] = useState('all');
    const [search, setSearch] = useState('');
    const [actionKey, setActionKey] = useState<string | null>(null);
    const [rejectTarget, setRejectTarget] = useState<{ definition: ApprovalQueueDefinition; item: ApprovalQueueItem } | null>(null);
    const [rejectReason, setRejectReason] = useState('');

    const loadQueue = useCallback(async (definition: ApprovalQueueDefinition) => {
        setQueues(previous => ({
            ...previous,
            [definition.id]: {
                items: previous[definition.id]?.items || [],
                loading: true,
            },
        }));

        try {
            const items = await definition.load();
            setQueues(previous => ({
                ...previous,
                [definition.id]: {
                    items,
                    loading: false,
                },
            }));
        } catch (error: any) {
            const accessDenied = isAccessDenied(error);
            setQueues(previous => ({
                ...previous,
                [definition.id]: {
                    items: [],
                    loading: false,
                    accessDenied,
                    error: accessDenied
                        ? definition.accessDeniedMessage || `You do not have access to ${definition.documentLabel} approvals.`
                        : getErrorMessage(error, `Failed to load ${definition.documentLabel} approvals.`),
                },
            }));
        }
    }, []);

    const loadAll = useCallback(async () => {
        await Promise.all(definitions.map(definition => loadQueue(definition)));
    }, [definitions, loadQueue]);

    useEffect(() => {
        void loadAll();
    }, [loadAll]);

    const definitionById = useMemo(() => {
        return new Map(definitions.map(definition => [definition.id, definition]));
    }, [definitions]);

    const allRows = useMemo<ApprovalRow[]>(() => {
        return definitions.flatMap(definition =>
            (queues[definition.id]?.items || []).map(item => ({
                ...item,
                queueId: definition.id,
            }))
        );
    }, [definitions, queues]);

    const visibleRows = useMemo(() => {
        const normalizedSearch = search.trim().toLowerCase();

        return allRows.filter(row => {
            if (activeTab !== 'all' && row.queueId !== activeTab) return false;
            if (!normalizedSearch) return true;

            return [
                row.reference,
                row.title,
                row.documentType,
                row.module,
                row.submittedBy,
                row.statusLabel,
            ]
                .filter(Boolean)
                .some(value => String(value).toLowerCase().includes(normalizedSearch));
        });
    }, [activeTab, allRows, search]);

    const totalPending = allRows.length;
    const anyLoading = definitions.some(definition => queues[definition.id]?.loading);
    const activeDefinition = activeTab === 'all' ? null : definitionById.get(activeTab);
    const activeState = activeDefinition ? queues[activeDefinition.id] : null;

    const handleApprove = async (definition: ApprovalQueueDefinition, item: ApprovalQueueItem) => {
        if (item.canApprove === false) {
            toast({
                title: 'Approval unavailable',
                description: item.approveDisabledReason || 'Your current roles do not authorize this approval.',
                variant: 'destructive',
            });
            return;
        }

        const key = `${definition.id}:${item.id}:approve`;

        try {
            setActionKey(key);
            await definition.approve(item, `Approved from ${title}`);
            toast({
                title: 'Approved',
                description: `${item.reference} has been approved.`,
            });
            await loadQueue(definition);
        } catch (error: any) {
            toast({
                title: 'Approval failed',
                description: getErrorMessage(error, `Failed to approve ${item.reference}.`),
                variant: 'destructive',
            });
        } finally {
            setActionKey(null);
        }
    };

    const handleReject = async () => {
        if (!rejectTarget) return;

        const reason = rejectReason.trim();
        if (!reason) {
            toast({
                title: 'Rejection reason required',
                description: 'Enter a reason before rejecting this document.',
                variant: 'destructive',
            });
            return;
        }

        const { definition, item } = rejectTarget;
        if (item.canReject === false) {
            toast({
                title: 'Rejection unavailable',
                description: item.rejectDisabledReason || 'Your current roles do not authorize this rejection.',
                variant: 'destructive',
            });
            setRejectTarget(null);
            return;
        }

        const key = `${definition.id}:${item.id}:reject`;

        try {
            setActionKey(key);
            await definition.reject?.(item, reason);
            toast({
                title: 'Rejected',
                description: `${item.reference} has been rejected.`,
            });
            setRejectTarget(null);
            setRejectReason('');
            await loadQueue(definition);
        } catch (error: any) {
            toast({
                title: 'Rejection failed',
                description: getErrorMessage(error, `Failed to reject ${item.reference}.`),
                variant: 'destructive',
            });
        } finally {
            setActionKey(null);
        }
    };

    const renderEmptyState = () => {
        if (anyLoading) {
            return (
                <div className="flex items-center justify-center py-12 text-muted-foreground">
                    <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                    Loading approval queues...
                </div>
            );
        }

        if (activeState?.accessDenied || activeState?.error) {
            return (
                <div className="rounded-lg border border-amber-200 bg-amber-50 p-6 text-sm text-amber-900">
                    <div className="flex items-start gap-3">
                        <AlertCircle className="mt-0.5 h-5 w-5 shrink-0" />
                        <div>
                            <div className="font-semibold">{activeState.accessDenied ? 'Access restricted' : 'Queue unavailable'}</div>
                            <p className="mt-1">{activeState.error}</p>
                        </div>
                    </div>
                </div>
            );
        }

        return (
            <div className="rounded-lg border border-dashed py-12 text-center text-muted-foreground">
                {activeDefinition?.emptyMessage || 'No documents are awaiting your approval.'}
            </div>
        );
    };

    return (
        <div className={cn('mx-auto space-y-6 p-8', maxWidthClassName)}>
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
                    <p className="text-muted-foreground">{description}</p>
                </div>
                <Button variant="outline" onClick={loadAll} disabled={anyLoading}>
                    {anyLoading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-2 h-4 w-4" />}
                    Refresh
                </Button>
            </div>

            {breadcrumbs && breadcrumbs.length > 0 && (
                <Breadcrumb>
                    <BreadcrumbList>
                        {breadcrumbs.map((breadcrumb, index) => (
                            <React.Fragment key={`${breadcrumb.label}-${index}`}>
                                <BreadcrumbItem>
                                    {breadcrumb.href ? (
                                        <BreadcrumbLink href={breadcrumb.href}>{breadcrumb.label}</BreadcrumbLink>
                                    ) : (
                                        <BreadcrumbPage>{breadcrumb.label}</BreadcrumbPage>
                                    )}
                                </BreadcrumbItem>
                                {index < breadcrumbs.length - 1 && <BreadcrumbSeparator />}
                            </React.Fragment>
                        ))}
                    </BreadcrumbList>
                </Breadcrumb>
            )}

            <div className="grid gap-4 md:grid-cols-3">
                <Card className="border-blue-100 bg-blue-50/50">
                    <CardHeader className="pb-2">
                        <CardDescription>Total Pending</CardDescription>
                        <CardTitle className="text-3xl">{totalPending}</CardTitle>
                    </CardHeader>
                </Card>
                {definitions.map(definition => {
                    const Icon = definition.icon || ClipboardCheck;
                    const state = queues[definition.id];
                    const count = state?.items.length || 0;

                    return (
                        <Card key={definition.id} className={cn('overflow-hidden', definition.accentClassName)}>
                            <CardHeader className="pb-2">
                                <div className="flex items-center justify-between">
                                    <CardDescription>{definition.title}</CardDescription>
                                    <Icon className="h-4 w-4 text-muted-foreground" />
                                </div>
                                <CardTitle className="flex items-center gap-2 text-3xl">
                                    {state?.loading ? <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /> : count}
                                    {state?.accessDenied && <Badge variant="outline">Restricted</Badge>}
                                </CardTitle>
                            </CardHeader>
                        </Card>
                    );
                })}
            </div>

            <Card>
                <CardHeader className="space-y-4">
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                        <div>
                            <CardTitle>Approval Inbox</CardTitle>
                            <CardDescription>Review, approve, or reject submitted finance documents from one queue.</CardDescription>
                        </div>
                        <div className="relative w-full md:w-80">
                            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                            <Input
                                value={search}
                                onChange={event => setSearch(event.target.value)}
                                placeholder="Search approvals..."
                                className="pl-9"
                            />
                        </div>
                    </div>

                    <Tabs value={activeTab} onValueChange={setActiveTab}>
                        <TabsList className="flex h-auto flex-wrap justify-start">
                            <TabsTrigger value="all">All ({totalPending})</TabsTrigger>
                            {definitions.map(definition => (
                                <TabsTrigger key={definition.id} value={definition.id}>
                                    {definition.documentLabel} ({queues[definition.id]?.items.length || 0})
                                </TabsTrigger>
                            ))}
                        </TabsList>
                    </Tabs>
                </CardHeader>
                <CardContent>
                    {visibleRows.length === 0 ? (
                        renderEmptyState()
                    ) : (
                        <div className="space-y-3">
                            {visibleRows.map(row => {
                                const definition = definitionById.get(row.queueId);
                                if (!definition) return null;

                                const Icon = definition.icon || ClipboardCheck;
                                const amountText = formatAmount(row.amount, row.currencyCode);
                                const approveKey = `${definition.id}:${row.id}:approve`;
                                const rejectKey = `${definition.id}:${row.id}:reject`;
                                const restrictionMessages = Array.from(new Set([
                                    row.canApprove === false ? row.approveDisabledReason : null,
                                    definition.reject && row.canReject === false ? row.rejectDisabledReason : null,
                                ].filter((message): message is string => Boolean(message))));

                                return (
                                    <div key={`${row.queueId}:${row.id}`} className="rounded-xl border bg-card p-4 shadow-sm transition-colors hover:bg-muted/30">
                                        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                                            <div className="flex min-w-0 flex-1 items-start gap-3">
                                                <span className="rounded-full bg-blue-100 p-2 text-blue-600">
                                                    <Icon className="h-4 w-4" />
                                                </span>
                                                <div className="min-w-0 space-y-2">
                                                    <div className="flex flex-wrap items-center gap-2">
                                                        <span className="font-semibold">{row.reference}</span>
                                                        <Badge variant="outline">{row.documentType}</Badge>
                                                        <Badge variant="secondary">{row.statusLabel || 'Pending Approval'}</Badge>
                                                    </div>
                                                    <div>
                                                        <p className="font-medium leading-tight">{row.title}</p>
                                                        <p className="mt-1 text-sm text-muted-foreground">
                                                            {row.module || definition.title} - {formatDate(row.date)}
                                                            {row.submittedBy ? ` - Submitted by ${row.submittedBy}` : ''}
                                                        </p>
                                                    </div>
                                                    {row.metadata && row.metadata.length > 0 && (
                                                        <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                                                            {row.metadata.map(detail => (
                                                                <span key={detail.label} className="rounded-full bg-muted px-2.5 py-1">
                                                                    <span className="font-medium text-foreground">{detail.label}:</span> {detail.value}
                                                                </span>
                                                            ))}
                                                        </div>
                                                    )}
                                                </div>
                                            </div>

                                            <div className="flex flex-col gap-3 lg:items-end">
                                                {amountText && (
                                                    <div className="text-right">
                                                        <div className="text-xs uppercase tracking-wide text-muted-foreground">Amount</div>
                                                        <div className="text-lg font-semibold">{amountText}</div>
                                                    </div>
                                                )}
                                                {restrictionMessages.length > 0 && (
                                                    <div className="max-w-md rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
                                                        {restrictionMessages.map(message => (
                                                            <div key={message} className="flex items-start gap-2">
                                                                <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                                                                <span>{message}</span>
                                                            </div>
                                                        ))}
                                                    </div>
                                                )}
                                                <div className="flex flex-wrap gap-2">
                                                    <Button variant="outline" asChild>
                                                        <Link href={row.detailHref}>
                                                            <Clock className="mr-2 h-4 w-4" />
                                                            {row.decisionOnDetailPage ? 'Review & decide' : 'Review'}
                                                        </Link>
                                                    </Button>
                                                    {!row.decisionOnDetailPage && <Button
                                                        onClick={() => handleApprove(definition, row)}
                                                        disabled={actionKey !== null || row.canApprove === false}
                                                        title={row.canApprove === false ? row.approveDisabledReason || 'Approval unavailable' : undefined}
                                                    >
                                                        {actionKey === approveKey ? (
                                                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                        ) : (
                                                            <CheckCircle2 className="mr-2 h-4 w-4" />
                                                        )}
                                                        Approve
                                                    </Button>}
                                                    {!row.decisionOnDetailPage && definition.reject && (
                                                        <Button
                                                            variant="destructive"
                                                            onClick={() => setRejectTarget({ definition, item: row })}
                                                            disabled={actionKey !== null || row.canReject === false}
                                                            title={row.canReject === false ? row.rejectDisabledReason || 'Rejection unavailable' : undefined}
                                                        >
                                                            {actionKey === rejectKey ? (
                                                                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                                            ) : (
                                                                <XCircle className="mr-2 h-4 w-4" />
                                                            )}
                                                            Reject
                                                        </Button>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </CardContent>
            </Card>

            <Dialog open={!!rejectTarget} onOpenChange={open => {
                if (!open && actionKey === null) {
                    setRejectTarget(null);
                    setRejectReason('');
                }
            }}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>Reject {rejectTarget?.item.reference}</DialogTitle>
                        <DialogDescription>
                            Provide a clear reason so the document creator knows what to correct before resubmission.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="space-y-2">
                        <Label htmlFor="approval-rejection-reason">Rejection reason</Label>
                        <Textarea
                            id="approval-rejection-reason"
                            value={rejectReason}
                            onChange={event => setRejectReason(event.target.value)}
                            placeholder="Explain why this document is being rejected..."
                        />
                    </div>
                    <DialogFooter>
                        <Button
                            variant="outline"
                            onClick={() => {
                                setRejectTarget(null);
                                setRejectReason('');
                            }}
                            disabled={actionKey !== null}
                        >
                            Cancel
                        </Button>
                        <Button variant="destructive" onClick={handleReject} disabled={actionKey !== null || !rejectReason.trim()}>
                            {actionKey?.endsWith(':reject') ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <XCircle className="mr-2 h-4 w-4" />}
                            Reject Document
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
}
