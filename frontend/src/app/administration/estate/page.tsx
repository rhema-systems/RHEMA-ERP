'use client';

import {
  BookOpen,
  BookTemplate,
  FileText,
  Globe2,
  ShieldCheck,
  SlidersHorizontal,
  Workflow,
} from 'lucide-react';

import {
  ModuleSetupHub,
  type ModuleSetupLink,
} from '@/components/settings/ModuleSetupHub';

const links: ModuleSetupLink[] = [
  {
    title: 'Workflow Setup',
    href: '/administration/workflow?q=Estate',
    icon: Workflow,
    description:
      'Manage Estate routing, stages, approver roles, and activation.',
  },
  {
    title: 'Lease & Agreement Templates',
    href: '/administration/document-management/document-templates?q=Estate',
    icon: FileText,
    description:
      'Maintain Estate generation templates, merge fields, and signature roles.',
  },
  {
    title: 'Document Metadata',
    href: '/administration/document-management/metadata-templates?q=Estate',
    icon: BookTemplate,
    description:
      'Define the metadata required when Estate publishes documents to DMS.',
  },
  {
    title: 'Access & Retention',
    href: '/administration/document-management/access-retention?q=Estate',
    icon: ShieldCheck,
    description:
      'Control Estate document access, archive periods, and legal-hold review.',
  },
  {
    title: 'Land Plot Setup',
    href: '/administration/estate/settings',
    icon: SlidersHorizontal,
    description:
      'Set the square meters that equal one plot for land portal listings.',
  },
  {
    title: 'GIS Integration',
    href: '/estate/gis',
    icon: Globe2,
    description:
      'Configure spatial data providers, layers, credentials, and connection health.',
  },
  {
    title: 'Procedure Catalogue',
    href: '/estate',
    icon: BookOpen,
    description:
      'Review the active Estate procedures and their case workspaces.',
  },
];

export default function EstateAdministrationPage() {
  return (
    <ModuleSetupHub
      moduleName="Estate"
      title="Estate Setup"
      description="Estate configuration is maintained by its owning systems: Workflow, Central DMS, GIS, and the procedure catalogue."
      links={links}
      workspaceHref="/estate"
      workspaceLabel="Open Estate"
    />
  );
}

