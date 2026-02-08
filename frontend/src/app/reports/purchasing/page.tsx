'use client';

import React, { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  FileText,
  ShoppingCart,
  TruckIcon,
  Building2,
  DollarSign,
  TrendingUp,
  BarChart3,
  Download,
  Calendar,
  Filter
} from 'lucide-react';
import { toast } from 'sonner';

const reportTypes = [
  {
    id: 'pr-report',
    title: 'Purchase Requisition Report',
    description: 'Detailed report of all purchase requisitions',
    icon: FileText,
    color: 'text-blue-600',
    bgColor: 'bg-blue-100'
  },
  {
    id: 'po-report',
    title: 'Purchase Order Report',
    description: 'Comprehensive purchase order analysis',
    icon: ShoppingCart,
    color: 'text-green-600',
    bgColor: 'bg-green-100'
  },
  {
    id: 'grn-report',
    title: 'Goods Receipt Report',
    description: 'Receipt and inspection history',
    icon: TruckIcon,
    color: 'text-purple-600',
    bgColor: 'bg-purple-100'
  },
  {
    id: 'supplier-performance',
    title: 'Supplier Performance Report',
    description: 'Supplier ratings and performance metrics',
    icon: Building2,
    color: 'text-orange-600',
    bgColor: 'bg-orange-100'
  },
  {
    id: 'spend-analysis',
    title: 'Spend Analysis by Supplier',
    description: 'Spending breakdown by supplier',
    icon: DollarSign,
    color: 'text-teal-600',
    bgColor: 'bg-teal-100'
  },
  {
    id: 'outstanding-pos',
    title: 'Outstanding Purchase Orders',
    description: 'Open and pending purchase orders',
    icon: FileText,
    color: 'text-red-600',
    bgColor: 'bg-red-100'
  },
  {
    id: 'price-variance',
    title: 'Purchase Price Variance',
    description: 'Price variance analysis',
    icon: TrendingUp,
    color: 'text-indigo-600',
    bgColor: 'bg-indigo-100'
  },
  {
    id: 'budget-vs-actual',
    title: 'Budget vs Actual Report',
    description: 'Budget utilization and variance',
    icon: BarChart3,
    color: 'text-pink-600',
    bgColor: 'bg-pink-100'
  }
];

