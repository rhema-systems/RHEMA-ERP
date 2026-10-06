'use client';

import {
  BookOpen,
  BookTemplate,
  FileSignature,
  Scale,
  ShieldCheck,
  Workflow,
} from 'lucide-react';

import {
  ModuleSetupHub,
  type ModuleSetupLink,
} from '@/components/settings/ModuleSetupHub';

const links: ModuleSetupLink[] = [
  {
    title: 'Workflow Setup',
    href: '/administration/workflow?q=Legal',
    icon: Workflow,
    description:
      'Manage Legal stages, routing, approver roles, and published workflow versions.',
  },
  {
    title: 'Agreement & Signature Templates',
    href: '/administration/document-management/document-templates?q=Legal',
    icon: FileSignature,
    description:
      'Maintain agreement content, approval roles, final signature roles, and dispatch channels.',
  },
  {
    title: 'Document Metadata',
    href: '/administration/document-management/metadata-templates?q=Legal',
    icon: BookTemplate,
    description:
      'Define the metadata required for Legal instruments and executed agreements.',
  },
  {
    title: 'Access & Retention',
    href: '/administration/document-management/access-retention?q=Legal',
    icon: ShieldCheck,
    description:
      'Maintain confidentiality, legal-hold review, archive, and destruction controls.',
  },
  {
    title: 'Procedure Catalogue',
    href: '/legal',
    icon: BookOpen,
    description:
      'Review Legal procedures, required documents, checklists, and case workspaces.',
  },
  {
    title: 'Property Agreement Reviews',
    href: '/legal/LegalPropertyAgreementReview',
    icon: Scale,
    description:
      'Open the governed customer and internal signature review workspace.',
  },
];

export default function LegalAdministrationPage() {
  return (
    <ModuleSetupHub
      moduleName="Legal"
      title="Legal Setup"
      description="Legal routing, agreements, signatures, document governance, and procedures are managed from one setup area."
      links={links}
      workspaceHref="/legal"
      workspaceLabel="Open Legal"
    />
  );
}

