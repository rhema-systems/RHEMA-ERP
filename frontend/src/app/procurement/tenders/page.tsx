'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { formatPendingApprovers, useWorkflowEntitySummaries } from '@/hooks/useWorkflowEntitySummaries';
import {
  Search,
  Eye,
  Edit,
  Download,
  RefreshCw,
  Filter,
  XCircle,
  Trash2
} from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type TenderDto } from '@/services/tenderService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useAuth } from '@/hooks/use-auth';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';

type PendingTenderAction = {
  kind: 'close' | 'delete';
  id: string;
  label: string;
};

export default function TendersPage() {
  const router = useRouter();
  const { hasPermission } = useAuth();
  const canAdministerTender = hasPermission('procurement.tender.administer');
  const [tenders, setTenders] = useState<TenderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [tenderTypeFilter, setTenderTypeFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [pendingAction, setPendingAction] = useState<PendingTenderAction | null>(null);
  const [actionBusy, setActionBusy] = useState(false);

  const { summariesById: workflowSummariesById } = useWorkflowEntitySummaries(
    'Tender',
    tenders.map((t) => t.id),
    tenders.length > 0
  );

  useEffect(() => {
    loadTenders();
  }, [page, statusFilter, tenderTypeFilter]);

  const loadTenders = async () => {
    try {
      setLoading(true);
      const result = await tenderService.getTenders({
        page,
        pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        tenderType: tenderTypeFilter !== 'all' ? tenderTypeFilter : undefined,
      });
      setTenders(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading tenders:', error);
      toast.error('Failed to load tenders');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadTenders();
  };

  const handleViewDetails = (id: string) => {
    router.push(`/procurement/tenders/${id}`);
  };

  const handleEdit = (id: string) => {
    router.push(`/procurement/tenders/${id}/edit`);
  };

  const confirmTenderAction = async () => {
    if (!pendingAction || !canAdministerTender) return false;

    try {
      setActionBusy(true);
      if (pendingAction.kind === 'close') {
        await tenderService.closeTender(pendingAction.id);
        toast.success('Tender closed successfully');
      } else {
        await tenderService.deleteTender(pendingAction.id);
        toast.success('Tender deleted successfully');
      }
      setPendingAction(null);
      await loadTenders();
      return true;
    } catch (error) {
      console.error(`Error ${pendingAction.kind}ing tender:`, error);
      toast.error(
        getProcurementProblemMessage(
          error,
          pendingAction.kind === 'close' ? 'Failed to close tender' : 'Failed to delete tender'
        )
      );
      return false;
    } finally {
      setActionBusy(false);
    }
  };

  const handleExportToExcel = () => {
    try {
      if (tenders.length === 0) {
        toast.error('No data to export');
        return;
      }

      const exportData = tenders.map(tender => ({
        'Tender Number': tender.tenderNumber,
        'Title': tender.title,
        'Type': tender.tenderType,
        'Status': tender.status,
        'Publish Date': tender.publishDate ? format(new Date(tender.publishDate), 'yyyy-MM-dd') : '',
        'Submission Deadline': tender.submissionDeadline ? format(new Date(tender.submissionDeadline), 'yyyy-MM-dd') : '',
        'Estimated Value': tender.estimatedValue || '',
        'Currency': tender.currency || '',
        'Bid Count': tender.bidCount,
        'Invitation Count': tender.invitationCount,
        'Created At': format(new Date(tender.createdAt), 'yyyy-MM-dd'),
        'Created By': tender.createdByName || '',
      }));

      const ws = XLSX.utils.json_to_sheet(exportData);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Tenders');

      const excelBuffer = XLSX.write(wb, { bookType: 'xlsx', type: 'array' });
      const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      saveAs(data, `tenders_${format(new Date(), 'yyyyMMdd')}.xlsx`);

      toast.success('Tenders exported successfully');
    } catch (error) {
      console.error('Error exporting tenders:', error);
      toast.error('Failed to export tenders');
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Submitted': { variant: 'outline', className: 'bg-yellow-100 text-yellow-800' },
      'Approved': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Published': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Closed': { variant: 'outline', className: 'bg-yellow-100 text-yellow-800' },
      'Awarded': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Cancelled': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
    };

    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Tender Management</h1>
          <p className="text-muted-foreground">
            Manage tenders, invitations, bids, and awards
          </p>
        </div>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Filter className="h-5 w-5" />
            Filters
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search tenders..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                className="flex-1"
              />
              <Button onClick={handleSearch} size="icon" variant="secondary">
                <Search className="h-4 w-4" />
              </Button>
            </div>

            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="All Statuses" />
              </SelectTrigger>
            <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Draft">Draft</SelectItem>
                <SelectItem value="Submitted">Pending Approval</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Published">Published</SelectItem>
                <SelectItem value="Closed">Closed</SelectItem>
                <SelectItem value="Awarded">Awarded</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
              </SelectContent>
            </Select>

            <Select value={tenderTypeFilter} onValueChange={setTenderTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="All Types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="RFQ">RFQ - Request for Quotation</SelectItem>
                <SelectItem value="RFP">RFP - Request for Proposal</SelectItem>
                <SelectItem value="ITB">ITB - Invitation to Bid</SelectItem>
                <SelectItem value="EOI">EOI - Expression of Interest</SelectItem>
              </SelectContent>
            </Select>

            <div className="flex gap-2">
              <Button onClick={loadTenders} variant="outline" className="gap-2 flex-1">
                <RefreshCw className="h-4 w-4" />
                Refresh
              </Button>
              <Button onClick={handleExportToExcel} variant="outline" className="gap-2 flex-1">
                <Download className="h-4 w-4" />
                Export
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tenders Table */}
      <Card>
        <CardHeader>
          <CardTitle>Tenders</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${tenders.length} tender(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">Loading tenders...</div>
          ) : tenders.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No tenders found</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tender #</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Submission Deadline</TableHead>
                    <TableHead>Estimated Value</TableHead>
                    <TableHead>Bids</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {tenders.map((tender) => {
                    const summary = workflowSummariesById[tender.id];
                    const stepName = summary?.currentStepName || tender.currentWorkflowStepName;
                    const pending = formatPendingApprovers(summary?.pendingApprovers || []);

                    return (
                    <TableRow key={tender.id}>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <span className="font-medium">{tender.tenderNumber}</span>
                          {tender.status === 'Submitted' && stepName && (
                            <Badge variant="outline" className="text-xs">
                              Step: {stepName}
                            </Badge>
                          )}
                          {tender.status === 'Submitted' && pending.short && (
                            <Badge variant="outline" className="text-xs" title={pending.full}>
                              Pending with: {pending.short}
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>
                          <div className="font-medium">{tender.title}</div>
                          <div className="text-sm text-gray-500">
                            Created {format(new Date(tender.createdAt), 'MMM dd, yyyy')}
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{tender.tenderType}</Badge>
                      </TableCell>
                      <TableCell>{getStatusBadge(tender.status)}</TableCell>
                      <TableCell>
                        {tender.submissionDeadline
                          ? format(new Date(tender.submissionDeadline), 'MMM dd, yyyy HH:mm')
                          : 'Not set'}
                      </TableCell>
                      <TableCell>
                        {tender.estimatedValue
                          ? `${tender.currency || 'USD'} ${tender.estimatedValue.toLocaleString()}`
                          : 'N/A'}
                      </TableCell>
                      <TableCell>
                        <Badge variant="secondary">{tender.bidCount} bids</Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleViewDetails(tender.id)}
                            title="View Details"
                          >
                            <Eye className="h-4 w-4" />
                          </Button>

                          <WorkflowApprovalActions
                            entityType="Tender"
                            entityId={tender.id}
                            entityLabel="Tender"
                            entityNumber={tender.tenderNumber}
                            status={tender.status}
                            currentStepName={stepName}
                            workflowSummary={summary}
                            canSubmit={tender.status === 'Draft'}
                            canApproveReject={tender.status === 'Submitted'}
                            onSubmit={async () => {
                              await tenderService.submitTenderForApproval(tender.id);
                            }}
                            onApprove={async (comments) => {
                              await tenderService.approveTender(tender.id, comments || undefined);
                            }}
                            onReject={async (comments) => {
                              await tenderService.rejectTender(tender.id, comments);
                            }}
                            onAfterAction={loadTenders}
                            onOpenWorkflows={() => router.push('/administration/workflow')}
                            size="icon"
                            iconOnly
                          />

                          {tender.status === 'Draft' && (
                            <>
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleEdit(tender.id)}
                                title="Edit"
                              >
                                <Edit className="h-4 w-4" />
                              </Button>
                              {canAdministerTender && (
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => setPendingAction({ kind: 'delete', id: tender.id, label: tender.tenderNumber })}
                                  title="Delete"
                                >
                                  <Trash2 className="h-4 w-4 text-red-600" />
                                </Button>
                              )}
                            </>
                          )}

                          {canAdministerTender && tender.status === 'Published' && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setPendingAction({ kind: 'close', id: tender.id, label: tender.tenderNumber })}
                              title="Close"
                            >
                              <XCircle className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <div className="text-sm text-gray-500">
                Page {page} of {totalPages}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={pendingAction !== null}
        onOpenChange={(open) => { if (!open) setPendingAction(null); }}
        title={pendingAction?.kind === 'delete' ? 'Delete Tender' : 'Close Tender'}
        description={
          pendingAction?.kind === 'delete'
            ? `Delete draft tender ${pendingAction.label}? This action cannot be undone.`
            : `Close published tender ${pendingAction?.label ?? ''}? No further bids can be submitted.`
        }
        confirmText={actionBusy
          ? (pendingAction?.kind === 'delete' ? 'Deleting...' : 'Closing...')
          : (pendingAction?.kind === 'delete' ? 'Delete Tender' : 'Close Tender')}
        variant={pendingAction?.kind === 'delete' ? 'destructive' : 'default'}
        onConfirm={confirmTenderAction}
        isLoading={actionBusy}
      />
    </div>
  );
}
