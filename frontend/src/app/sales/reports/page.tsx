'use client';

import { useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { BarChart3, DollarSign, Users, Target, TrendingUp, Megaphone, RefreshCw, ArrowUpRight, ArrowDownRight } from 'lucide-react';
import { toast } from 'sonner';
import { salesReportingService, type SalesReportSummary } from '@/services/salesReportingService';

export default function SalesReportsPage() {
  const [summary, setSummary] = useState<SalesReportSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  useEffect(() => { loadSummary(); }, []);

  const loadSummary = async () => {
    try {
      setLoading(true);
      const data = await salesReportingService.getSummary(fromDate || undefined, toDate || undefined);
      setSummary(data.data);
    } catch { toast.error('Failed to load sales summary'); }
    finally { setLoading(false); }
  };

  const formatAmount = (amount?: number) => {
    if (amount === undefined || amount === null) return '$0.00';
    return `$${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  if (loading) {
    return (
      <div className="container mx-auto py-6">
        <div className="text-center py-12"><BarChart3 className="h-16 w-16 animate-pulse mx-auto mb-4 text-indigo-500" /><p className="text-gray-500 text-lg">Loading sales dashboard...</p></div>
      </div>
    );
  }

  if (!summary) return <div className="container mx-auto py-6 text-center">No data available</div>;

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2"><BarChart3 className="h-8 w-8 text-indigo-600" />Sales Dashboard</h1>
          <p className="text-gray-500">Comprehensive sales performance overview</p>
        </div>
        <Button variant="outline" onClick={loadSummary}><RefreshCw className="h-4 w-4 mr-2" />Refresh</Button>
      </div>

      <Card>
        <CardContent className="pt-6">
          <div className="grid gap-4 md:grid-cols-[minmax(180px,240px)_minmax(180px,240px)_auto] md:items-end">
            <div className="space-y-2">
              <Label>From</Label>
              <Input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>To</Label>
              <Input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
            </div>
            <Button onClick={loadSummary}>
              <BarChart3 className="mr-2 h-4 w-4" />
              Apply
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Revenue KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="border-l-4 border-l-green-500">
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Revenue</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600"><DollarSign className="h-5 w-5 inline" />{summary.totalRevenue.toLocaleString()}</p></CardContent>
        </Card>
        <Card className="border-l-4 border-l-blue-500">
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Orders</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold">{summary.completedOrders} <span className="text-sm font-normal text-gray-500">/ {summary.totalOrders}</span></p></CardContent>
        </Card>
        <Card className="border-l-4 border-l-amber-500">
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Avg Order Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-amber-600"><DollarSign className="h-5 w-5 inline" />{summary.averageOrderValue.toLocaleString()}</p></CardContent>
        </Card>
        <Card className="border-l-4 border-l-purple-500">
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pipeline Value</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-purple-600"><DollarSign className="h-5 w-5 inline" />{summary.pipelineValue.toLocaleString()}</p></CardContent>
        </Card>
      </div>

      {/* Lead & Opportunity KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Total Leads</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold"><Users className="h-5 w-5 inline text-blue-500" /> {summary.totalLeads}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Conversion Rate</CardTitle></CardHeader>
          <CardContent><p className={`text-2xl font-bold ${summary.conversionRate >= 20 ? 'text-green-600' : 'text-amber-600'}`}>{summary.conversionRate}%</p>
            <p className="text-sm text-gray-500">{summary.convertedLeads} converted</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Open Opportunities</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold"><Target className="h-5 w-5 inline text-purple-500" /> {summary.openOpportunities}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active Campaigns</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold"><Megaphone className="h-5 w-5 inline text-pink-500" /> {summary.activeCampaigns}</p>
            <p className="text-sm text-gray-500">Spend: ${summary.campaignSpend.toLocaleString()}</p></CardContent>
        </Card>
      </div>

      {/* Allocation, Agreement, Refund, and Competitor KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Active Allocations</CardTitle></CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-emerald-600">{summary.activeAllocations}</p>
            <p className="text-sm text-gray-500">{summary.reservedAllocations} reserved</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Sold / Leased</CardTitle></CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{summary.soldAllocations} / {summary.leasedAllocations}</p>
            <p className="text-sm text-gray-500">{formatAmount(summary.allocationTrackedValue)} tracked</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Reservation Exposure</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-blue-600">{formatAmount(summary.reservationExposure)}</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Expiring Agreements</CardTitle></CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-amber-600">{summary.agreementsExpiringSoon}</p>
            <p className="text-sm text-gray-500">{summary.activeSalesAgreements} active</p>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Pending Refunds</CardTitle></CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-red-600">{summary.pendingRefunds}</p>
            <p className="text-sm text-gray-500">{formatAmount(summary.refundExposure)} exposure</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Competitor Deals</CardTitle></CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-indigo-600">{summary.competitorOpenDeals}</p>
            <p className="text-sm text-gray-500">{formatAmount(summary.competitorOpenDealValue)} open</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">Competitive Win Rate</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-green-600">{summary.competitorWinRate}%</p></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-gray-500">High Threat Competitors</CardTitle></CardHeader>
          <CardContent><p className="text-2xl font-bold text-orange-600">{summary.highThreatCompetitors}</p></CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Allocation by Source</CardTitle>
            <CardDescription>Source-wise sales pipeline and allocation value.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {summary.allocationBySource.length === 0 ? (
              <p className="text-sm text-gray-500">No active allocation source data yet.</p>
            ) : summary.allocationBySource.map((item) => (
              <div key={item.label} className="flex items-center justify-between rounded-md border p-3 text-sm">
                <span className="font-medium">{item.label}</span>
                <span>{item.count} records | {formatAmount(item.amount)}</span>
              </div>
            ))}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Allocation by Status</CardTitle>
            <CardDescription>Reservation, allocation, sold, leased, and release exposure.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {summary.allocationByStatus.length === 0 ? (
              <p className="text-sm text-gray-500">No allocation status data yet.</p>
            ) : summary.allocationByStatus.map((item) => (
              <div key={item.label} className="flex items-center justify-between rounded-md border p-3 text-sm">
                <span className="font-medium">{item.label}</span>
                <span>{item.count} records | {formatAmount(item.amount)}</span>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      {/* Monthly Sales */}
      {summary.monthlySales.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><TrendingUp className="h-5 w-5 text-green-500" />Monthly Revenue</CardTitle>
            <CardDescription>Revenue trend over the selected period</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {summary.monthlySales.map((m, i) => {
                const maxRevenue = Math.max(...summary.monthlySales.map(s => s.revenue));
                const barWidth = maxRevenue > 0 ? (m.revenue / maxRevenue) * 100 : 0;
                const prev = i > 0 ? summary.monthlySales[i - 1].revenue : m.revenue;
                const change = prev > 0 ? ((m.revenue - prev) / prev * 100) : 0;
                return (
                  <div key={`${m.year}-${m.month}`} className="flex items-center gap-4">
                    <span className="text-sm font-medium w-20 text-gray-600">{m.monthName} {m.year}</span>
                    <div className="flex-1 bg-gray-100 rounded-full h-6 relative">
                      <div className="bg-gradient-to-r from-green-400 to-green-600 h-6 rounded-full transition-all" style={{ width: `${barWidth}%` }} />
                    </div>
                    <span className="text-sm font-semibold w-28 text-right">${m.revenue.toLocaleString()}</span>
                    <span className={`text-xs font-medium w-16 text-right flex items-center justify-end gap-0.5 ${change >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                      {change >= 0 ? <ArrowUpRight className="h-3 w-3" /> : <ArrowDownRight className="h-3 w-3" />}
                      {Math.abs(change).toFixed(1)}%
                    </span>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
