'use client';

import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { UserPlus, Edit, Trash2, CheckCircle, XCircle, Search, Key } from 'lucide-react';
import { toast } from 'sonner';
import * as businessPartnerUserService from '@/services/businessPartnerUserService';
import { type BusinessPartnerUserDto, type CreateBusinessPartnerUserDto, type UpdateBusinessPartnerUserDto } from '@/services/businessPartnerUserService';
import { format } from 'date-fns';

export default function UserManagementPage() {
  const [users, setUsers] = useState<BusinessPartnerUserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [businessPartnerId, setBusinessPartnerId] = useState<string>('');
  
  // Dialog states
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [isResetPasswordDialogOpen, setIsResetPasswordDialogOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<BusinessPartnerUserDto | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  
  // Form states
  const [formData, setFormData] = useState<CreateBusinessPartnerUserDto>({
    businessPartnerId: '',
    email: '',
    firstName: '',
    lastName: '',
    userName: '',
    password: '',
    phoneNumber: '',
    role: 'User',
    notes: '',
  });
  
  const [editFormData, setEditFormData] = useState<UpdateBusinessPartnerUserDto>({
    firstName: '',
    lastName: '',
    phoneNumber: '',
    role: 'User',
    isActive: true,
    notes: '',
  });

  useEffect(() => {
    // Get business partner ID by fetching current user's business partner
    const loadBusinessPartner = async () => {
      try {
        const currentUser = JSON.parse(localStorage.getItem('user') || '{}');
        console.log('Current user from localStorage:', currentUser);

        if (!currentUser.id) {
          toast.error('User information not found. Please log in again.');
          setLoading(false);
          return;
        }

        console.log('Fetching business partner for user ID:', currentUser.id);

        // First, try to get the business partner user link (for sub-users)
        try {
          const businessPartnerUser = await businessPartnerUserService.getUserByUserId(currentUser.id);
          console.log('Business partner user link found:', businessPartnerUser);

          if (businessPartnerUser && businessPartnerUser.businessPartnerId) {
            console.log('Setting business partner ID from user link:', businessPartnerUser.businessPartnerId);
            setBusinessPartnerId(businessPartnerUser.businessPartnerId);
            loadUsers(businessPartnerUser.businessPartnerId);
            return;
          }
        } catch (error) {
          console.log('No business partner user link found, trying direct business partner lookup');
        }

        // If no business partner user link, fetch the business partner directly (for main user)
        // We need to import and use the business partner service
        const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || '/api'}/procurement/business-partners/user/${currentUser.id}`, {
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${localStorage.getItem('token') || localStorage.getItem('authToken')}`,
          },
        });

        if (response.ok) {
          const businessPartner = await response.json();
          console.log('Business partner found:', businessPartner);

          if (businessPartner && businessPartner.id) {
            console.log('Setting business partner ID:', businessPartner.id);
            setBusinessPartnerId(businessPartner.id);
            loadUsers(businessPartner.id);
          } else {
            setLoading(false);
            toast.error('Business partner information not found. Please complete your business partner registration first.');
          }
        } else {
          setLoading(false);
          toast.error('Business partner information not found. Please complete your business partner registration first.');
        }
      } catch (error: any) {
        console.error('Error loading business partner:', error);
        console.error('Error details:', error.message);
        setLoading(false);
        toast.error('Failed to load business partner information: ' + (error.message || 'Unknown error'));
      }
    };

    loadBusinessPartner();
  }, []);

  const loadUsers = async (bpId: string) => {
    try {
      setLoading(true);

      // Fetch sub-users from BusinessPartnerUser table
      const subUsers = await businessPartnerUserService.getUsersByBusinessPartnerId(bpId);

      // Fetch the main business partner to get the main account owner
      const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || '/api'}/procurement/business-partners/${bpId}`, {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token') || localStorage.getItem('authToken')}`,
        },
      });

      if (response.ok) {
        const businessPartner = await response.json();

        // If there's a main user, create a user object for them
        if (businessPartner.userId) {
          const mainUser: BusinessPartnerUserDto = {
            id: businessPartner.userId, // Use userId as the ID for display purposes
            businessPartnerId: businessPartner.id,
            businessPartnerName: businessPartner.partnerName,
            userId: businessPartner.userId,
            userName: businessPartner.userEmail || 'Main Account',
            userEmail: businessPartner.userEmail,
            userFullName: businessPartner.userFullName || businessPartner.contactPerson || 'Main Account Owner',
            role: 'Admin', // Main account owner is always Admin
            isActive: true,
            grantedAt: businessPartner.createdAt || new Date().toISOString(),
            grantedByName: 'System',
            notes: 'Main account owner',
          };

          // Combine main user with sub-users
          setUsers([mainUser, ...subUsers]);
        } else {
          setUsers(subUsers);
        }
      } else {
        // If we can't fetch the business partner, just show sub-users
        setUsers(subUsers);
      }
    } catch (error) {
      console.error('Error loading users:', error);
      toast.error('Failed to load users');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateUser = async () => {
    try {
      // Validate required fields
      if (!businessPartnerId) {
        toast.error('Business partner information not found');
        return;
      }

      if (!formData.firstName || !formData.lastName || !formData.email || !formData.userName || !formData.password) {
        toast.error('Please fill in all required fields');
        return;
      }

      const dataToSubmit = {
        ...formData,
        businessPartnerId: businessPartnerId,
      };

      console.log('Submitting user data:', dataToSubmit);

      await businessPartnerUserService.createUser(dataToSubmit);
      toast.success('User created successfully');
      setIsCreateDialogOpen(false);
      resetFormData();
      loadUsers(businessPartnerId);
    } catch (error: any) {
      console.error('Error creating user:', error);
      toast.error(error.message || 'Failed to create user');
    }
  };

  const handleUpdateUser = async () => {
    if (!selectedUser) return;

    // Prevent editing main account owner
    if (selectedUser.notes === 'Main account owner') {
      toast.error('Cannot edit the main account owner');
      return;
    }

    // Validate required fields
    if (!editFormData.firstName || !editFormData.lastName || !editFormData.role) {
      toast.error('Please fill in all required fields');
      return;
    }

    try {
      await businessPartnerUserService.updateUser(selectedUser.id, editFormData);
      toast.success('User updated successfully');
      setIsEditDialogOpen(false);
      setSelectedUser(null);
      loadUsers(businessPartnerId);
    } catch (error: any) {
      console.error('Error updating user:', error);
      toast.error(error.message || 'Failed to update user');
    }
  };

  const handleToggleActive = async (user: BusinessPartnerUserDto) => {
    // Prevent deactivating main account owner
    if (user.notes === 'Main account owner') {
      toast.error('Cannot deactivate the main account owner');
      return;
    }

    try {
      if (user.isActive) {
        await businessPartnerUserService.deactivateUser(user.id);
        toast.success('User deactivated successfully');
      } else {
        await businessPartnerUserService.activateUser(user.id);
        toast.success('User activated successfully');
      }
      loadUsers(businessPartnerId);
    } catch (error: any) {
      console.error('Error toggling user status:', error);
      toast.error(error.message || 'Failed to update user status');
    }
  };

  const handleDeleteUser = async () => {
    if (!selectedUser) return;

    // Prevent deleting main account owner
    if (selectedUser.notes === 'Main account owner') {
      toast.error('Cannot delete the main account owner');
      return;
    }
    
    try {
      await businessPartnerUserService.deleteUser(selectedUser.id);
      toast.success('User deleted successfully');
      setIsDeleteDialogOpen(false);
      setSelectedUser(null);
      loadUsers(businessPartnerId);
    } catch (error: any) {
      console.error('Error deleting user:', error);
      toast.error(error.message || 'Failed to delete user');
    }
  };

  const openEditDialog = (user: BusinessPartnerUserDto) => {
    // Prevent editing main account owner
    if (user.notes === 'Main account owner') {
      toast.error('Cannot edit the main account owner');
      return;
    }

    setSelectedUser(user);
    setEditFormData({
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      phoneNumber: user.phoneNumber || '',
      role: user.role,
      isActive: user.isActive,
      notes: user.notes || '',
    });
    setIsEditDialogOpen(true);
  };

  const openDeleteDialog = (user: BusinessPartnerUserDto) => {
    // Prevent deleting main account owner
    if (user.notes === 'Main account owner') {
      toast.error('Cannot delete the main account owner');
      return;
    }

    setSelectedUser(user);
    setIsDeleteDialogOpen(true);
  };

  const openResetPasswordDialog = (user: BusinessPartnerUserDto) => {
    // Prevent resetting password for main account owner
    if (user.notes === 'Main account owner') {
      toast.error('Cannot reset password for the main account owner');
      return;
    }

    setSelectedUser(user);
    setNewPassword('');
    setConfirmPassword('');
    setIsResetPasswordDialogOpen(true);
  };

  const handleResetPassword = async () => {
    if (!selectedUser) return;

    // Prevent resetting password for main account owner
    if (selectedUser.notes === 'Main account owner') {
      toast.error('Cannot reset password for the main account owner');
      return;
    }

    // Validate passwords
    if (!newPassword || !confirmPassword) {
      toast.error('Please enter and confirm the new password');
      return;
    }

    if (newPassword !== confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }

    if (newPassword.length < 6) {
      toast.error('Password must be at least 6 characters long');
      return;
    }

    try {
      await businessPartnerUserService.resetPassword(selectedUser.id, newPassword);
      toast.success('Password reset successfully');
      setIsResetPasswordDialogOpen(false);
      setSelectedUser(null);
      setNewPassword('');
      setConfirmPassword('');
    } catch (error: any) {
      console.error('Error resetting password:', error);
      toast.error(error.message || 'Failed to reset password');
    }
  };

  const resetFormData = () => {
    setFormData({
      businessPartnerId: '',
      email: '',
      firstName: '',
      lastName: '',
      userName: '',
      password: '',
      phoneNumber: '',
      role: 'User',
      notes: '',
    });
  };

  const filteredUsers = users.filter(user => {
    if (!searchTerm) return true;
    const lowerSearch = searchTerm.toLowerCase();
    return (
      user.userFullName?.toLowerCase().includes(lowerSearch) ||
      user.userEmail?.toLowerCase().includes(lowerSearch) ||
      user.userName?.toLowerCase().includes(lowerSearch) ||
      user.role.toLowerCase().includes(lowerSearch)
    );
  });

  const getRoleBadge = (role: string) => {
    const roleConfig: Record<string, { className: string }> = {
      'Admin': { className: 'bg-purple-100 text-purple-800' },
      'User': { className: 'bg-blue-100 text-blue-800' },
      'Viewer': { className: 'bg-gray-100 text-gray-800' },
    };

    const config = roleConfig[role] || { className: 'bg-gray-100 text-gray-800' };

    return (
      <Badge className={config.className}>
        {role}
      </Badge>
    );
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-muted-foreground">Loading users...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-3xl font-bold">User Management</h1>
          <p className="text-muted-foreground mt-1">
            Manage users for your organization
          </p>
        </div>
        <Button onClick={() => setIsCreateDialogOpen(true)}>
          <UserPlus className="h-4 w-4 mr-2" />
          Add User
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Users</CardTitle>
          <CardDescription>
            View and manage all users in your organization
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="mb-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search by name, email, username, or role..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-10"
              />
            </div>
          </div>

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Email</TableHead>
                  <TableHead>Username</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Granted Date</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredUsers.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7} className="text-center py-8 text-muted-foreground">
                      No users found
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredUsers.map((user) => (
                    <TableRow key={user.id}>
                      <TableCell className="font-medium">{user.userFullName || 'N/A'}</TableCell>
                      <TableCell>{user.userEmail || 'N/A'}</TableCell>
                      <TableCell>{user.userName || 'N/A'}</TableCell>
                      <TableCell>{getRoleBadge(user.role)}</TableCell>
                      <TableCell>
                        {user.isActive ? (
                          <Badge className="bg-green-100 text-green-800">
                            <CheckCircle className="h-3 w-3 mr-1" />
                            Active
                          </Badge>
                        ) : (
                          <Badge className="bg-red-100 text-red-800">
                            <XCircle className="h-3 w-3 mr-1" />
                            Inactive
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>{format(new Date(user.grantedAt), 'MMM dd, yyyy')}</TableCell>
                      <TableCell className="text-right">
                        {user.notes === 'Main account owner' ? (
                          <Badge variant="outline" className="text-xs">
                            Main Account
                          </Badge>
                        ) : (
                          <div className="flex justify-end gap-2">
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => openEditDialog(user)}
                              title="Edit user"
                            >
                              <Edit className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => openResetPasswordDialog(user)}
                              title="Reset password"
                            >
                              <Key className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => handleToggleActive(user)}
                              title={user.isActive ? 'Deactivate user' : 'Activate user'}
                            >
                              {user.isActive ? (
                                <XCircle className="h-4 w-4" />
                              ) : (
                                <CheckCircle className="h-4 w-4" />
                              )}
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => openDeleteDialog(user)}
                              title="Delete user"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        )}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {/* Create User Dialog */}
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Add New User</DialogTitle>
            <DialogDescription>
              Create a new user account for your organization
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="firstName">First Name *</Label>
                <Input
                  id="firstName"
                  value={formData.firstName}
                  onChange={(e) => setFormData({ ...formData, firstName: e.target.value })}
                  placeholder="John"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="lastName">Last Name *</Label>
                <Input
                  id="lastName"
                  value={formData.lastName}
                  onChange={(e) => setFormData({ ...formData, lastName: e.target.value })}
                  placeholder="Doe"
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="email">Email *</Label>
              <Input
                id="email"
                type="email"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                placeholder="john.doe@example.com"
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="userName">Username *</Label>
                <Input
                  id="userName"
                  value={formData.userName}
                  onChange={(e) => setFormData({ ...formData, userName: e.target.value })}
                  placeholder="johndoe"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Password *</Label>
                <Input
                  id="password"
                  type="password"
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  placeholder="••••••••"
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="phoneNumber">Phone Number</Label>
                <Input
                  id="phoneNumber"
                  value={formData.phoneNumber}
                  onChange={(e) => setFormData({ ...formData, phoneNumber: e.target.value })}
                  placeholder="+1234567890"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="role">Role *</Label>
                <Select
                  value={formData.role}
                  onValueChange={(value) => setFormData({ ...formData, role: value })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Admin">Admin</SelectItem>
                    <SelectItem value="User">User</SelectItem>
                    <SelectItem value="Viewer">Viewer</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="notes">Notes</Label>
              <Input
                id="notes"
                value={formData.notes}
                onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                placeholder="Additional notes..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleCreateUser}>
              Create User
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Edit User Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit User</DialogTitle>
            <DialogDescription>
              Update user information
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="editFirstName">First Name *</Label>
                <Input
                  id="editFirstName"
                  value={editFormData.firstName}
                  onChange={(e) => setEditFormData({ ...editFormData, firstName: e.target.value })}
                  placeholder="First name"
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="editLastName">Last Name *</Label>
                <Input
                  id="editLastName"
                  value={editFormData.lastName}
                  onChange={(e) => setEditFormData({ ...editFormData, lastName: e.target.value })}
                  placeholder="Last name"
                  required
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="editPhoneNumber">Phone Number</Label>
              <Input
                id="editPhoneNumber"
                type="tel"
                value={editFormData.phoneNumber}
                onChange={(e) => setEditFormData({ ...editFormData, phoneNumber: e.target.value })}
                placeholder="+233 XX XXX XXXX"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="editRole">Role *</Label>
              <Select
                value={editFormData.role}
                onValueChange={(value) => setEditFormData({ ...editFormData, role: value })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Admin">Admin</SelectItem>
                  <SelectItem value="User">User</SelectItem>
                  <SelectItem value="Viewer">Viewer</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="editNotes">Notes</Label>
              <Input
                id="editNotes"
                value={editFormData.notes}
                onChange={(e) => setEditFormData({ ...editFormData, notes: e.target.value })}
                placeholder="Additional notes..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsEditDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleUpdateUser}>
              Update User
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete User Dialog */}
      <Dialog open={isDeleteDialogOpen} onOpenChange={setIsDeleteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete User</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete this user? This action cannot be undone.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsDeleteDialogOpen(false)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDeleteUser}>
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reset Password Dialog */}
      <Dialog open={isResetPasswordDialogOpen} onOpenChange={setIsResetPasswordDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reset Password</DialogTitle>
            <DialogDescription>
              Set a new password for {selectedUser?.userFullName || selectedUser?.userEmail}
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="newPassword">New Password *</Label>
              <Input
                id="newPassword"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="Enter new password"
                required
              />
              <p className="text-xs text-muted-foreground">
                Password must be at least 6 characters long
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="confirmPassword">Confirm Password *</Label>
              <Input
                id="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Confirm new password"
                required
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsResetPasswordDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleResetPassword}>
              Reset Password
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
