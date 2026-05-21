'use client';

import * as React from 'react';

import { TabsContent, TabsTrigger } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import {
  WorkflowApprovalHistoryPanel,
  type WorkflowApprovalHistoryPanelProps,
} from '@/components/workflow/WorkflowApprovalHistoryPanel';

export const DEFAULT_WORKFLOW_TAB_VALUE = 'workflow';

interface WorkflowTabTriggerProps
  extends Omit<React.ComponentPropsWithoutRef<typeof TabsTrigger>, 'value' | 'children'> {
  value?: string;
  label?: string;
}

export function WorkflowTabTrigger({
  value = DEFAULT_WORKFLOW_TAB_VALUE,
  label = 'Workflow',
  ...props
}: WorkflowTabTriggerProps) {
  return (
    <TabsTrigger value={value} {...props}>
      {label}
    </TabsTrigger>
  );
}

export interface WorkflowTabContentProps extends WorkflowApprovalHistoryPanelProps {
  value?: string;
  className?: string;
  children?: React.ReactNode;
}

export interface WorkflowRecordPanelProps extends WorkflowApprovalHistoryPanelProps {
  children?: React.ReactNode;
}

export function WorkflowRecordPanel({ children, ...props }: WorkflowRecordPanelProps) {
  return (
    <>
      <WorkflowApprovalHistoryPanel {...props} />
      {children}
    </>
  );
}

export function WorkflowTabContent({
  value = DEFAULT_WORKFLOW_TAB_VALUE,
  className,
  children,
  ...props
}: WorkflowTabContentProps) {
  return (
    <TabsContent value={value} className={cn('space-y-4 mt-4', className)}>
      <WorkflowRecordPanel {...props}>{children}</WorkflowRecordPanel>
    </TabsContent>
  );
}
