'use client';

import React from 'react';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import type { BudgetDimensionCellPosition } from '@/types/budget';

interface BudgetDimensionCellDrilldownProps {
    cells: Array<BudgetDimensionCellPosition & { periodCode: string }>;
    currencyCode: string;
}

export function BudgetDimensionCellDrilldown({
    cells,
    currencyCode,
}: BudgetDimensionCellDrilldownProps) {
    const money = (value: number) => new Intl.NumberFormat('en-GH', {
        style: 'currency',
        currency: currencyCode || 'GHS',
        maximumFractionDigits: 2,
    }).format(value);

    return (
        <div className="rounded-md border bg-muted/20 p-3">
            <p className="mb-3 text-sm font-medium">Dimension-cell budget position</p>
            <div className="overflow-x-auto">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>Period</TableHead>
                            <TableHead className="min-w-72">Dimension combination</TableHead>
                            <TableHead className="text-right">Budget</TableHead>
                            <TableHead className="text-right">Actual</TableHead>
                            <TableHead className="text-right">Reserved</TableHead>
                            <TableHead className="text-right">Available</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {cells.map((cell, index) => (
                            <TableRow key={`${cell.periodCode}-${cell.financeDimensionSetId ?? 'legacy'}-${index}`}>
                                <TableCell>{cell.periodCode}</TableCell>
                                <TableCell>
                                    {cell.dimensionAssignments.length > 0 ? (
                                        <div className="flex flex-wrap gap-1.5">
                                            {cell.dimensionAssignments.map(assignment => (
                                                <Badge
                                                    key={`${assignment.financeDimensionDefinitionId}-${assignment.financeDimensionValueId}`}
                                                    variant="outline"
                                                    title={`${assignment.dimensionName}: ${assignment.valueName}`}
                                                >
                                                    {assignment.dimensionCode}: {assignment.valueCode} — {assignment.valueName}
                                                </Badge>
                                            ))}
                                        </div>
                                    ) : (
                                        <span>{cell.dimensionDisplayValue || 'Legacy / no dimensions'}</span>
                                    )}
                                </TableCell>
                                <TableCell className="text-right">{money(cell.budgetAmount)}</TableCell>
                                <TableCell className="text-right">{money(cell.actualAmount)}</TableCell>
                                <TableCell className="text-right">{money(cell.reservedAmount)}</TableCell>
                                <TableCell className={`text-right font-medium ${
                                    cell.availableAmount < 0 ? 'text-red-600' : 'text-green-700'
                                }`}>
                                    {money(cell.availableAmount)}
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </div>
        </div>
    );
}
