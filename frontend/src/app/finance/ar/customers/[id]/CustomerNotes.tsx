'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { arService } from '@/services/ar-service';
import { useToast } from '@/components/ui/use-toast';
import { Loader2, Edit, Save, X } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { Customer } from '@/types/ar';

interface CustomerNotesProps {
    customer: Customer;
}

export function CustomerNotes({ customer }: CustomerNotesProps) {
    const [isEditing, setIsEditing] = useState(false);
    const [notes, setNotes] = useState(customer.notes || '');
    const [isSaving, setIsSaving] = useState(false);
    const { toast } = useToast();
    const queryClient = useQueryClient();

    const handleSave = async () => {
        setIsSaving(true);
        try {
            await arService.updateCustomer(customer.id, {
                ...customer,
                notes: notes,
                currencyCode: customer.currencyCode // Ensure required field is passed if partial update isn't fully supported
            });

            await queryClient.invalidateQueries({ queryKey: ['customer', customer.id] });

            toast({
                title: 'Notes Saved',
                description: 'Customer notes have been updated.',
            });
            setIsEditing(false);
        } catch (error: any) {
            toast({
                title: 'Error',
                description: 'Failed to save notes: ' + error.message,
                variant: 'destructive',
            });
        } finally {
            setIsSaving(false);
        }
    };

    const handleCancel = () => {
        setNotes(customer.notes || '');
        setIsEditing(false);
    };

    return (
        <div className="space-y-4">
            <div className="flex justify-between items-center">
                <h3 className="text-lg font-medium">Notes</h3>
                {!isEditing && (
                    <Button variant="outline" size="sm" onClick={() => setIsEditing(true)}>
                        <Edit className="mr-2 h-3 w-3" /> Edit Notes
                    </Button>
                )}
            </div>

            {isEditing ? (
                <div className="space-y-4">
                    <Textarea
                        value={notes}
                        onChange={(e) => setNotes(e.target.value)}
                        placeholder="Add notes about this customer..."
                        className="min-h-[200px]"
                    />
                    <div className="flex justify-end space-x-2">
                        <Button variant="ghost" onClick={handleCancel} disabled={isSaving}>
                            <X className="mr-2 h-4 w-4" /> Cancel
                        </Button>
                        <Button onClick={handleSave} disabled={isSaving}>
                            {isSaving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            <Save className="mr-2 h-4 w-4" /> Save Notes
                        </Button>
                    </div>
                </div>
            ) : (
                <div className="p-4 rounded-lg border bg-muted/30 min-h-[100px] whitespace-pre-wrap text-sm">
                    {customer.notes ? (
                        customer.notes
                    ) : (
                        <span className="text-muted-foreground italic">No notes added.</span>
                    )}
                </div>
            )}
        </div>
    );
}
