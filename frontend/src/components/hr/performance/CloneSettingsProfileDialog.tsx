'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Copy, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/components/ui/use-toast';
import { appraisalSettingsService } from '@/services/hr/appraisal.service';

/**
 * Copies a settings profile under a new name (performance closure E-e, D-46/D-67) and opens the copy.
 *
 * A profile in use — any appraisal on a cycle running under it — keeps its rules: the appraisals read
 * them live, finished ones included. Changing them means a copy, edited, then made the default (or
 * chosen when the next cycle is created). The copy is never the default itself.
 */
export function CloneSettingsProfileDialog({
  profile,
  onOpenChange,
}: {
  profile: { id: string; settingsName: string } | null;
  onOpenChange: (open: boolean) => void;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [name, setName] = useState('');

  useEffect(() => {
    if (profile) setName(`${profile.settingsName} (copy)`);
  }, [profile]);

  const clone = useMutation({
    mutationFn: () => {
      if (!profile) throw new Error('No profile selected to copy.');
      return appraisalSettingsService.clone(profile.id, name.trim());
    },
    onSuccess: async (copy) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-settings'] });
      toast({ title: 'Copied', description: `“${copy.settingsName}” created — not the default.` });
      onOpenChange(false);
      router.push(`/administration/hr/performance/settings/${copy.id}`);
    },
    onError: (e: any) =>
      toast({
        title: 'Could not copy the profile',
        description: e?.message || 'Please try again.',
        variant: 'destructive',
      }),
  });

  return (
    <Dialog open={profile !== null} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Copy profile</DialogTitle>
          <DialogDescription>
            The copy takes every rule and setting from “{profile?.settingsName}”. It is not the
            default: make it the default, or pick it when you create the next cycle.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2 py-2">
          <Label htmlFor="cloneProfileName">New profile name</Label>
          <Input id="cloneProfileName" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={() => clone.mutate()} disabled={clone.isPending || !name.trim()}>
            {clone.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Copy className="mr-2 h-4 w-4" />
            )}
            Copy
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
