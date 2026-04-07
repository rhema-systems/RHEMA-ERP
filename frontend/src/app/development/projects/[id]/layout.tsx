import type { ReactNode } from 'react';
import ProjectWorkspaceLayoutClient from './ProjectWorkspaceLayoutClient';

export default function ProjectWorkspaceLayout({ children }: { children: ReactNode }) {
  return <ProjectWorkspaceLayoutClient>{children}</ProjectWorkspaceLayoutClient>;
}
