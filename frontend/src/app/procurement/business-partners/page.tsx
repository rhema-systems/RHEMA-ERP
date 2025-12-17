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
  Edit,
  Ban,
  Star,
  Filter,
  RefreshCw,
  Plus,
  Download
} from 'lucide-react';
import { toast } from 'sonner';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

export default function BusinessPartnersPage() {
  const router = useRouter();
  const [partners, setPartners] = useState<BusinessPartnerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [partnerTypeFilter, setPartnerTypeFilter] = useState('all');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  useEffect(() => {
    loadPartners();
  }, [page, statusFilter, partnerTypeFilter]);

  const loadPartners = async () => {
    try {
      setLoading(true);
      const result = await businessPartnerService.getPartners({
        page,
        pageSize: 25,
        search: searchTerm || undefined,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        partnerType: partnerTypeFilter !== 'all' ? partnerTypeFilter : undefined,
      });
      setPartners(result.items);
      setTotalPages(result.totalPages);
    } catch (error) {
      console.error('Error loading business partners:', error);
      toast.error('Failed to load business partners');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setPage(1);
    loadPartners();
  };

  const handleViewDetails = (id: string) => {
    router.push(`/procurement/business-partners/${id}`);
  };

  const handleEdit = (id: string) => {
    router.push(`/procurement/business-partners/${id}/edit`);
  };

  const handleExportToExcel = () => {
    try {
      if (partners.length === 0) {
        toast.error('No data to export');
        return;
      }

      // Prepare data for export
      const exportData = partners.map(partner => ({
        'Partner Code': partner.partnerCode,
        'Company Name': partner.companyName,
        'Trading Name': partner.tradingName || '',
        'Partner Type': partner.partnerType,
        'Status': partner.status,
        'Email': partner.email || '',
        'Phone': partner.phone || '',
        'City': partner.city || '',
        'Country': partner.country || '',
        'Is Preferred': partner.isPreferred ? 'Yes' : 'No',
        'Is Blacklisted': partner.isBlacklisted ? 'Yes' : 'No',
        'Registration Date': partner.createdAt ? new Date(partner.createdAt).toLocaleDateString() : '',
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
      XLSX.utils.book_append_sheet(wb, ws, 'Business Partners');

      // Generate filename with current date
      const filename = `business_partners_${new Date().toISOString().split('T')[0]}.xlsx`;

      // Save file
      XLSX.writeFile(wb, filename);

      toast.success(`Exported ${partners.length} business partner(s) to Excel`);
    } catch (error) {
      console.error('Error exporting to Excel:', error);
      toast.error('Failed to export to Excel');
    }
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { label: string; variant: 'default' | 'secondary' | 'destructive' | 'outline' }> = {
      Active: { label: 'Active', variant: 'default' },
      Inactive: { label: 'Inactive', variant: 'secondary' },
      Suspended: { label: 'Suspended', variant: 'destructive' },
      PendingApproval: { label: 'Pending Approval', variant: 'outline' },
    };

    const config = statusConfig[status] || { label: status, variant: 'secondary' };
    return <Badge variant={config.variant}>{config.label}</Badge>;
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Business Partners</h1>
          <p className="text-gray-600 mt-2">Manage suppliers and contractors</p>
        </div>
        <Button onClick={() => router.push('/procurement/business-partners/new')}>
          <Plus className="w-4 h-4 mr-2" />
          Add Business Partner
        </Button>
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
                  placeholder="Search by company name, code, or email..."
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
                <SelectItem value="Active">Active</SelectItem>
                <SelectItem value="Inactive">Inactive</SelectItem>
                <SelectItem value="Suspended">Suspended</SelectItem>
                <SelectItem value="PendingApproval">Pending Approval</SelectItem>
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
            <Button variant="outline" onClick={handleExportToExcel} disabled={partners.length === 0}>
              <Download className="w-4 h-4 mr-2" />
              Export to Excel
            </Button>
            <Button variant="outline" onClick={loadPartners}>
              <RefreshCw className="w-4 h-4 mr-2" />
              Refresh
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Partners Table */}
      <Card>
        <CardHeader>
          <CardTitle>Business Partners</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${partners.length} partner(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">Loading business partners...</div>
          ) : partners.length === 0 ? (
            <div className="text-center py-8 text-gray-500">No business partners found</div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Partner Code</TableHead>
                    <TableHead>Company Name</TableHead>
                    <TableHead>Partner Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Rating</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {partners.map((partner) => (
                    <TableRow key={partner.id}>
                      <TableCell className="font-mono text-sm">{partner.partnerCode}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          {partner.isPreferred && <Star className="w-4 h-4 text-yellow-500 fill-yellow-500" />}
                          <div>
                            <div className="font-medium">{partner.partnerName || partner.companyName}</div>
                            {partner.tradingName && (
                              <div className="text-sm text-gray-500">{partner.tradingName}</div>
                            )}
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{partner.partnerType}</Badge>
                      </TableCell>
                      <TableCell>{getStatusBadge(partner.status)}</TableCell>
                      <TableCell>
                        {partner.performanceRating ? (
                          <div className="flex items-center gap-1">
                            <Star className="w-4 h-4 text-yellow-500 fill-yellow-500" />
                            <span>{partner.performanceRating.toFixed(1)}</span>
                          </div>
                        ) : (
                          <span className="text-gray-400">-</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => handleViewDetails(partner.id)}
                          >
                            <Eye className="w-4 h-4" />
                          </Button>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => handleEdit(partner.id)}
                          >
                            <Edit className="w-4 h-4" />
                          </Button>
                        </div>
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

