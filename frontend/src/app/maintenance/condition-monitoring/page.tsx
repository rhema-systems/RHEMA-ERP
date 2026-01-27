'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Activity, 
  AlertTriangle, 
  CheckCircle, 
  TrendingUp,
  TrendingDown,
  Gauge,
  Thermometer,
  Zap,
  Wind,
  Droplets,
  BarChart3,
  RefreshCw,
  Plus,
  Trash2,
  Calendar
} from 'lucide-react';
import {
  LineChart,
  Line,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend
} from 'recharts';
import { assetUsageTrackingService, AssetUsageSummary, AssetUsageTracking } from '@/services/assetUsageTrackingService';
import { useToast } from '@/hooks/use-toast';
import { RecordUsageModal } from '@/components/maintenance/RecordUsageModal';

interface ConditionMetric {
  parameter: string;
  currentValue: number;
  threshold: number;
  unit: string;
  status: 'Normal' | 'Warning' | 'Critical';
  trend: 'Up' | 'Down' | 'Stable';
  icon: React.ReactNode;
}

interface AssetCondition {
  assetId: string;
  assetName: string;
  assetNumber: string;
  location?: string;
  healthScore: number;
  metrics: ConditionMetric[];
  lastUpdated: string;
}

export default function ConditionMonitoringPage() {
  const { toast } = useToast();
  const [loading, setLoading] = useState(true);
  const [usageSummaries, setUsageSummaries] = useState<AssetUsageSummary[]>([]);
  const [selectedAsset, setSelectedAsset] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [recordUsageModalOpen, setRecordUsageModalOpen] = useState(false);
  const [usageRecords, setUsageRecords] = useState<AssetUsageTracking[]>([]);
  const [loadingRecords, setLoadingRecords] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  useEffect(() => {
    if (selectedAsset) {
      loadUsageRecords();
    }
  }, [selectedAsset]);

  const loadData = async () => {
    try {
      setLoading(true);
      const summaries = await assetUsageTrackingService.getAllAssetUsageSummaries();
      setUsageSummaries(summaries);
      
      if (summaries.length > 0 && !selectedAsset) {
        setSelectedAsset(summaries[0].assetId);
      }
    } catch (error) {
      console.error('Error loading condition data:', error);
      toast({
        title: 'Error',
        description: 'Failed to load condition monitoring data',
        variant: 'destructive'
      });
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    await loadData();
    setRefreshing(false);
    toast({
      title: 'Refreshed',
      description: 'Condition monitoring data updated',
    });
  };

  const loadUsageRecords = async () => {
    if (!selectedAsset) return;
    try {
      setLoadingRecords(true);
      const records = await assetUsageTrackingService.getUsageRecords(selectedAsset);
      setUsageRecords(records);
    } catch (error) {
      console.error('Error loading usage records:', error);
      toast({
        title: 'Error',
        description: 'Failed to load usage records',
        variant: 'destructive'
      });
    } finally {
      setLoadingRecords(false);
    }
  };

  const getHealthBadge = (score: number) => {
    if (score >= 80) return <Badge className="bg-green-100 text-green-800">Excellent</Badge>;
    if (score >= 60) return <Badge className="bg-blue-100 text-blue-800">Good</Badge>;
    if (score >= 40) return <Badge className="bg-yellow-100 text-yellow-800">Fair</Badge>;
    return <Badge className="bg-red-100 text-red-800">Poor</Badge>;
  };

  const getStatusBadge = (status: string) => {
    const colors = {
      'Normal': 'bg-green-100 text-green-800',
      'Warning': 'bg-yellow-100 text-yellow-800',
      'Critical': 'bg-red-100 text-red-800',
    } as any;

    return (
      <Badge className={colors[status] || 'bg-gray-100 text-gray-800'}>
        {status}
      </Badge>
    );
  };

  const calculateHealthScore = (summary: AssetUsageSummary): number => {
    // Simple health score based on usage patterns
    let score = 100;
    
    // Deduct if no recent data
    if (!summary.lastRecordedAt) {
      score -= 30;
    } else {
      const daysSinceUpdate = Math.floor(
        (Date.now() - new Date(summary.lastRecordedAt).getTime()) / (1000 * 60 * 60 * 24)
      );
      if (daysSinceUpdate > 7) score -= 20;
    }
    
    // Deduct if usage is unusually high
    if (summary.averageDailyMileage && summary.averageDailyMileage > 500) score -= 15;
    if (summary.averageDailyOperatingHours && summary.averageDailyOperatingHours > 16) score -= 15;
    
    return Math.max(0, score);
  };

  const selectedSummary = usageSummaries.find(s => s.assetId === selectedAsset);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Condition Monitoring</h1>
          <p className="text-muted-foreground">
            Real-time asset condition monitoring and health analytics
          </p>
        </div>
        <Button onClick={handleRefresh} disabled={refreshing}>
          <RefreshCw className={`mr-2 h-4 w-4 ${refreshing ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
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
            <BreadcrumbPage>Condition Monitoring</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {loading ? (
        <Card>
          <CardContent className="p-12">
            <div className="flex items-center justify-center">
              <RefreshCw className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          </CardContent>
        </Card>
      ) : usageSummaries.length === 0 ? (
        <Card>
          <CardContent className="p-12 text-center">
            <Activity className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
            <h3 className="text-lg font-semibold mb-2">No Assets with Usage Tracking</h3>
            <p className="text-muted-foreground">
              Start tracking asset usage to enable condition monitoring
            </p>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Asset Overview Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {usageSummaries.slice(0, 4).map(summary => {
              const healthScore = calculateHealthScore(summary);
              return (
                <Card 
                  key={summary.assetId} 
                  className={`cursor-pointer transition-all ${selectedAsset === summary.assetId ? 'ring-2 ring-primary' : ''}`}
                  onClick={() => setSelectedAsset(summary.assetId)}
                >
                  <CardHeader className="pb-3">
                    <div className="flex items-center justify-between">
                      <CardTitle className="text-sm font-medium">{summary.assetName}</CardTitle>
                      <Activity className="h-4 w-4 text-muted-foreground" />
                    </div>
                    <CardDescription>{summary.assetNumber}</CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-xs text-muted-foreground">Health Score</span>
                        {getHealthBadge(healthScore)}
                      </div>
                      <div className="text-2xl font-bold">{healthScore}%</div>
                      {summary.lastRecordedAt && (
                        <p className="text-xs text-muted-foreground">
                          Updated {new Date(summary.lastRecordedAt).toLocaleDateString()}
                        </p>
                      )}
                    </div>
                  </CardContent>
                </Card>
              );
            })}
          </div>

          {/* Detailed View */}
          {selectedSummary && (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Asset Details */}
              <Card className="lg:col-span-2">
                <CardHeader>
                  <CardTitle>{selectedSummary.assetName}</CardTitle>
                  <CardDescription>Asset Number: {selectedSummary.assetNumber}</CardDescription>
                </CardHeader>
                <CardContent>
                  <Tabs defaultValue="usage">
                    <TabsList className="grid w-full grid-cols-3">
                      <TabsTrigger value="usage">Usage Metrics</TabsTrigger>
                      <TabsTrigger value="health">Health Status</TabsTrigger>
                      <TabsTrigger value="history">Usage History</TabsTrigger>
                    </TabsList>
                    
                    <TabsContent value="usage" className="space-y-4 mt-4">
                      <div className="grid grid-cols-2 gap-4">
                        {selectedSummary.currentMileage !== null && selectedSummary.currentMileage !== undefined && (
                          <Card>
                            <CardHeader className="pb-2">
                              <div className="flex items-center space-x-2">
                                <Gauge className="h-4 w-4 text-blue-500" />
                                <CardTitle className="text-sm font-medium">Current Mileage</CardTitle>
                              </div>
                            </CardHeader>
                            <CardContent>
                              <div className="text-2xl font-bold">{selectedSummary.currentMileage?.toLocaleString()}</div>
                              {selectedSummary.averageDailyMileage && (
                                <p className="text-xs text-muted-foreground mt-1">
                                  Avg: {selectedSummary.averageDailyMileage.toFixed(1)} / day
                                </p>
                              )}
                            </CardContent>
                          </Card>
                        )}
                        
                        {selectedSummary.currentOperatingHours !== null && selectedSummary.currentOperatingHours !== undefined && (
                          <Card>
                            <CardHeader className="pb-2">
                              <div className="flex items-center space-x-2">
                                <Activity className="h-4 w-4 text-green-500" />
                                <CardTitle className="text-sm font-medium">Operating Hours</CardTitle>
                              </div>
                            </CardHeader>
                            <CardContent>
                              <div className="text-2xl font-bold">{selectedSummary.currentOperatingHours?.toLocaleString()}</div>
                              {selectedSummary.averageDailyOperatingHours && (
                                <p className="text-xs text-muted-foreground mt-1">
                                  Avg: {selectedSummary.averageDailyOperatingHours.toFixed(1)} hrs/day
                                </p>
                              )}
                            </CardContent>
                          </Card>
                        )}
                        
                        {selectedSummary.currentCycles !== null && selectedSummary.currentCycles !== undefined && (
                          <Card>
                            <CardHeader className="pb-2">
                              <div className="flex items-center space-x-2">
                                <RefreshCw className="h-4 w-4 text-purple-500" />
                                <CardTitle className="text-sm font-medium">Cycles</CardTitle>
                              </div>
                            </CardHeader>
                            <CardContent>
                              <div className="text-2xl font-bold">{selectedSummary.currentCycles?.toLocaleString()}</div>
                            </CardContent>
                          </Card>
                        )}
                        
                        {selectedSummary.totalFuelConsumed !== null && selectedSummary.totalFuelConsumed !== undefined && (
                          <Card>
                            <CardHeader className="pb-2">
                              <div className="flex items-center space-x-2">
                                <Droplets className="h-4 w-4 text-orange-500" />
                                <CardTitle className="text-sm font-medium">Fuel Consumed</CardTitle>
                              </div>
                            </CardHeader>
                            <CardContent>
                              <div className="text-2xl font-bold">{selectedSummary.totalFuelConsumed?.toLocaleString()}</div>
                              <p className="text-xs text-muted-foreground mt-1">Total liters</p>
                            </CardContent>
                          </Card>
                        )}
                      </div>
                    </TabsContent>
                    
                    <TabsContent value="health" className="space-y-4 mt-4">
                      <div className="space-y-3">
                        <div className="flex items-center justify-between p-3 border rounded-lg">
                          <div className="flex items-center space-x-3">
                            <CheckCircle className="h-5 w-5 text-green-500" />
                            <div>
                              <p className="font-medium text-sm">Overall Health</p>
                              <p className="text-xs text-muted-foreground">All systems normal</p>
                            </div>
                          </div>
                          {getStatusBadge('Normal')}
                        </div>
                        
                        <div className="flex items-center justify-between p-3 border rounded-lg">
                          <div className="flex items-center space-x-3">
                            <Activity className="h-5 w-5 text-blue-500" />
                            <div>
                              <p className="font-medium text-sm">Usage Pattern</p>
                              <p className="text-xs text-muted-foreground">Within expected range</p>
                            </div>
                          </div>
                          {getStatusBadge('Normal')}
                        </div>
                        
                        {selectedSummary.totalRecords < 5 && (
                          <div className="flex items-center justify-between p-3 border rounded-lg bg-yellow-50">
                            <div className="flex items-center space-x-3">
                              <AlertTriangle className="h-5 w-5 text-yellow-600" />
                              <div>
                                <p className="font-medium text-sm">Limited Data</p>
                                <p className="text-xs text-muted-foreground">More tracking data needed for accurate analysis</p>
                              </div>
                            </div>
                            {getStatusBadge('Warning')}
                          </div>
                        )}
                      </div>
                    </TabsContent>
                    
                    <TabsContent value="history" className="space-y-4 mt-4">
                      {loadingRecords ? (
                        <div className="flex items-center justify-center py-8">
                          <RefreshCw className="h-6 w-6 animate-spin text-muted-foreground" />
                        </div>
                      ) : usageRecords.length === 0 ? (
                        <div className="text-center py-8">
                          <Activity className="h-12 w-12 mx-auto text-muted-foreground mb-3 opacity-50" />
                          <p className="text-muted-foreground">No usage records yet</p>
                        </div>
                      ) : (
                        <div className="space-y-3 max-h-96 overflow-y-auto">
                          {usageRecords.map(record => (
                            <div key={record.id} className="border rounded-lg p-3 space-y-2 hover:bg-muted/50 transition-colors">
                              <div className="flex items-center justify-between">
                                <div className="flex items-center space-x-2">
                                  <Calendar className="h-4 w-4 text-muted-foreground" />
                                  <p className="text-sm font-medium">
                                    {new Date(record.recordedAt).toLocaleDateString('en-US', {
                                      month: 'short',
                                      day: 'numeric',
                                      year: 'numeric',
                                      hour: '2-digit',
                                      minute: '2-digit'
                                    })}
                                  </p>
                                </div>
                                <Badge variant="outline" className="text-xs">
                                  {record.dataSource || 'Manual'}
                                </Badge>
                              </div>
                              
                              <div className="grid grid-cols-2 gap-2 text-xs">
                                {record.mileage !== null && record.mileage !== undefined && (
                                  <div className="flex items-center space-x-1">
                                    <Gauge className="h-3 w-3 text-blue-500" />
                                    <span className="text-muted-foreground">Mileage:</span>
                                    <span className="font-semibold">{record.mileage.toLocaleString()} {record.mileageUnit || 'km'}</span>
                                  </div>
                                )}
                                {record.operatingHours !== null && record.operatingHours !== undefined && (
                                  <div className="flex items-center space-x-1">
                                    <Activity className="h-3 w-3 text-green-500" />
                                    <span className="text-muted-foreground">Hours:</span>
                                    <span className="font-semibold">{record.operatingHours.toLocaleString()} hrs</span>
                                  </div>
                                )}
                                {record.cycles !== null && record.cycles !== undefined && (
                                  <div className="flex items-center space-x-1">
                                    <RefreshCw className="h-3 w-3 text-purple-500" />
                                    <span className="text-muted-foreground">Cycles:</span>
                                    <span className="font-semibold">{record.cycles.toLocaleString()}</span>
                                  </div>
                                )}
                                {record.fuelConsumed !== null && record.fuelConsumed !== undefined && (
                                  <div className="flex items-center space-x-1">
                                    <Droplets className="h-3 w-3 text-orange-500" />
                                    <span className="text-muted-foreground">Fuel:</span>
                                    <span className="font-semibold">{record.fuelConsumed.toLocaleString()} {record.fuelUnit || 'L'}</span>
                                  </div>
                                )}
                              </div>
                              
                              {record.notes && (
                                <p className="text-xs text-muted-foreground bg-muted/30 rounded p-2 italic">
                                  {record.notes}
                                </p>
                              )}
                              
                              <div className="flex items-center justify-between pt-2 border-t">
                                <span className="text-xs text-muted-foreground">
                                  {record.isValidated ? <CheckCircle className="h-3 w-3 text-green-600 inline mr-1" /> : null}
                                  {record.recordedByName || 'System'}
                                </span>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </TabsContent>
                  </Tabs>
                </CardContent>
              </Card>

              {/* Statistics Summary */}
              <Card>
                <CardHeader>
                  <CardTitle>Statistics</CardTitle>
                  <CardDescription>Usage tracking summary</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div>
                    <Label className="text-sm text-muted-foreground">Total Records</Label>
                    <p className="text-2xl font-bold mt-1">{selectedSummary.totalRecords}</p>
                  </div>
                  
                  {selectedSummary.lastRecordedAt && (
                    <div>
                      <Label className="text-sm text-muted-foreground">Last Updated</Label>
                      <p className="text-sm mt-1">
                        {new Date(selectedSummary.lastRecordedAt).toLocaleDateString('en-US', {
                          year: 'numeric',
                          month: 'long',
                          day: 'numeric',
                          hour: '2-digit',
                          minute: '2-digit'
                        })}
                      </p>
                    </div>
                  )}
                  
                  <div className="pt-4 border-t">
                    <Label className="text-sm text-muted-foreground mb-2 block">Health Score</Label>
                    <div className="flex items-center space-x-2">
                      <div className="flex-1 h-2 bg-gray-200 rounded-full overflow-hidden">
                        <div 
                          className={`h-full ${
                            calculateHealthScore(selectedSummary) >= 80 ? 'bg-green-500' :
                            calculateHealthScore(selectedSummary) >= 60 ? 'bg-blue-500' :
                            calculateHealthScore(selectedSummary) >= 40 ? 'bg-yellow-500' : 'bg-red-500'
                          }`}
                          style={{ width: `${calculateHealthScore(selectedSummary)}%` }}
                        />
                      </div>
                      <span className="text-sm font-bold">{calculateHealthScore(selectedSummary)}%</span>
                    </div>
                  </div>
                  
                  <div className="flex flex-col gap-2 mt-4">
                    <Button 
                      className="w-full" 
                      onClick={() => setRecordUsageModalOpen(true)}
                    >
                      <Plus className="mr-2 h-4 w-4" />
                      Record Usage
                    </Button>
                    <Button 
                      className="w-full" 
                      variant="outline"
                      onClick={() => window.open(`/maintenance/assets?id=${selectedSummary.assetId}`, '_blank')}
                    >
                      View Asset Details
                    </Button>
                  </div>
                </CardContent>
              </Card>
            </div>
          )}

          {/* All Assets List */}
          {usageSummaries.length > 4 && (
            <Card>
              <CardHeader>
                <CardTitle>All Monitored Assets</CardTitle>
                <CardDescription>Complete list of assets with usage tracking</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {usageSummaries.map(summary => (
                    <div 
                      key={summary.assetId}
                      className={`flex items-center justify-between p-3 border rounded-lg cursor-pointer transition-all ${
                        selectedAsset === summary.assetId ? 'bg-primary/5 border-primary' : 'hover:bg-muted/50'
                      }`}
                      onClick={() => setSelectedAsset(summary.assetId)}
                    >
                      <div>
                        <p className="font-medium text-sm">{summary.assetName}</p>
                        <p className="text-xs text-muted-foreground">{summary.assetNumber}</p>
                      </div>
                      <div className="flex items-center space-x-3">
                        {getHealthBadge(calculateHealthScore(summary))}
                        <span className="text-sm font-bold">{calculateHealthScore(summary)}%</span>
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}

      {/* Record Usage Modal */}
      {selectedSummary && (
        <RecordUsageModal
          assetId={selectedSummary.assetId}
          assetName={selectedSummary.assetName}
          assetNumber={selectedSummary.assetNumber}
          open={recordUsageModalOpen}
          onOpenChange={setRecordUsageModalOpen}
          onSuccess={loadData}
        />
      )}
    </div>
  );
}
