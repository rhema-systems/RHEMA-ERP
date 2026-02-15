'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  Search, Eye, CheckCircle, XCircle, Clock, Package,
  FileText, AlertTriangle, TruckIcon, Loader2, Filter
} from 'lucide-react';
import {
  purchasingService,
  PurchaseOrderReceiptDto
} from '@/services/purchasingService';
import { format } from 'date-fns';
import Link from 'next/link';
import { toast } from 'sonner';

const GRNStatuses = [
  { value: 'Pending', label: 'Pending', color: 'bg-yellow-100 text-yellow-800', icon: Clock },
  { value: 'Pending Inspection', label: 'Pending Inspection', color: 'bg-yellow-100 text-yellow-800', icon: AlertTriangle },
  { value: 'Inspection In Progress', label: 'Inspection In Progress', color: 'bg-blue-100 text-blue-800', icon: Clock },
  { value: 'Approved', label: 'Approved', color: 'bg-green-100 text-green-800', icon: CheckCircle },
  { value: 'Partially Approved', label: 'Partially Approved', color: 'bg-orange-100 text-orange-800', icon: AlertTriangle },
  { value: 'Rejected', label: 'Rejected', color: 'bg-red-100 text-red-800', icon: XCircle },
  { value: 'Completed', label: 'Completed', color: 'bg-purple-100 text-purple-800', icon: CheckCircle }
];

export default function PurchaseReceiptsPage() {
  const [receipts, setReceipts] = useState<PurchaseOrderReceiptDto[]>([]);
  const [filteredReceipts, setFilteredReceipts] = useState<PurchaseOrderReceiptDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');

  const fetchReceipts = async () => {
    try {
      setLoading(true);
      setError(null);
      const result = await purchasingService.getPurchaseReceipts({
        page: 1,
        pageSize: 1000 // Get all for now
      });
      setReceipts(result.items);
    } catch (err: any) {
      console.error('Error fetching purchase receipts:', err);
      setError('Failed to load purchase receipts');
      toast.error('Failed to load purchase receipts');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReceipts();
  }, []);

  useEffect(() => {
    let filtered = receipts;
    
    if (searchTerm) {
      filtered = filtered.filter(r =>
        r.receiptNumber.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.purchaseOrderNumber?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.supplierName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        r.deliveryNote?.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }
    
    if (statusFilter !== 'all') {
      filtered = filtered.filter(r => r.status === statusFilter);
    }
    
    setFilteredReceipts(filtered);
  }, [searchTerm, statusFilter, receipts]);

  const getStatusBadge = (status: string) => {
    const statusConfig = GRNStatuses.find(s => s.value === status);
    const Icon = statusConfig?.icon || FileText;
    return (
      <Badge className={statusConfig?.color || 'bg-gray-100'}>
        <Icon className="h-3 w-3 mr-1" />
        {statusConfig?.label || status}
      </Badge>
    );
  };

  // Calculate stats
  const totalCount = receipts.length;
  const pendingCount = receipts.filter(r => 
    r.status === 'Pending' || r.status === 'Pending Inspection' || r.status === 'Inspection In Progress'
  ).length;
  const completedCount = receipts.filter(r => r.status === 'Completed').length;
  const rejectedCount = receipts.filter(r => r.status === 'Rejected').length;

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Purchase Receipts (GRN)</h1>
          <p className="text-muted-foreground">Manage goods receipt notes and quality inspections</p>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/procurement">Procurement</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Purchase Receipts</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">{totalCount}</p>
                <p className="text-sm text-muted-foreground">Total Receipts</p>
              </div>
              <FileText className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-yellow-600">{pendingCount}</p>
                <p className="text-sm text-muted-foreground">Pending/Inspection</p>
              </div>
              <Clock className="h-8 w-8 text-yellow-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-green-600">{completedCount}</p>
                <p className="text-sm text-muted-foreground">Completed</p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold text-red-600">{rejectedCount}</p>
                <p className="text-sm text-muted-foreground">Rejected</p>
              </div>
              <XCircle className="h-8 w-8 text-red-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center">
            <Filter className="h-4 w-4 mr-2" />
            Filters
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search receipts, PO, or supplier..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Status</SelectItem>
                {GRNStatuses.map(s => (
                  <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Receipts List */}
      <Card>
        <CardHeader>
          <CardTitle>Purchase Receipts</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${filteredReceipts.length} receipt(s) found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading && (
            <div className="text-center py-8">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground mx-auto" />
            </div>
          )}
          
          {error && (
            <div className="text-center py-8 text-red-600">{error}</div>
          )}
          
          {!loading && !error && (
            <div className="space-y-3">
              {filteredReceipts.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No purchase receipts found.
                </div>
              ) : (
                filteredReceipts.map((receipt) => (
                  <div
                    key={receipt.id}
                    className="border rounded-lg p-4 hover:bg-muted/50 transition-colors"
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 rounded-lg bg-teal-100 flex items-center justify-center">
                          <TruckIcon className="h-6 w-6 text-teal-600" />
                        </div>
                        <div>
                          <div className="flex items-center space-x-2">
                            <h3 className="font-semibold">{receipt.receiptNumber}</h3>
                            {getStatusBadge(receipt.status)}
                            {receipt.requiresInspection && (
                              <Badge variant="outline" className="bg-yellow-50">
                                <AlertTriangle className="h-3 w-3 mr-1" />
                                Inspection Required
                              </Badge>
                            )}
                          </div>
                          <p className="text-sm text-muted-foreground">
                            {receipt.purchaseOrderNumber && (
                              <>
                                PO: <Link href={`/procurement/purchase-orders/${receipt.purchaseOrderId}`} className="text-blue-600 hover:underline">
                                  {receipt.purchaseOrderNumber}
                                </Link> •{' '}
                              </>
                            )}
                            Supplier: {receipt.supplierName || 'N/A'} •
                            Date: {format(new Date(receipt.receiptDate), 'MMM dd, yyyy')}
                          </p>
                          <p className="text-sm text-muted-foreground">
                            Received by: {receipt.receivedByName || 'N/A'}
                            {receipt.deliveryNote && ` • DN: ${receipt.deliveryNote}`}
                            {receipt.carrierName && ` • Carrier: ${receipt.carrierName}`}
                          </p>
                        </div>
                      </div>
                      
                      <div className="flex items-center space-x-2">
                        <Link href={`/procurement/purchase-receipts/${receipt.id}`}>
                          <Button size="sm" variant="outline">
                            <Eye className="h-4 w-4 mr-1" />
                            View
                          </Button>
                        </Link>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
