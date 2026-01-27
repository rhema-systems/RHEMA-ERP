'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { FileText, Search, Calendar, DollarSign, Clock, Eye, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type TenderDto } from '@/services/tenderService';
import { format, formatDistanceToNow } from 'date-fns';

export default function ExternalTendersPage() {
  const router = useRouter();
  const [tenders, setTenders] = useState<TenderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [tenderTypeFilter, setTenderTypeFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 12;

  useEffect(() => {
    loadTenders();
  }, [page, tenderTypeFilter]);

  const loadTenders = async () => {
    try {
      setLoading(true);
      // Only show published tenders for external users
      const data = await tenderService.getTenders({
        page,
        pageSize,
        search: searchTerm,
        status: 'Published',
        tenderType: tenderTypeFilter === 'all' ? undefined : tenderTypeFilter,
      });
      setTenders(data.items);
      setTotalCount(data.totalCount);
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

  const handleViewTender = (tenderId: string) => {
    router.push(`/external-portal/tenders/${tenderId}`);
  };

  const getDeadlineStatus = (deadline: string) => {
    const deadlineDate = new Date(deadline);
    const now = new Date();
    const hoursRemaining = (deadlineDate.getTime() - now.getTime()) / (1000 * 60 * 60);

    if (hoursRemaining < 0) {
      return { text: 'Closed', variant: 'destructive' as const, urgent: false };
    } else if (hoursRemaining < 24) {
      return { text: 'Closing Soon', variant: 'destructive' as const, urgent: true };
    } else if (hoursRemaining < 72) {
      return { text: formatDistanceToNow(deadlineDate, { addSuffix: true }), variant: 'default' as const, urgent: true };
    } else {
      return { text: formatDistanceToNow(deadlineDate, { addSuffix: true }), variant: 'secondary' as const, urgent: false };
    }
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
          Available Tenders
        </h1>
        <p className="text-gray-500">Browse and submit bids for open tenders</p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Open Tenders</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-blue-600">{totalCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Closing Soon</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-orange-600">
              {tenders.filter(t => {
                const hours = (new Date(t.submissionDeadline).getTime() - new Date().getTime()) / (1000 * 60 * 60);
                return hours > 0 && hours < 72;
              }).length}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-green-600">
              {tenders.reduce((sum, t) => sum + (t.estimatedValue || 0), 0).toLocaleString()}
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filter Tenders</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2 md:col-span-2">
              <Input
                placeholder="Search tenders..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
              <Button onClick={handleSearch}>
                <Search className="h-4 w-4" />
              </Button>
            </div>

            <Select value={tenderTypeFilter} onValueChange={setTenderTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="All Types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="RFQ">RFQ</SelectItem>
                <SelectItem value="RFP">RFP</SelectItem>
                <SelectItem value="ITB">ITB</SelectItem>
                <SelectItem value="EOI">EOI</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Tenders Grid */}
      {loading ? (
        <div className="text-center py-12">
          <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
          <p className="text-gray-500">Loading tenders...</p>
        </div>
      ) : tenders.length === 0 ? (
        <Card>
          <CardContent className="text-center py-12">
            <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
            <p className="text-gray-500">No tenders available at the moment</p>
          </CardContent>
        </Card>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {tenders.map((tender) => {
              const deadlineStatus = getDeadlineStatus(tender.submissionDeadline);
              const isExpired = new Date(tender.submissionDeadline) < new Date();

              return (
                <Card
                  key={tender.id}
                  className={`hover:shadow-lg transition-shadow ${deadlineStatus.urgent ? 'border-orange-300' : ''}`}
                >
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <Badge className="mb-2">{tender.tenderType}</Badge>
                        <CardTitle className="text-lg line-clamp-2">{tender.title}</CardTitle>
                        <p className="text-sm text-gray-500 font-mono mt-1">{tender.tenderNumber}</p>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {/* Description */}
                    <p className="text-sm text-gray-600 line-clamp-3">
                      {tender.description || 'No description available'}
                    </p>

                    {/* Details */}
                    <div className="space-y-2">
                      <div className="flex items-center gap-2 text-sm">
                        <DollarSign className="h-4 w-4 text-gray-400" />
                        <span className="text-gray-600">Est. Value:</span>
                        <span className="font-medium">
                          {tender.currency} {tender.estimatedValue?.toLocaleString() || 'N/A'}
                        </span>
                      </div>

                      <div className="flex items-center gap-2 text-sm">
                        <Calendar className="h-4 w-4 text-gray-400" />
                        <span className="text-gray-600">Deadline:</span>
                        <span className="font-medium">{formatDate(tender.submissionDeadline)}</span>
                      </div>

                      <div className="flex items-center gap-2 text-sm">
                        <Clock className="h-4 w-4 text-gray-400" />
                        <Badge variant={deadlineStatus.variant} className="text-xs">
                          {deadlineStatus.text}
                        </Badge>
                      </div>
                    </div>

                    {/* Urgent Notice */}
                    {deadlineStatus.urgent && !isExpired && (
                      <div className="flex items-center gap-2 p-2 bg-orange-50 border border-orange-200 rounded">
                        <AlertCircle className="h-4 w-4 text-orange-600" />
                        <span className="text-xs text-orange-800 font-medium">
                          Closing soon! Submit your bid now
                        </span>
                      </div>
                    )}

                    {/* Action Button */}
                    <Button
                      className="w-full"
                      onClick={() => handleViewTender(tender.id)}
                      disabled={isExpired}
                    >
                      <Eye className="h-4 w-4 mr-2" />
                      {isExpired ? 'Tender Closed' : 'View Details'}
                    </Button>
                  </CardContent>
                </Card>
              );
            })}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-6">
              <p className="text-sm text-gray-600">
                Page {page} of {totalPages} ({totalCount} tenders)
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
    </div>
  );
}

