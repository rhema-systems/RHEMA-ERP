'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { FileText, Search, Eye, Clock, CheckCircle, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import * as tenderAssignmentService from '@/services/tenderAssignmentService';
import * as tenderService from '@/services/tenderService';
import { type TenderAssignmentDto } from '@/services/tenderAssignmentService';
import { type TenderDto } from '@/services/tenderService';
import { format } from 'date-fns';

interface TenderWithAssignment {
  assignment: TenderAssignmentDto;
  tender?: TenderDto;
}

export default function TaskListPage() {
  const router = useRouter();
  const [tasks, setTasks] = useState<TenderWithAssignment[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [userId, setUserId] = useState<string>('');

  useEffect(() => {
    // Get user ID from auth context
    const currentUser = JSON.parse(localStorage.getItem('user') || '{}');
    if (currentUser.id) {
      setUserId(currentUser.id);
      loadTasks(currentUser.id);
    } else {
      setLoading(false);
      toast.error('User information not found');
    }
  }, []);

  const loadTasks = async (uid: string) => {
    try {
      setLoading(true);
      // Get all assignments for the user
      const assignments = await tenderAssignmentService.getAssignmentsByUserId(uid);
      
      // Fetch tender details for each assignment
      const tasksWithTenders = await Promise.all(
        assignments.map(async (assignment) => {
          try {
            const tender = await tenderService.getTenderById(assignment.tenderId);
            return { assignment, tender };
          } catch (error) {
            console.error(`Error loading tender ${assignment.tenderId}:`, error);
            return { assignment };
          }
        })
      );
      
      setTasks(tasksWithTenders);
    } catch (error) {
      console.error('Error loading tasks:', error);
      toast.error('Failed to load tasks');
    } finally {
      setLoading(false);
    }
  };

  const handleViewTender = (tenderId: string) => {
    router.push(`/external-portal/tenders/${tenderId}`);
  };

  const handleBidOnTender = (tenderId: string) => {
    router.push(`/external-portal/tenders/${tenderId}/initiate-bid`);
  };

  const filteredTasks = tasks.filter(task => {
    if (!searchTerm) return true;
    const lowerSearch = searchTerm.toLowerCase();
    return (
      task.assignment.tenderNumber?.toLowerCase().includes(lowerSearch) ||
      task.assignment.tenderTitle?.toLowerCase().includes(lowerSearch) ||
      task.tender?.tenderNumber?.toLowerCase().includes(lowerSearch) ||
      task.tender?.title?.toLowerCase().includes(lowerSearch)
    );
  });

  const getStatusBadge = (status?: string) => {
    if (!status) return null;
    
    const statusConfig: Record<string, { className: string, icon: any }> = {
      'Draft': { className: 'bg-gray-100 text-gray-800', icon: Clock },
      'Published': { className: 'bg-blue-100 text-blue-800', icon: CheckCircle },
      'Closed': { className: 'bg-red-100 text-red-800', icon: AlertCircle },
      'Awarded': { className: 'bg-green-100 text-green-800', icon: CheckCircle },
    };
    
    const config = statusConfig[status] || { className: 'bg-gray-100 text-gray-800', icon: FileText };
    const Icon = config.icon;
    
    return (
      <Badge className={config.className}>
        <Icon className="h-3 w-3 mr-1" />
        {status}
      </Badge>
    );
  };

  const getAssignmentTypeBadge = (type: string) => {
    const typeConfig: Record<string, { className: string }> = {
      'AllUsers': { className: 'bg-purple-100 text-purple-800' },
      'Self': { className: 'bg-blue-100 text-blue-800' },
      'SelectedUsers': { className: 'bg-green-100 text-green-800' },
    };
    
    const config = typeConfig[type] || { className: 'bg-gray-100 text-gray-800' };
    
    return (
      <Badge className={config.className}>
        {type === 'AllUsers' ? 'All Users' : type === 'SelectedUsers' ? 'Selected Users' : type}
      </Badge>
    );
  };

  const isDeadlinePassed = (deadline?: string) => {
    if (!deadline) return false;
    return new Date(deadline) < new Date();
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-muted-foreground">Loading tasks...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div>
        <h1 className="text-3xl font-bold">My Tasks</h1>
        <p className="text-muted-foreground mt-1">
          Tenders assigned to you for bidding
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Assigned Tenders</CardTitle>
          <CardDescription>
            View all tenders that have been assigned to you
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="mb-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search by tender number or title..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-10"
              />
            </div>
          </div>

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Tender Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Assignment Type</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Deadline</TableHead>
                  <TableHead>Assigned Date</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredTasks.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                      No tasks found
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredTasks.map((task) => (
                    <TableRow key={task.assignment.id}>
                      <TableCell className="font-medium">
                        {task.tender?.tenderNumber || task.assignment.tenderNumber || 'N/A'}
                      </TableCell>
                      <TableCell>
                        {task.tender?.title || task.assignment.tenderTitle || 'N/A'}
                      </TableCell>
                      <TableCell>
                        {getAssignmentTypeBadge(task.assignment.assignmentType)}
                      </TableCell>
                      <TableCell>
                        {getStatusBadge(task.tender?.status)}
                      </TableCell>
                      <TableCell>
                        {task.tender?.submissionDeadline ? (
                          <div className="flex items-center gap-2">
                            <span className={isDeadlinePassed(task.tender.submissionDeadline) ? 'text-red-600' : ''}>
                              {format(new Date(task.tender.submissionDeadline), 'MMM dd, yyyy HH:mm')}
                            </span>
                            {isDeadlinePassed(task.tender.submissionDeadline) && (
                              <Badge variant="destructive" className="text-xs">
                                Expired
                              </Badge>
                            )}
                          </div>
                        ) : (
                          'N/A'
                        )}
                      </TableCell>
                      <TableCell>
                        {format(new Date(task.assignment.assignedAt), 'MMM dd, yyyy')}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => handleViewTender(task.assignment.tenderId)}
                          >
                            <Eye className="h-4 w-4 mr-1" />
                            View
                          </Button>
                          {task.tender?.status === 'Published' && !isDeadlinePassed(task.tender.submissionDeadline) && (
                            <Button
                              size="sm"
                              onClick={() => handleBidOnTender(task.assignment.tenderId)}
                            >
                              <FileText className="h-4 w-4 mr-1" />
                              Bid
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

