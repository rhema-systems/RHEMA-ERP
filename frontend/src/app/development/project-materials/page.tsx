'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { IssueRequisitionDialog } from '@/components/inventory/IssueRequisitionDialog';
import { ReturnRequisitionDialog } from '@/components/inventory/ReturnRequisitionDialog';
import { RequisitionDialog } from '@/components/inventory/RequisitionDialog';
import { inventoryManagementService, type WarehouseDto } from '@/services/inventoryManagementService';
import { inventoryRequisitionService, type InventoryRequisitionDto, RequisitionStatusMap } from '@/services/inventoryRequisitionService';
import { projectService, type ProjectMaterialCostEntryDto, type ProjectMaterialReconciliationReportItemDto, type ProjectProcurementReconciliationReportItemDto } from '@/services/projectService';
import { toast } from 'sonner';

const normalizeStatus = (status: number | string) => {
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    Draft: 1,
    Submitted: 2,
    Approved: 3,
    InProgress: 4,
    PartiallyIssued: 5,
    Issued: 6,
    Completed: 7,
    Cancelled: 8,
    Rejected: 9,
  };
  return statusMap[status] ?? 0;
};

export default function ProjectMaterialsPage() {
  const [loading, setLoading] = useState(true);
  const [requisitions, setRequisitions] = useState<InventoryRequisitionDto[]>([]);
  const [reconciliation, setReconciliation] = useState<ProjectMaterialReconciliationReportItemDto[]>([]);
  const [procurementReconciliation, setProcurementReconciliation] = useState<ProjectProcurementReconciliationReportItemDto[]>([]);
  const [ledgerEntries, setLedgerEntries] = useState<ProjectMaterialCostEntryDto[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseDto[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [ledgerPostingFilter, setLedgerPostingFilter] = useState('all');
  const [ledgerSourceFilter, setLedgerSourceFilter] = useState('all');
  const [ledgerExceptionFilter, setLedgerExceptionFilter] = useState('all');
  const [ledgerReversalFilter, setLedgerReversalFilter] = useState('all');
  const [selectedRequisitionId, setSelectedRequisitionId] = useState<string | undefined>();
  const [dialogMode, setDialogMode] = useState<'create' | 'edit' | 'view'>('view');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [issueDialogOpen, setIssueDialogOpen] = useState(false);
  const [issueRequisitionId, setIssueRequisitionId] = useState<string | null>(null);
  const [returnDialogOpen, setReturnDialogOpen] = useState(false);
  const [returnRequisitionId, setReturnRequisitionId] = useState<string | null>(null);

  const load = async () => {
    try {
      setLoading(true);
      const [requisitionResult, warehouseResult, reconciliationResult, procurementReconciliationResult, ledgerResult] = await Promise.all([
        inventoryRequisitionService.getAll(),
        inventoryManagementService.getWarehouses(),
        projectService.getMaterialReconciliationReport(100),
        projectService.getProcurementReconciliationReport(100),
        projectService.getMaterialCostLedgerReport(undefined, 150),
      ]);

      setRequisitions(
        requisitionResult
          .filter((item) => item.projectId || item.requisitionType === 2)
          .sort((left, right) => new Date(right.requestDate).getTime() - new Date(left.requestDate).getTime()),
      );
      setWarehouses(warehouseResult);
      setReconciliation(reconciliationResult);
      setProcurementReconciliation(procurementReconciliationResult);
      setLedgerEntries(ledgerResult);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project materials oversight');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const filteredRequisitions = useMemo(
    () =>
      requisitions.filter((item) => {
        const matchesSearch = !searchTerm
          || item.requisitionNumber.toLowerCase().includes(searchTerm.toLowerCase())
          || (item.projectCode || '').toLowerCase().includes(searchTerm.toLowerCase())
          || (item.warehouseName || '').toLowerCase().includes(searchTerm.toLowerCase());
        const matchesStatus = statusFilter === 'all' || normalizeStatus(item.status) === Number(statusFilter);
        return matchesSearch && matchesStatus;
      }),
    [requisitions, searchTerm, statusFilter],
  );

  const stats = useMemo(() => ({
    pendingApproval: requisitions.filter((item) => normalizeStatus(item.status) === 2).length,
    pendingIssue: requisitions.filter((item) => [3, 4, 5].includes(normalizeStatus(item.status))).length,
    completed: requisitions.filter((item) => normalizeStatus(item.status) === 7).length,
    totalValue: requisitions.reduce((sum, item) => sum + (item.totalValue || 0), 0),
  }), [requisitions]);

  const reconciliationStats = useMemo(() => ({
    watchProjects: reconciliation.filter((item) => item.reconciliationStatus !== 'Balanced').length,
    underTracked: reconciliation.filter((item) => item.reconciliationStatus === 'UnderTracked').length,
    totalVariance: reconciliation.reduce((sum, item) => sum + item.materialCostVariance, 0),
    totalNetIssued: reconciliation.reduce((sum, item) => sum + item.netIssuedValue, 0),
  }), [reconciliation]);

  const procurementStats = useMemo(() => ({
    watchProjects: procurementReconciliation.filter((item) => item.reconciliationStatus !== 'Balanced').length,
    pendingInspection: procurementReconciliation.filter((item) => item.pendingInspectionAmount > 0).length,
    receiptToIssueVariance: procurementReconciliation.reduce((sum, item) => sum + item.receiptToIssueVariance, 0),
    issueToPostingVariance: procurementReconciliation.reduce((sum, item) => sum + item.issueToPostingVariance, 0),
  }), [procurementReconciliation]);

  const filteredLedgerEntries = useMemo(
    () =>
      ledgerEntries.filter((item) => {
        const matchesPosting = ledgerPostingFilter === 'all' || item.postingState === ledgerPostingFilter;
        const matchesSource = ledgerSourceFilter === 'all' || item.sourceDocumentType === ledgerSourceFilter;
        const matchesExceptions = ledgerExceptionFilter === 'all'
          || (ledgerExceptionFilter === 'exceptions' && (item.hasMissingSourceLink || item.hasReversalGap))
          || (ledgerExceptionFilter === 'clean' && !item.hasMissingSourceLink && !item.hasReversalGap);
        const matchesReversal = ledgerReversalFilter === 'all'
          || (ledgerReversalFilter === 'reversed' && item.isReversed)
          || (ledgerReversalFilter === 'active' && !item.isReversed);
        return matchesPosting && matchesSource && matchesExceptions && matchesReversal;
      }),
    [ledgerEntries, ledgerExceptionFilter, ledgerPostingFilter, ledgerReversalFilter, ledgerSourceFilter],
  );

  const ledgerStats = useMemo(() => ({
    movements: ledgerEntries.length,
    exceptionCount: ledgerEntries.filter((item) => item.hasMissingSourceLink || item.hasReversalGap).length,
    reversedCount: ledgerEntries.filter((item) => item.isReversed).length,
    actualCostImpact: ledgerEntries.filter((item) => item.affectsActualCost).reduce((sum, item) => sum + item.amount, 0),
  }), [ledgerEntries]);

  const openDialog = (mode: 'create' | 'edit' | 'view', requisitionId?: string) => {
    setDialogMode(mode);
    setSelectedRequisitionId(requisitionId);
    setDialogOpen(true);
  };

  const openIssueDialog = (requisitionId: string) => {
    setIssueRequisitionId(requisitionId);
    setIssueDialogOpen(true);
  };

  const openReturnDialog = (requisitionId: string) => {
    setReturnRequisitionId(requisitionId);
    setReturnDialogOpen(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Project Materials</h1>
          <p className="text-muted-foreground">Manage project-linked inventory requisitions and issue status.</p>
        </div>
        <Button variant="outline" onClick={() => void load()}>Refresh</Button>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Project Requisitions</CardDescription><CardTitle>{requisitions.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Approval</CardDescription><CardTitle>{stats.pendingApproval}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Issue</CardDescription><CardTitle>{stats.pendingIssue}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Requested Value</CardDescription><CardTitle>{stats.totalValue.toLocaleString()}</CardTitle></CardHeader><CardContent className="pt-0 text-sm text-muted-foreground">Completed {stats.completed}</CardContent></Card>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Projects on Watch</CardDescription><CardTitle>{reconciliationStats.watchProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Under-Tracked Cost</CardDescription><CardTitle>{reconciliationStats.underTracked}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Total Net Issued</CardDescription><CardTitle>{reconciliationStats.totalNetIssued.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Total Variance</CardDescription><CardTitle>{reconciliationStats.totalVariance.toLocaleString()}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Procurement Watch</CardDescription><CardTitle>{procurementStats.watchProjects}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Inspection</CardDescription><CardTitle>{procurementStats.pendingInspection}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Receipt To Issue Variance</CardDescription><CardTitle>{procurementStats.receiptToIssueVariance.toLocaleString()}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Issue To Posting Variance</CardDescription><CardTitle>{procurementStats.issueToPostingVariance.toLocaleString()}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Ledger Movements</CardDescription><CardTitle>{ledgerStats.movements}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Exceptions</CardDescription><CardTitle>{ledgerStats.exceptionCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Reversed Entries</CardDescription><CardTitle>{ledgerStats.reversedCount}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Actual Cost Impact</CardDescription><CardTitle>{ledgerStats.actualCostImpact.toLocaleString()}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <Input value={searchTerm} onChange={(e) => setSearchTerm(e.target.value)} placeholder="Search requisition, project code, or warehouse" />
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger><SelectValue placeholder="All statuses" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {Object.entries(RequisitionStatusMap).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}
            </SelectContent>
          </Select>
          <div className="text-sm text-muted-foreground md:text-right md:self-center">{filteredRequisitions.length} requisitions shown</div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Project Material Queue</CardTitle>
          <CardDescription>Project-linked requisitions and issue status.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? <div className="py-10 text-center text-muted-foreground">Loading project material queue...</div> : null}
          {!loading && filteredRequisitions.length === 0 ? <div className="py-10 text-center text-muted-foreground">No project requisitions match the current filter.</div> : null}
          {filteredRequisitions.map((item) => {
            const statusValue = normalizeStatus(item.status);
            const canEdit = statusValue === 1;
            const canSubmit = statusValue === 1;
            const canIssue = [3, 4, 5].includes(statusValue);
            const canReturn = [5, 6, 7].includes(statusValue);
            const canComplete = statusValue === 6;

            return (
              <div key={item.id} className="rounded-md border p-4">
                <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="font-medium">{item.requisitionNumber}</div>
                      <Badge variant="outline">{RequisitionStatusMap[statusValue] || item.status}</Badge>
                      <Badge variant="secondary">{item.projectCode || 'Uncoded project'}</Badge>
                      {item.currentWorkflowStepName ? <Badge variant="outline">Step: {item.currentWorkflowStepName}</Badge> : null}
                    </div>
                    <div className="text-sm text-muted-foreground">
                      Warehouse {item.warehouseName} | Items {item.totalItems} | Qty {item.totalQuantity} | Value {item.totalValue.toLocaleString()}
                    </div>
                    {item.purpose ? <div className="text-sm text-muted-foreground">{item.purpose}</div> : null}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {item.projectId ? (
                      <Button asChild variant="ghost" size="sm">
                        <Link href={`/development/projects/${item.projectId}`}>Open Project</Link>
                      </Button>
                    ) : null}
                    <Button variant="outline" size="sm" onClick={() => openDialog('view', item.id)}>View</Button>
                    {canEdit ? <Button variant="outline" size="sm" onClick={() => openDialog('edit', item.id)}>Edit</Button> : null}
                    {canSubmit ? <Button variant="outline" size="sm" onClick={() => void inventoryRequisitionService.submit(item.id).then(() => load()).then(() => toast.success('Requisition submitted')).catch((error) => toast.error(error.message || 'Failed to submit requisition'))}>Submit</Button> : null}
                    {canIssue ? <Button size="sm" onClick={() => openIssueDialog(item.id)}>Issue</Button> : null}
                    {canReturn ? <Button variant="outline" size="sm" onClick={() => openReturnDialog(item.id)}>Return</Button> : null}
                    {canComplete ? <Button variant="outline" size="sm" onClick={() => void inventoryRequisitionService.complete(item.id).then(() => load()).then(() => toast.success('Requisition completed')).catch((error) => toast.error(error.message || 'Failed to complete requisition'))}>Complete</Button> : null}
                  </div>
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Material Reconciliation</CardTitle>
          <CardDescription>Project issue and return postings reconciled against the material ledger.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? <div className="py-10 text-center text-muted-foreground">Loading reconciliation...</div> : null}
          {!loading && reconciliation.length === 0 ? <div className="py-10 text-center text-muted-foreground">No project material reconciliation data is available.</div> : null}
          {reconciliation.map((item) => (
            <div key={item.projectId} className="rounded-lg border p-4">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{item.projectCode}</div>
                    <Badge variant="secondary">{item.projectTitle}</Badge>
                    <Badge variant={item.reconciliationStatus === 'Balanced' ? 'outline' : 'destructive'}>{item.reconciliationStatus}</Badge>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Requested {item.requestedValue.toLocaleString()} | Issued {item.issuedValue.toLocaleString()} | Returned {item.returnedValue.toLocaleString()} | Net Issued {item.netIssuedValue.toLocaleString()}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Tracked Material Cost {item.trackedMaterialCost.toLocaleString()} | Variance {item.materialCostVariance.toLocaleString()} | Pending Requisitions {item.pendingRequisitionCount}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Ledger Entries {item.materialLedgerEntryCount} | Missing Links {item.missingSourceLinkCount} | Reversal Gaps {item.reversalGapCount}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button asChild variant="ghost" size="sm">
                    <Link href={`/development/projects/${item.projectId}`}>Open Project</Link>
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Procurement Reconciliation</CardTitle>
          <CardDescription>Receipts, supplier returns, project issues, and posted material cost aligned per project.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? <div className="py-10 text-center text-muted-foreground">Loading procurement reconciliation...</div> : null}
          {!loading && procurementReconciliation.length === 0 ? <div className="py-10 text-center text-muted-foreground">No procurement reconciliation data is available.</div> : null}
          {procurementReconciliation.map((item) => (
            <div key={item.projectId} className="rounded-lg border p-4">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{item.projectCode}</div>
                    <Badge variant="secondary">{item.projectTitle}</Badge>
                    <Badge variant={item.reconciliationStatus === 'Balanced' ? 'outline' : item.reconciliationStatus === 'PendingInspection' ? 'secondary' : 'destructive'}>
                      {item.reconciliationStatus}
                    </Badge>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    PR {item.purchaseRequisitionCount} | PO {item.purchaseOrderCount} | Receipts {item.purchaseReceiptCount} | Open PO {item.openPurchaseOrderCount}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Received {item.receivedAmount.toLocaleString()} | Accepted {item.acceptedReceiptAmount.toLocaleString()} | Pending Inspection {item.pendingInspectionAmount.toLocaleString()} | Supplier Return {item.supplierReturnAmount.toLocaleString()}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Issued {item.issuedInventoryValue.toLocaleString()} | Net Issued {item.netIssuedInventoryValue.toLocaleString()} | Posted {item.postedMaterialCost.toLocaleString()}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Receipt to Issue Variance {item.receiptToIssueVariance.toLocaleString()} | Issue to Posting Variance {item.issueToPostingVariance.toLocaleString()}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Ledger Entries {item.procurementLedgerEntryCount} | Missing Links {item.missingSourceLinkCount} | Reversal Gaps {item.reversalGapCount}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button asChild variant="ghost" size="sm">
                    <Link href={`/development/projects/${item.projectId}`}>Open Project</Link>
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Project Cost Movement Ledger</CardTitle>
          <CardDescription>Auditable project material postings from receipt, issue, return, and reversal paths.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-4">
            <Select value={ledgerPostingFilter} onValueChange={setLedgerPostingFilter}>
              <SelectTrigger><SelectValue placeholder="Posting state" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All posting states</SelectItem>
                <SelectItem value="Posted">Posted</SelectItem>
                <SelectItem value="PendingInspection">Pending inspection</SelectItem>
                <SelectItem value="PendingReversal">Pending reversal</SelectItem>
              </SelectContent>
            </Select>
            <Select value={ledgerSourceFilter} onValueChange={setLedgerSourceFilter}>
              <SelectTrigger><SelectValue placeholder="Source document" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All sources</SelectItem>
                <SelectItem value="InventoryRequisition">Inventory requisition</SelectItem>
                <SelectItem value="PurchaseReceipt">Purchase receipt</SelectItem>
                <SelectItem value="PurchaseReturn">Purchase return</SelectItem>
              </SelectContent>
            </Select>
            <Select value={ledgerReversalFilter} onValueChange={setLedgerReversalFilter}>
              <SelectTrigger><SelectValue placeholder="Reversal state" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All movements</SelectItem>
                <SelectItem value="active">Active only</SelectItem>
                <SelectItem value="reversed">Reversals only</SelectItem>
              </SelectContent>
            </Select>
            <Select value={ledgerExceptionFilter} onValueChange={setLedgerExceptionFilter}>
              <SelectTrigger><SelectValue placeholder="Exception state" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All entries</SelectItem>
                <SelectItem value="exceptions">Exceptions only</SelectItem>
                <SelectItem value="clean">Clean only</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {loading ? <div className="py-10 text-center text-muted-foreground">Loading project material ledger...</div> : null}
          {!loading && filteredLedgerEntries.length === 0 ? <div className="py-10 text-center text-muted-foreground">No material cost ledger entries match the current filter.</div> : null}
          {filteredLedgerEntries.map((item) => (
            <div key={item.id} className="rounded-lg border p-4">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <div className="font-medium">{item.projectCode || 'Uncoded project'}</div>
                    <Badge variant="secondary">{item.projectTitle}</Badge>
                    <Badge variant="outline">{item.entryType}</Badge>
                    <Badge variant={item.postingState === 'Posted' ? 'outline' : 'secondary'}>{item.postingState}</Badge>
                    {item.isReversed ? <Badge variant="secondary">Reversed</Badge> : null}
                    {item.hasMissingSourceLink ? <Badge variant="destructive">Missing Link</Badge> : null}
                    {item.hasReversalGap ? <Badge variant="destructive">Reversal Gap</Badge> : null}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {new Date(item.entryDate).toLocaleString()} | {item.sourceDocumentType || 'Source'} {item.sourceDocumentNumber || item.sourceDocumentId || 'Unlinked'}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Item {(item.inventoryItemCode || item.inventoryItemName || 'Unlinked item')} | Qty {item.quantity.toLocaleString()} {item.unitOfMeasure || ''} | Unit Cost {item.unitCost.toLocaleString()} | Amount {item.amount.toLocaleString()} {item.currency}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    Affects Actual Cost {item.affectsActualCost ? 'Yes' : 'No'}{item.notes ? ` | ${item.notes}` : ''}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button asChild variant="ghost" size="sm">
                    <Link href={`/development/projects/${item.projectId}`}>Open Project</Link>
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <RequisitionDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        mode={dialogMode}
        requisitionId={selectedRequisitionId}
        warehouses={warehouses}
        onSuccess={() => { void load(); }}
      />
      <IssueRequisitionDialog
        open={issueDialogOpen}
        onOpenChange={setIssueDialogOpen}
        requisitionId={issueRequisitionId}
        onSuccess={() => { void load(); }}
      />
      <ReturnRequisitionDialog
        open={returnDialogOpen}
        onOpenChange={setReturnDialogOpen}
        requisitionId={returnRequisitionId}
        onSuccess={() => { void load(); }}
      />
    </div>
  );
}
