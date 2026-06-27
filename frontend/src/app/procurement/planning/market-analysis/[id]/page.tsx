'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Edit, Plus, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import {
  marketAnalysisService,
  type CreatePriceHistoryDto,
  type MarketAnalysisDetailDto,
  type MarketSurveySummaryDto,
  type PriceTrendDto,
} from '@/services/procurementPlanningService';

const today = new Date().toISOString().slice(0, 10);

export default function MarketAnalysisDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const analysisId = params.id;
  const [analysis, setAnalysis] = useState<MarketAnalysisDetailDto | null>(null);
  const [survey, setSurvey] = useState<MarketSurveySummaryDto | null>(null);
  const [trend, setTrend] = useState<PriceTrendDto | null>(null);
  const [suppliers, setSuppliers] = useState<BusinessPartnerDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [addingQuote, setAddingQuote] = useState(false);
  const [quote, setQuote] = useState<CreatePriceHistoryDto>({
    supplierName: '',
    priceDate: today,
    unitPrice: 0,
    currency: 'USD',
    unitOfMeasure: 'EA',
    priceSource: 'MarketSurvey',
    notes: '',
  });

  const load = async () => {
    try {
      setLoading(true);
      const [analysisData, surveyData, trendData] = await Promise.all([
        marketAnalysisService.getAnalysisById(analysisId),
        marketAnalysisService.getSurveySummary(analysisId),
        marketAnalysisService.getPriceTrend(analysisId),
      ]);
      setAnalysis(analysisData);
      setSurvey(surveyData);
      setTrend(trendData);
      setQuote((current) => ({
        ...current,
        itemCategory: analysisData.itemCategory,
        itemDescription: analysisData.itemDescription,
        currency: analysisData.currency,
      }));
    } catch (error) {
      console.error('Error loading market analysis:', error);
      toast.error('Failed to load market analysis');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    businessPartnerService.getActivePartners('Supplier')
      .then(setSuppliers)
      .catch(() => setSuppliers([]));
  }, [analysisId]);

  const formatCurrency = (amount: number, currency = analysis?.currency || 'USD') =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 2 }).format(amount || 0);

  const setSupplier = (value: string) => {
    const supplier = suppliers.find((item) => item.id === value);
    setQuote((current) => ({
      ...current,
      supplierId: value === 'manual' ? undefined : value,
      supplierName: value === 'manual' ? '' : supplier?.partnerName || '',
    }));
  };

  const addQuote = async () => {
    if (!quote.supplierName?.trim()) {
      toast.error('Supplier name is required');
      return;
    }
    if (!quote.unitPrice || quote.unitPrice <= 0) {
      toast.error('Quote price must be greater than zero');
      return;
    }

    try {
      setAddingQuote(true);
      await marketAnalysisService.addSurveyQuote(analysisId, quote);
      toast.success('Survey quote added');
      setQuote((current) => ({ ...current, supplierId: undefined, supplierName: '', unitPrice: 0, notes: '' }));
      await load();
    } catch (error) {
      console.error('Error adding quote:', error);
      toast.error('Failed to add survey quote');
    } finally {
      setAddingQuote(false);
    }
  };

  if (loading && !analysis) {
    return <div className="py-12 text-center text-muted-foreground">Loading market analysis...</div>;
  }

  if (!analysis) {
    return <div className="py-12 text-center text-muted-foreground">Market analysis not found.</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{analysis.title}</h1>
            <Badge variant={analysis.status === 'Published' ? 'default' : 'outline'}>{analysis.status}</Badge>
          </div>
          <p className="text-sm text-muted-foreground">{analysis.analysisCode}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => router.push('/procurement/planning/market-analysis')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <Button variant="outline" onClick={load}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          {analysis.status === 'Draft' && (
            <Button onClick={() => router.push(`/procurement/planning/market-analysis/${analysis.id}/edit`)}>
              <Edit className="mr-2 h-4 w-4" />
              Edit
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-4 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Current Price</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{formatCurrency(analysis.currentMarketPrice)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Variance</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{((analysis.priceVariancePercent ?? analysis.priceChangePercent) || 0).toFixed(1)}%</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Survey Average</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{formatCurrency(survey?.averageMarketPrice || analysis.currentMarketPrice, survey?.currency)}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Market Risk</CardTitle></CardHeader>
          <CardContent><div className="text-2xl font-semibold">{analysis.marketRiskLevel}</div></CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader><CardTitle>Market Intelligence</CardTitle></CardHeader>
          <CardContent className="grid gap-3 text-sm sm:grid-cols-2">
            <div><span className="text-muted-foreground">Category</span><div className="font-medium">{analysis.itemCategory || '-'}</div></div>
            <div><span className="text-muted-foreground">Item</span><div className="font-medium">{analysis.itemDescription || '-'}</div></div>
            <div><span className="text-muted-foreground">Previous Price</span><div className="font-medium">{formatCurrency(analysis.previousPrice || analysis.historicalAveragePrice)}</div></div>
            <div><span className="text-muted-foreground">Forecasted Price</span><div className="font-medium">{formatCurrency(analysis.forecastedPrice)}</div></div>
            <div><span className="text-muted-foreground">Lead Time</span><div className="font-medium">{analysis.leadTimeDays ? `${analysis.leadTimeDays} days` : '-'}</div></div>
            <div><span className="text-muted-foreground">Availability</span><div className="font-medium">{analysis.marketAvailability}</div></div>
            <div><span className="text-muted-foreground">Supply Risk</span><div className="font-medium">{analysis.supplyRiskLevel}</div></div>
            <div><span className="text-muted-foreground">Trend</span><div className="font-medium">{trend?.trend || analysis.priceTrend}</div></div>
            <div><span className="text-muted-foreground">Inflation Impact</span><div className="font-medium">{analysis.inflationImpactPercent.toFixed(1)}%</div></div>
            <div><span className="text-muted-foreground">Budget Adjustment</span><div className="font-medium">{analysis.recommendedBudgetAdjustmentPercent.toFixed(1)}%</div></div>
            <div className="sm:col-span-2"><span className="text-muted-foreground">Risk Factors</span><div className="font-medium whitespace-pre-wrap">{analysis.riskFactors || '-'}</div></div>
            <div className="sm:col-span-2"><span className="text-muted-foreground">Notes</span><div className="font-medium whitespace-pre-wrap">{analysis.notes || '-'}</div></div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Market Survey</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-4">
              <div><span className="text-xs text-muted-foreground">Quotes</span><div className="font-semibold">{survey?.quoteCount || 0}</div></div>
              <div><span className="text-xs text-muted-foreground">Lowest</span><div className="font-semibold">{formatCurrency(survey?.lowestPrice || 0, survey?.currency)}</div></div>
              <div><span className="text-xs text-muted-foreground">Highest</span><div className="font-semibold">{formatCurrency(survey?.highestPrice || 0, survey?.currency)}</div></div>
              <div><span className="text-xs text-muted-foreground">Estimate</span><div className="font-semibold">{formatCurrency(survey?.recommendedPlanningEstimate || 0, survey?.currency)}</div></div>
            </div>

            <div className="grid gap-3 md:grid-cols-5">
              <div className="space-y-2 md:col-span-2">
                <Label>Supplier</Label>
                <Select value={quote.supplierId || 'manual'} onValueChange={setSupplier}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="manual">Manual Entry</SelectItem>
                    {suppliers.map((supplier) => (
                      <SelectItem key={supplier.id} value={supplier.id}>{supplier.partnerName}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2 md:col-span-2">
                <Label>Supplier Name</Label>
                <Input value={quote.supplierName || ''} onChange={(event) => setQuote((current) => ({ ...current, supplierName: event.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Price</Label>
                <Input type="number" min={0} step="0.01" value={quote.unitPrice || ''} onChange={(event) => setQuote((current) => ({ ...current, unitPrice: Number(event.target.value) }))} />
              </div>
              <div className="space-y-2">
                <Label>Date</Label>
                <Input type="date" value={quote.priceDate} onChange={(event) => setQuote((current) => ({ ...current, priceDate: event.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>UOM</Label>
                <Input value={quote.unitOfMeasure || 'EA'} onChange={(event) => setQuote((current) => ({ ...current, unitOfMeasure: event.target.value }))} />
              </div>
              <div className="space-y-2 md:col-span-3">
                <Label>Notes</Label>
                <Textarea rows={1} value={quote.notes || ''} onChange={(event) => setQuote((current) => ({ ...current, notes: event.target.value }))} />
              </div>
              <div className="flex items-end">
                <Button onClick={addQuote} disabled={addingQuote} className="w-full">
                  <Plus className="mr-2 h-4 w-4" />
                  {addingQuote ? 'Adding...' : 'Add'}
                </Button>
              </div>
            </div>

            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow><TableHead>Supplier</TableHead><TableHead>Date</TableHead><TableHead>Price</TableHead><TableHead>Source</TableHead><TableHead>Notes</TableHead></TableRow>
                </TableHeader>
                <TableBody>
                  {(survey?.quotes || []).map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>{item.supplierName || '-'}</TableCell>
                      <TableCell>{item.priceDate?.slice(0, 10)}</TableCell>
                      <TableCell>{formatCurrency(item.unitPrice, item.currency)}</TableCell>
                      <TableCell>{item.priceSource}</TableCell>
                      <TableCell>{item.notes || '-'}</TableCell>
                    </TableRow>
                  ))}
                  {(!survey || survey.quotes.length === 0) && (
                    <TableRow><TableCell colSpan={5} className="py-6 text-center text-muted-foreground">No survey quotes captured.</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
