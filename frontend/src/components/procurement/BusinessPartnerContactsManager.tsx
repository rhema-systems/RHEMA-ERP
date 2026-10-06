'use client';

import { useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
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
import {
  businessPartnerService,
  type BusinessPartnerContactDto,
  type CreateBusinessPartnerContactDto,
} from '@/services/businessPartnerService';

const emptyContact = (): CreateBusinessPartnerContactDto => ({
  contactName: '',
  title: '',
  department: '',
  email: '',
  phone: '',
  mobile: '',
  isPrimary: false,
});

export function BusinessPartnerContactsManager({
  partnerId,
  contacts,
  onContactsChange,
}: {
  partnerId: string;
  contacts: BusinessPartnerContactDto[];
  onContactsChange: (contacts: BusinessPartnerContactDto[]) => void;
}) {
  const [editing, setEditing] = useState<BusinessPartnerContactDto>();
  const [form, setForm] = useState<CreateBusinessPartnerContactDto>(emptyContact);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<BusinessPartnerContactDto>();
  const [busy, setBusy] = useState(false);

  const openCreate = () => {
    setEditing(undefined);
    setForm({ ...emptyContact(), isPrimary: contacts.length === 0 });
    setDialogOpen(true);
  };

  const openEdit = (contact: BusinessPartnerContactDto) => {
    setEditing(contact);
    setForm({
      contactName: contact.contactName || contact.name || '',
      title: contact.title || contact.contactTitle || '',
      department: contact.department || '',
      email: contact.email || '',
      phone: contact.phone || '',
      mobile: contact.mobile || '',
      isPrimary: contact.isPrimary,
    });
    setDialogOpen(true);
  };

  const save = async () => {
    if (!form.contactName.trim()) {
      toast.error('Enter the contact name.');
      return false;
    }

    try {
      setBusy(true);
      const saved = editing
        ? await businessPartnerService.updatePartnerContact(
            partnerId,
            editing.id,
            form
          )
        : await businessPartnerService.createPartnerContact(partnerId, form);
      onContactsChange(
        editing
          ? contacts.map((contact) => (contact.id === editing.id ? saved : contact))
          : [...contacts, saved]
      );
      setDialogOpen(false);
      toast.success(editing ? 'Contact updated.' : 'Contact added.');
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to save contact.');
      return false;
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!pendingDelete) return false;
    try {
      setBusy(true);
      await businessPartnerService.deletePartnerContact(partnerId, pendingDelete.id);
      onContactsChange(contacts.filter((contact) => contact.id !== pendingDelete.id));
      setPendingDelete(undefined);
      toast.success('Contact removed.');
      return true;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Unable to remove contact.');
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <div>
          <CardTitle>Contact Persons</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            Maintain the people responsible for this business partner relationship.
          </p>
        </div>
        <Button type="button" variant="outline" onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4" /> Add contact
        </Button>
      </CardHeader>
      <CardContent>
        {contacts.length === 0 ? (
          <p className="py-8 text-center text-muted-foreground">No contact persons recorded.</p>
        ) : (
          <div className="space-y-3">
            {contacts.map((contact) => (
              <div key={contact.id} className="flex flex-col justify-between gap-3 rounded-lg border p-4 sm:flex-row sm:items-center">
                <div>
                  <p className="font-medium">
                    {contact.contactName || contact.name || 'Unnamed contact'}
                    {contact.isPrimary ? ' (Primary)' : ''}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {[contact.title || contact.contactTitle, contact.department, contact.email, contact.phone]
                      .filter(Boolean)
                      .join(' · ') || 'No additional contact details'}
                  </p>
                </div>
                <div className="flex gap-2">
                  <Button type="button" size="sm" variant="outline" onClick={() => openEdit(contact)}>
                    <Pencil className="mr-2 h-4 w-4" /> Edit
                  </Button>
                  <Button type="button" size="sm" variant="outline" onClick={() => setPendingDelete(contact)}>
                    <Trash2 className="mr-2 h-4 w-4" /> Remove
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit contact' : 'Add contact'}</DialogTitle>
            <DialogDescription>Save the contact directly against this business partner.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <ContactField id="bp-contact-name" label="Full name *" value={form.contactName} onChange={(contactName) => setForm((current) => ({ ...current, contactName }))} />
            <ContactField id="bp-contact-title" label="Position" value={form.title} onChange={(title) => setForm((current) => ({ ...current, title }))} />
            <ContactField id="bp-contact-department" label="Department" value={form.department} onChange={(department) => setForm((current) => ({ ...current, department }))} />
            <ContactField id="bp-contact-email" label="Email" type="email" value={form.email} onChange={(email) => setForm((current) => ({ ...current, email }))} />
            <ContactField id="bp-contact-phone" label="Phone" value={form.phone} onChange={(phone) => setForm((current) => ({ ...current, phone }))} />
            <ContactField id="bp-contact-mobile" label="Mobile" value={form.mobile} onChange={(mobile) => setForm((current) => ({ ...current, mobile }))} />
            <label className="flex items-center gap-2 text-sm sm:col-span-2">
              <input type="checkbox" checked={form.isPrimary} onChange={(event) => setForm((current) => ({ ...current, isPrimary: event.target.checked }))} />
              Primary contact
            </label>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDialogOpen(false)} disabled={busy}>Cancel</Button>
            <Button type="button" onClick={() => void save()} disabled={busy}>{busy ? 'Saving...' : 'Save contact'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={Boolean(pendingDelete)}
        onOpenChange={(open) => { if (!open) setPendingDelete(undefined); }}
        title="Remove this contact?"
        description="The contact will be removed from this business partner record."
        confirmText="Remove contact"
        variant="destructive"
        onConfirm={remove}
        isLoading={busy}
      />
    </Card>
  );
}

function ContactField({
  id,
  label,
  value,
  type = 'text',
  onChange,
}: {
  id: string;
  label: string;
  value?: string;
  type?: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} type={type} value={value || ''} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}
