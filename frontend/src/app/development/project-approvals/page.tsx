'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Eye, RefreshCw } from 'lucide-react';
import { ProjectWorkflowApprovalQueueItemDto, projectService } from '@/services/projectService';
import { toast } from 'sonner';

const entityTypeOptions = [
  { value: 'all', label: 'All items' },
  { value: 'Project', label: 'Projects' },
  { value: 'ProjectBudgetRevision', label: 'Budget revisions' },
  { value: 'ProjectDeliverable', label: 'Deliverables' },
  { value: 'ProjectClosure', label: 'Closures' },
];

const formatEntityLabel = (entityType: string) => entityType.replace('Project', '').replace(/([A-Z])/g, ' $1').trim();

export default function ProjectApprovalsPage() {
  const router = useRouter();
  const [items, setItems] = useState<ProjectWorkflowApprovalQueueItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [entityType, setEntityType] = useState('all');

  const load = async () => {
    try {
      setLoading(true);
      setItems(await projectService.getWorkflowApprovalQueueReport(150, entityType === 'all' ? undefined : entityType));
    } catch (error: any) {
      toast.error(error.message || 'Failed to load workflow approvals');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [entityType]);

  const pendingItems = useMemo(() => items.filter((item) => item.status === 'PendingApproval'), [items]);
  const approvedItems = useMemo(() => items.filter((item) => item.status === 'Approved'), [items]);
  const rejectedItems = useMemo(() => items.filter((item) => item.status === 'Rejected'), [items]);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Project Approvals</h1>
          <p className="text-muted-foreground">Workflow-backed approvals across project setup, delivery, finance, and closure.</p>
        </div>
        <div className="flex gap-2">
          <Select value={entityType} onValueChange={setEntityType}>
            <SelectTrigger className="w-[220px]">
              <SelectValue placeholder="Filter by entity" />
            </SelectTrigger>
            <SelectContent>
              {entityTypeOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button variant="outline" onClick={load}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardHeader className="pb-2"><CardDescription>Total Items</CardDescription><CardTitle>{items.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Approval</CardDescription><CardTitle>{pendingItems.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Approved</CardDescription><CardTitle>{approvedItems.length}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Rejected</CardDescription><CardTitle>{rejectedItems.length}</CardTitle></CardHeader></Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Approval Queue</CardTitle>
          <CardDescription>Open and recent workflow items from the project module.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? <div className="py-10 text-center text-muted-foreground">Loading approval queue...</div> : null}
          {!loading && items.length === 0 ? <div className="py-10 text-center text-muted-foreground">No workflow approvals found for the selected filter.</div> : null}
          {!loading && items.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Project</TableHead>
                  <TableHead>Item</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Submitted</TableHead>
                  <TableHead>Days Pending</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((item) => (
                  <TableRow key={`${item.entityType}-${item.entityId}`}>
                    <TableCell>
                      <div className="font-medium">{item.projectCode}</div>
                      <div className="text-xs text-muted-foreground">{item.projectTitle}</div>
                    </TableCell>
                    <TableCell>{item.itemTitle}</TableCell>
                    <TableCell>{formatEntityLabel(item.entityType)}</TableCell>
                    <TableCell>
                      <Badge variant={item.status === 'PendingApproval' ? 'secondary' : 'outline'}>
                        {item.queueStage}
                      </Badge>
                    </TableCell>
                    <TableCell>{item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : 'Not recorded'}</TableCell>
                    <TableCell>{item.daysPending}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="outline" size="sm" onClick={() => router.push(`/development/projects/${item.projectId}`)}>
                        <Eye className="mr-2 h-4 w-4" />
                        View Project
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : null}
        </CardContent>
      </Card>
    </div>
  );
}
