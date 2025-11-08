'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Calendar, DateRange } from '@/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { 
  BarChart, 
  Bar, 
  XAxis, 
  YAxis, 
  CartesianGrid, 
  Tooltip, 
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  LineChart,
  Line,
  AreaChart,
  Area
} from 'recharts';
import { 
  BarChart3,
  Download,
  Calendar as CalendarIcon,
  Filter,
  FileText,
  TrendingUp,
  TrendingDown,
  DollarSign,
  Clock,
  CheckCircle,
  AlertTriangle,
  Users,
  Package,
  Wrench,
  Eye,
  Plus
} from 'lucide-react';
import { format } from 'date-fns';

interface ReportsData {
  workOrdersByMonth: Array<{ month: string; preventive: number; corrective: number; emergency: number }>;
  maintenanceCostTrend: Array<{ month: string; cost: number }>;
  workOrdersByType: Array<{ name: string; value: number; color: string }>;
  assetReliability: Array<{ asset: string; uptime: number; downtime: number }>;
  technicianPerformance: Array<{ technician: string; completed: number; avgTime: number; rating: number }>;
  reportTemplates: Array<{
    id: number;
    name: string;
    description: string;
    category: string;
    frequency: string;
  }>;
}

export default function MaintenanceReportsPage() {
  const [loading, setLoading] = useState(true);
  const [reportsData, setReportsData] = useState<ReportsData>({
    workOrdersByMonth: [],
    maintenanceCostTrend: [],
    workOrdersByType: [],
    assetReliability: [],
    technicianPerformance: [],
    reportTemplates: []
  });
  const [dateRange, setDateRange] = useState<DateRange | undefined>();
  const [selectedReportType, setSelectedReportType] = useState('summary');
  const [selectedAsset, setSelectedAsset] = useState('all');

  // Load reports data from API
  useEffect(() => {
    const loadReportsData = async () => {
      setLoading(true);
      try {
        const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
        const token = localStorage.getItem('authToken');
        const response = await fetch(`${API_URL}/maintenance/reports/data`, {
          headers: {
            'Authorization': token ? `Bearer ${token}` : '',
            'Content-Type': 'application/json'
          }
        });
        if (response.ok) {
          const data = await response.json();
          setReportsData(data.data || data || {
            workOrdersByMonth: [],
            maintenanceCostTrend: [],
            workOrdersByType: [],
            assetReliability: [],
            technicianPerformance: [],
            reportTemplates: []
          });
        }
      } catch (error) {
        console.error('Failed to load reports data:', error);
      } finally {
        setLoading(false);
      }
    };

    loadReportsData();
  }, []);

  const renderCustomizedLabel = ({ cx, cy, midAngle, innerRadius, outerRadius, percent }) => {
    const RADIAN = Math.PI / 180;
    const radius = innerRadius + (outerRadius - innerRadius) * 0.5;
    const x = cx + radius * Math.cos(-midAngle * RADIAN);
    const y = cy + radius * Math.sin(-midAngle * RADIAN);

    return (
      <text x={x} y={y} fill="white" textAnchor={x > cx ? 'start' : 'end'} dominantBaseline="central">
        {`${(percent * 100).toFixed(0)}%`}
      </text>
    );
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Maintenance Reports</h1>
          <p className="text-muted-foreground">
            Analytics, insights, and performance metrics for maintenance operations
          </p>
        </div>
        <div className="flex space-x-2">
          <Button variant="outline">
            <Filter className="mr-2 h-4 w-4" />
            Filters
          </Button>
          <Button>
            <Download className="mr-2 h-4 w-4" />
            Export
          </Button>
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
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Reports</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">189</p>
                <p className="text-sm text-muted-foreground">Total Work Orders</p>
                <p className="text-xs text-green-600 flex items-center mt-1">
                  <TrendingUp className="h-3 w-3 mr-1" />
                  +12% from last month
                </p>
              </div>
              <Wrench className="h-8 w-8 text-blue-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">$71,400</p>
                <p className="text-sm text-muted-foreground">Total Costs</p>
                <p className="text-xs text-red-600 flex items-center mt-1">
                  <TrendingDown className="h-3 w-3 mr-1" />
                  -8% from last month
                </p>
              </div>
              <DollarSign className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">98.2%</p>
                <p className="text-sm text-muted-foreground">Avg Uptime</p>
                <p className="text-xs text-green-600 flex items-center mt-1">
                  <TrendingUp className="h-3 w-3 mr-1" />
                  +0.3% from last month
                </p>
              </div>
              <CheckCircle className="h-8 w-8 text-green-500" />
            </div>
          </CardContent>
        </Card>
        
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-2xl font-bold">2.8h</p>
                <p className="text-sm text-muted-foreground">Avg Resolution</p>
                <p className="text-xs text-green-600 flex items-center mt-1">
                  <TrendingDown className="h-3 w-3 mr-1" />
                  -15min from last month
                </p>
              </div>
              <Clock className="h-8 w-8 text-orange-500" />
            </div>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="analytics" className="space-y-4">
        <TabsList>
          <TabsTrigger value="analytics">Analytics</TabsTrigger>
          <TabsTrigger value="templates">Report Templates</TabsTrigger>
          <TabsTrigger value="scheduled">Scheduled Reports</TabsTrigger>
        </TabsList>

        <TabsContent value="analytics" className="space-y-4">
          {/* Filters */}
          <Card>
            <CardHeader>
              <CardTitle>Report Filters</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Select value={selectedReportType} onValueChange={setSelectedReportType}>
                  <SelectTrigger>
                    <SelectValue placeholder="Report Type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="summary">Summary</SelectItem>
                    <SelectItem value="asset">Asset Performance</SelectItem>
                    <SelectItem value="cost">Cost Analysis</SelectItem>
                    <SelectItem value="technician">Technician Performance</SelectItem>
                  </SelectContent>
                </Select>

                <Select value={selectedAsset} onValueChange={setSelectedAsset}>
                  <SelectTrigger>
                    <SelectValue placeholder="Asset Filter" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All Assets</SelectItem>
                    <SelectItem value="hvac">HVAC Systems</SelectItem>
                    <SelectItem value="electrical">Electrical</SelectItem>
                    <SelectItem value="plumbing">Plumbing</SelectItem>
                    <SelectItem value="elevator">Elevators</SelectItem>
                  </SelectContent>
                </Select>

                <Popover>
                  <PopoverTrigger asChild>
                    <Button variant="outline" className="justify-start text-left font-normal">
                      <CalendarIcon className="mr-2 h-4 w-4" />
                      {dateRange?.from ? (
                        dateRange.to ? (
                          <>
                            {format(dateRange.from, "LLL dd, y")} -{" "}
                            {format(dateRange.to, "LLL dd, y")}
                          </>
                        ) : (
                          format(dateRange.from, "LLL dd, y")
                        )
                      ) : (
                        <span>Pick date range</span>
                      )}
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent className="w-auto p-0" align="start">
                    <Calendar
                      initialFocus
                      mode="range"
                      defaultMonth={dateRange?.from}
                      selected={dateRange}
                      onSelect={setDateRange}
                      numberOfMonths={2}
                    />
                  </PopoverContent>
                </Popover>
              </div>
            </CardContent>
          </Card>

          {/* Charts */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Work Orders by Month */}
            <Card>
              <CardHeader>
                <CardTitle>Work Orders Trend</CardTitle>
                <CardDescription>Monthly breakdown by maintenance type</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <AreaChart data={reportsData.workOrdersByMonth}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="month" />
                    <YAxis />
                    <Tooltip />
                    <Area type="monotone" dataKey="preventive" stackId="1" stroke="#3b82f6" fill="#3b82f6" />
                    <Area type="monotone" dataKey="corrective" stackId="1" stroke="#f59e0b" fill="#f59e0b" />
                    <Area type="monotone" dataKey="emergency" stackId="1" stroke="#ef4444" fill="#ef4444" />
                  </AreaChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>

            {/* Work Orders by Type */}
            <Card>
              <CardHeader>
                <CardTitle>Work Orders by Type</CardTitle>
                <CardDescription>Distribution of maintenance types</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <PieChart>
                    <Pie
                      data={reportsData.workOrdersByType}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={renderCustomizedLabel}
                      outerRadius={100}
                      fill="#8884d8"
                      dataKey="value"
                    >
                      {reportsData.workOrdersByType.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
                <div className="flex flex-wrap justify-center gap-4 mt-4">
                  {reportsData.workOrdersByType.map((item) => (
                    <div key={item.name} className="flex items-center space-x-2">
                      <div
                        className="w-3 h-3 rounded-full"
                        style={{ backgroundColor: item.color }}
                      />
                      <span className="text-sm">{item.name} ({item.value})</span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

            {/* Maintenance Cost Trend */}
            <Card>
              <CardHeader>
                <CardTitle>Maintenance Costs</CardTitle>
                <CardDescription>Monthly cost trends</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={reportsData.maintenanceCostTrend}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="month" />
                    <YAxis />
                    <Tooltip formatter={(value) => [`$${value.toLocaleString()}`, 'Cost']} />
                    <Line type="monotone" dataKey="cost" stroke="#10b981" strokeWidth={3} />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>

            {/* Asset Reliability */}
            <Card>
              <CardHeader>
                <CardTitle>Asset Reliability</CardTitle>
                <CardDescription>Uptime percentage by asset category</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={reportsData.assetReliability} layout="horizontal">
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis type="number" domain={[90, 100]} />
                    <YAxis dataKey="asset" type="category" width={80} />
                    <Tooltip formatter={(value) => [`${value}%`, 'Uptime']} />
                    <Bar dataKey="uptime" fill="#10b981" />
                  </BarChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </div>

          {/* Technician Performance Table */}
          <Card>
            <CardHeader>
              <CardTitle>Technician Performance</CardTitle>
              <CardDescription>Performance metrics by technician</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="border-b">
                      <th className="text-left py-2">Technician</th>
                      <th className="text-right py-2">Completed</th>
                      <th className="text-right py-2">Avg Time (hrs)</th>
                      <th className="text-right py-2">Rating</th>
                      <th className="text-right py-2">Performance</th>
                    </tr>
                  </thead>
                  <tbody>
                    {reportsData.technicianPerformance.map((tech, index) => (
                      <tr key={index} className="border-b">
                        <td className="py-3">{tech.technician}</td>
                        <td className="text-right py-3">{tech.completed}</td>
                        <td className="text-right py-3">{tech.avgTime}</td>
                        <td className="text-right py-3">{tech.rating}/5</td>
                        <td className="text-right py-3">
                          <div className="inline-flex items-center space-x-1">
                            <div className="w-16 h-2 bg-gray-200 rounded-full">
                              <div 
                                className="h-2 bg-green-500 rounded-full" 
                                style={{ width: `${(tech.rating / 5) * 100}%` }}
                              />
                            </div>
                            <span className="text-sm text-green-600">
                              {((tech.rating / 5) * 100).toFixed(0)}%
                            </span>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="templates" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Report Templates</CardTitle>
              <CardDescription>Pre-configured report templates for quick generation</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {reportsData.reportTemplates.map((template) => (
                  <Card key={template.id} className="cursor-pointer hover:shadow-md transition-shadow">
                    <CardContent className="pt-6">
                      <div className="space-y-4">
                        <div className="flex items-center justify-between">
                          <FileText className="h-8 w-8 text-blue-500" />
                          <span className="text-xs px-2 py-1 bg-blue-100 text-blue-800 rounded-full">
                            {template.category}
                          </span>
                        </div>
                        <div>
                          <h3 className="font-semibold mb-2">{template.name}</h3>
                          <p className="text-sm text-muted-foreground mb-2">
                            {template.description}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            Frequency: {template.frequency}
                          </p>
                        </div>
                        <div className="flex space-x-2">
                          <Button size="sm" variant="outline">
                            <Eye className="mr-2 h-4 w-4" />
                            Preview
                          </Button>
                          <Button size="sm">
                            Generate
                          </Button>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="scheduled" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Scheduled Reports</CardTitle>
              <CardDescription>Automatically generated reports sent via email</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="text-center py-12 text-muted-foreground">
                <BarChart3 className="h-12 w-12 mx-auto mb-4" />
                <p>No scheduled reports configured</p>
                <Button className="mt-4">
                  <Plus className="mr-2 h-4 w-4" />
                  Schedule Report
                </Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}