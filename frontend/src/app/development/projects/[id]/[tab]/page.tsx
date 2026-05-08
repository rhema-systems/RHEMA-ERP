import { notFound } from 'next/navigation';
import { PROJECT_WORKSPACE_TABS, type ProjectWorkspaceTab } from '../projectWorkspaceTabs';

export default async function ProjectWorkspaceTabPage({ params }: { params: Promise<{ tab: string }> }) {
  const { tab } = await params;
  if (!PROJECT_WORKSPACE_TABS.includes(tab as ProjectWorkspaceTab)) {
    notFound();
  }

  return null;
}
