'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Trash2, Plus } from 'lucide-react';
import { toast } from 'sonner';
import { tenderService, TenderEvaluatorDto, AssignEvaluatorsDto } from '@/services/tenderService';
import AssignEvaluatorsDialog from './AssignEvaluatorsDialog';
import { format } from 'date-fns';

interface TenderEvaluatorsProps {
  tenderId: string;
  tenderStatus: string;
  onEvaluatorsChanged?: () => void;
}

export default function TenderEvaluators({ tenderId, tenderStatus, onEvaluatorsChanged }: TenderEvaluatorsProps) {
  const [evaluators, setEvaluators] = useState<TenderEvaluatorDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAssignDialog, setShowAssignDialog] = useState(false);

  useEffect(() => {
    loadEvaluators();
  }, [tenderId]);

  const loadEvaluators = async () => {
    try {
      setLoading(true);
      const data = await tenderService.getTenderEvaluators(tenderId);
      setEvaluators(data);
    } catch (error) {
      toast.error('Failed to load evaluators');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleAssignEvaluators = async (data: AssignEvaluatorsDto) => {
    try {
      await tenderService.assignEvaluators(tenderId, data);
      toast.success('Evaluators assigned successfully');
      setShowAssignDialog(false);
      await loadEvaluators();
      onEvaluatorsChanged?.();
    } catch (error) {
      toast.error('Failed to assign evaluators');
      console.error(error);
    }
  };

  const handleRemoveEvaluator = async (evaluatorId: string) => {
    if (!confirm('Are you sure you want to remove this evaluator?')) return;

    try {
      await tenderService.removeEvaluator(tenderId, evaluatorId);
      toast.success('Evaluator removed successfully');
      await loadEvaluators();
      onEvaluatorsChanged?.();
    } catch (error) {
      toast.error('Failed to remove evaluator');
      console.error(error);
    }
  };

  const isEditable = ['Draft', 'Published'].includes(tenderStatus);

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h3 className="text-lg font-semibold">Evaluators</h3>
        {isEditable && (
          <Button onClick={() => setShowAssignDialog(true)} size="sm">
            <Plus className="h-4 w-4 mr-2" />
            Assign Evaluators
          </Button>
        )}
      </div>

      {loading ? (
        <Card>
          <CardContent className="pt-6">
            <p className="text-center text-gray-500">Loading evaluators...</p>
          </CardContent>
        </Card>
      ) : evaluators.length === 0 ? (
        <Card>
          <CardContent className="pt-6">
            <p className="text-center text-gray-500">No evaluators assigned yet</p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="pt-6">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Assigned Date</TableHead>
                  <TableHead>Weightage</TableHead>
                  <TableHead>Evaluations</TableHead>
                  {isEditable && <TableHead>Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {evaluators.map((evaluator) => (
                  <TableRow key={evaluator.id}>
                    <TableCell className="font-medium">{evaluator.userName}</TableCell>
                    <TableCell>{evaluator.role}</TableCell>
                    <TableCell>
                      <Badge variant={evaluator.status === 'Assigned' ? 'default' : 'secondary'}>
                        {evaluator.status}
                      </Badge>
                    </TableCell>
                    <TableCell>{format(new Date(evaluator.assignedDate), 'PPP p')}</TableCell>
                    <TableCell>{evaluator.weightagePercentage ? `${evaluator.weightagePercentage}%` : '-'}</TableCell>
                    <TableCell>{evaluator.evaluationCount}</TableCell>
                    {isEditable && (
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleRemoveEvaluator(evaluator.id)}
                        >
                          <Trash2 className="h-4 w-4 text-red-500" />
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {showAssignDialog && (
        <AssignEvaluatorsDialog
          tenderId={tenderId}
          onClose={() => setShowAssignDialog(false)}
          onAssign={handleAssignEvaluators}
        />
      )}
    </div>
  );
}

