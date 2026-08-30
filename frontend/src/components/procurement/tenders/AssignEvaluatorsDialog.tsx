'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent } from '@/components/ui/card';
import { Trash2, Plus } from 'lucide-react';
import { toast } from 'sonner';
import {
  AssignEvaluatorsDto,
  EvaluatorAssignmentDto,
  TenderEvaluatorCandidateDto,
  tenderService,
} from '@/services/tenderService';

interface AssignEvaluatorsDialogProps {
  tenderId: string;
  onClose: () => void;
  onAssign: (data: AssignEvaluatorsDto) => Promise<void>;
}

export default function AssignEvaluatorsDialog({
  tenderId,
  onClose,
  onAssign,
}: AssignEvaluatorsDialogProps) {
  const [evaluators, setEvaluators] = useState<EvaluatorAssignmentDto[]>([
    { userId: '', role: 'Evaluator', weightagePercentage: 100 },
  ]);
  const [users, setUsers] = useState<TenderEvaluatorCandidateDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const loadUsers = useCallback(async () => {
    try {
      setLoading(true);
      setUsers(await tenderService.getEvaluatorCandidates(tenderId));
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load authorised tender evaluators'
      );
    } finally {
      setLoading(false);
    }
  }, [tenderId]);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  const handleAddEvaluator = () => {
    setEvaluators([
      ...evaluators,
      { userId: '', role: 'Evaluator', weightagePercentage: 100 },
    ]);
  };

  const handleRemoveEvaluator = (index: number) => {
    setEvaluators(evaluators.filter((_, i) => i !== index));
  };

  const handleEvaluatorChange = (
    index: number,
    field: keyof EvaluatorAssignmentDto,
    value: any
  ) => {
    const updated = [...evaluators];
    updated[index] = { ...updated[index], [field]: value };
    setEvaluators(updated);
  };

  const handleSubmit = async () => {
    // Validate
    if (evaluators.some((e) => !e.userId)) {
      toast.error('Please select a user for all evaluators');
      return;
    }

    try {
      setSubmitting(true);
      await onAssign({ evaluators });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={true} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Assign Evaluators</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 max-h-96 overflow-y-auto">
          {evaluators.map((evaluator, index) => (
            <Card key={index}>
              <CardContent className="pt-6">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <Label htmlFor={`user-${index}`}>User</Label>
                    <Select
                      value={evaluator.userId}
                      onValueChange={(value) =>
                        handleEvaluatorChange(index, 'userId', value)
                      }
                    >
                      <SelectTrigger id={`user-${index}`}>
                        <SelectValue placeholder="Select user" />
                      </SelectTrigger>
                      <SelectContent>
                        {users.map((user) => {
                          const displayName =
                            user.fullName || user.userName || user.email;
                          const roleNames =
                            user.roleNames.length > 0
                              ? ` — ${user.roleNames.join(', ')}`
                              : '';
                          return (
                            <SelectItem key={user.userId} value={user.userId}>
                              {displayName}
                              {roleNames}
                            </SelectItem>
                          );
                        })}
                      </SelectContent>
                    </Select>
                  </div>

                  <div>
                    <Label htmlFor={`role-${index}`}>Role</Label>
                    <Select
                      value={evaluator.role}
                      onValueChange={(value) =>
                        handleEvaluatorChange(index, 'role', value)
                      }
                    >
                      <SelectTrigger id={`role-${index}`}>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Evaluator">Evaluator</SelectItem>
                        <SelectItem value="ChairPerson">
                          Chair Person
                        </SelectItem>
                        <SelectItem value="Secretary">Secretary</SelectItem>
                        <SelectItem value="Observer">Observer</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="flex items-end gap-2">
                    <div className="flex-1">
                      <Label htmlFor={`weightage-${index}`}>Weightage %</Label>
                      <Input
                        id={`weightage-${index}`}
                        type="number"
                        value={100}
                        readOnly
                        className="bg-gray-100 cursor-not-allowed"
                      />
                    </div>
                    {evaluators.length > 1 && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleRemoveEvaluator(index)}
                      >
                        <Trash2 className="h-4 w-4 text-red-500" />
                      </Button>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>

        {!loading && users.length === 0 && (
          <p className="text-sm text-amber-700">
            No active user in this tenant has a Security role granting tender
            evaluation.
          </p>
        )}

        <Button
          variant="outline"
          onClick={handleAddEvaluator}
          className="w-full"
        >
          <Plus className="h-4 w-4 mr-2" />
          Add Another Evaluator
        </Button>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={submitting || loading}>
            {submitting ? 'Assigning...' : 'Assign Evaluators'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
