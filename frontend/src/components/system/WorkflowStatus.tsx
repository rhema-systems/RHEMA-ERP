'use client';

import { Badge } from '../ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';

interface WorkflowStatusProps {
  className?: string;
}

export function WorkflowStatus({ className }: WorkflowStatusProps) {
  const workflows = [
    { name: 'CI/CD Pipeline', status: 'running', type: 'build' },
    { name: 'Security Scan', status: 'running', type: 'security' },
    { name: 'Performance Test', status: 'running', type: 'performance' },
    { name: 'UI Testing', status: 'running', type: 'ui' },
    { name: 'Documentation', status: 'running', type: 'docs' },
    { name: 'Deployment', status: 'pending', type: 'deploy' },
  ];

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'running': return 'bg-blue-500 text-white';
      case 'success': return 'bg-green-500 text-white';
      case 'failed': return 'bg-red-500 text-white';
      case 'pending': return 'bg-yellow-500 text-white';
      default: return 'bg-gray-500 text-white';
    }
  };

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>Workflow Automation Status</CardTitle>
        <CardDescription>
          Real-time status of all automated workflows
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
          {workflows.map((workflow) => (
            <div key={workflow.name} className="flex items-center space-x-2">
              <Badge className={getStatusColor(workflow.status)}>
                {workflow.status}
              </Badge>
              <span className="text-sm font-medium">{workflow.name}</span>
            </div>
          ))}
        </div>
        <div className="mt-4 text-xs text-gray-500">
          This component triggers frontend testing workflows
        </div>
      </CardContent>
    </Card>
  );
}