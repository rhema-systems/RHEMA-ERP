'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { formatPendingApprovers, useWorkflowEntitySummaries } from '@/hooks/useWorkflowEntitySummaries';
import { toast } from 'sonner';
import { Eye, RefreshCw } from 'lucide-react';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';

export default function PendingBusinessPartnersPage() {
  const router = useRouter();

  const [loading, setLoading] = useState(true);
  const [partners, setPartners] = useState<BusinessPartnerDto[]>([]);
  const [search, setSearch] = useState('');
  const [partnerType, setPartnerType] = useState<'all' | 'Supplier' | 'Contractor' | 'Customer' | 'Both'>('all');
  const [actionLoading, setActionLoading] = useState(false);

  const load = async () => {
    try {
      setLoading(true);
      const result = await businessPartnerService.getPartners({
        page: 1,
        pageSize: 200,
        search: search || undefined,
        partnerType: partnerType !== 'all' ? partnerType : undefined,
        approvalStatus: 'Pending',
      });

      // Most pending partners should also be in a PendingApproval operational status,
      // but we intentionally don't hard-filter on it here (so admins can find odd states).
      setPartners(result.items || []);
    } catch (e: any) {
      console.error(e);
      toast.error(e?.message || 'Failed to load pending business partners');
      setPartners([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
     
  }, []);

  const filtered = useMemo(() => partners, [partners]);

  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'BusinessPartner',
    filtered.map((p) => p.id),
    filtered.length > 0
  );

  const statusBadge = (status: string) => {
    const s = (status || '').toLowerCase();
    if (s === 'pendingapproval') return <Badge variant="outline">Pending Approval</Badge>;
    if (s === 'active') return <Badge variant="default">Active</Badge>;
    if (s === 'inactive') return <Badge variant="secondary">Inactive</Badge>;
    if (s === 'suspended') return <Badge variant="destructive">Suspended</Badge>;
    return <Badge variant="secondary">{status || '-'}</Badge>;
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Pending Business Partners</h1>
          <p className="text-muted-foreground mt-2">
            Review and approve/reject business partners before they can be used in transactions (e.g. PO creation).
          </p>
        </div>
        <Button variant="outline" onClick={load} disabled={loading || actionLoading}>
          <RefreshCw className="w-4 h-4 mr-2" />
          Refresh
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search and filter the pending queue</CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="md:col-span-2">
            <Input
              placeholder="Search by name, code, email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && load()}
            />
          </div>
          <Select value={partnerType} onValueChange={(v) => setPartnerType(v as any)}>
            <SelectTrigger>
              <SelectValue placeholder="All Types" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Types</SelectItem>
              <SelectItem value="Supplier">Supplier</SelectItem>
              <SelectItem value="Contractor">Contractor</SelectItem>
              <SelectItem value="Customer">Customer</SelectItem>
              <SelectItem value="Both">Both</SelectItem>
            </SelectContent>
          </Select>
          <div className="md:col-span-3 flex justify-end">
            <Button onClick={load} disabled={loading || actionLoading}>
              Load
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Queue</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${filtered.length} pending partner(s)`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-10 text-muted-foreground">Loading...</div>
          ) : filtered.length === 0 ? (
            <div className="text-center py-10 text-muted-foreground">No pending business partners found.</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Code</TableHead>
                    <TableHead>Name</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Approval</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filtered.map((p) => {
                    const summary = workflowSummariesById[p.id];
                    const stepName = summary?.currentStepName || p.currentWorkflowStepName;
                    const pending = formatPendingApprovers(summary?.pendingApprovers || []);
                    const isPending = (p.approvalStatus || 'Pending').toLowerCase() === 'pending';
                    const isSubmitted = !!stepName; // if we have a step name, workflow is in progress

                    return (
                    <TableRow key={p.id}>
                      <TableCell className="font-mono">{p.partnerCode}</TableCell>
                      <TableCell>{p.partnerName}</TableCell>
                      <TableCell>{p.partnerType}</TableCell>
                      <TableCell>{statusBadge(p.status)}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Badge variant={p.approvalStatus === 'Approved' ? 'default' : 'outline'}>
                            {p.approvalStatus || 'Pending'}
                          </Badge>
                          {stepName && (
                            <Badge variant="outline" className="text-xs">
                              Step: {stepName}
                            </Badge>
                          )}
                          {stepName && pending.short && (
                            <Badge variant="outline" className="text-xs" title={pending.full}>
                              Pending with: {pending.short}
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-right space-x-2">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => router.push(`/procurement/business-partners/${p.id}`)}
                        >
                          <Eye className="w-4 h-4 mr-1" />
                          View
                        </Button>
                        <WorkflowApprovalActions
                          entityType="BusinessPartner"
                          entityId={p.id}
                          entityLabel="Business Partner"
                          entityNumber={p.partnerCode}
                          status={isSubmitted ? 'Submitted' : 'Draft'}
                          currentStepName={stepName}
                          workflowSummary={summary}
                          canSubmit={!isSubmitted && isPending}
                          canApproveReject={isSubmitted && isPending}
                          onSubmit={async () => {
                            setActionLoading(true);
                            try {
                              await businessPartnerService.submitPartnerForApproval(p.id);
                            } finally {
                              setActionLoading(false);
                            }
                          }}
                          onApprove={async (comments) => {
                            setActionLoading(true);
                            try {
                              await businessPartnerService.approvePartner(p.id, comments || undefined);
                            } finally {
                              setActionLoading(false);
                            }
                          }}
                          onReject={async (comments) => {
                            setActionLoading(true);
                            try {
                              await businessPartnerService.rejectPartner(p.id, comments);
                            } finally {
                              setActionLoading(false);
                            }
                          }}
                          onAfterAction={load}
                          onOpenWorkflows={() => router.push('/administration/workflow')}
                          size="sm"
                        />
                      </TableCell>
                    </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
