'use client';

import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { MessageSquare, Send, Clock, CheckCircle2 } from 'lucide-react';
import { toast } from 'sonner';
import { format } from 'date-fns';
import { tenderService, type TenderClarificationDto, type CreateClarificationDto } from '@/services/tenderService';

interface TenderClarificationsProps {
  tenderId: string;
  canAskQuestions?: boolean;
}

export function TenderClarifications({ tenderId, canAskQuestions = true }: TenderClarificationsProps) {
  const [clarifications, setClarifications] = useState<TenderClarificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [question, setQuestion] = useState('');

  useEffect(() => {
    loadClarifications();
  }, [tenderId]);

  const loadClarifications = async () => {
    try {
      setLoading(true);
      const data = await tenderService.getTenderClarifications(tenderId, true);
      setClarifications(data);
    } catch (error) {
      console.error('Error loading clarifications:', error);
      toast.error('Failed to load clarifications');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmitQuestion = async () => {
    if (!question.trim()) {
      toast.error('Please enter a question');
      return;
    }

    try {
      setSubmitting(true);
      const dto: CreateClarificationDto = {
        question: question.trim(),
        isPublic: true,
        category: 'General'
      };
      
      await tenderService.createClarification(tenderId, dto);
      toast.success('Question submitted successfully. You will be notified when it is answered.');
      setQuestion('');
      loadClarifications();
    } catch (error: any) {
      console.error('Error submitting question:', error);
      toast.error(error.message || 'Failed to submit question');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      {/* Ask Question Form */}
      {canAskQuestions && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <MessageSquare className="h-5 w-5" />
              Ask a Question
            </CardTitle>
            <CardDescription>
              Submit your questions about this tender. All questions and answers will be visible to all bidders.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="question">Your Question</Label>
              <Textarea
                id="question"
                placeholder="Enter your question about the tender requirements, specifications, or submission process..."
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                rows={4}
                disabled={submitting}
              />
            </div>
            <Button onClick={handleSubmitQuestion} disabled={submitting || !question.trim()}>
              <Send className="h-4 w-4 mr-2" />
              {submitting ? 'Submitting...' : 'Submit Question'}
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Clarifications List */}
      <Card>
        <CardHeader>
          <CardTitle>Questions & Answers ({clarifications.length})</CardTitle>
          <CardDescription>
            View all questions and answers about this tender
          </CardDescription>
        </CardHeader>
        <CardContent>
          {clarifications.length === 0 ? (
            <div className="text-center py-8 text-gray-500">
              <MessageSquare className="h-12 w-12 mx-auto mb-4 opacity-50" />
              <p>No questions have been asked yet.</p>
              {canAskQuestions && <p className="text-sm mt-2">Be the first to ask a question!</p>}
            </div>
          ) : (
            <div className="space-y-6">
              {clarifications.map((clarification) => (
                <div key={clarification.id} className="border-b pb-6 last:border-0 last:pb-0">
                  {/* Question */}
                  <div className="space-y-2">
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-2">
                          <MessageSquare className="h-4 w-4 text-blue-600" />
                          <span className="font-medium text-sm text-gray-700">Question</span>
                          <Badge variant={clarification.status === 'Answered' ? 'default' : 'secondary'} className="text-xs">
                            {clarification.status === 'Answered' ? (
                              <><CheckCircle2 className="h-3 w-3 mr-1" /> Answered</>
                            ) : (
                              <><Clock className="h-3 w-3 mr-1" /> Pending</>
                            )}
                          </Badge>
                        </div>
                        <p className="text-gray-900">{clarification.question}</p>
                      </div>
                    </div>
                    <p className="text-xs text-gray-500">
                      Asked on {format(new Date(clarification.questionDate), 'PPP')}
                    </p>
                  </div>

                  {/* Answer */}
                  {clarification.answer && (
                    <div className="mt-4 pl-6 border-l-2 border-green-200 bg-green-50 p-4 rounded-r">
                      <div className="flex items-center gap-2 mb-2">
                        <CheckCircle2 className="h-4 w-4 text-green-600" />
                        <span className="font-medium text-sm text-green-900">Answer</span>
                      </div>
                      <p className="text-gray-900">{clarification.answer}</p>
                      {clarification.answerDate && (
                        <p className="text-xs text-gray-600 mt-2">
                          Answered by {clarification.answeredByName || 'Procurement Team'} on {format(new Date(clarification.answerDate), 'PPP')}
                        </p>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

