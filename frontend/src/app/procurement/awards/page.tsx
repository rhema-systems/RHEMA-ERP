'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Award, Search, Eye, FileText, Download, Plus } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderAwardService from '@/services/tenderAwardService';
import { type TenderAwardDto } from '@/services/tenderAwardService';
import { format } from 'date-fns';

export default function AwardsPage() {
  const router = useRouter();
  const [awards, setAwards] = useState<TenderAwardDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 10;

  useEffect(() => {
    loadAwards();
  }, [page, statusFilter]);

  const loadAwards = async () => {
    try {
      setLoading(true);
      const data = await tenderAwardService.getAwards(page, pageSize, searchTerm, statusFilter);
      setAwards(data.items);
      setTotalCount(data.totalCount);
    } catch (error) {
      console.error('Error loading awards:', error);
      toast.error('Failed to load awards');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadAwards();
  };

  const handleViewAward = (awardId: string) => {
    router.push(`/procurement/awards/${awardId}`);
  };

  const handleViewTender = (tenderId: string) => {
    router.push(`/procurement/tenders/${tenderId}`);
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Awarded': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Cancelled': { variant: 'destructive', className: 'bg-red-100 text-red-800' },
    };
    
    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
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
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <Award className="h-8 w-8 text-green-600" />
            Tender Awards
          </h1>
          <p className="text-gray-500">Manage tender awards and notifications</p>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Awards</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{totalCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Active Awards</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-green-600">
              {awards.filter(a => a.status === 'Awarded').length}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Cancelled</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-red-600">
              {awards.filter(a => a.status === 'Cancelled').length}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Value</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-blue-600">
              {awards.reduce((sum, a) => sum + a.awardedAmount, 0).toLocaleString()}
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filter Awards</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="flex gap-2">
              <Input
                placeholder="Search by tender, bid, or business partner..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
              <Button onClick={handleSearch}>
                <Search className="h-4 w-4" />
              </Button>
            </div>

            <Select value={statusFilter || 'all'} onValueChange={(value) => setStatusFilter(value === 'all' ? '' : value)}>
              <SelectTrigger>
                <SelectValue placeholder="Filter by status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Awarded">Awarded</SelectItem>
                <SelectItem value="Cancelled">Cancelled</SelectItem>
              </SelectContent>
            </Select>

            <Button variant="outline" onClick={() => { setSearchTerm(''); setStatusFilter(''); setPage(1); loadAwards(); }}>
              Clear Filters
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Awards Table */}
      <Card>
        <CardHeader>
          <CardTitle>Awards List</CardTitle>
          <CardDescription>
            Showing {awards.length} of {totalCount} awards
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <Award className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
              <p className="text-gray-500">Loading awards...</p>
            </div>
          ) : awards.length === 0 ? (
            <div className="text-center py-8">
              <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No awards found</p>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tender</TableHead>
                    <TableHead>Business Partner</TableHead>
                    <TableHead>Award Date</TableHead>
                    <TableHead>Awarded Amount</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Awarded By</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {awards.map((award) => (
                    <TableRow key={award.id}>
                      <TableCell>
                        <div>
                          <p className="font-mono text-sm text-blue-600 cursor-pointer hover:underline"
                             onClick={() => handleViewTender(award.tenderId)}>
                            {award.tenderNumber}
                          </p>
                          <p className="text-sm text-gray-600">{award.tenderTitle}</p>
                        </div>
                      </TableCell>
                      <TableCell className="font-medium">{award.businessPartnerName}</TableCell>
                      <TableCell>{formatDate(award.awardDate)}</TableCell>
                      <TableCell>
                        <span className="font-bold text-green-600">
                          {award.currency} {award.awardedAmount.toLocaleString()}
                        </span>
                      </TableCell>
                      <TableCell>{getStatusBadge(award.status)}</TableCell>
                      <TableCell className="text-sm text-gray-600">{award.awardedByName || 'N/A'}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => handleViewAward(award.id)}
                          >
                            <Eye className="h-4 w-4 mr-1" />
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

