'use client';

import type { ReactNode } from 'react';
import { Ban, ChevronDown, Send, Shield, ShoppingCart, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

interface AwardActionsProps {
  onNotify?: () => void;
  onPerformanceBond?: () => void;
  performanceBondStatus?: ReactNode;
  onCreate?: () => void;
  onNegotiate?: () => void;
  onCancel?: () => void;
}

// Callbacks are supplied only for permitted actions. Ordering is presentation,
// not a new mandatory bond gate or a substitute for server authorization.
export function AwardActions({
  onNotify, onPerformanceBond, performanceBondStatus, onCreate, onNegotiate, onCancel,
}: AwardActionsProps) {
  const hasRoutineActions = Boolean(onNotify || onPerformanceBond || onCreate);
  const hasOtherActions = Boolean(onNegotiate || onCancel);
  if (!hasRoutineActions && !hasOtherActions) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Actions</CardTitle>
        <CardDescription>Notify the awardee, address any required bond, then create the PO or contract.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {hasRoutineActions && (
          <div role="group" aria-label="Award follow-up actions" className="flex flex-wrap items-center gap-3">
            {onNotify && <Button onClick={onNotify} className="bg-blue-600 hover:bg-blue-700">
              <Send className="h-4 w-4 mr-2" />
              Send Notification
            </Button>}
            {onPerformanceBond && <Button onClick={onPerformanceBond} variant="outline" className="border-purple-300 text-purple-700 hover:bg-purple-50">
              <Shield className="h-4 w-4 mr-2" />
              Performance Bond
              {performanceBondStatus && <span className="ml-2">{performanceBondStatus}</span>}
            </Button>}
            {onCreate && <Button onClick={onCreate} className="bg-green-600 hover:bg-green-700">
              <ShoppingCart className="h-4 w-4 mr-2" />
              Create PO / Contract
            </Button>}
          </div>
        )}
        {hasOtherActions && (
          <details className="group border-t pt-3">
            <summary className="flex w-fit cursor-pointer list-none items-center gap-2 rounded text-sm font-medium text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring [&::-webkit-details-marker]:hidden">
              Other actions
              <ChevronDown aria-hidden="true" className="h-4 w-4 transition-transform group-open:rotate-180" />
            </summary>
            <div role="group" aria-label="Exceptional award actions" className="mt-3 space-y-3">
              <p className="text-sm text-muted-foreground">Use only when applicable; these are not routine next steps.</p>
              <div className="flex flex-wrap items-center gap-3">
                {onNegotiate && <Button onClick={onNegotiate} variant="outline" className="border-amber-300 text-amber-700 hover:bg-amber-50">
                  <Users className="h-4 w-4 mr-2" />
                  Invite for Negotiation
                </Button>}
                {onCancel && <Button variant="destructive" onClick={onCancel}>
                  <Ban className="h-4 w-4 mr-2" />
                  Cancel Award
                </Button>}
              </div>
            </div>
          </details>
        )}
      </CardContent>
    </Card>
  );
}
