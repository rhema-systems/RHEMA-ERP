'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  Search,
  Eye,
  CheckCircle,
  XCircle,
  Clock,
  Filter,
  RefreshCw,
  Download
} from 'lucide-react';
import { toast } from 'sonner';
import { registrationReviewService, type RegistrationReviewDto } from '@/services/registrationReviewService';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function RegistrationReviewPage() {
  const router = useRouter();
  const [registrations, setRegistrations] = useState<RegistrationReviewDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [partnerTypeFilter, setPartnerTypeFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  // Format date as "MMM dd, yyyy HH:mm AM/PM"
  const formatDateTime = (dateString: string | undefined) => {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: '2-digit',
      year: 'numeric'
    }) + ' ' + date.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });
  };

  useEffect(() => {
    loadRegistrations();
  }, [page, statusFilter, partnerTypeFilter]);

  const loadRegistrations = async () => {
    try {
      setLoading(true);
      const result = await registrationReviewService.getRegistrations({
        page,
        pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        partnerType: partnerTypeFilter !== 'all' ? partnerTypeFilter : undefined,
      });
      setRegistrations(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading registrations:', error);
      toast.error('Failed to load registrations');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadRegistrations();
  };

  const handleViewDetails = (id: string) => {
    router.push(`/administration/procurement/registrations/${id}`);
  };

  const handleExportToExcel = () => {
    try {
      if (registrations.length === 0) {
        toast.error('No data to export');
        return;
      }

      // Prepare data for export
      const exportData = registrations.map(registration => ({
        'Application Number': registration.applicationNumber,
        'Company Name': registration.companyName,
        'Email': registration.email || '',
        'Phone': registration.phone || '',
        'Partner Type': registration.partnerType,
        'Status': registration.status,
        'Completion %': registration.completionPercentage ? `${registration.completionPercentage}%` : '0%',
        'Submitted Date': formatDateTime(registration.submittedDate),
        'Reviewed Date': formatDateTime(registration.reviewedDate),
        'Reviewed By': registration.reviewedBy || '',
        'Created Date': formatDateTime(registration.createdAt),
      }));

      // Create worksheet
      const ws = XLSX.utils.json_to_sheet(exportData);

      // Auto-adjust column widths
      const colWidths = Object.keys(exportData[0] || {}).map(key => ({
        wch: Math.max(key.length, 15)
      }));
      ws['!cols'] = colWidths;

      // Create workbook
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Registrations');

      // Generate filename with current date
      const filename = `business_partner_registrations_${new Date().toISOString().split('T')[0]}.xlsx`;

      // Save file
      XLSX.writeFile(wb, filename);

      toast.success(`Exported ${registrations.length} registration(s) to Excel`);
    } catch (error) {
      console.error('Error exporting to Excel:', error);
      toast.error('Failed to export to Excel');
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline'; className?: string }> = {
      Draft: { label: 'Draft', variant: 'secondary' },
      Submitted: { label: 'Submitted', variant: 'default' },
      UnderReview: { label: 'Under Review', variant: 'outline' },
      Approved: { label: 'Approved', variant: 'default', className: 'bg-green-100 text-green-800 hover:bg-green-100' },
      Rejected: { label: 'Rejected', variant: 'destructive' },
      MoreInfoRequired: { label: 'More Info Required', variant: 'outline' },
    };

    const config = statusConfig[status] || { label: status, variant: 'secondary' };
    return <Badge variant={config.variant} className={config.className}>{config.label}</Badge>;
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Business Partner Registrations</h1>
        <p className="text-gray-600 mt-2">Review and manage business partner registration applications</p>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Filter className="w-5 h-5" />
            Filters
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="md:col-span-2">
              <div className="flex gap-2">
                <Input
                  placeholder="Search by company name, email, or application number..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                />
                <Button onClick={handleSearch}>
                  <Search className="w-4 h-4" />
                </Button>
              </div>
            </div>

            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="All Statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Statuses</SelectItem>
                <SelectItem value="Submitted">Submitted</SelectItem>
                <SelectItem value="UnderReview">Under Review</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
                <SelectItem value="MoreInfoRequired">More Info Required</SelectItem>
              </SelectContent>
            </Select>

            <Select value={partnerTypeFilter} onValueChange={setPartnerTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="All Types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="Supplier">Supplier</SelectItem>
                <SelectItem value="Contractor">Contractor</SelectItem>
                <SelectItem value="Both">Both</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="flex justify-end gap-2 mt-4">
            <Button variant="outline" onClick={handleExportToExcel} disabled={registrations.length === 0}>
              <Download className="w-4 h-4 mr-2" />
              Export to Excel
            </Button>
            <Button variant="outline" onClick={loadRegistrations}>
              <RefreshCw className="w-4 h-4 mr-2" />
              Refresh
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Registrations Table */}
      <Card>
        <CardHeader>
          <CardTitle>Registrations</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${registrations.length} registration(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">Loading registrations...</div>
          ) : registrations.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No registrations found</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Application #</TableHead>
                    <TableHead>Company Name</TableHead>
                    <TableHead>Partner Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Submitted</TableHead>
                    <TableHead>Completion</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {registrations.map((registration) => (
                    <TableRow key={registration.id}>
                      <TableCell className="font-mono text-sm">{registration.applicationNumber}</TableCell>
                      <TableCell>
                        <div>
                          <div className="font-medium">{registration.companyName}</div>
                          {registration.tradingName && (
                            <div className="text-sm text-gray-500">{registration.tradingName}</div>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{registration.partnerType}</Badge>
                      </TableCell>
                      <TableCell>{getStatusBadge(registration.status)}</TableCell>
                      <TableCell>
                        {registration.submittedDate
                          ? formatDateTime(registration.submittedDate)
                          : '-'}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <div className="w-16 bg-gray-200 rounded-full h-2">
                            <div
                              className="bg-blue-600 h-2 rounded-full"
                              style={{ width: `${registration.completionPercentage}%` }}
                            />
                          </div>
                          <span className="text-sm">{registration.completionPercentage}%</span>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleViewDetails(registration.id)}
                        >
                          <Eye className="w-4 h-4 mr-1" />
                          Review
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-4">
              <Button
                variant="outline"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <span className="flex items-center px-4">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

