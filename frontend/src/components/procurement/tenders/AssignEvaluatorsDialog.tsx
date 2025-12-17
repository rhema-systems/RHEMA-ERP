'use client';

import { useState, useEffect } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Card, CardContent } from '@/components/ui/card';
import { Trash2, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { AssignEvaluatorsDto, EvaluatorAssignmentDto } from '@/services/tenderService';

interface AssignEvaluatorsDialogProps {
  tenderId: string;
  onClose: () => void;
  onAssign: (data: AssignEvaluatorsDto) => Promise<void>;
}

export default function AssignEvaluatorsDialog({ tenderId, onClose, onAssign }: AssignEvaluatorsDialogProps) {
  const [evaluators, setEvaluators] = useState<EvaluatorAssignmentDto[]>([
    { userId: '', role: 'Evaluator', weightagePercentage: undefined }
  ]);
  const [users, setUsers] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    loadUsers();
  }, []);

  const loadUsers = async () => {
    try {
      setLoading(true);
      const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
      const token = localStorage.getItem('authToken') || localStorage.getItem('token');

      if (!token) {
        console.error('No authentication token found');
        toast.error('Authentication required');
        return;
      }

      console.log('🔐 Loading users - Authorization header present:', !!token);

      const response = await fetch(`${apiUrl}/user`, {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        }
      });

      if (!response.ok) {
        console.error('❌ Failed to load users:', response.status, response.statusText);
        toast.error(`Failed to load users: ${response.status} ${response.statusText}`);
        return;
      }

      const data = await response.json();
      console.log('✅ Users loaded:', data.length || 0, 'users');

      // Handle both array and object responses
      const userList = Array.isArray(data) ? data : (data.users || data.items || []);
      setUsers(Array.isArray(userList) ? userList : []);
    } catch (error) {
      console.error('Failed to load users:', error);
      toast.error('Failed to load users');
    } finally {
      setLoading(false);
    }
  };

  const handleAddEvaluator = () => {
    setEvaluators([...evaluators, { userId: '', role: 'Evaluator', weightagePercentage: undefined }]);
  };

  const handleRemoveEvaluator = (index: number) => {
    setEvaluators(evaluators.filter((_, i) => i !== index));
  };

  const handleEvaluatorChange = (index: number, field: keyof EvaluatorAssignmentDto, value: any) => {
    const updated = [...evaluators];
    updated[index] = { ...updated[index], [field]: value };
    setEvaluators(updated);
  };

  const handleSubmit = async () => {
    // Validate
    if (evaluators.some(e => !e.userId)) {
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
                    <Select value={evaluator.userId} onValueChange={(value) => handleEvaluatorChange(index, 'userId', value)}>
                      <SelectTrigger id={`user-${index}`}>
                        <SelectValue placeholder="Select user" />
                      </SelectTrigger>
                      <SelectContent>
                        {users.map((user) => {
                          const displayName = user.fullName || `${user.firstName || ''} ${user.lastName || ''}`.trim() || user.username || user.email;
                          return (
                            <SelectItem key={user.id} value={user.id}>
                              {displayName}
                            </SelectItem>
                          );
                        })}
                      </SelectContent>
                    </Select>
                  </div>

                  <div>
                    <Label htmlFor={`role-${index}`}>Role</Label>
                    <Select value={evaluator.role} onValueChange={(value) => handleEvaluatorChange(index, 'role', value)}>
                      <SelectTrigger id={`role-${index}`}>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Evaluator">Evaluator</SelectItem>
                        <SelectItem value="ChairPerson">Chair Person</SelectItem>
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
                        min="0"
                        max="100"
                        value={evaluator.weightagePercentage || ''}
                        onChange={(e) => handleEvaluatorChange(index, 'weightagePercentage', e.target.value ? parseFloat(e.target.value) : undefined)}
                        placeholder="0-100"
                      />
                    </div>
                    {evaluators.length > 1 && (
                      <Button variant="ghost" size="sm" onClick={() => handleRemoveEvaluator(index)}>
                        <Trash2 className="h-4 w-4 text-red-500" />
                      </Button>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>

        <Button variant="outline" onClick={handleAddEvaluator} className="w-full">
          <Plus className="h-4 w-4 mr-2" />
          Add Another Evaluator
        </Button>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button onClick={handleSubmit} disabled={submitting || loading}>
            {submitting ? 'Assigning...' : 'Assign Evaluators'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

