import React, { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { ScrollArea } from '@/components/ui/scroll-area';
import { SegmentStructure, SegmentLookupValue, SegmentSelection } from '@/types/finance';
import { financeService } from '@/services/finance.service';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface SegmentValueSelectorProps {
    onSelectionChange: (selections: SegmentSelection[]) => void;
}

export const SegmentValueSelector: React.FC<SegmentValueSelectorProps> = ({ onSelectionChange }) => {
    const [segments, setSegments] = useState<SegmentStructure[]>([]);
    const [loading, setLoading] = useState(true);
    const [selections, setSelections] = useState<Record<string, string[]>>({});

    useEffect(() => {
        loadSegmentStructures();
    }, []);

    const loadSegmentStructures = async () => {
        try {
            const segmentsList = await financeService.getSegments();

            // Filter for segments that are relevant (lookup required or has values)
            // And fetch lookup values for them if not already populated
            const enrichedSegments = await Promise.all(segmentsList.map(async (segment) => {
                if (segment.lookupTableRequired || (segment.lookupValuesCount || 0) > 0) {
                    try {
                        const values = await financeService.getSegmentLookupValues(segment.id);
                        return { ...segment, lookupValues: values };
                    } catch (e) {
                        console.warn(`Failed to load values for segment ${segment.segmentName}`, e);
                        return segment;
                    }
                }
                return segment;
            }));

            // Filter out those with no values to select (unless we want to show empty ones?)
            // We only want segments where selection is possible/required
            const applicableSegments = enrichedSegments.filter(s =>
                (s.lookupValues && s.lookupValues.length > 0)
            ).sort((a, b) => a.segmentPosition - b.segmentPosition);

            setSegments(applicableSegments);

            // Initialize selections
            const initialSelections: Record<string, string[]> = {};
            applicableSegments.forEach(s => {
                initialSelections[s.id] = [];
            });
            setSelections(initialSelections);
        } catch (error) {
            console.error('Failed to load segment structures', error);
        } finally {
            setLoading(false);
        }
    };

    const handleValueToggle = (segmentId: string, valueId: string) => {
        const currentValues = selections[segmentId] || [];
        const newValues = currentValues.includes(valueId)
            ? currentValues.filter(id => id !== valueId)
            : [...currentValues, valueId];
        const newSelections = { ...selections, [segmentId]: newValues };
        setSelections(newSelections);
        notifyParent(newSelections);
    };

    const handleSelectAll = (segmentId: string, allValues: SegmentLookupValue[]) => {
        const currentValues = selections[segmentId] || [];
        const allValueIds = allValues.map(v => v.id);
        const newValues = currentValues.length === allValues.length ? [] : allValueIds;
        const newSelections = { ...selections, [segmentId]: newValues };
        setSelections(newSelections);
        notifyParent(newSelections);
    };

    const notifyParent = (currentSelections: Record<string, string[]>) => {
        const selectionArray: SegmentSelection[] = Object.entries(currentSelections)
            .filter(([_, values]) => values.length > 0)
            .map(([segmentId, values]) => ({
                segmentStructureId: segmentId,
                selectedLookupValueIds: values
            }));
        onSelectionChange(selectionArray);
    };

    if (loading) {
        return <div className="flex justify-center p-8"><Loader2 className="h-8 w-8 animate-spin text-primary" /></div>;
    }

    if (segments.length === 0) {
        return <div className="p-4 text-center text-muted-foreground">No segmented structures found with lookup values.</div>;
    }

    return (
        <div className="space-y-6">
            {segments.map(segment => (
                <Card key={segment.id}>
                    <CardHeader className="pb-3">
                        <div className="flex justify-between items-center">
                            <div>
                                <CardTitle className="text-base">{segment.segmentName}</CardTitle>
                                <CardDescription>Select values for {segment.segmentName}</CardDescription>
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => handleSelectAll(segment.id, segment.lookupValues || [])}
                            >
                                {selections[segment.id]?.length === (segment.lookupValues?.length || 0) ? 'Deselect All' : 'Select All'}
                            </Button>
                        </div>
                    </CardHeader>
                    <CardContent>
                        <ScrollArea className="h-[200px] pr-4">
                            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                                {segment.lookupValues?.map(value => (
                                    <div key={value.id} className="flex items-start space-x-2 border p-2 rounded-md hover:bg-muted/50 transition-colors">
                                        <Checkbox
                                            id={`val-${value.id}`}
                                            checked={selections[segment.id]?.includes(value.id)}
                                            onCheckedChange={() => handleValueToggle(segment.id, value.id)}
                                        />
                                        <div className="grid gap-1.5 leading-none">
                                            <Label
                                                htmlFor={`val-${value.id}`}
                                                className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 cursor-pointer"
                                            >
                                                {value.segmentValue} - {value.description}
                                            </Label>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </ScrollArea>
                        <div className="mt-2 text-xs text-muted-foreground text-right">
                            {selections[segment.id]?.length || 0} selected
                        </div>
                    </CardContent>
                </Card>
            ))}
        </div>
    );
};
