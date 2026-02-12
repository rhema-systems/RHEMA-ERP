'use client';

import { useMemo, useState } from 'react';
import { useMutation } from '@tanstack/react-query';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Mail, Phone, Save } from 'lucide-react';
import { authService } from '@/services/auth';
import { useToast } from '@/hooks/use-toast';
import { userProfileService } from '@/services/userProfileService';

export default function ProfilePage() {
  const { toast } = useToast();
  const user = authService.getStoredUser();
  const initial = useMemo(
    () => ({
      firstName: user?.firstName ?? '',
      lastName: user?.lastName ?? '',
      email: user?.email ?? '',
      phoneNumber: user?.phoneNumber ?? '',
    }),
    [user?.firstName, user?.lastName, user?.email, user?.phoneNumber],
  );

  const [firstName, setFirstName] = useState(initial.firstName);
  const [lastName, setLastName] = useState(initial.lastName);
  const [email, setEmail] = useState(initial.email);
  const [phoneNumber, setPhoneNumber] = useState(initial.phoneNumber);

  const canSave =
    Boolean(email.trim()) &&
    (firstName.trim() !== initial.firstName.trim() ||
      lastName.trim() !== initial.lastName.trim() ||
      email.trim() !== initial.email.trim() ||
      phoneNumber.trim() !== initial.phoneNumber.trim());

  const updateMutation = useMutation({
    mutationFn: async () => {
      const updated = await userProfileService.updateProfile({
        firstName: firstName.trim() || null,
        lastName: lastName.trim() || null,
        email: email.trim() || null,
        phoneNumber: phoneNumber.trim() || null,
      });

      const stored = authService.getStoredUser();
      if (stored) {
        localStorage.setItem(
          'user',
          JSON.stringify({
            ...stored,
            firstName: updated.firstName ?? stored.firstName,
            lastName: updated.lastName ?? stored.lastName,
            email: updated.email ?? stored.email,
            phoneNumber: updated.phoneNumber ?? stored.phoneNumber,
          }),
        );
      }

      return updated;
    },
    onSuccess: () => {
      toast({ title: 'Saved', description: 'Profile updated successfully.', variant: 'success' });
    },
    onError: (err) => {
      toast({
        title: 'Error',
        description: err instanceof Error ? err.message : 'Failed to update profile.',
        variant: 'destructive',
      });
    },
  });

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-bold">My Profile</h1>
        <p className="text-gray-600 mt-2">
          Manage your personal information and account settings
        </p>
      </div>

      {/* Profile Information */}
      <Card>
        <CardHeader>
          <CardTitle>Personal Information</CardTitle>
          <CardDescription>
            Update your personal details and contact information
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="firstName">First Name</Label>
              <Input
                id="firstName"
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                placeholder="Enter first name"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="lastName">Last Name</Label>
              <Input
                id="lastName"
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                placeholder="Enter last name"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="email">Email Address</Label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
              <Input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Enter email"
                className="pl-10"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="phone">Phone Number</Label>
            <div className="relative">
              <Phone className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
              <Input
                id="phone"
                type="tel"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
                placeholder="Enter phone number"
                className="pl-10"
              />
            </div>
          </div>

          <div className="flex justify-end space-x-3 pt-4">
            <Button
              variant="outline"
              onClick={() => {
                setFirstName(initial.firstName);
                setLastName(initial.lastName);
                setEmail(initial.email);
                setPhoneNumber(initial.phoneNumber);
              }}
              disabled={updateMutation.isPending}
            >
              Cancel
            </Button>
            <Button onClick={() => updateMutation.mutate()} disabled={!canSave || updateMutation.isPending}>
              <Save className="mr-2 h-4 w-4" />
              {updateMutation.isPending ? 'Saving…' : 'Save Changes'}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Account Information */}
      <Card>
        <CardHeader>
          <CardTitle>Account Information</CardTitle>
          <CardDescription>
            View your account details
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label className="text-gray-600">Username</Label>
              <p className="font-medium mt-1">{user?.username}</p>
            </div>
            <div>
              <Label className="text-gray-600">Account Type</Label>
              <p className="font-medium mt-1">External User</p>
            </div>
            <div>
              <Label className="text-gray-600">Authentication Provider</Label>
              <p className="font-medium mt-1">{user?.authenticationProvider}</p>
            </div>
            <div>
              <Label className="text-gray-600">Account Status</Label>
              <p className="font-medium mt-1 text-green-600">Active</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Security */}
      <Card>
        <CardHeader>
          <CardTitle>Security</CardTitle>
          <CardDescription>
            Manage your password and security settings
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Button variant="outline">
            Change Password
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

