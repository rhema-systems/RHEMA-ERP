'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

// Lengths mirror the entity's MaxLength attributes exactly — a form that allows more just moves the
// refusal to the server and loses the user's typing on the way. ⚠ `phoneNumber` really is capped at
// 15 on ExternalAssociate, narrower than most contact fields in HR.
export const externalAssociateSchema = z.object({
  title: z.string().max(10, 'Keep the title under 10 characters').optional().or(z.literal('')),
  firstName: z.string().min(1, 'First name is required').max(50),
  middleName: z.string().max(50).optional().or(z.literal('')),
  lastName: z.string().min(1, 'Last name is required').max(50),
  // Required on the server too: an associate with no address cannot be sent a panel invitation,
  // which is the whole reason the register exists.
  email: z.string().min(1, 'Email is required').email('Enter a valid email address').max(100),
  phoneNumber: z.string().min(1, 'Phone number is required').max(15, 'Maximum 15 characters'),
  companyName: z.string().max(100).optional().or(z.literal('')),
  role: z.string().max(100).optional().or(z.literal('')),
  isActive: z.boolean(),
});

export type ExternalAssociateFormValues = z.infer<typeof externalAssociateSchema>;

export const emptyExternalAssociate: ExternalAssociateFormValues = {
  title: '',
  firstName: '',
  middleName: '',
  lastName: '',
  email: '',
  phoneNumber: '',
  companyName: '',
  role: '',
  isActive: true,
};

interface ExternalAssociateFormProps {
  defaultValues: ExternalAssociateFormValues;
  onSubmit: (values: ExternalAssociateFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Shown beside the name once the record exists; it is minted on create and never changes. */
  associateNumber?: string;
}

/**
 * ⚠ `hasFixedModule` and `moduleId` are deliberately absent. Nothing anywhere reads either — grepped
 * across every service, template and screen in slice 8 — and an input that changes nothing is worse
 * than a missing one. The edit screen still sends the loaded values back so a round trip does not
 * wipe them.
 */
export function ExternalAssociateForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  associateNumber,
}: ExternalAssociateFormProps) {
  const form = useForm<ExternalAssociateFormValues>({
    resolver: zodResolver(externalAssociateSchema) as any,
    defaultValues,
  });

  const isActive = form.watch('isActive');

  return (
    <Card>
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>
            Associate Details
            {associateNumber && (
              <span className="ml-2 font-mono text-sm font-normal text-muted-foreground">
                {associateNumber}
              </span>
            )}
          </CardTitle>
          <CardDescription>
            Someone who acts for the organisation without holding an ERP login — an interview
            panellist, a technical assessor, an external adviser. The email address is how a panel
            invitation and its confirmation link reach them.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-4 gap-4">
            <div className="space-y-2">
              <Label htmlFor="title">Title</Label>
              <Input id="title" placeholder="Dr" {...form.register('title')} />
              {form.formState.errors.title && (
                <p className="text-sm text-red-500">{form.formState.errors.title.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="firstName">First name</Label>
              <Input id="firstName" {...form.register('firstName')} />
              {form.formState.errors.firstName && (
                <p className="text-sm text-red-500">{form.formState.errors.firstName.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="middleName">Middle name</Label>
              <Input id="middleName" {...form.register('middleName')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="lastName">Last name</Label>
              <Input id="lastName" {...form.register('lastName')} />
              {form.formState.errors.lastName && (
                <p className="text-sm text-red-500">{form.formState.errors.lastName.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" placeholder="assessor@firm.com" {...form.register('email')} />
              <p className="text-xs text-muted-foreground">
                Must be unique among your associates. A deleted associate releases their address.
              </p>
              {form.formState.errors.email && (
                <p className="text-sm text-red-500">{form.formState.errors.email.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="phoneNumber">Phone</Label>
              <Input id="phoneNumber" placeholder="0244 000 000" {...form.register('phoneNumber')} />
              {form.formState.errors.phoneNumber && (
                <p className="text-sm text-red-500">{form.formState.errors.phoneNumber.message}</p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="companyName">Organisation</Label>
              <Input id="companyName" placeholder="Fixture Partners Ltd" {...form.register('companyName')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="role">Role</Label>
              <Input id="role" placeholder="Technical assessor" {...form.register('role')} />
              <p className="text-xs text-muted-foreground">
                Free text — what they do for you, not the seat they take on a given panel.
              </p>
            </div>
          </div>

          <div className="flex items-center justify-between rounded-md border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="isActive">Active</Label>
              <p className="text-xs text-muted-foreground">
                Inactive associates stay on file and keep their place on every interview panel they
                have sat on — they simply stop appearing in the panel picker.
              </p>
            </div>
            <Switch
              id="isActive"
              checked={isActive}
              onCheckedChange={(v) => form.setValue('isActive', v)}
            />
          </div>
        </CardContent>
        <CardFooter className="flex justify-end space-x-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {submitLabel}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
