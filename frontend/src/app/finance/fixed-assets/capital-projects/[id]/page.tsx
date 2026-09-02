'use client';

import React, { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { HardHat, Loader2, ArrowLeft, Trash2, Plus, CheckCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { capitalProjectService, type CapitalProjectDetail, type ProjectStatus, type AddProjectCostDto, type AddSettlementRuleDto } from '@/services/finance/capitalProjectService';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { SourceDocumentDimensionEvidence } from '@/components/finance/dimensions/source-document-dimension-panel';

export default function CapitalProjectDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [project, setProject] = useState<CapitalProjectDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [categories, setCategories] = useState<{ id: string; name: string }[]>([]);

  // Add Cost dialog state
  const [costOpen, setCostOpen] = useState(false);
  const [costForm, setCostForm] = useState<AddProjectCostDto>({
    sourceDocumentType: 'ManualJournal',
    amount: 0,
    transactionDate: new Date().toISOString().split('T')[0],
    description: '',
  });

  // Add Settlement Rule dialog state
  const [ruleOpen, setRuleOpen] = useState(false);
  const [ruleForm, setRuleForm] = useState<AddSettlementRuleDto>({
    targetFixedAssetCategoryId: '',
    proposedAssetName: '',
    allocationPercentage: 0,
  });

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const [data, cats] = await Promise.all([
          capitalProjectService.getById(id),
          fixedAssetsDataService.getCategories(),
        ]);
        setProject(data);
        setCategories(cats.map((c: { id: string; name: string }) => ({ id: c.id, name: c.name })));
      } catch (error) {
        console.error('Failed to load project:', error);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [id]);

  const refresh = async () => {
    const data = await capitalProjectService.getById(id);
    setProject(data);
  };

  const handleStatusChange = async (status: ProjectStatus) => {
    try {
      await capitalProjectService.updateStatus(id, status);
      await refresh();
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Status update failed');
    }
  };

  const handleAddCost = async () => {
    try {
      await capitalProjectService.postCost(id, costForm);
      setCostOpen(false);
      setCostForm({ sourceDocumentType: 'ManualJournal', amount: 0, transactionDate: new Date().toISOString().split('T')[0], description: '' });
      await refresh();
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Failed to add cost');
    }
  };

  const handleRemoveCost = async (costId: string) => {
    if (!confirm('Remove this cost line?')) return;
    try {
      await capitalProjectService.removeCost(id, costId);
      await refresh();
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Failed to remove cost');
    }
  };

  const handleAddRule = async () => {
    try {
      await capitalProjectService.addSettlementRule(id, ruleForm);
      setRuleOpen(false);
      setRuleForm({ targetFixedAssetCategoryId: '', proposedAssetName: '', allocationPercentage: 0 });
      await refresh();
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Failed to add rule');
    }
  };

  const handleRemoveRule = async (ruleId: string) => {
    if (!confirm('Remove this settlement rule?')) return;
    try {
      await capitalProjectService.removeSettlementRule(id, ruleId);
      await refresh();
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Failed to remove rule');
    }
  };

  const handleCapitalize = async () => {
    if (!confirm('Capitalize this project? This will create fixed assets and post GL journals. This action cannot be undone.')) return;
    try {
      await capitalProjectService.capitalize(id);
      await refresh();
    } catch (error: unknown) {
      alert(error instanceof Error ? error.message : 'Capitalization failed');
    }
  };

  const formatMoney = (amount: number) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(amount || 0);

  const getStatusBadge = (status: ProjectStatus) => {
    const variants: Record<ProjectStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
      Planning: 'outline', InProgress: 'default', OnHold: 'secondary', Completed: 'default', Cancelled: 'destructive',
    };
    return <Badge variant={variants[status] || 'default'}>{status}</Badge>;
  };

  if (loading) {
    return <div className="flex items-center justify-center min-h-[400px]"><Loader2 className="h-8 w-8 animate-spin text-muted-foreground" /></div>;
  }

  if (!project) {
    return <div className="text-center py-8 text-muted-foreground">Project not found.</div>;
  }

  const totalAllocation = project.settlementRules.reduce((sum, r) => sum + r.allocationPercentage, 0);
  const canCapitalize = project.status === 'InProgress' && project.totalAccumulatedCost > 0 && totalAllocation === 100;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.push('/finance/fixed-assets/capital-projects')}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
              <HardHat className="h-8 w-8" />
              {project.projectCode} — {project.name}
            </h1>
            <p className="text-muted-foreground">{project.description || 'No description'}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {project.status === 'Planning' && (
            <Button onClick={() => handleStatusChange('InProgress')}>Start Project</Button>
          )}
          {project.status === 'InProgress' && (
            <>
              <Button variant="outline" onClick={() => handleStatusChange('OnHold')}>Put On Hold</Button>
              <Button variant="destructive" onClick={() => handleStatusChange('Cancelled')}>Cancel</Button>
            </>
          )}
          {project.status === 'OnHold' && (
            <Button onClick={() => handleStatusChange('InProgress')}>Resume</Button>
          )}
        </div>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem><BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbLink href="/finance/fixed-assets/capital-projects">Capital Projects</BreadcrumbLink></BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem><BreadcrumbPage>{project.projectCode}</BreadcrumbPage></BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Status</CardTitle></CardHeader><CardContent>{getStatusBadge(project.status)}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Budget</CardTitle></CardHeader><CardContent className="text-xl font-semibold">{formatMoney(project.totalBudgetAmount)}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Accumulated Cost</CardTitle></CardHeader><CardContent className="text-xl font-semibold">{formatMoney(project.totalAccumulatedCost)}</CardContent></Card>
        <Card><CardHeader className="pb-2"><CardTitle className="text-sm text-muted-foreground">Capitalized</CardTitle></CardHeader><CardContent className="text-xl font-semibold">{formatMoney(project.capitalizedAmount)}</CardContent></Card>
      </div>

      <SourceDocumentDimensionEvidence evidence={project.financeDimensions} />

      {/* Cost Lines */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Cost Lines</CardTitle>
            <CardDescription>Costs accumulated for this project. These are for tracking/consolidation only — no GL postings are made.</CardDescription>
          </div>
          {project.status === 'InProgress' && (
            <Dialog open={costOpen} onOpenChange={setCostOpen}>
              <DialogTrigger asChild><Button size="sm"><Plus className="mr-1 h-4 w-4" />Add Cost</Button></DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>Add Cost</DialogTitle>
                  <DialogDescription>Normally costs are accumulated via AP invoices or integrations. Use this to manually post a cost.</DialogDescription>
                </DialogHeader>
                <div className="space-y-4">
                  <div><Label>Amount</Label><Input type="number" value={costForm.amount} onChange={(e) => setCostForm({ ...costForm, amount: parseFloat(e.target.value) || 0 })} /></div>
                  <div><Label>Transaction Date</Label><Input type="date" value={costForm.transactionDate} onChange={(e) => setCostForm({ ...costForm, transactionDate: e.target.value })} /></div>
                  <div><Label>Reference</Label><Input value={costForm.sourceDocumentReference || ''} onChange={(e) => setCostForm({ ...costForm, sourceDocumentReference: e.target.value })} /></div>
                  <div><Label>Description</Label><Textarea value={costForm.description || ''} onChange={(e) => setCostForm({ ...costForm, description: e.target.value })} /></div>
                </div>
                <DialogFooter><Button onClick={handleAddCost}>Add Cost</Button></DialogFooter>
              </DialogContent>
            </Dialog>
          )}
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Source</TableHead>
                <TableHead>Reference</TableHead>
                <TableHead>Description</TableHead>
                <TableHead className="text-right">Amount</TableHead>
                {project.status !== 'Completed' && <TableHead />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {project.costLines.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center py-4 text-muted-foreground">No costs recorded.</TableCell></TableRow>
              ) : (
                project.costLines.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell>{new Date(c.transactionDate).toLocaleDateString()}</TableCell>
                    <TableCell><Badge variant="outline">{c.sourceDocumentType}</Badge></TableCell>
                    <TableCell className="font-mono text-sm">{c.sourceDocumentReference || '—'}</TableCell>
                    <TableCell>{c.description || '—'}</TableCell>
                    <TableCell className="text-right">{formatMoney(c.amount)}</TableCell>
                    {project.status !== 'Completed' && (
                      <TableCell><Button variant="ghost" size="icon" onClick={() => handleRemoveCost(c.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button></TableCell>
                    )}
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Settlement Rules */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Settlement Rules</CardTitle>
            <CardDescription>How accumulated costs will be split into fixed assets on capitalization. Total must equal 100%. Currently: {totalAllocation}%</CardDescription>
          </div>
          {project.status !== 'Completed' && project.status !== 'Cancelled' && (
            <Dialog open={ruleOpen} onOpenChange={setRuleOpen}>
              <DialogTrigger asChild><Button size="sm"><Plus className="mr-1 h-4 w-4" />Add Rule</Button></DialogTrigger>
              <DialogContent>
                <DialogHeader><DialogTitle>Add Settlement Rule</DialogTitle></DialogHeader>
                <div className="space-y-4">
                  <div>
                    <Label>Target Category</Label>
                    <Select value={ruleForm.targetFixedAssetCategoryId} onValueChange={(v) => setRuleForm({ ...ruleForm, targetFixedAssetCategoryId: v })}>
                      <SelectTrigger><SelectValue placeholder="Select category" /></SelectTrigger>
                      <SelectContent>
                        {categories.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div><Label>Proposed Asset Name</Label><Input value={ruleForm.proposedAssetName} onChange={(e) => setRuleForm({ ...ruleForm, proposedAssetName: e.target.value })} /></div>
                  <div><Label>Allocation %</Label><Input type="number" min={0} max={100} value={ruleForm.allocationPercentage} onChange={(e) => setRuleForm({ ...ruleForm, allocationPercentage: parseFloat(e.target.value) || 0 })} /></div>
                </div>
                <DialogFooter><Button onClick={handleAddRule}>Add Rule</Button></DialogFooter>
              </DialogContent>
            </Dialog>
          )}
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Category</TableHead>
                <TableHead>Proposed Asset Name</TableHead>
                <TableHead className="text-right">Allocation %</TableHead>
                <TableHead className="text-right">Allocated Amount</TableHead>
                <TableHead>Resulting Asset</TableHead>
                {project.status !== 'Completed' && <TableHead />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {project.settlementRules.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center py-4 text-muted-foreground">No settlement rules defined.</TableCell></TableRow>
              ) : (
                project.settlementRules.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.targetFixedAssetCategoryName || 'Unknown'}</TableCell>
                    <TableCell className="font-medium">{r.proposedAssetName}</TableCell>
                    <TableCell className="text-right">{r.allocationPercentage}%</TableCell>
                    <TableCell className="text-right">{r.allocatedAmount > 0 ? formatMoney(r.allocatedAmount) : '—'}</TableCell>
                    <TableCell>{r.resultingFixedAssetId ? <Badge>Created</Badge> : '—'}</TableCell>
                    {project.status !== 'Completed' && (
                      <TableCell><Button variant="ghost" size="icon" onClick={() => handleRemoveRule(r.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button></TableCell>
                    )}
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Capitalize Button */}
      {project.status === 'InProgress' && (
        <Card>
          <CardContent className="flex items-center justify-between py-6">
            <div>
              <p className="font-medium">Ready to Capitalize?</p>
              <p className="text-sm text-muted-foreground">
                {!canCapitalize
                  ? `Settlement rules must total 100% (currently ${totalAllocation}%) and costs must be accumulated.`
                  : `${formatMoney(project.totalAccumulatedCost)} will be allocated across ${project.settlementRules.length} asset(s).`}
              </p>
            </div>
            <Button size="lg" disabled={!canCapitalize} onClick={handleCapitalize}>
              <CheckCircle className="mr-2 h-5 w-5" />
              Capitalize Project
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
