'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Eye, RefreshCw, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, type TenderDto, getMyAssignedTenders } from '@/services/tenderService';
import { format } from 'date-fns';

export default function MyAssignedTendersPage() {
  const router = useRouter();
  const [tenders, setTenders] = useState<TenderDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadAssignedTenders();
  }, []);

  const loadAssignedTenders = async () => {
    try {
      setLoading(true);
      const data = await getMyAssignedTenders();
      setTenders(data);
    } catch (error) {
      console.error('Error loading assigned tenders:', error);
      toast.error('Failed to load assigned tenders');
    } finally {
      setLoading(false);
    }
  };

  const handleViewDetails = (id: string) => {
    router.push(`/procurement/tenders/${id}`);
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Published': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Opened': { variant: 'default', className: 'bg-green-100 text-green-800' },
      'Closed': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Awarded': { variant: 'default', className: 'bg-purple-100 text-purple-800' },
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

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">My Assigned Tenders</h1>
          <p className="text-gray-500">Tenders assigned to you for evaluation</p>
        </div>
        <Button onClick={loadAssignedTenders} disabled={loading} variant="outline">
          <RefreshCw className="w-4 h-4 mr-2" />
          Refresh
        </Button>
      </div>

      {/* Stats */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm font-medium text-gray-500">Total Assigned Tenders</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-2xl font-bold">{tenders.length}</p>
        </CardContent>
      </Card>

      {/* Tenders Table */}
      <Card>
        <CardHeader>
          <CardTitle>Assigned Tenders</CardTitle>
          <CardDescription>
            {tenders.length === 0 ? 'No tenders assigned to you yet' : `You have ${tenders.length} tender(s) assigned for evaluation`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex items-center justify-center py-8">
              <div className="text-gray-500">Loading...</div>
            </div>
          ) : tenders.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-8 text-center">
              <AlertCircle className="w-12 h-12 text-gray-300 mb-4" />
              <p className="text-gray-500">No tenders assigned to you yet</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tender Number</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Closing Date</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {tenders.map((tender) => (
                    <TableRow key={tender.id}>
                      <TableCell className="font-medium">{tender.tenderNumber}</TableCell>
                      <TableCell>{tender.title}</TableCell>
                      <TableCell>{getStatusBadge(tender.status)}</TableCell>
                      <TableCell>{formatDate(tender.closingDate)}</TableCell>
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleViewDetails(tender.id)}
                        >
                          <Eye className="w-4 h-4 mr-2" />
                          View
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

