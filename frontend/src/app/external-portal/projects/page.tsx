'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { ProjectExternalSummaryDto, projectService } from '@/services/projectService';

export default function ExternalProjectsPage() {
  const [projects, setProjects] = useState<ProjectExternalSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setProjects(await projectService.getExternalProjects());
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">My Projects</h1>
        <p className="text-muted-foreground">View project status, milestones, and assigned items.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Projects</CardTitle>
          <CardDescription>{loading ? 'Loading...' : `${projects.length} projects available`}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading projects...</div>
          ) : !projects.length ? (
            <div className="py-10 text-center text-muted-foreground">No projects are available for this account.</div>
          ) : (
            projects.map((project) => (
              <Link key={project.id} href={`/external-portal/projects/${project.id}`} className="block rounded-lg border p-4 transition-colors hover:bg-slate-50">
                <div className="flex items-start justify-between gap-4">
                  <div className="space-y-2">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold">{project.projectCode}</span>
                      <Badge variant="outline">{project.status}</Badge>
                      {project.externalCollaborationEnabled && <Badge>Collaboration</Badge>}
                    </div>
                    <div className="text-lg font-medium">{project.title}</div>
                    <div className="text-sm text-muted-foreground">{project.summary || 'No summary provided.'}</div>
                    <div className="text-sm text-muted-foreground">
                      {project.startDate ? new Date(project.startDate).toLocaleDateString() : 'No start date'} to {project.targetEndDate ? new Date(project.targetEndDate).toLocaleDateString() : 'No target date'}
                    </div>
                  </div>
                  <div className="w-52 space-y-2">
                    <div className="flex items-center justify-between text-sm">
                      <span>Progress</span>
                      <span>{project.progressPercent}%</span>
                    </div>
                    <Progress value={project.progressPercent} />
                    <div className="text-xs text-muted-foreground">{project.openMilestoneCount} open milestones</div>
                  </div>
                </div>
              </Link>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
