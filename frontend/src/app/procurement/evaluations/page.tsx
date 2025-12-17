'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { FileText, Award, Clock } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderEvaluationService from '@/services/tenderEvaluationService';
import { type TenderEvaluationDto } from '@/services/tenderEvaluationService';
import { format } from 'date-fns';

export default function EvaluationsPage() {
  const router = useRouter();
  const [evaluations, setEvaluations] = useState<TenderEvaluationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedTenderId, setSelectedTenderId] = useState<string>('');

  useEffect(() => {
    loadEvaluations();
  }, []);

  const loadEvaluations = async () => {
    try {
      setLoading(true);
      const data = await tenderEvaluationService.getMyEvaluations();
      setEvaluations(data);
    } catch (error) {
      console.error('Error loading evaluations:', error);
      toast.error('Failed to load evaluations');
    } finally {
      setLoading(false);
    }
  };

  const handleEvaluate = (evaluation: TenderEvaluationDto) => {
    router.push(`/procurement/evaluations/${evaluation.id}`);
  };

  const getStatusBadge = (status: string) => {
    const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline', className: string }> = {
      'Draft': { variant: 'secondary', className: 'bg-gray-100 text-gray-800' },
      'Submitted': { variant: 'default', className: 'bg-blue-100 text-blue-800' },
      'Approved': { variant: 'default', className: 'bg-green-100 text-green-800' },
    };
    
    const config = statusConfig[status] || { variant: 'outline' as const, className: '' };
    return (
      <Badge variant={config.variant} className={config.className}>
        {status}
      </Badge>
    );
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    try {
      return format(new Date(dateString), 'PPP');
    } catch {
      return dateString;
    }
  };

  // Get unique tenders for filter
  const uniqueTenders = Array.from(
    new Map(
      evaluations.map(e => [e.tenderId, { id: e.tenderId, number: e.tenderNumber, title: e.tenderTitle }])
    ).values()
  );

  // Filter evaluations by selected tender
  const filteredEvaluations = selectedTenderId
    ? evaluations.filter(e => e.tenderId === selectedTenderId)
    : evaluations;

  // Group evaluations by status
  const draftEvaluations = filteredEvaluations.filter(e => e.status === 'Draft');
  const submittedEvaluations = filteredEvaluations.filter(e => e.status === 'Submitted');
  const approvedEvaluations = filteredEvaluations.filter(e => e.status === 'Approved');

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">My Evaluations</h1>
          <p className="text-gray-500">View and manage your bid evaluations</p>
        </div>
      </div>

      {/* Tender Filter */}
      {uniqueTenders.length > 0 && (
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center gap-4">
              <label className="text-sm font-medium text-gray-700">Filter by Tender:</label>
              <select
                value={selectedTenderId}
                onChange={(e) => setSelectedTenderId(e.target.value)}
                className="px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
              >
                <option value="">All Tenders</option>
                {uniqueTenders.map((tender) => (
                  <option key={tender.id} value={tender.id}>
                    {tender.number} - {tender.title}
                  </option>
                ))}
              </select>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Total Evaluations</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold">{evaluations.length}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Pending (Draft)</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-orange-600">{draftEvaluations.length}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Submitted</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-blue-600">{submittedEvaluations.length}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-gray-500">Approved</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-bold text-green-600">{approvedEvaluations.length}</p>
          </CardContent>
        </Card>
      </div>

      {/* Pending Evaluations */}
      {draftEvaluations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-orange-500" />
              Pending Evaluations
            </CardTitle>
            <CardDescription>Complete these evaluations</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Bid</TableHead>
                  <TableHead>Business Partner</TableHead>
                  <TableHead>Assigned Date</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Progress</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {draftEvaluations.map((evaluation) => (
                  <TableRow key={evaluation.id}>
                    <TableCell className="font-mono">{evaluation.bidNumber}</TableCell>
                    <TableCell className="font-medium">{evaluation.businessPartnerName}</TableCell>
                    <TableCell>{formatDate(evaluation.evaluationDate)}</TableCell>
                    <TableCell>{getStatusBadge(evaluation.status)}</TableCell>
                    <TableCell>
                      {evaluation.totalScore !== undefined && evaluation.totalScore !== null ? (
                        <div className="flex items-center gap-2">
                          <div className="w-24 bg-gray-200 rounded-full h-2">
                            <div
                              className="bg-blue-600 h-2 rounded-full"
                              style={{ width: `${evaluation.totalScore}%` }}
                            />
                          </div>
                          <span className="text-sm font-medium">{evaluation.totalScore.toFixed(0)}%</span>
                        </div>
                      ) : (
                        <span className="text-sm text-gray-500">Not started</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="default"
                        size="sm"
                        onClick={() => handleEvaluate(evaluation)}
                      >
                        <Award className="h-4 w-4 mr-2" />
                        Evaluate
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Submitted Evaluations */}
      {submittedEvaluations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <FileText className="h-5 w-5 text-blue-500" />
              Submitted Evaluations
            </CardTitle>
            <CardDescription>Awaiting approval</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Bid</TableHead>
                  <TableHead>Business Partner</TableHead>
                  <TableHead>Submitted Date</TableHead>
                  <TableHead>Total Score</TableHead>
                  <TableHead>Recommended</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {submittedEvaluations.map((evaluation) => (
                  <TableRow key={evaluation.id}>
                    <TableCell className="font-mono">{evaluation.bidNumber}</TableCell>
                    <TableCell className="font-medium">{evaluation.businessPartnerName}</TableCell>
                    <TableCell>{formatDate(evaluation.evaluationDate)}</TableCell>
                    <TableCell>
                      <span className="text-lg font-bold text-blue-600">
                        {evaluation.totalScore !== undefined && evaluation.totalScore !== null ? `${evaluation.totalScore.toFixed(2)}%` : 'N/A'}
                      </span>
                    </TableCell>
                    <TableCell>
                      {evaluation.isRecommended ? (
                        <Badge variant="default" className="bg-green-100 text-green-800">Yes</Badge>
                      ) : (
                        <Badge variant="outline">No</Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => handleEvaluate(evaluation)}
                      >
                        View
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Approved Evaluations */}
      {approvedEvaluations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Award className="h-5 w-5 text-green-500" />
              Approved Evaluations
            </CardTitle>
            <CardDescription>Completed evaluations</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Bid</TableHead>
                  <TableHead>Business Partner</TableHead>
                  <TableHead>Approved Date</TableHead>
                  <TableHead>Total Score</TableHead>
                  <TableHead>Recommended</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {approvedEvaluations.map((evaluation) => (
                  <TableRow key={evaluation.id}>
                    <TableCell className="font-mono">{evaluation.bidNumber}</TableCell>
                    <TableCell className="font-medium">{evaluation.businessPartnerName}</TableCell>
                    <TableCell>{formatDate(evaluation.evaluationDate)}</TableCell>
                    <TableCell>
                      <span className="text-lg font-bold text-green-600">
                        {evaluation.totalScore !== undefined && evaluation.totalScore !== null ? `${evaluation.totalScore.toFixed(2)}%` : 'N/A'}
                      </span>
                    </TableCell>
                    <TableCell>
                      {evaluation.isRecommended ? (
                        <Badge variant="default" className="bg-green-100 text-green-800">Yes</Badge>
                      ) : (
                        <Badge variant="outline">No</Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => handleEvaluate(evaluation)}
                      >
                        View
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Empty State */}
      {loading ? (
        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <Clock className="h-12 w-12 animate-spin mx-auto mb-4 text-blue-500" />
              <p className="text-gray-500">Loading evaluations...</p>
            </div>
          </CardContent>
        </Card>
      ) : evaluations.length === 0 ? (
        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <FileText className="h-12 w-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-500">No evaluations assigned yet</p>
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