export default function PurchasingReportsPage() {
  const [selectedReport, setSelectedReport] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [supplierId, setSupplierId] = useState('');
  const [status, setStatus] = useState('all');
  const [generating, setGenerating] = useState(false);

  const handleGenerateReport = async () => {
    if (!selectedReport) {
      toast.error('Please select a report type');
      return;
    }

    try {
      setGenerating(true);
      
      // Simulate report generation
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      toast.success('Report generated successfully');
      
      // In a real implementation, this would call the backend API
      // and download the report file
    } catch (error) {
      console.error('Error generating report:', error);
      toast.error('Failed to generate report');
    } finally {
      setGenerating(false);
    }
  };

  const selectedReportConfig = reportTypes.find(r => r.id === selectedReport);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Purchasing Reports</h1>
        <p className="text-muted-foreground">Generate and export purchasing analytics and reports</p>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/reports">Reports</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Purchasing</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Report Selection */}
      <Card>
        <CardHeader>
          <CardTitle>Select Report Type</CardTitle>
          <CardDescription>Choose the type of purchasing report you want to generate</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {reportTypes.map((report) => {
              const Icon = report.icon;
              const isSelected = selectedReport === report.id;
              
              return (
                <div
                  key={report.id}
                  onClick={() => setSelectedReport(report.id)}
                  className={`
                    cursor-pointer rounded-lg border-2 p-4 transition-all
                    ${isSelected 
                      ? 'border-blue-500 bg-blue-50 shadow-md' 
                      : 'border-gray-200 hover:border-gray-300 hover:bg-gray-50'
                    }
                  `}
                >
                  <div className="flex flex-col items-center text-center">
                    <div className={`
                      p-3 rounded-full mb-3
                      ${isSelected ? 'bg-blue-500 text-white' : `${report.bgColor} ${report.color}`}
                    `}>
                      <Icon className="w-6 h-6" />
                    </div>
                    <h3 className={`font-semibold text-sm ${isSelected ? 'text-blue-700' : 'text-gray-900'}`}>
                      {report.title}
                    </h3>
                    <p className="text-xs text-gray-500 mt-1">{report.description}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      {/* Report Parameters */}
      {selectedReport && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Filter className="h-5 w-5" />
              Report Parameters
            </CardTitle>
            <CardDescription>
              Configure the parameters for {selectedReportConfig?.title}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-2">
                <Label htmlFor="startDate" className="flex items-center gap-2">
                  <Calendar className="h-4 w-4" />
                  Start Date
                </Label>
                <Input
                  id="startDate"
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                />
              </div>
              
              <div className="space-y-2">
                <Label htmlFor="endDate" className="flex items-center gap-2">
                  <Calendar className="h-4 w-4" />
                  End Date
                </Label>
                <Input
                  id="endDate"
                  type="date"
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                />
              </div>
              
              {(selectedReport === 'po-report' || selectedReport === 'grn-report' || 
                selectedReport === 'supplier-performance' || selectedReport === 'spend-analysis') && (
                <div className="space-y-2">
                  <Label htmlFor="supplier">Supplier (Optional)</Label>
                  <Select value={supplierId} onValueChange={setSupplierId}>
                    <SelectTrigger>
                      <SelectValue placeholder="All Suppliers" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Suppliers</SelectItem>
                      {/* In real implementation, load suppliers from API */}
                    </SelectContent>
                  </Select>
                </div>
              )}
              
              {(selectedReport === 'pr-report' || selectedReport === 'po-report' || 
                selectedReport === 'outstanding-pos') && (
                <div className="space-y-2">
                  <Label htmlFor="status">Status</Label>
                  <Select value={status} onValueChange={setStatus}>
                    <SelectTrigger>
                      <SelectValue placeholder="All Status" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Status</SelectItem>
                      <SelectItem value="Draft">Draft</SelectItem>
                      <SelectItem value="Pending Approval">Pending Approval</SelectItem>
                      <SelectItem value="Approved">Approved</SelectItem>
                      <SelectItem value="Sent">Sent</SelectItem>
                      <SelectItem value="Received">Received</SelectItem>
                      <SelectItem value="Cancelled">Cancelled</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              )}
            </div>
            
            <div className="flex justify-end gap-4">
              <Button variant="outline" onClick={() => setSelectedReport('')}>
                Clear Selection
              </Button>
              <Button onClick={handleGenerateReport} disabled={generating}>
                {generating ? (
                  <>
                    <Download className="w-4 h-4 mr-2 animate-pulse" />
                    Generating...
                  </>
                ) : (
                  <>
                    <Download className="w-4 h-4 mr-2" />
                    Generate Report
                  </>
                )}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Report Descriptions */}
      <Card>
        <CardHeader>
          <CardTitle>Available Reports</CardTitle>
          <CardDescription>Detailed information about each report type</CardDescription>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="pr-report" className="space-y-4">
            <TabsList className="grid w-full grid-cols-4">
              <TabsTrigger value="pr-report">PR Report</TabsTrigger>
              <TabsTrigger value="po-report">PO Report</TabsTrigger>
              <TabsTrigger value="grn-report">GRN Report</TabsTrigger>
              <TabsTrigger value="supplier-performance">Performance</TabsTrigger>
            </TabsList>

            <TabsContent value="pr-report" className="space-y-4">
              <div className="space-y-2">
                <h3 className="font-semibold">Purchase Requisition Report</h3>
                <p className="text-sm text-muted-foreground">
                  This report provides a comprehensive view of all purchase requisitions including:
                </p>
                <ul className="list-disc list-inside text-sm text-muted-foreground space-y-1 ml-4">
                  <li>Requisition details (number, date, requester, department)</li>
                  <li>Item details and quantities</li>
                  <li>Approval status and history</li>
                  <li>Conversion to purchase orders</li>
                  <li>Budget allocation and utilization</li>
                </ul>
              </div>
            </TabsContent>

            <TabsContent value="po-report" className="space-y-4">
              <div className="space-y-2">
                <h3 className="font-semibold">Purchase Order Report</h3>
                <p className="text-sm text-muted-foreground">
                  Comprehensive analysis of purchase orders including:
                </p>
                <ul className="list-disc list-inside text-sm text-muted-foreground space-y-1 ml-4">
                  <li>Order details and supplier information</li>
                  <li>Item quantities and pricing</li>
                  <li>Delivery status and timelines</li>
                  <li>Receipt history and outstanding items</li>
                  <li>Financial summary and payment status</li>
                </ul>
              </div>
            </TabsContent>

            <TabsContent value="grn-report" className="space-y-4">
              <div className="space-y-2">
                <h3 className="font-semibold">Goods Receipt Report</h3>
                <p className="text-sm text-muted-foreground">
                  Detailed receipt and inspection information including:
                </p>
                <ul className="list-disc list-inside text-sm text-muted-foreground space-y-1 ml-4">
                  <li>Receipt details and delivery information</li>
                  <li>Quality inspection results</li>
                  <li>Accepted and rejected quantities</li>
                  <li>Warehouse and location assignments</li>
                  <li>Lot/serial number tracking</li>
                </ul>
              </div>
            </TabsContent>

            <TabsContent value="supplier-performance" className="space-y-4">
              <div className="space-y-2">
                <h3 className="font-semibold">Supplier Performance Report</h3>
                <p className="text-sm text-muted-foreground">
                  Performance metrics and ratings including:
                </p>
                <ul className="list-disc list-inside text-sm text-muted-foreground space-y-1 ml-4">
                  <li>On-time delivery rates</li>
                  <li>Quality acceptance rates</li>
                  <li>Compliance scores</li>
                  <li>Quality incidents and resolutions</li>
                  <li>Overall performance grades</li>
                </ul>
              </div>
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      {/* Quick Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Total PRs</p>
                <p className="text-2xl font-bold">-</p>
              </div>
              <FileText className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Total POs</p>
                <p className="text-2xl font-bold">-</p>
              </div>
              <ShoppingCart className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Total Receipts</p>
                <p className="text-2xl font-bold">-</p>
              </div>
              <TruckIcon className="h-8 w-8 text-purple-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground">Total Spend</p>
                <p className="text-2xl font-bold">-</p>
              </div>
              <DollarSign className="h-8 w-8 text-teal-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Info Card */}
      <Card className="border-blue-200 bg-blue-50">
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <FileText className="h-5 w-5 text-blue-600 mt-0.5" />
            <div>
              <p className="font-medium text-blue-900">Report Generation</p>
              <p className="text-sm text-blue-700 mt-1">
                Select a report type above and configure the parameters to generate detailed purchasing reports. 
                Reports can be exported in PDF, Excel, or CSV formats.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
