'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { toast } from 'sonner';
import { tenderService, type TenderClarificationDto, type AnswerClarificationDto } from '@/services/tenderService';

interface AnswerClarificationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenderId: string;
  clarification: TenderClarificationDto;
  onAnswered: () => void;
}

export function AnswerClarificationDialog({
  open,
  onOpenChange,
  tenderId,
  clarification,
  onAnswered,
}: AnswerClarificationDialogProps) {
  const [answer, setAnswer] = useState('');
  const [isPublic, setIsPublic] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async () => {
    if (!answer.trim()) {
      toast.error('Please enter an answer');
      return;
    }

    try {
      setSubmitting(true);
      
      const dto: AnswerClarificationDto = {
        answer: answer.trim(),
        isPublic,
      };

      await tenderService.answerClarification(tenderId, clarification.id, dto);
      toast.success('Clarification answered successfully');
      setAnswer('');
      setIsPublic(true);
      onOpenChange(false);
      onAnswered();
    } catch (error: any) {
      console.error('Error answering clarification:', error);
      toast.error(error.message || 'Failed to answer clarification');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Answer Clarification</DialogTitle>
          <DialogDescription>
            Provide an answer to the bidder's question
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Question */}
          <div className="p-4 bg-gray-50 border rounded-lg">
            <p className="text-sm font-medium text-gray-600 mb-2">Question from {clarification.businessPartnerName || 'Anonymous'}</p>
            <p className="text-gray-900">{clarification.question}</p>
          </div>

          {/* Answer */}
          <div className="space-y-2">
            <Label htmlFor="answer">Your Answer *</Label>
            <Textarea
              id="answer"
              value={answer}
              onChange={(e) => setAnswer(e.target.value)}
              placeholder="Enter your answer to this clarification..."
              rows={6}
              disabled={submitting}
            />
          </div>

          {/* Public Checkbox */}
          <div className="flex items-center space-x-2">
            <Checkbox
              id="isPublic"
              checked={isPublic}
              onCheckedChange={(checked) => setIsPublic(checked as boolean)}
              disabled={submitting}
            />
            <Label
              htmlFor="isPublic"
              className="text-sm font-normal cursor-pointer"
            >
              Make this answer visible to all bidders (recommended for transparency)
            </Label>
          </div>

          {!isPublic && (
            <div className="p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
              <p className="text-sm text-yellow-800">
                ⚠️ This answer will only be visible to the bidder who asked the question.
                For transparency, it's recommended to make clarifications public.
              </p>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={submitting || !answer.trim()}
          >
            {submitting ? 'Submitting...' : 'Submit Answer'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

