'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { onboardingPlanTemplateService } from '@/services/hr/onboarding.service';
import type { HrAudienceTargetType } from '@/types/hr/orientation';
import { AudienceTargetPicker } from './AudienceTargetPicker';

/**
 * Who an onboarding plan template is for (round 4, lane I4).
 *
 * When a hire's start is confirmed the system now creates their onboarding plan from the most
 * specific template whose audience reaches them — position, then the nearest unit, then level,
 * location, everyone — falling back to the default. Before this nothing stored who a template was
 * for, and plans were only ever created by hand.
 */
export function OnboardingTemplateAudiencePanel({
  templateId,
  isDefault,
}: {
  templateId: string;
  isDefault: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const queryKey = ['hr', 'onboarding-templates', templateId, 'audiences'];

  const [targetType, setTargetType] = useState<HrAudienceTargetType>('Position');
  const [targetId, setTargetId] = useState<string | null>(null);
  const [isInclusive, setIsInclusive] = useState(true);

  const { data: audiences = [], isLoading } = useQuery({
    queryKey,
    queryFn: () => onboardingPlanTemplateService.getAudiences(templateId),
    enabled: !!templateId,
  });

  const add = useMutation({
    mutationFn: () =>
      onboardingPlanTemplateService.addAudience(templateId, {
        targetType,
        targetEntityId: targetType === 'AllEmployees' ? null : targetId,
        isInclusive,
      }),
    onSuccess: async () => {
      setTargetId(null);
      await queryClient.invalidateQueries({ queryKey });
    },
    onError: (e: Error) => toast({ title: 'Could not add', description: e.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (audienceId: string) => onboardingPlanTemplateService.removeAudience(templateId, audienceId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey }),
    onError: (e: Error) => toast({ title: 'Could not remove', description: e.message, variant: 'destructive' }),
  });

  const canAdd = targetType === 'AllEmployees' || !!targetId;

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Who this template is for</CardTitle>
        <p className="text-muted-foreground text-sm">
          When a hire’s start is confirmed, their onboarding plan is created from the most specific
          template that reaches them: a position beats a unit, the nearest unit beats one further up, then
          level, location and everyone.{' '}
          {isDefault
            ? 'This is the default template — it is used when no other template reaches the hire.'
            : audiences.length === 0
              ? 'With no audience this template is only ever chosen by hand.'
              : ''}
        </p>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <Loader2 className="text-muted-foreground h-4 w-4 animate-spin" />
        ) : audiences.length === 0 ? (
          <p className="text-muted-foreground text-sm">No audience yet.</p>
        ) : (
          <ul className="divide-y rounded-md border">
            {audiences.map((a) => (
              <li key={a.id} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                <span>{a.targetEntityName ?? a.targetType}</span>
                <span className="flex items-center gap-2">
                  {a.isInclusive ? (
                    <Badge variant="secondary">Applies</Badge>
                  ) : (
                    <Badge variant="outline">Never for</Badge>
                  )}
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label="Remove"
                    disabled={remove.isPending}
                    onClick={() => remove.mutate(a.id)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </span>
              </li>
            ))}
          </ul>
        )}

        <div className="space-y-3 rounded-md border p-3">
          <AudienceTargetPicker
            targetType={targetType}
            targetId={targetId}
            onTypeChange={setTargetType}
            onTargetChange={setTargetId}
            allowEmployee={false}
            idPrefix="template-audience"
          />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-2">
              <Switch id="template-audience-inclusive" checked={isInclusive} onCheckedChange={setIsInclusive} />
              <Label htmlFor="template-audience-inclusive" className="text-sm">
                {isInclusive ? 'Applies to them' : 'Never for them (an exclusion)'}
              </Label>
            </div>
            <Button size="sm" onClick={() => add.mutate()} disabled={!canAdd || add.isPending}>
              {add.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Plus className="mr-2 h-4 w-4" />}
              Add audience
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
