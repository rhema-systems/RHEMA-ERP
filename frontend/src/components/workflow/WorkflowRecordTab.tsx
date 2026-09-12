'use client';

import * as React from 'react';

import { TabsContent, TabsTrigger } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import { useWorkflowSummary, type WorkflowSummaryOptions } from '@/hooks/useWorkflowSummary';
import {
  WorkflowApprovalHistoryPanel,
  type WorkflowApprovalHistoryPanelProps,
} from '@/components/workflow/WorkflowApprovalHistoryPanel';

export const DEFAULT_WORKFLOW_TAB_VALUE = 'workflow';

interface WorkflowTabTriggerProps
  extends Omit<React.ComponentPropsWithoutRef<typeof TabsTrigger>, 'value' | 'children'>, WorkflowSummaryOptions {
  value?: string;
  label?: string;
  /** Some workspaces retain a normal operational timeline alongside workflow history. */
  hasHistoryContent?: boolean;
}

export function WorkflowTabTrigger({
  value = DEFAULT_WORKFLOW_TAB_VALUE,
  label = 'Workflow',
  entityType,
  entityId,
  workflowSummary,
  workflowSummaryLoading,
  workflowSummaryError,
  loadWorkflowSummary,
  hasHistoryContent = false,
  ...props
}: WorkflowTabTriggerProps) {
  const { visibility, error } = useWorkflowSummary({
    entityType, entityId, workflowSummary, workflowSummaryLoading, workflowSummaryError, loadWorkflowSummary,
  });
  if (!visibility.showTab && !hasHistoryContent) return null;
  return (
    <TabsTrigger value={value} title={error ? 'Workflow status unavailable' : undefined} {...props} disabled={props.disabled || !visibility.known}>
      {visibility.direct ? 'History' : label}
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
  const { visibility } = useWorkflowSummary(props);
  if (!visibility.showTab && !children) return null;
  return (
    <TabsContent value={value} className={cn('space-y-4 mt-4', className)}>
      <WorkflowRecordPanel {...props}>{children}</WorkflowRecordPanel>
    </TabsContent>
  );
}
