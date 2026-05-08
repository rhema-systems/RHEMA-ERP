'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { performanceTrackingService, type SupplierPerformanceMetricDto } from '@/services/performanceTrackingService';
import { toast } from 'sonner';
import { ArrowLeft, Download, TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

export default function SupplierComparisonPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [selectedSupplierIds, setSelectedSupplierIds] = useState<string[]>([]);
  const [performanceData, setPerformanceData] = useState<Map<string, SupplierPerformanceMetricDto>>(new Map());

  useEffect(() => {
    loadSuppliers();
  }, []);

  useEffect(() => {
    if (selectedSupplierIds.length > 0) {
      loadPerformanceData();
    }
  }, [selectedSupplierIds]);

  const loadSuppliers = async () => {
    try {
      setLoading(true);
      console.log('Loading suppliers...');
      const response = await businessPartnerService.getPartners({
        partnerType: 'Supplier',
        approvalStatus: 'Approved',
        pageSize: 1000 // Get all approved suppliers
      });
      console.log('Suppliers loaded:', response);
      setSuppliers(response.items || []);
    } catch (error) {
      console.error('Error loading suppliers:', error);
      toast.error('Failed to load suppliers');
    } finally {
      setLoading(false);
    }
  };

  const loadPerformanceData = async () => {
    try {
      const dataMap = new Map<string, SupplierPerformanceMetricDto>();
      
      for (const supplierId of selectedSupplierIds) {
        const metrics = await performanceTrackingService.getMetricsByBusinessPartner(supplierId);
        if (metrics.length > 0) {
          // Get the most recent metric
          const latestMetric = metrics.sort((a, b) => 
            new Date(b.calculationDate || b.calculatedAt).getTime() - new Date(a.calculationDate || a.calculatedAt).getTime()
          )[0];
          dataMap.set(supplierId, latestMetric);
        }
      }
      
      setPerformanceData(dataMap);
    } catch (error) {
      console.error('Error loading performance data:', error);
      toast.error('Failed to load performance data');
    }
  };

  const handleSupplierSelect = (supplierId: string) => {
    if (selectedSupplierIds.includes(supplierId)) {
      setSelectedSupplierIds(selectedSupplierIds.filter((id) => id !== supplierId));
    } else if (selectedSupplierIds.length < 5) {
      setSelectedSupplierIds([...selectedSupplierIds, supplierId]);
    } else {
      toast.error('You can compare up to 5 suppliers at a time');
    }
  };

  const exportToCSV = () => {
    if (selectedSupplierIds.length === 0) {
      toast.error('Please select suppliers to compare');
      return;
    }

    const headers = ['Metric', ...selectedSupplierIds.map((id) => {
      const supplier = suppliers.find((s) => s.id === id);
      return supplier?.partnerName || supplier?.companyName || 'Unknown';
    })];

    const metrics = [
      'Performance Grade',
      'On-Time Delivery Rate (%)',
      'Quality Acceptance Rate (%)',
      'Defect Rate (%)',
      'Compliance Score',
      'Total Orders',
      'Total Deliveries'
    ];

    const rows = metrics.map((metric) => {
      const row = [metric];
      selectedSupplierIds.forEach((id) => {
        const data = performanceData.get(id);
        if (!data) {
          row.push('N/A');
          return;
        }

        switch (metric) {
          case 'Performance Grade':
            row.push(data.performanceGrade);
            break;
          case 'On-Time Delivery Rate (%)':
            row.push(data.onTimeDeliveryRate.toFixed(1));
            break;
          case 'Quality Acceptance Rate (%)':
            row.push(data.qualityAcceptanceRate.toFixed(1));
            break;
          case 'Defect Rate (%)':
            row.push(data.defectRate.toFixed(1));
            break;
          case 'Compliance Score':
            row.push(data.complianceScore.toFixed(1));
            break;
          case 'Total Orders':
            row.push(data.totalOrders.toString());
            break;
          case 'Total Deliveries':
            row.push(((data.onTimeDeliveries ?? 0) + (data.lateDeliveries ?? 0)).toString());
            break;
          default:
            row.push('N/A');
        }
      });
      return row;
    });

    const csvContent = [headers, ...rows].map((row) => row.join(',')).join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `Supplier_Comparison_${new Date().toISOString().split('T')[0]}.csv`;
    link.click();
    URL.revokeObjectURL(url);
    toast.success('Comparison exported to CSV');
  };

  const getGradeBadge = (grade: string) => {
    const colors: Record<string, string> = {
      'A+': 'bg-green-100 text-green-800',
      'A': 'bg-green-100 text-green-800',
      'B+': 'bg-blue-100 text-blue-800',
      'B': 'bg-blue-100 text-blue-800',
      'C+': 'bg-yellow-100 text-yellow-800',
      'C': 'bg-yellow-100 text-yellow-800',
      'D': 'bg-orange-100 text-orange-800',
      'F': 'bg-red-100 text-red-800'
    };
    return <Badge className={colors[grade] || 'bg-gray-100 text-gray-800'}>{grade}</Badge>;
  };

  const getTrendIcon = (value: number, threshold: number) => {
    if (value >= threshold + 10) return <TrendingUp className="w-4 h-4 text-green-600" />;
    if (value <= threshold - 10) return <TrendingDown className="w-4 h-4 text-red-600" />;
    return <Minus className="w-4 h-4 text-gray-600" />;
  };

  return (
    <div className="container mx-auto p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <Button variant="ghost" onClick={() => router.back()} className="mb-2">
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back
          </Button>
          <h1 className="text-3xl font-bold">Supplier Comparison</h1>
          <p className="text-gray-600">Compare performance metrics across multiple suppliers</p>
        </div>
        <Button onClick={exportToCSV} disabled={selectedSupplierIds.length === 0}>
          <Download className="w-4 h-4 mr-2" />
          Export to CSV
        </Button>
      </div>

      {/* Supplier Selection */}
      <Card>
        <CardHeader>
          <CardTitle>Select Suppliers to Compare (up to 5)</CardTitle>
          <CardDescription>
            Choose suppliers to view their performance metrics side-by-side
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="text-center py-8">
              <p className="text-gray-600">Loading suppliers...</p>
            </div>
          ) : suppliers.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-gray-600">No approved suppliers found.</p>
              <p className="text-sm text-gray-500 mt-2">
                Make sure you have approved suppliers in the system.
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {suppliers.map((supplier) => (
                <div
                  key={supplier.id}
                  className={`p-4 border rounded-lg cursor-pointer transition-all ${
                    selectedSupplierIds.includes(supplier.id)
                      ? 'border-blue-500 bg-blue-50'
                      : 'border-gray-200 hover:border-gray-300'
                  }`}
                  onClick={() => handleSupplierSelect(supplier.id)}
                >
                  <div className="font-semibold">{supplier.companyName || supplier.partnerName}</div>
                  <div className="text-sm text-gray-600">{supplier.partnerCode}</div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Comparison Table */}
      {selectedSupplierIds.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Performance Comparison</CardTitle>
            <CardDescription>
              Side-by-side comparison of selected suppliers
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-4 font-semibold bg-gray-50">Metric</th>
                    {selectedSupplierIds.map((id) => {
                      const supplier = suppliers.find((s) => s.id === id);
                      return (
                        <th key={id} className="text-center p-4 font-semibold bg-gray-50">
                          {supplier?.partnerName || supplier?.companyName}
                        </th>
                      );
                    })}
                  </tr>
                </thead>
                <tbody>
                  {/* Performance Grade */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">Performance Grade</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? getGradeBadge(data.performanceGrade) : 'N/A'}
                        </td>
                      );
                    })}
                  </tr>

                  {/* On-Time Delivery Rate */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">On-Time Delivery Rate</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? (
                            <div className="flex items-center justify-center gap-2">
                              <span>{data.onTimeDeliveryRate.toFixed(1)}%</span>
                              {getTrendIcon(data.onTimeDeliveryRate, 80)}
                            </div>
                          ) : (
                            'N/A'
                          )}
                        </td>
                      );
                    })}
                  </tr>

                  {/* Quality Acceptance Rate */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">Quality Acceptance Rate</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? (
                            <div className="flex items-center justify-center gap-2">
                              <span>{data.qualityAcceptanceRate.toFixed(1)}%</span>
                              {getTrendIcon(data.qualityAcceptanceRate, 90)}
                            </div>
                          ) : (
                            'N/A'
                          )}
                        </td>
                      );
                    })}
                  </tr>

                  {/* Defect Rate */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">Defect Rate</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? `${data.defectRate.toFixed(1)}%` : 'N/A'}
                        </td>
                      );
                    })}
                  </tr>

                  {/* Compliance Score */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">Compliance Score</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? data.complianceScore.toFixed(1) : 'N/A'}
                        </td>
                      );
                    })}
                  </tr>

                  {/* Total Orders */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">Total Orders</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? data.totalOrders.toLocaleString() : 'N/A'}
                        </td>
                      );
                    })}
                  </tr>

                  {/* Total Deliveries */}
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-4 font-medium">Total Deliveries</td>
                    {selectedSupplierIds.map((id) => {
                      const data = performanceData.get(id);
                      const totalDeliveries = data ? ((data.onTimeDeliveries ?? 0) + (data.lateDeliveries ?? 0)) : 0;
                      return (
                        <td key={id} className="p-4 text-center">
                          {data ? totalDeliveries.toLocaleString() : 'N/A'}
                        </td>
                      );
                    })}
                  </tr>
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
