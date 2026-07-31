'use client';

import React from 'react';
import { TableCell, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';
import type { FinancialStatementLayoutExecutionRowDto } from '@/types/finance';

interface FinancialStatementLayoutRowsProps {
    rows: FinancialStatementLayoutExecutionRowDto[];
    formatMoney: (amount: number) => string;
}

export function FinancialStatementLayoutRows({
    rows,
    formatMoney,
}: FinancialStatementLayoutRowsProps) {
    return (
        <>
            {rows
                .filter((row) => row.isDisplayed)
                .sort((left, right) => left.sequence - right.sequence)
                .map((row) => {
                    if (row.rowType === 'Spacer') {
                        return (
                            <TableRow key={row.rowId} className="h-5 border-0">
                                <TableCell colSpan={3} />
                            </TableRow>
                        );
                    }

                    const isHeading = row.rowType === 'Header';
                    const isTotal = row.rowType === 'Total' || row.rowType === 'Formula';
                    const accountNumbers = row.accounts
                        .map((account) => account.accountNumber)
                        .join(', ');

                    return (
                        <TableRow
                            key={row.rowId}
                            className={cn(
                                isHeading && 'bg-slate-100 hover:bg-slate-100',
                                isTotal && 'border-t bg-muted/40',
                                row.isBold && 'font-bold',
                                row.isItalic && 'italic',
                            )}
                        >
                            <TableCell className="w-[180px] font-mono text-xs text-muted-foreground">
                                <div>{row.rowCode}</div>
                                {accountNumbers && <div className="mt-1">{accountNumbers}</div>}
                            </TableCell>
                            <TableCell
                                colSpan={isHeading ? 2 : 1}
                                className={cn(isHeading && 'py-3 text-base')}
                                style={{
                                    paddingLeft: `${0.75 + Math.max(row.indentLevel, 0) * 1.25}rem`,
                                    textDecoration: row.isUnderlined ? 'underline' : undefined,
                                }}
                            >
                                {row.label}
                            </TableCell>
                            {!isHeading && (
                                <TableCell className="w-[160px] text-right">
                                    {formatMoney(row.amount)}
                                </TableCell>
                            )}
                        </TableRow>
                    );
                })}
        </>
    );
}
