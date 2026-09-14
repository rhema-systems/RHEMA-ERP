'use client';

import React, { useState } from 'react';
import { Upload, Download, FileSpreadsheet, CheckCircle2, XCircle, Loader2, AlertCircle, ArrowLeft } from 'lucide-react';
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { useToast } from "@/components/ui/use-toast";
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import { BulkImportResult } from '@/types/fixed-assets';
import Link from 'next/link';

export default function AssetImportPage() {
    const [file, setFile] = useState<File | null>(null);
    const [validating, setValidating] = useState(false);
    const [importing, setImporting] = useState(false);
    const [result, setResult] = useState<BulkImportResult | null>(null);
    const [isValidationResult, setIsValidationResult] = useState(false);
    const [downloading, setDownloading] = useState(false);
    const { toast } = useToast();

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files && e.target.files[0]) {
            setFile(e.target.files[0]);
            setResult(null);
            setIsValidationResult(false);
        }
    };

    const handleDownloadTemplate = async () => {
        try {
            setDownloading(true);
            await fixedAssetsDataService.downloadImportTemplate();
            toast({
                title: "Success",
                description: "Import template downloaded successfully.",
            });
        } catch (error) {
            console.error('Failed to download template:', error);
            toast({
                title: "Error",
                description: "Failed to download import template.",
                variant: "destructive",
            });
        } finally {
            setDownloading(false);
        }
    };

    const handleValidate = async () => {
        if (!file) {
            toast({
                title: "Validation Error",
                description: "Please select a file to validate.",
                variant: "destructive",
            });
            return;
        }

        try {
            setValidating(true);
            const validationResult = await fixedAssetsDataService.bulkImportAssets(file, true);
            setResult(validationResult);
            setIsValidationResult(true);

            if (validationResult.errorCount === 0) {
                toast({
                    title: "Validation Passed",
                    description: `${validationResult.successCount} assets are ready to import.`,
                });
            } else if (validationResult.successCount > 0) {
                toast({
                    title: "Validation Found Issues",
                    description: `${validationResult.successCount} rows are valid, but ${validationResult.errorCount} rows need correction before import.`,
                    variant: "destructive",
                });
            } else {
                toast({
                    title: "Validation Failed",
                    description: `${validationResult.errorCount} rows need correction before import.`,
                    variant: "destructive",
                });
            }
        } catch (error) {
            console.error('Failed to validate assets:', error);
            toast({
                title: "Error",
                description: "Failed to validate assets. Please check the file format.",
                variant: "destructive",
            });
        } finally {
            setValidating(false);
        }
    };

    const handleImport = async () => {
        if (!file || !result || !isValidationResult || result.errorCount > 0) {
            toast({
                title: "Validation Required",
                description: "Validate a file with no errors before importing.",
                variant: "destructive",
            });
            return;
        }

        try {
            setImporting(true);
            const importResult = await fixedAssetsDataService.bulkImportAssets(file, false);
            setResult(importResult);
            setIsValidationResult(false);

            if (importResult.errorCount === 0) {
                toast({
                    title: "Import Successful",
                    description: `Successfully imported ${importResult.successCount} assets.`,
                });
            } else {
                toast({
                    title: "Import Failed",
                    description: `${importResult.errorCount} rows need correction. No assets were imported.`,
                    variant: "destructive",
                });
            }
        } catch (error) {
            console.error('Failed to import assets:', error);
            toast({
                title: "Error",
                description: "Failed to import assets. Please check the file format.",
                variant: "destructive",
            });
        } finally {
            setImporting(false);
        }
    };

    return (
        <div className="p-6 space-y-6">
            <div className="flex items-center gap-4">
                <Link href="/finance/fixed-assets">
                    <Button variant="ghost" size="icon">
                        <ArrowLeft className="h-5 w-5" />
                    </Button>
                </Link>
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Bulk Asset Import</h1>
                    <p className="text-slate-500 dark:text-slate-400 text-sm">Validate and create controlled draft/opening records. Import never posts or activates assets.</p>
                </div>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Upload Section */}
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="bg-slate-50 border-b">
                        <CardTitle className="text-lg font-semibold flex items-center gap-2">
                            <Upload className="h-5 w-5 text-indigo-600" />
                            Upload File
                        </CardTitle>
                        <CardDescription>Select an Excel file (.xlsx) containing asset data</CardDescription>
                    </CardHeader>
                    <CardContent className="p-6 space-y-4">
                        <div className="border-2 border-dashed border-slate-300 rounded-lg p-8 text-center hover:border-indigo-400 transition-colors">
                            <FileSpreadsheet className="h-12 w-12 mx-auto mb-4 text-slate-400" />
                            <input
                                type="file"
                                accept=".xlsx"
                                onChange={handleFileChange}
                                className="hidden"
                                id="file-upload"
                            />
                            <label htmlFor="file-upload" className="cursor-pointer">
                                <Button variant="outline" className="mb-2" asChild>
                                    <span>Choose File</span>
                                </Button>
                            </label>
                            {file && (
                                <p className="text-sm text-slate-600 mt-2">
                                    Selected: <span className="font-semibold">{file.name}</span>
                                </p>
                            )}
                        </div>

                        <Button
                            onClick={handleValidate}
                            disabled={!file || validating || importing}
                            className="w-full bg-indigo-600 hover:bg-indigo-700"
                        >
                            {validating ? (
                                <>
                                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                                    Validating...
                                </>
                            ) : (
                                <>
                                    <Upload className="h-4 w-4 mr-2" />
                                    Validate File
                                </>
                            )}
                        </Button>
                        <Button
                            onClick={handleImport}
                            disabled={!file || !result || !isValidationResult || result.errorCount > 0 || importing || validating}
                            className="w-full"
                            variant={result && isValidationResult && result.errorCount === 0 ? "default" : "outline"}
                        >
                            {importing ? (
                                <>
                                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                                    Importing...
                                </>
                            ) : (
                                <>
                                    <Upload className="h-4 w-4 mr-2" />
                                    Import Assets
                                </>
                            )}
                        </Button>
                    </CardContent>
                </Card>

                {/* Template Download Section */}
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="bg-slate-50 border-b">
                        <CardTitle className="text-lg font-semibold flex items-center gap-2">
                            <Download className="h-5 w-5 text-emerald-600" />
                            Download Template
                        </CardTitle>
                        <CardDescription>Get the Excel template with sample data and instructions</CardDescription>
                    </CardHeader>
                    <CardContent className="p-6 space-y-4">
                        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 text-sm text-blue-800">
                            <AlertCircle className="h-4 w-4 inline mr-2" />
                            <strong>Required Fields:</strong> Asset Code, Name, Category Code, Purchase Date, Purchase Price, Useful Life
                        </div>

                        <ul className="text-sm text-slate-600 space-y-2">
                            <li className="flex items-start gap-2">
                                <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5 flex-shrink-0" />
                                <span>Category Code must match an existing category</span>
                            </li>
                            <li className="flex items-start gap-2">
                                <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5 flex-shrink-0" />
                                <span>Asset Codes must be unique</span>
                            </li>
                            <li className="flex items-start gap-2">
                                <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5 flex-shrink-0" />
                                <span>Purchase Date cannot be in the future</span>
                            </li>
                            <li className="flex items-start gap-2">
                                <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5 flex-shrink-0" />
                                <span>Location is optional and stored directly on the finance asset register</span>
                            </li>
                            <li className="flex items-start gap-2">
                                <CheckCircle2 className="h-4 w-4 text-emerald-600 mt-0.5 flex-shrink-0" />
                                <span>Historical opening values remain blocked from depreciation until their governed opening batch is posted</span>
                            </li>
                        </ul>

                        <Button
                            onClick={handleDownloadTemplate}
                            disabled={downloading}
                            variant="outline"
                            className="w-full border-emerald-200 text-emerald-700 hover:bg-emerald-50"
                        >
                            {downloading ? (
                                <>
                                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                                    Downloading...
                                </>
                            ) : (
                                <>
                                    <Download className="h-4 w-4 mr-2" />
                                    Download Template
                                </>
                            )}
                        </Button>
                    </CardContent>
                </Card>
            </div>

            {/* Results Section */}
            {result && (
                <Card className="border-slate-200 shadow-sm">
                    <CardHeader className="bg-slate-50 border-b">
                        <CardTitle className="text-lg font-semibold">{isValidationResult ? "Validation Results" : "Import Results"}</CardTitle>
                        <div className="flex gap-4 mt-2">
                            <div className="flex items-center gap-2">
                                <Badge variant="outline" className="bg-slate-100">
                                    Total: {result.totalRows}
                                </Badge>
                            </div>
                            <div className="flex items-center gap-2">
                                <Badge variant="outline" className="bg-emerald-50 text-emerald-700 border-emerald-200">
                                    <CheckCircle2 className="h-3 w-3 mr-1" />
                                    {isValidationResult ? "Ready" : "Success"}: {result.successCount}
                                </Badge>
                            </div>
                            <div className="flex items-center gap-2">
                                <Badge variant="outline" className="bg-red-50 text-red-700 border-red-200">
                                    <XCircle className="h-3 w-3 mr-1" />
                                    Errors: {result.errorCount}
                                </Badge>
                            </div>
                        </div>
                    </CardHeader>
                    <CardContent className="p-0">
                        {result.errors && result.errors.length > 0 && (
                            <div className="overflow-auto max-h-96">
                                <Table>
                                    <TableHeader>
                                        <TableRow className="bg-slate-50/50">
                                            <TableHead className="w-[100px] font-semibold">Row</TableHead>
                                            <TableHead className="font-semibold">Asset Code</TableHead>
                                            <TableHead className="font-semibold">Field</TableHead>
                                            <TableHead className="font-semibold">Error</TableHead>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {result.errors.map((error, idx) => (
                                            <TableRow key={idx} className="hover:bg-slate-50">
                                                <TableCell className="font-mono text-xs">{error.rowNumber}</TableCell>
                                                <TableCell className="font-mono text-xs">{error.assetCode || '-'}</TableCell>
                                                <TableCell className="text-sm">{error.field}</TableCell>
                                                <TableCell className="text-sm text-red-600">{error.error}</TableCell>
                                            </TableRow>
                                        ))}
                                    </TableBody>
                                </Table>
                            </div>
                        )}

                        {result.successfulAssetCodes && result.successfulAssetCodes.length > 0 && (
                            <div className="p-4 bg-emerald-50 border-t">
                                <p className="text-sm font-semibold text-emerald-800 mb-2">
                                    {isValidationResult ? "Assets Ready For Import:" : "Successfully Imported Assets:"}
                                </p>
                                <div className="flex flex-wrap gap-2">
                                    {result.successfulAssetCodes.map((code, idx) => (
                                        <Badge key={idx} variant="outline" className="bg-white text-emerald-700 border-emerald-200 font-mono text-xs">
                                            {code}
                                        </Badge>
                                    ))}
                                </div>
                            </div>
                        )}
                    </CardContent>
                </Card>
            )}
        </div>
    );
}
