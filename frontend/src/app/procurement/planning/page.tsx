'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { AlertCircle, BarChart3, Calendar, Clock, DollarSign, Download, FileText, GitMerge, Loader2, Package, RefreshCw, ShieldAlert, TrendingUp, Users } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  procurementPlanService,
  type ProcurementPlanConsolidationOpportunityDto,
  type ProcurementPlanningDashboardDto,
} from '@/services/procurementPlanningService';
import { FiscalYearSelect } from './components/FiscalYearSelect';

const currentYear = new Date().getFullYear();

export default function ProcurementPlanningDashboardPage() {
  const router = useRouter();
  const [fiscalYear, setFiscalYear] = useState(currentYear);
  const [planningQuarter, setPlanningQuarter] = useState('all');
  const [dashboard, setDashboard] = useState<ProcurementPlanningDashboardDto | null>(null);
  const [opportunities, setOpportunities] = useState<ProcurementPlanConsolidationOpportunityDto[]>([]);
  const [loading, setLoading] = useState(true);

  const query = useMemo(() => ({
    fiscalYear,
    planningQuarter: planningQuarter === 'all' ? undefined : planningQuarter,
  }), [fiscalYear, planningQuarter]);

  const loadDashboard = async () => {
    try {
      setLoading(true);
      const [dashboardData, opportunityData] = await Promise.all([
        procurementPlanService.getDashboard(query),
        procurementPlanService.getConsolidationOpportunities(query),
      ]);
      setDashboard(dashboardData);
      setOpportunities(opportunityData);
    } catch (error) {
      console.error('Error loading procurement planning dashboard:', error);
      toast.error('Failed to load procurement planning dashboard');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDashboard();
  }, [query]);

  const formatCurrency = (amount: number, currency = dashboard?.currency || 'USD') =>
    new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount || 0);

  const statusTotal = dashboard
    ? dashboard.draftPlans + dashboard.submittedPlans + dashboard.approvedPlans + dashboard.activePlans + dashboard.completedPlans
    : 0;
  const strategic = dashboard?.strategicAnalytics;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Procurement Planning Overview</h1>
          <p className="text-sm text-muted-foreground">Annual and quarterly plan performance, budget coverage, and consolidation signals.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <FiscalYearSelect
            value={fiscalYear}
            onValueChange={setFiscalYear}
            triggerClassName="h-9 w-56"
            autoSelectFirstAvailable
          />
          <Select value={planningQuarter} onValueChange={setPlanningQuarter}>
            <SelectTrigger className="h-9 w-36">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Quarters</SelectItem>
              <SelectItem value="Q1">Q1</SelectItem>
              <SelectItem value="Q2">Q2</SelectItem>
              <SelectItem value="Q3">Q3</SelectItem>
              <SelectItem value="Q4">Q4</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" size="sm" onClick={loadDashboard} disabled={loading}>
            {loading ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <RefreshCw className="h-4 w-4 mr-2" />}
            Refresh
          </Button>
          <Button size="sm" onClick={() => router.push('/procurement/planning/reports')}>
            <Download className="h-4 w-4 mr-2" />
            Reports
          </Button>
        </div>
      </div>

      {loading && !dashboard ? (
        <div className="flex items-center justify-center py-16 text-muted-foreground">
          <Loader2 className="h-6 w-6 mr-2 animate-spin" />
          Loading planning overview...
        </div>
      ) : dashboard ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Plans</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-end justify-between">
                  <div className="text-2xl font-semibold">{dashboard.totalPlans}</div>
                  <FileText className="h-5 w-5 text-blue-600" />
                </div>
                <p className="mt-1 text-xs text-muted-foreground">{statusTotal} in tracked statuses</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Items</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-end justify-between">
                  <div className="text-2xl font-semibold">{dashboard.totalItems}</div>
                  <Package className="h-5 w-5 text-emerald-600" />
                </div>
                <p className="mt-1 text-xs text-muted-foreground">{dashboard.criticalItems} critical</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Estimated Budget</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-end justify-between">
                  <div className="text-2xl font-semibold">{formatCurrency(dashboard.estimatedBudget)}</div>
                  <DollarSign className="h-5 w-5 text-amber-600" />
                </div>
                <p className="mt-1 text-xs text-muted-foreground">{formatCurrency(dashboard.approvedBudget)} approved</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Budget Coverage</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-end justify-between">
                  <div className="text-2xl font-semibold">{dashboard.budgetUtilizationPercent.toFixed(1)}%</div>
                  <BarChart3 className="h-5 w-5 text-violet-600" />
                </div>
                <p className="mt-1 text-xs text-muted-foreground">approved vs estimated</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Consolidation Savings</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-end justify-between">
                  <div className="text-2xl font-semibold">{formatCurrency(dashboard.consolidationPotentialSavings)}</div>
                  <GitMerge className="h-5 w-5 text-cyan-600" />
                </div>
                <p className="mt-1 text-xs text-muted-foreground">{opportunities.length} opportunities</p>
              </CardContent>
            </Card>
          </div>

          {strategic && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-lg font-semibold tracking-tight">Strategic Procurement Analytics</h2>
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" onClick={() => router.push('/procurement/planning/market-analysis')}>Market</Button>
                  <Button variant="outline" size="sm" onClick={() => router.push('/procurement/planning/supplier-consolidation')}>Suppliers</Button>
                </div>
              </div>
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">Avg Price Increase</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="flex items-end justify-between">
                      <div className="text-2xl font-semibold">{strategic.market.averagePriceIncreasePercent.toFixed(1)}%</div>
                      <TrendingUp className="h-5 w-5 text-red-600" />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{strategic.market.highInflationCategoryCount} high-inflation categories</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">Market Risk Index</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="flex items-end justify-between">
                      <div className="text-2xl font-semibold">{strategic.market.marketRiskIndex.toFixed(1)}</div>
                      <ShieldAlert className="h-5 w-5 text-amber-600" />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{strategic.market.highRiskCategoryCount} high-risk categories</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">Long Lead-Time Items</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="flex items-end justify-between">
                      <div className="text-2xl font-semibold">{strategic.market.longLeadTimeItemCount}</div>
                      <Clock className="h-5 w-5 text-cyan-600" />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{strategic.market.inflationImpactPercent.toFixed(1)}% avg inflation impact</p>
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm font-medium text-muted-foreground">Supplier Risk Score</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <div className="flex items-end justify-between">
                      <div className="text-2xl font-semibold">{strategic.supplier.supplierRiskScore.toFixed(1)}%</div>
                      <Users className="h-5 w-5 text-violet-600" />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{strategic.supplier.activeSuppliers} active, {strategic.supplier.preferredSuppliers} preferred</p>
                  </CardContent>
                </Card>
              </div>

              <div className="grid gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader>
                    <CardTitle>Market Risk Categories</CardTitle>
                  </CardHeader>
                  <CardContent>
                    {strategic.market.highRiskCategories.length === 0 ? (
                      <div className="py-6 text-sm text-muted-foreground">No high-risk market categories for this period.</div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow><TableHead>Category</TableHead><TableHead>Analyses</TableHead><TableHead>Increase</TableHead><TableHead>Risk</TableHead></TableRow>
                        </TableHeader>
                        <TableBody>
                          {strategic.market.highRiskCategories.slice(0, 5).map((row) => (
                            <TableRow key={row.categoryName}>
                              <TableCell className="font-medium">{row.categoryName}</TableCell>
                              <TableCell>{row.analysisCount}</TableCell>
                              <TableCell>{row.averagePriceIncreasePercent.toFixed(1)}%</TableCell>
                              <TableCell><Badge variant={row.highestRiskLevel === 'High' ? 'default' : 'outline'}>{row.highestRiskLevel}</Badge></TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle>Supplier Spend Signals</CardTitle>
                  </CardHeader>
                  <CardContent>
                    {strategic.supplier.spendBySupplier.length === 0 ? (
                      <div className="py-6 text-sm text-muted-foreground">No supplier spend available for this period.</div>
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow><TableHead>Supplier</TableHead><TableHead>Spend</TableHead><TableHead>Share</TableHead><TableHead>Risk</TableHead></TableRow>
                        </TableHeader>
                        <TableBody>
                          {strategic.supplier.spendBySupplier.slice(0, 5).map((row) => (
                            <TableRow key={row.supplierId}>
                              <TableCell className="font-medium">{row.supplierName}</TableCell>
                              <TableCell>{formatCurrency(row.totalSpend)}</TableCell>
                              <TableCell>{row.percentageOfTotalSpend.toFixed(1)}%</TableCell>
                              <TableCell><Badge variant={row.riskLevel === 'High' || row.riskLevel === 'Critical' ? 'default' : 'outline'}>{row.riskLevel || 'Low'}</Badge></TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </CardContent>
                </Card>
              </div>
            </div>
          )}

          <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
            <Card>
              <CardHeader>
                <CardTitle>Department Planning Position</CardTitle>
                <CardDescription>Budget and item load by department.</CardDescription>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Department</TableHead>
                      <TableHead>Plans</TableHead>
                      <TableHead>Items</TableHead>
                      <TableHead>Estimated</TableHead>
                      <TableHead>Approved</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {dashboard.departmentSummaries.map((row) => (
                      <TableRow key={row.departmentId}>
                        <TableCell className="font-medium">{row.departmentName}</TableCell>
                        <TableCell>{row.planCount}</TableCell>
                        <TableCell>{row.itemCount}</TableCell>
                        <TableCell>{formatCurrency(row.estimatedBudget)}</TableCell>
                        <TableCell>{formatCurrency(row.approvedBudget)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Quarterly Delivery View</CardTitle>
                <CardDescription>Planned item value by quarter.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {dashboard.quarterSummaries.length === 0 ? (
                  <div className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Calendar className="h-4 w-4" />
                    No quarterly item schedule yet.
                  </div>
                ) : dashboard.quarterSummaries.map((row) => (
                  <div key={row.quarter} className="flex items-center justify-between border-b pb-2 last:border-b-0">
                    <div>
                      <div className="font-medium">{row.quarter}</div>
                      <div className="text-xs text-muted-foreground">{row.itemCount} items</div>
                    </div>
                    <div className="font-medium">{formatCurrency(row.estimatedCost)}</div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Top Consolidation Opportunities</CardTitle>
              <CardDescription>Grouped approved/active plan needs with possible savings from consolidated sourcing.</CardDescription>
            </CardHeader>
            <CardContent>
              {opportunities.length === 0 ? (
                <div className="flex items-center gap-2 py-8 text-muted-foreground">
                  <AlertCircle className="h-4 w-4" />
                  No consolidation opportunities found for the selected period.
                </div>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Need</TableHead>
                      <TableHead>Departments</TableHead>
                      <TableHead>Plans</TableHead>
                      <TableHead>Value</TableHead>
                      <TableHead>Savings</TableHead>
                      <TableHead>Level</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {opportunities.slice(0, 8).map((opportunity) => (
                      <TableRow key={opportunity.opportunityKey}>
                        <TableCell>
                          <div className="font-medium">{opportunity.itemDescription}</div>
                          <div className="text-xs text-muted-foreground">{opportunity.itemCategory}</div>
                        </TableCell>
                        <TableCell>{opportunity.departmentCount}</TableCell>
                        <TableCell>{opportunity.planCount}</TableCell>
                        <TableCell>{formatCurrency(opportunity.estimatedTotalCost, opportunity.currency)}</TableCell>
                        <TableCell>{formatCurrency(opportunity.potentialSavings, opportunity.currency)}</TableCell>
                        <TableCell>
                          <Badge variant={opportunity.opportunityLevel === 'High' ? 'default' : 'outline'}>
                            {opportunity.opportunityLevel}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </>
      ) : null}
    </div>
  );
}
