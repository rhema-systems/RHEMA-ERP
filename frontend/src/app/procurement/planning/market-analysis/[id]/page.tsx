'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Edit, Pencil, Plus, RefreshCw, Rocket, X } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { workflowApiService } from '@/services/workflow-api.service';
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
  const [editingQuoteId, setEditingQuoteId] = useState<string>();
  const [publishOpen, setPublishOpen] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [approvalRequired, setApprovalRequired] = useState<boolean | null>(null);
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
      const workflowSummaryPromise = workflowApiService
        .getWorkflowEntitySummary('MarketAnalysis', analysisId)
        .catch((error) => {
          toast.error('Unable to determine approval configuration', {
            description: error instanceof Error ? error.message : undefined,
          });
          return null;
        });
      const [analysisData, surveyData, trendData, workflowSummary] = await Promise.all([
        marketAnalysisService.getAnalysisById(analysisId),
        marketAnalysisService.getSurveySummary(analysisId),
        marketAnalysisService.getPriceTrend(analysisId),
        workflowSummaryPromise,
      ]);
      setAnalysis(analysisData);
      setSurvey(surveyData);
      setTrend(trendData);
      setApprovalRequired(workflowSummary?.approvalRequired ?? null);
      setQuote((current) => ({
        ...current,
        itemCategory: analysisData.itemCategory,
        itemDescription: analysisData.itemDescription,
        currency: analysisData.currency,
      }));
    } catch (error) {
      console.error('Error loading market analysis:', error);
      toast.error('Failed to load market analysis', {
        description: error instanceof Error ? error.message : undefined,
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    Promise.all([
      businessPartnerService.getActivePartners('Supplier'),
      businessPartnerService.getActivePartners('Both'),
    ])
      .then(([supplierPartners, bothPartners]) => {
        const uniquePartners = new Map(
          [...supplierPartners, ...bothPartners].map((partner) => [partner.id, partner])
        );
        setSuppliers(Array.from(uniquePartners.values()));
      })
      .catch((error) => {
        setSuppliers([]);
        toast.error('Unable to load approved suppliers', {
          description: error instanceof Error ? error.message : undefined,
        });
      });
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

  const resetQuote = (currency = analysis?.currency || '') => {
    setEditingQuoteId(undefined);
    setQuote({
      supplierId: undefined,
      supplierName: '',
      itemCategory: analysis?.itemCategory,
      itemDescription: analysis?.itemDescription,
      priceDate: today,
      unitPrice: 0,
      currency,
      unitOfMeasure: 'EA',
      priceSource: 'MarketSurvey',
      notes: '',
    });
  };

  const editQuote = (item: NonNullable<MarketSurveySummaryDto['quotes']>[number]) => {
    setEditingQuoteId(item.id);
    setQuote({
      marketAnalysisId: analysisId,
      itemCategory: analysis?.itemCategory,
      itemDescription: analysis?.itemDescription,
      supplierId: item.supplierId,
      supplierName: item.supplierName || '',
      priceDate: item.priceDate?.slice(0, 10) || today,
      unitPrice: Number(item.unitPrice || 0),
      currency: analysis?.currency || item.currency,
      unitOfMeasure: item.unitOfMeasure || 'EA',
      priceSource: 'MarketSurvey',
      notes: item.notes || '',
    });
  };

  const saveQuote = async () => {
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
      if (editingQuoteId) {
        await marketAnalysisService.updateSurveyQuote(analysisId, editingQuoteId, quote);
        toast.success('Survey quote updated');
      } else {
        await marketAnalysisService.addSurveyQuote(analysisId, quote);
        toast.success('Survey quote added');
      }
      resetQuote();
      await load();
    } catch (error) {
      console.error('Error saving quote:', error);
      toast.error(editingQuoteId ? 'Failed to update survey quote' : 'Failed to add survey quote', {
        description: error instanceof Error ? error.message : undefined,
      });
    } finally {
      setAddingQuote(false);
    }
  };

  const publish = async () => {
    try {
      setPublishing(true);
      await marketAnalysisService.publishAnalysis(analysisId);
      toast.success('Market analysis published');
      setPublishOpen(false);
      await load();
      return true;
    } catch (error) {
      toast.error('Failed to publish market analysis', {
        description: error instanceof Error ? error.message : undefined,
      });
      return false;
    } finally {
      setPublishing(false);
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
            <>
              <Button variant="outline" onClick={() => router.push(`/procurement/planning/market-analysis/${analysis.id}/edit`)}>
                <Edit className="mr-2 h-4 w-4" />
                Edit
              </Button>
              {approvalRequired === false && (
                <Button onClick={() => setPublishOpen(true)} disabled={publishing}>
                  <Rocket className="mr-2 h-4 w-4" />
                  {publishing ? 'Publishing...' : 'Publish'}
                </Button>
              )}
            </>
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
            <div><span className="text-muted-foreground">Inflation Impact</span><div className="font-medium">{Number(analysis.inflationImpactPercent || 0).toFixed(1)}%</div></div>
            <div><span className="text-muted-foreground">Budget Adjustment</span><div className="font-medium">{Number(analysis.recommendedBudgetAdjustmentPercent || 0).toFixed(1)}%</div></div>
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

            {analysis.status === 'Draft' && <div className="grid gap-3 md:grid-cols-5">
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
                <Input value={quote.supplierName || ''} readOnly={Boolean(quote.supplierId)} onChange={(event) => setQuote((current) => ({ ...current, supplierName: event.target.value }))} />
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
                <div className="flex w-full gap-2">
                  <Button onClick={saveQuote} disabled={addingQuote} className="flex-1">
                    {editingQuoteId ? <Pencil className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}
                    {addingQuote ? 'Saving...' : editingQuoteId ? 'Update' : 'Add'}
                  </Button>
                  {editingQuoteId && (
                    <Button variant="outline" size="icon" onClick={() => resetQuote()} disabled={addingQuote} title="Cancel edit">
                      <X className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </div>
            </div>}

            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow><TableHead>Supplier</TableHead><TableHead>Date</TableHead><TableHead>Price</TableHead><TableHead>Source</TableHead><TableHead>Notes</TableHead>{analysis.status === 'Draft' && <TableHead>Actions</TableHead>}</TableRow>
                </TableHeader>
                <TableBody>
                  {(survey?.quotes || []).map((item) => (
                    <TableRow key={item.id}>
                      <TableCell>{item.supplierName || '-'}</TableCell>
                      <TableCell>{item.priceDate?.slice(0, 10)}</TableCell>
                      <TableCell>{formatCurrency(item.unitPrice, item.currency)}</TableCell>
                      <TableCell>{item.priceSource}</TableCell>
                      <TableCell>{item.notes || '-'}</TableCell>
                      {analysis.status === 'Draft' && (
                        <TableCell>
                          <Button variant="ghost" size="sm" onClick={() => editQuote(item)} title="Edit quote">
                            <Pencil className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                  {(!survey || survey.quotes.length === 0) && (
                    <TableRow><TableCell colSpan={analysis.status === 'Draft' ? 6 : 5} className="py-6 text-center text-muted-foreground">No survey quotes captured.</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      </div>

      <ConfirmationDialog
        open={publishOpen}
        onOpenChange={setPublishOpen}
        title="Publish Market Analysis?"
        description="Publishing makes this market analysis available to procurement plans and locks its survey data from further editing."
        confirmText={publishing ? 'Publishing...' : 'Publish'}
        cancelText="Cancel"
        onConfirm={publish}
        isLoading={publishing}
      />
    </div>
  );
}
