'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { Calculator, Loader2, PlayCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { FixedAssetDepreciationReversalPanel } from '@/components/finance/FixedAssetDepreciationReversalPanel';
import type { FiscalPeriod } from '@/types/finance';
import type { AssetDepreciationSchedule, FixedAsset } from '@/types/fixed-assets';

export default function DepreciationPage() {
  const { toast } = useToast();
  const [periods, setPeriods] = useState<FiscalPeriod[]>([]);
  const [assets, setAssets] = useState<FixedAsset[]>([]);
  const [selectedPeriod, setSelectedPeriod] = useState<string>('');
  const [selectedAsset, setSelectedAsset] = useState<string>('all');
  const [postToGl, setPostToGl] = useState(true);
  const [isRunning, setIsRunning] = useState(false);
  const [results, setResults] = useState<AssetDepreciationSchedule[]>([]);

  useEffect(() => {
    const loadData = async () => {
      try {
        const [periodsData, assetsData] = await Promise.all([
          financeDataService.getFiscalPeriods(),
          fixedAssetsDataService.getAssets(),
        ]);
        setPeriods(periodsData);
        setAssets(assetsData);
      } catch (error) {
        console.error('Failed to load depreciation inputs:', error);
      }
    };

    loadData();
  }, []);

  const loadPeriodResults = useCallback(async () => {
    if (!selectedPeriod) {
      setResults([]);
      return;
    }

    try {
      setResults(await fixedAssetsDataService.getPeriodSchedule(selectedPeriod));
    } catch (error) {
      console.error('Failed to load period depreciation schedules:', error);
      toast({
        title: 'Could not load depreciation history',
        description: error instanceof Error ? error.message : 'The selected period schedule could not be loaded.',
        variant: 'destructive',
      });
    }
  }, [selectedPeriod, toast]);

  useEffect(() => { void loadPeriodResults(); }, [loadPeriodResults]);

  const reversalRuns = useMemo(() => {
    const groups = new Map<string, AssetDepreciationSchedule[]>();
    for (const schedule of results) {
      if (!schedule.fixedAssetDepreciationRunId || !schedule.isPosted) continue;
      const group = groups.get(schedule.fixedAssetDepreciationRunId) ?? [];
      group.push(schedule);
      groups.set(schedule.fixedAssetDepreciationRunId, group);
    }

    return Array.from(groups.entries()).map(([id, schedules]) => {
      const amount = schedules.reduce((sum, schedule) => sum + schedule.depreciationAmount, 0);
      const first = schedules[0];
      return {
        id,
        label: `${first.bookClassification} · revision ${first.correctionSequence} · ${amount.toFixed(2)} · ${schedules.length} line${schedules.length === 1 ? '' : 's'}`,
        reversed: schedules.every(schedule => schedule.isReversed),
      };
    });
  }, [results]);

  const handleRun = async () => {
    if (!selectedPeriod) {
      toast({
        title: 'Select a period',
        description: 'Please choose a fiscal period before running depreciation.',
        variant: 'destructive',
      });
      return;
    }

    try {
      setIsRunning(true);
      const data = await fixedAssetsDataService.runDepreciation({
        fiscalPeriodId: selectedPeriod,
        fixedAssetId: selectedAsset === 'all' ? undefined : selectedAsset,
        postToGl,
      });
      setResults(data);
      toast({
        title: 'Depreciation complete',
        description: `Generated ${data.length} schedule entries.`,
      });
    } catch (error) {
      console.error('Failed to run depreciation:', error);
      toast({
        title: 'Error',
        description: 'Depreciation run failed.',
        variant: 'destructive',
      });
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight flex items-center gap-2">
            <Calculator className="h-8 w-8" />
            Run Depreciation
          </h1>
          <p className="text-muted-foreground">Generate depreciation schedules and post to GL.</p>
        </div>
        <Link href="/finance/fixed-assets/register">
          <Button variant="outline">Back to Register</Button>
        </Link>
      </div>

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/finance">Finance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Depreciation</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Run Settings</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">Fiscal Period</label>
            <Select value={selectedPeriod} onValueChange={setSelectedPeriod}>
              <SelectTrigger>
                <SelectValue placeholder="Select period" />
              </SelectTrigger>
              <SelectContent>
                {periods.map((period) => (
                  <SelectItem key={period.id} value={period.id}>
                    {period.periodName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">Asset (optional)</label>
            <Select value={selectedAsset} onValueChange={setSelectedAsset}>
              <SelectTrigger>
                <SelectValue placeholder="All assets" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All assets</SelectItem>
                {assets.map((asset) => (
                  <SelectItem key={asset.id} value={asset.id}>
                    {asset.assetCode} - {asset.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="flex items-center gap-2 pt-6">
            <Checkbox checked={postToGl} onCheckedChange={(value) => setPostToGl(Boolean(value))} />
            <span className="text-sm">Post to GL</span>
          </div>
        </CardContent>
        <CardContent>
          <Button onClick={handleRun} disabled={isRunning}>
            {isRunning ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <PlayCircle className="mr-2 h-4 w-4" />}
            Run Depreciation
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Results</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Asset</TableHead>
                <TableHead className="text-right">Depreciation</TableHead>
                <TableHead className="text-right">Accumulated</TableHead>
                <TableHead className="text-right">Net Book</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {results.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center py-8 text-muted-foreground">
                    No depreciation results yet.
                  </TableCell>
                </TableRow>
              ) : (
                results.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell className="font-mono">{row.fixedAssetId}</TableCell>
                    <TableCell className="text-right">{row.depreciationAmount.toFixed(2)}</TableCell>
                    <TableCell className="text-right">{row.accumulatedDepreciation.toFixed(2)}</TableCell>
                    <TableCell className="text-right">{row.netBookValue.toFixed(2)}</TableCell>
                    <TableCell>{row.isReversed ? 'Reversed' : row.isPosted ? 'Posted' : row.isProjected ? 'Projected' : 'Pending'}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <FixedAssetDepreciationReversalPanel runs={reversalRuns} onChanged={loadPeriodResults} />
    </div>
  );
}
