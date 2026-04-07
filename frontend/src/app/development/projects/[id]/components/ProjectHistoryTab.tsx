import { format } from 'date-fns';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { ProjectDetailDto } from '@/services/projectService';

type ProjectHistoryTabProps = {
  project: ProjectDetailDto;
};

export function ProjectHistoryTab({ project }: ProjectHistoryTabProps) {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Initiation Versions</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {project.initiationVersions.map((version) => (
            <div key={version.id} className="rounded-lg border p-4">
              <div className="flex items-center justify-between">
                <div className="font-medium">
                  Version {version.versionNumber} - {version.changeType}
                </div>
                <div className="text-xs text-muted-foreground">
                  {format(new Date(version.createdAt), 'MMM dd, yyyy HH:mm')}
                </div>
              </div>
              <div className="text-sm text-muted-foreground">{version.notes || 'No notes provided'}</div>
            </div>
          ))}
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Status Timeline</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div>
            <div className="text-sm text-muted-foreground">Created</div>
            <div className="font-medium">{format(new Date(project.createdAt), 'MMM dd, yyyy HH:mm')}</div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground">Submitted</div>
            <div className="font-medium">
              {project.submittedAt ? format(new Date(project.submittedAt), 'MMM dd, yyyy HH:mm') : 'Not submitted'}
            </div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground">Approved</div>
            <div className="font-medium">
              {project.approvedAt ? format(new Date(project.approvedAt), 'MMM dd, yyyy HH:mm') : 'Not approved'}
            </div>
          </div>
          <div>
            <div className="text-sm text-muted-foreground">Status Remarks</div>
            <div className="font-medium">{project.statusRemarks || 'None'}</div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
