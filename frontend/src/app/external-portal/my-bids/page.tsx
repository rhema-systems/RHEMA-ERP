'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { FileText, Search, Eye, Clock, CheckCircle, XCircle, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderBidService from '@/services/tenderBidService';
import { type TenderBidSummaryDto } from '@/services/tenderBidService';
import { format } from 'date-fns';

export default function MyBidsPage() {
  const router = useRouter();
  const [bids, setBids] = useState<TenderBidSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 10;

  useEffect(() => {
    loadBids();
     
  }, [page, statusFilter, searchTerm]);

  const loadBids = async () => {
    try {
      setLoading(true);
      const allBids = await tenderBidService.getMyBids();

      // Apply client-side filtering
      let filteredBids = allBids;

      // Filter by status
      if (statusFilter !== 'all') {
        filteredBids = filteredBids.filter(bid => bid.status === statusFilter);
      }

      // Filter by search term
      if (searchTerm) {
        const lowerSearch = searchTerm.toLowerCase();
        filteredBids = filteredBids.filter(bid =>
          bid.bidNumber.toLowerCase().includes(lowerSearch) ||
          bid.tenderNumber.toLowerCase().includes(lowerSearch) ||
          bid.tenderTitle.toLowerCase().includes(lowerSearch)
        );
      }

      // Apply pagination
      const startIndex = (page - 1) * pageSize;
      const endIndex = startIndex + pageSize;
      const paginatedBids = filteredBids.slice(startIndex, endIndex);

      setBids(paginatedBids);
      setTotalCount(filteredBids.length);
    } catch (error) {
      console.error('Error loading bids:', error);
      toast.error('Failed to load bids');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    // loadBids will be triggered by useEffect when searchTerm changes
  };

  const handleViewBid = (bidId: string) => {
    router.push(`/external-portal/my-bids/${bidId}`);
  };

  const handleViewTender = (tenderId: string) => {
    router.push(`/external-portal/tenders/${tenderId}`);
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string, icon: any }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800', icon: Clock },
      'Submitted': { variant: 'default', className: 'bg-blue-100 text-blue-800', icon: CheckCircle },
      'Opened': { variant: 'default', className: 'bg-purple-100 text-purple-800', icon: Eye },
      'UnderEvaluation': { variant: 'default', className: 'bg-yellow-100 text-yellow-800', icon: AlertCircle },
      'Accepted': { variant: 'default', className: 'bg-green-100 text-green-800', icon: CheckCircle },
      'Rejected': { variant: 'destructive', className: 'bg-red-100 text-red-800', icon: XCircle },
      'Withdrawn': { variant: 'outline', className: 'bg-gray-100 text-gray-600', icon: XCircle },
    };

    const config = statusConfig[status] || { variant: 'outline' as const, className: '', icon: FileText };
    const Icon = config.icon;

    return (
      <Badge variant={config.variant} className={config.className}>
        <Icon className="h-3 w-3 mr-1" />
        {status}
      </Badge>
    );
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP');
    } catch {
      return dateString;
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold flex items-center gap-2">
          <FileText className="h-8 w-8 text-blue-600" />
          My Bids
        </h1>
        <p className="text-gray-500">View and manage your submitted bids</p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Bids</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{totalCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Submitted</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-blue-600">
              {bids.filter(b => b.status === 'Submitted' || b.status === 'UnderEvaluation').length}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Accepted</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-green-600">
              {bids.filter(b => b.status === 'Accepted').length}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Draft</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-gray-600">
              {bids.filter(b => b.status === 'Draft').length}
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filter Bids</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2 md:col-span-2">
              <Input
                placeholder="Search by tender or bid number..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
              <Button onClick={handleSearch}>
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
                <SelectItem value="Submitted">Submitted</SelectItem>
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
          <CardTitle>Bids List</CardTitle>
          <CardDescription>
            Showing {bids.length} of {totalCount} bids
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
              <p className="text-gray-500">Loading bids...</p>
            </div>
          ) : bids.length === 0 ? (
            <div className="text-center py-8">
              <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No bids found</p>
              <Button
                className="mt-4"
                onClick={() => router.push('/external-portal/tenders')}
              >
                Browse Tenders
              </Button>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Bid Number</TableHead>
                    <TableHead>Tender</TableHead>
                    <TableHead>Submitted Date</TableHead>
                    <TableHead>Total Amount</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {bids.map((bid) => (
                    <TableRow key={bid.id}>
                      <TableCell className="font-mono font-medium">{bid.bidNumber}</TableCell>
                      <TableCell>
                        <div>
                          <p
                            className="font-medium text-blue-600 cursor-pointer hover:underline"
                            onClick={() => handleViewTender(bid.tenderId)}
                          >
                            {bid.tenderNumber}
                          </p>
                          <p className="text-sm text-gray-600">{bid.tenderTitle}</p>
                        </div>
                      </TableCell>
                      <TableCell>{formatDate(bid.submittedDate)}</TableCell>
                      <TableCell>
                        <span className="font-bold text-green-600">
                          {bid.currency} {bid.totalBidAmount.toLocaleString()}
                        </span>
                      </TableCell>
                      <TableCell>{getStatusBadge(bid.status)}</TableCell>
                      <TableCell>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleViewBid(bid.id)}
                        >
                          <Eye className="h-4 w-4 mr-1" />
                          View
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex items-center justify-between mt-4">
                  <p className="text-sm text-gray-600">
                    Page {page} of {totalPages}
                  </p>
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
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

