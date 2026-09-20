'use client';

import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { procurementConfigurationService } from '@/services/procurement-configuration.service';
import type {
  ProcurementConfigurationDecision,
  ProcurementConfigurationProfileStatus,
} from '@/types/procurement-configuration';

type Props = {
  profileId: string;
  profileStatus: ProcurementConfigurationProfileStatus;
  decision: ProcurementConfigurationDecision;
  canManage: boolean;
  onChanged: () => void | Promise<void>;
};

export function WithdrawSupplierPolicyDecision({
  profileId,
  profileStatus,
  decision,
  canManage,
  onChanged,
}: Props) {
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [failure, setFailure] = useState<string>();
  const withdrawal = useMutation({
    mutationFn: () =>
      procurementConfigurationService.withdrawDecision(
        profileId,
        decision.decisionKey,
        {
          rowVersion: decision.rowVersion,
          reason: reason.trim(),
        }
      ),
  });

  if (decision.decisionKey !== 'DEC-011') return null;
  if (decision.status === 'Withdrawn')
    return (
      <p className="text-sm text-muted-foreground">
        This optional supplier policy was withdrawn. Its values, evidence and
        assessment history remain available for audit.
      </p>
    );
  if (
    !canManage ||
    profileStatus !== 'Published' ||
    decision.status !== 'Approved'
  )
    return null;

  const confirm = async () => {
    if (!reason.trim()) return false;
    setFailure(undefined);
    try {
      await withdrawal.mutateAsync();
    } catch (error) {
      const message = getProcurementProblemMessage(
        error,
        'The decision could not be withdrawn.'
      );
      setFailure(message);
      toast({
        title: 'Withdrawal rejected',
        description: message,
        variant: 'destructive',
      });
      return false;
    }
    toast({
      title: 'Optional supplier policy withdrawn',
      description:
        'The other policy decisions and audit history are unchanged.',
      variant: 'success',
    });
    setReason('');
    setOpen(false);
    // A refresh failure must not imply that the successful withdrawal can be resubmitted.
    try {
      await onChanged();
    } catch {
      toast({
        title: 'Withdrawal saved',
        description: 'Refresh the profile to see the updated decision.',
      });
    }
    return true;
  };

  return (
    <div className="border-t pt-4">
      <Button
        variant="outline"
        onClick={() => {
          setFailure(undefined);
          setOpen(true);
        }}
      >
        Withdraw optional supplier policy
      </Button>
      <ConfirmationDialog
        open={open}
        onOpenChange={(value) => {
          if (!withdrawal.isPending) setOpen(value);
        }}
        title="Withdraw DEC-011 only?"
        confirmText="Withdraw decision"
        variant="destructive"
        isLoading={withdrawal.isPending}
        confirmDisabled={!reason.trim()}
        onConfirm={confirm}
        maxWidth="560px"
        description="This stops the optional supplier risk, annual-list and performance policy from applying to new decisions. Required supplier eligibility, tender terms, approvals and audit controls remain. The other 13 decisions are not changed; existing assessments and evidence are retained."
      >
        <div className="space-y-2">
          <Label htmlFor="supplier-policy-withdrawal-reason">
            Approved business reason or change reference
          </Label>
          <Textarea
            id="supplier-policy-withdrawal-reason"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={1000}
            placeholder="Explain the approved policy change and its business basis"
          />
          {failure && (
            <p role="alert" className="text-sm text-destructive">
              {failure}
            </p>
          )}
        </div>
      </ConfirmationDialog>
    </div>
  );
}
