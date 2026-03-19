"use client";

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { Loader2, ArrowLeft, RefreshCw, Save, CheckCircle2 } from 'lucide-react';
import { SegmentValueSelector } from '@/components/finance/generator/SegmentValueSelector';
import { CombinationPreviewGrid } from '@/components/finance/generator/CombinationPreviewGrid';
import { accountCombinationService } from '@/services/account-combination-service';
import { SegmentSelection, AccountCombinationPreview, CombinationRequest, BulkCreateAccountsRequest } from '@/types/finance';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';

export default function AccountGeneratorPage() {
    const router = useRouter();
    const { toast } = useToast();
    const [step, setStep] = useState<'select' | 'preview'>('select');
    const [generating, setGenerating] = useState(false);
    const [saving, setSaving] = useState(false);

    // State
    const [selections, setSelections] = useState<SegmentSelection[]>([]);
    const [previewResults, setPreviewResults] = useState<AccountCombinationPreview[]>([]);

    // Options
    const [includeExisting, setIncludeExisting] = useState(false);
    const [skipDuplicates, setSkipDuplicates] = useState(true);

    const handleSelectionChange = (newSelections: SegmentSelection[]) => {
        setSelections(newSelections);
    };

    const handleGenerate = async () => {
        if (selections.length === 0) {
            toast({
                title: "No segments selected",
                description: "Please select at least one value for each segment.",
                variant: "destructive"
            });
            return;
        }

        setGenerating(true);
        try {
            const request: CombinationRequest = {
                segmentSelections: selections,
                accountType: 'Expense', // Default
                currencyCode: 'GHS',    // Default base currency
                includeExistingInPreview: includeExisting,
                isMultiCurrency: false,
                allowDirectPosting: true,
                budgetTrackingEnabled: false
            };

            const results = await accountCombinationService.generateCombinations(request);

            // Mark valid non-duplicates as selected by default
            // tempId is not set by the backend, so use accountNumber as the unique identifier
            const resultsWithSelection = results.map(r => ({
                ...r,
                tempId: r.tempId || r.accountNumber,
                isSelected: r.status === 'Valid'
            }));

            setPreviewResults(resultsWithSelection);
            setStep('preview');
        } catch (error) {
            console.error(error);
            toast({
                title: "Generation failed",
                description: "Failed to generate combinations. Please try again.",
                variant: "destructive"
            });
        } finally {
            setGenerating(false);
        }
    };

    const handleToggleSelection = (tempId: string) => {
        setPreviewResults(prev => prev.map(c =>
            c.tempId === tempId ? { ...c, isSelected: !c.isSelected } : c
        ));
    };

    const handleToggleAll = (checked: boolean) => {
        setPreviewResults(prev => prev.map(c =>
            (c.status === 'Valid') ? { ...c, isSelected: checked } : c
        ));
    };

    const handleBulkCreate = async () => {
        const selected = previewResults.filter(c => c.isSelected);

        if (selected.length === 0) {
            toast({
                title: "No accounts selected",
                description: "Please select at least one account to create.",
                variant: "destructive"
            });
            return;
        }

        setSaving(true);
        try {
            const request: BulkCreateAccountsRequest = {
                combinations: selected,
                skipDuplicates: skipDuplicates
            };

            const result = await accountCombinationService.bulkCreateAccounts(request);

            toast({
                title: "Bulk Creation Complete",
                description: `Successfully created ${result.successCount} accounts. ${result.errorCount} failed, ${result.skipCount} skipped.`,
                variant: result.errorCount > 0 ? "default" : "default", // Using default for success-like 
                className: "bg-green-50 border-green-200"
            });

            if (result.successCount > 0) {
                // Return to list or reset?
                // For now, allow staying to see results or generate more
                // Maybe update list to show created ones as duplicates now?
                // Refresh preview?
            }

        } catch (error) {
            console.error(error);
            toast({
                title: "Creation failed",
                description: "An error occurred during bulk creation.",
                variant: "destructive"
            });
        } finally {
            setSaving(false);
        }
    };

    const validCount = previewResults.filter(r => r.status === 'Valid').length;
    const selectedCount = previewResults.filter(r => r.isSelected).length;

    return (
        <div className="container mx-auto py-6 space-y-6">
            <div className="flex flex-col gap-2">
                <Breadcrumb>
                    <BreadcrumbList>
                        <BreadcrumbItem>
                            <BreadcrumbLink href="/finance/dashboard">Finance</BreadcrumbLink>
                        </BreadcrumbItem>
                        <BreadcrumbSeparator />
                        <BreadcrumbItem>
                            <BreadcrumbLink href="/finance/accounts">Accounts</BreadcrumbLink>
                        </BreadcrumbItem>
                        <BreadcrumbSeparator />
                        <BreadcrumbItem>
                            <BreadcrumbPage>Account Generator</BreadcrumbPage>
                        </BreadcrumbItem>
                    </BreadcrumbList>
                </Breadcrumb>
                <div className="flex justify-between items-center">
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">Account Generator</h1>
                        <p className="text-muted-foreground">
                            Generate and bulk create GL account combinations from segment values.
                        </p>
                    </div>
                </div>
            </div>

            <Separator />

            <Tabs value={step} onValueChange={(v) => setStep(v as 'select' | 'preview')} className="w-full">
                <TabsList className="grid w-full max-w-[400px] grid-cols-2">
                    <TabsTrigger value="select" disabled={step === 'preview' && generating}>1. Select Segments</TabsTrigger>
                    <TabsTrigger value="preview" disabled={previewResults.length === 0}>2. Preview & Create</TabsTrigger>
                </TabsList>

                <TabsContent value="select" className="space-y-4 py-4">
                    <Card>
                        <CardHeader>
                            <CardTitle>Segment Selection</CardTitle>
                            <CardDescription>
                                Select the values for each segment to generate combinations.
                                The generator will create all possible combinations (Cartesian product) of the selected values.
                            </CardDescription>
                        </CardHeader>
                        <CardContent>
                            <SegmentValueSelector onSelectionChange={handleSelectionChange} />

                            <div className="mt-6 flex items-center space-x-2">
                                <Switch
                                    id="include-existing"
                                    checked={includeExisting}
                                    onCheckedChange={setIncludeExisting}
                                />
                                <Label htmlFor="include-existing">Include existing accounts in preview (marked as Duplicate)</Label>
                            </div>
                        </CardContent>
                        <CardFooter className="flex justify-end space-x-2 border-t pt-4">
                            <Button
                                onClick={handleGenerate}
                                disabled={generating || selections.length === 0}
                                className="min-w-[150px]"
                            >
                                {generating ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Generating...</> : <><RefreshCw className="mr-2 h-4 w-4" /> Generate Preview</>}
                            </Button>
                        </CardFooter>
                    </Card>
                </TabsContent>

                <TabsContent value="preview" className="space-y-4 py-4">
                    <div className="flex items-center justify-between">
                        <Button variant="ghost" onClick={() => setStep('select')} size="sm">
                            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Selection
                        </Button>
                        <div className="flex items-center space-x-4">
                            <div className="flex items-center space-x-2">
                                <Switch
                                    id="skip-duplicates"
                                    checked={skipDuplicates}
                                    onCheckedChange={setSkipDuplicates}
                                />
                                <Label htmlFor="skip-duplicates">Skip Duplicates</Label>
                            </div>
                            <Button
                                onClick={handleBulkCreate}
                                disabled={saving || selectedCount === 0}
                                className="bg-green-600 hover:bg-green-700 text-white"
                            >
                                {saving ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Creating...</> : <><Save className="mr-2 h-4 w-4" /> Create {selectedCount} Accounts</>}
                            </Button>
                        </div>
                    </div>

                    <CombinationPreviewGrid
                        combinations={previewResults}
                        onToggleSelection={handleToggleSelection}
                        onToggleAll={handleToggleAll}
                    />
                </TabsContent>
            </Tabs>
        </div>
    );
}
