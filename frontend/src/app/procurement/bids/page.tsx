'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Search, Eye, FileText, Download, ChevronLeft, ChevronRight, CheckCircle, XCircle, Clock } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderBidService from '@/services/tenderBidService';
import { type TenderBidSummaryDto } from '@/services/tenderBidService';
import * as tenderService from '@/services/tenderService';
import { type TenderDto } from '@/services/tenderService';
import { format } from 'date-fns';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';

export default function BidsPage() {
  const router = useRouter();
  const [bids, setBids] = useState<TenderBidSummaryDto[]>([]);
  const [tenders, setTenders] = useState<TenderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingTenders, setLoadingTenders] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [tenderFilter, setTenderFilter] = useState('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [showOpenDialog, setShowOpenDialog] = useState(false);
  const [bidToOpen, setBidToOpen] = useState<string | null>(null);
  const [opening, setOpening] = useState(false);
  const [showBulkOpenDialog, setShowBulkOpenDialog] = useState(false);
  const [bulkOpening, setBulkOpening] = useState(false);
  const pageSize = 10;

  useEffect(() => {
    loadTenders();
  }, []);

  useEffect(() => {
    loadBids();
  }, [currentPage, searchTerm, statusFilter, tenderFilter]);

  const loadTenders = async () => {
    try {
      setLoadingTenders(true);
      // Load published tenders
      const publishedData = await tenderService.getTenders({
        page: 1,
        pageSize: 500,
        status: 'Published',
      });
      // Load closed tenders
      const closedData = await tenderService.getTenders({
        page: 1,
        pageSize: 500,
        status: 'Closed',
      });
      // Combine and sort by creation date
      const allTenders = [...publishedData.items, ...closedData.items].sort((a, b) =>
        new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
      );
      console.log('Loaded tenders:', allTenders);
      setTenders(allTenders);
    } catch (error) {
      console.error('Error loading tenders:', error);
      toast.error('Failed to load tenders');
    } finally {
      setLoadingTenders(false);
    }
  };

  const loadBids = async () => {
    try {
      setLoading(true);
      const data = await tenderBidService.getBids({
        page: currentPage,
        pageSize,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        tenderId: tenderFilter !== 'all' ? tenderFilter : undefined,
      });
      setBids(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error('Error loading bids:', error);
      toast.error('Failed to load bids');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = (value: string) => {
    setSearchTerm(value);
    setCurrentPage(1);
  };

  const handleStatusFilter = (value: string) => {
    setStatusFilter(value);
    setCurrentPage(1);
  };

  const handleTenderFilter = (value: string) => {
    setTenderFilter(value);
    setCurrentPage(1);
  };

  const handleViewBid = (bidId: string) => {
    router.push(`/procurement/bids/${bidId}`);
  };

  const handleOpenBid = (bidId: string, event: React.MouseEvent) => {
    event.stopPropagation();
    setBidToOpen(bidId);
    setShowOpenDialog(true);
  };

  const confirmOpenBid = async () => {
    if (!bidToOpen) return;

    try {
      setOpening(true);
      await tenderBidService.openBid(bidToOpen);
      setShowOpenDialog(false);
      setBidToOpen(null);
      toast.success('Bid marked as opened');
      loadBids(); // Reload grid
    } catch (error) {
      console.error('Error opening bid:', error);
      toast.error('Failed to open bid');
    } finally {
      setOpening(false);
    }
  };

  const handleBulkOpenBids = () => {
    if (tenderFilter === 'all') {
      toast.error('Please select a tender first');
      return;
    }
    setShowBulkOpenDialog(true);
  };

  const confirmBulkOpenBids = async () => {
    if (tenderFilter === 'all') return;

    try {
      setBulkOpening(true);
      const result = await tenderBidService.openAllBidsByTender(tenderFilter);
      setShowBulkOpenDialog(false);
      toast.success(result.message);
      loadBids(); // Reload grid
    } catch (error) {
      console.error('Error opening bids:', error);
      toast.error('Failed to open bids');
    } finally {
      setBulkOpening(false);
    }
  };

  const handleExportToExcel = () => {
    const exportData = bids.map(bid => ({
      'Bid Number': bid.bidNumber,
      'Tender Number': bid.tenderNumber,
      'Tender Title': bid.tenderTitle,
      'Business Partner': bid.businessPartnerName,
      'Total Amount': bid.totalBidAmount,
      'Currency': bid.currency,
      'Submitted Date': bid.submittedDate ? format(new Date(bid.submittedDate), 'PPP') : '',
      'Status': bid.status,
    }));

    const worksheet = XLSX.utils.json_to_sheet(exportData);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Bids');

    const excelBuffer = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' });
    const data = new Blob([excelBuffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    saveAs(data, `Bids_${format(new Date(), 'yyyy-MM-dd')}.xlsx`);

    toast.success('Bids exported to Excel successfully');
  };

  const getSubmittedBidsCount = () => {
    return bids.filter(bid => bid.status === 'Submitted').length;
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Submitted': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Opened': { variant: 'default', className: 'bg-purple-100 text-purple-800' },
      'UnderEvaluation': { variant: 'default', className: 'bg-yellow-100 text-yellow-800' },
      'Accepted': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
      'Withdrawn': { variant: 'outline', className: 'bg-gray-100 text-gray-800' },
    };

    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };



  const formatCurrency = (amount?: number, currency?: string) => {
    if (amount === undefined || amount === null) return 'N/A';
    return `${currency || 'USD'} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Tender Bids</h1>
          <p className="text-gray-500">View and manage all tender bids</p>
        </div>
        <div className="flex gap-2">
          {tenderFilter !== 'all' && getSubmittedBidsCount() > 0 && (
            <Button variant="default" onClick={handleBulkOpenBids}>
              <CheckCircle className="h-4 w-4 mr-2" />
              Open All Bids ({getSubmittedBidsCount()})
            </Button>
          )}
          <Button variant="outline" onClick={handleExportToExcel}>
            <Download className="h-4 w-4 mr-2" />
            Export to Excel
          </Button>
        </div>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
              <Input
                placeholder="Search by bid number, tender, or business partner..."
                value={searchTerm}
                onChange={(e) => handleSearch(e.target.value)}
                className="pl-10"
              />
            </div>
            <Select value={tenderFilter} onValueChange={handleTenderFilter} disabled={loadingTenders}>
              <SelectTrigger>
                <SelectValue placeholder="Filter by tender" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Tenders</SelectItem>
                {tenders.map((tender) => (
                  <SelectItem key={tender.id} value={tender.id}>
                    {tender.tenderNumber} - {tender.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={statusFilter} onValueChange={handleStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Filter by status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Submitted">Submitted</SelectItem>
                <SelectItem value="Opened">Opened</SelectItem>
                <SelectItem value="UnderEvaluation">Under Evaluation</SelectItem>
                <SelectItem value="Accepted">Accepted</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="Withdrawn">Withdrawn</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Bids Table */}
      <Card>
        <CardHeader>
          <CardTitle>Bids</CardTitle>
          <CardDescription>
            Showing {bids.length} of {totalCount} bid(s)
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <p className="text-gray-500">Loading bids...</p>
            </div>
          ) : bids.length === 0 ? (
            <div className="text-center py-8">
              <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No bids found</p>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Bid Number</TableHead>
                    <TableHead>Tender</TableHead>
                    <TableHead>Business Partner</TableHead>
                    <TableHead>Total Amount</TableHead>
                    <TableHead>Submitted Date</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {bids.map((bid) => (
                    <TableRow key={bid.id}>
                      <TableCell className="font-mono font-semibold">{bid.bidNumber}</TableCell>
                      <TableCell>
                        <div>
                          <p className="font-medium">{bid.tenderTitle}</p>
                          <p className="text-sm text-gray-500">{bid.tenderNumber}</p>
                        </div>
                      </TableCell>
                      <TableCell className="font-medium">{bid.businessPartnerName}</TableCell>
                      <TableCell className="font-semibold">{formatCurrency(bid.totalBidAmount, bid.currency)}</TableCell>
                      <TableCell>
                        {bid.submittedDate ? format(new Date(bid.submittedDate), 'PPP') : 'N/A'}
                      </TableCell>
                      <TableCell>{getStatusBadge(bid.status)}</TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          {bid.status === 'Submitted' && (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={(e) => handleOpenBid(bid.id, e)}
                            >
                              <CheckCircle className="h-4 w-4 mr-2" />
                              Open
                            </Button>
                          )}
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleViewBid(bid.id)}
                          >
                            <Eye className="h-4 w-4 mr-2" />
                            View
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-500">
                    Page {currentPage} of {totalPages}
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
                      disabled={currentPage === 1}
                    >
                      <ChevronLeft className="h-4 w-4 mr-2" />
                      Previous
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
                      disabled={currentPage === totalPages}
                    >
                      Next
                      <ChevronRight className="h-4 w-4 ml-2" />
                    </Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* Open Bid Confirmation Dialog */}
      <ConfirmationDialog
        open={showOpenDialog}
        onOpenChange={setShowOpenDialog}
        title="Mark Bid as Opened"
        description="Are you sure you want to mark this bid as opened?"
        confirmText="Mark as Opened"
        cancelText="Cancel"
        variant="default"
        onConfirm={confirmOpenBid}
        isLoading={opening}
      />

      {/* Bulk Open Bids Confirmation Dialog */}
      <ConfirmationDialog
        open={showBulkOpenDialog}
        onOpenChange={setShowBulkOpenDialog}
        title="Open All Bids for Tender"
        description={`Are you sure you want to open all submitted bids for this tender? This will mark ${getSubmittedBidsCount()} bid(s) as opened.`}
        confirmText="Open All Bids"
        cancelText="Cancel"
        variant="default"
        onConfirm={confirmBulkOpenBids}
        isLoading={bulkOpening}
      />
    </div>
  );
}

