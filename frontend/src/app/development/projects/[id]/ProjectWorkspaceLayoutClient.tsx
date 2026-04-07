'use client';

import type { ReactNode } from 'react';
import { useSelectedLayoutSegment } from 'next/navigation';
import ProjectWorkspacePage from './ProjectWorkspacePage';
import { PROJECT_WORKSPACE_TABS, type ProjectWorkspaceTab } from './projectWorkspaceTabs';

export default function ProjectWorkspaceLayoutClient({ children }: { children: ReactNode }) {
  const selectedSegment = useSelectedLayoutSegment();
  const activeTab = PROJECT_WORKSPACE_TABS.includes(selectedSegment as ProjectWorkspaceTab)
    ? (selectedSegment as ProjectWorkspaceTab)
    : 'overview';

  return (
    <>
      <div className="hidden">{children}</div>
      <ProjectWorkspacePage initialTab={activeTab} />
    </>
  );
}
