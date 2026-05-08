import React from 'react';
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { AccountCombinationPreview } from '@/types/finance';
import { ScrollArea } from '@/components/ui/scroll-area';
import { AlertCircle, CheckCircle2, AlertTriangle } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Card, CardContent } from '@/components/ui/card';

interface CombinationPreviewGridProps {
    combinations: AccountCombinationPreview[];
    onToggleSelection: (tempId: string) => void;
    onToggleAll: (checked: boolean) => void;
}

export const CombinationPreviewGrid: React.FC<CombinationPreviewGridProps> = ({
    combinations,
    onToggleSelection,
    onToggleAll
}) => {
    if (combinations.length === 0) {
        return (
            <div className="text-center p-8 border border-dashed rounded-lg bg-muted/20">
                <p className="text-muted-foreground">Generating combinations...</p>
            </div>
        );
    }

    const allSelected = combinations.every(c => c.isSelected);
    const someSelected = combinations.some(c => c.isSelected) && !allSelected;

    const getStatusBadge = (status: string, message?: string) => {
        switch (status) {
            case 'Valid':
                return <Badge variant="default" className="bg-green-600 hover:bg-green-700"><CheckCircle2 className="w-3 h-3 mr-1" /> Valid</Badge>;
            case 'Duplicate':
                return <Badge variant="secondary" className="bg-yellow-100 text-yellow-800 hover:bg-yellow-200"><AlertTriangle className="w-3 h-3 mr-1" /> Duplicate</Badge>;
            case 'Invalid':
                return <Badge variant="destructive"><AlertCircle className="w-3 h-3 mr-1" /> Invalid</Badge>;
            default:
                return <Badge variant="outline">{status}</Badge>;
        }
    };

    return (
        <Card className="border shadow-sm">
            <CardContent className="p-0">
                <ScrollArea className="h-[500px] w-full rounded-md">
                    <Table>
                        <TableHeader className="bg-muted/50 sticky top-0 z-10">
                            <TableRow>
                                <TableHead className="w-[50px] text-center">
                                    <Checkbox
                                        checked={allSelected || (someSelected ? "indeterminate" : false)}
                                        onCheckedChange={(checked) => onToggleAll(checked === true)}
                                    />
                                </TableHead>
                                <TableHead>Account Number</TableHead>
                                <TableHead>Account Name</TableHead>
                                <TableHead>Status</TableHead>
                                <TableHead>Validation Info</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {combinations.map((combo) => (
                                <TableRow
                                    key={combo.tempId}
                                    className={cn(
                                        "hover:bg-muted/50 transition-colors",
                                        combo.status === 'Duplicate' && "bg-yellow-50/50",
                                        combo.status === 'Invalid' && "bg-red-50/50"
                                    )}
                                >
                                    <TableCell className="text-center">
                                        <Checkbox
                                            checked={combo.isSelected}
                                            onCheckedChange={() => onToggleSelection(combo.tempId)}
                                            disabled={combo.status === 'Invalid' || combo.status === 'Duplicate'} // Typically shouldn't select invalid/dup, but requirements might say duplicates can be skipped
                                        />
                                    </TableCell>
                                    <TableCell className="font-mono font-medium">{combo.accountNumber}</TableCell>
                                    <TableCell>{combo.generatedName}</TableCell>
                                    <TableCell>{getStatusBadge(combo.status)}</TableCell>
                                    <TableCell className="text-sm text-muted-foreground truncate max-w-[300px]" title={combo.validationMessage || ''}>
                                        {combo.validationMessage}
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </ScrollArea>
                <div className="p-4 border-t bg-muted/20 flex justify-between items-center text-sm text-muted-foreground">
                    <div>
                        Total: <span className="font-medium text-foreground">{combinations.length}</span> combinations
                    </div>
                    <div className="flex gap-4">
                        <span>Valid: <span className="font-medium text-green-600">{combinations.filter(c => c.status === 'Valid').length}</span></span>
                        <span>Duplicate: <span className="font-medium text-yellow-600">{combinations.filter(c => c.status === 'Duplicate').length}</span></span>
                        <span>Invalid: <span className="font-medium text-red-600">{combinations.filter(c => c.status === 'Invalid').length}</span></span>
                    </div>
                </div>
            </CardContent>
        </Card>
    );
};
