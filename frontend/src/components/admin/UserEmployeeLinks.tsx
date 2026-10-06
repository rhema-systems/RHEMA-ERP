'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { AlertCircle, Link2, RefreshCw, Unlink, UserCheck, UserMinus, UserPlus, Users } from 'lucide-react';

import { DataTable, type Column } from '@/components/admin/data-table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { apiService } from '@/services/api.service';
import { getAdminProblemMessage } from '@/services/admin-api.service';

interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  fullName?: string;
  emailAddress?: string;
  employeeNumber?: string;
  departmentName?: string;
  positionTitle?: string;
  isActive: boolean;
}
interface UserEmployeeLink {
  userId: string;
  userName: string;
  fullName: string;
  email: string;
  employeeId?: string;
  employeeName?: string;
  employeeNumber?: string;
  isLinked: boolean;
}

interface LinkResult {
  success: boolean;
  message: string;
}

const employeeLabel = (employee: Employee) =>
  employee.fullName?.trim() || [employee.firstName, employee.lastName].filter(Boolean).join(' ') || employee.employeeNumber || 'Unnamed employee';

export default function UserEmployeeLinks() {
  const { hasPermission } = useAuth();
  const canManage = hasPermission('users.update');
  const { toast } = useToast();
  const [links, setLinks] = useState<UserEmployeeLink[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'linked' | 'unlinked'>('all');
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<UserEmployeeLink | null>(null);
  const [selectedEmployeeId, setSelectedEmployeeId] = useState('');
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [employeesLoading, setEmployeesLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [unlinkingUser, setUnlinkingUser] = useState<UserEmployeeLink | null>(null);

  const loadLinks = async () => {
    try {
      setLoading(true);
      setLoadError('');
      const data = await apiService.get<UserEmployeeLink[]>('/useremployeelink/user-employee-links');
      setLinks(data);
    } catch (error) {
      const message = getAdminProblemMessage(error, 'User and employee links could not be loaded.');
      setLoadError(message);
      toast({ title: 'Unable to load links', description: message, variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadLinks();
  }, []);

  const loadEmployees = async () => {
    if (employees.length > 0) return;
    try {
      setEmployeesLoading(true);
      const data = await apiService.get<Employee[]>('/employees?pageSize=1000');
      setEmployees(data.filter(employee => employee.isActive));
    } catch (error) {
      toast({
        title: 'Unable to load employees',
        description: getAdminProblemMessage(error, 'Active employees could not be loaded.'),
        variant: 'destructive',
      });
    } finally {
      setEmployeesLoading(false);
    }
  };

  const openLinkDialog = (user: UserEmployeeLink) => {
    setSelectedUser(user);
    setSelectedEmployeeId(user.employeeId ?? '');
    setEmployeeSearch('');
    setIsDialogOpen(true);
    void loadEmployees();
  };

  const handleDialogOpenChange = (open: boolean) => {
    setIsDialogOpen(open);
    if (!open) {
      setSelectedUser(null);
      setSelectedEmployeeId('');
      setEmployeeSearch('');
    }
  };

  const saveLink = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedUser || !selectedEmployeeId) return;

    try {
      setSaving(true);
      const result = await apiService.post<LinkResult>('/useremployeelink/link-user-to-employee', {
        userId: selectedUser.userId,
        employeeId: selectedEmployeeId,
      });
      toast({ title: 'Link saved', description: result.message, variant: 'success' });
      handleDialogOpenChange(false);
      await loadLinks();
    } catch (error) {
      toast({
        title: 'Link not saved',
        description: getAdminProblemMessage(error, 'The user could not be linked to that employee.'),
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const unlinkUser = async () => {
    if (!unlinkingUser) return false;
    try {
      setSaving(true);
      const result = await apiService.post<LinkResult>('/useremployeelink/unlink-user-from-employee', {
        userId: unlinkingUser.userId,
      });
      toast({ title: 'Link removed', description: result.message, variant: 'success' });
      setUnlinkingUser(null);
      await loadLinks();
      return true;
    } catch (error) {
      toast({
        title: 'Link not removed',
        description: getAdminProblemMessage(error, 'The employee link could not be removed.'),
        variant: 'destructive',
      });
      return false;
    } finally {
      setSaving(false);
    }
  };

  const filteredLinks = useMemo(
    () => links.filter(link =>
      statusFilter === 'all' ||
      (statusFilter === 'linked' && link.isLinked) ||
      (statusFilter === 'unlinked' && !link.isLinked)),
    [links, statusFilter]
  );

  const availableEmployees = useMemo(() => {
    const query = employeeSearch.trim().toLowerCase();
    return employees
      .filter(employee => !query || [
        employeeLabel(employee),
        employee.employeeNumber,
        employee.emailAddress,
        employee.departmentName,
        employee.positionTitle,
      ].some(value => value?.toLowerCase().includes(query)))
      .slice(0, 100);
  }, [employees, employeeSearch]);

  const columns: Column<UserEmployeeLink>[] = [
    {
      key: 'fullName',
      label: 'User',
      sortable: true,
      render: (_, link) => (
        <div>
          <p className="font-medium">{link.fullName || link.userName}</p>
          <p className="text-xs text-muted-foreground">@{link.userName}</p>
        </div>
      ),
    },
    { key: 'email', label: 'Email', sortable: true },
    {
      key: 'isLinked',
      label: 'Status',
      sortable: true,
      render: (value: boolean) => (
        <Badge variant={value ? 'default' : 'outline'}>{value ? 'Linked' : 'Unlinked'}</Badge>
      ),
    },
    {
      key: 'employeeName',
      label: 'Employee record',
      sortable: true,
      render: (_, link) => link.isLinked ? (
        <div>
          <p className="font-medium">{link.employeeName}</p>
          <p className="text-xs text-muted-foreground">{link.employeeNumber || 'No employee number'}</p>
        </div>
      ) : <span className="text-muted-foreground">No employee assigned</span>,
    },
  ];

  const linkedCount = links.filter(link => link.isLinked).length;

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-3">
        {[
          { label: 'Tenant users', value: links.length, icon: Users, tone: 'bg-blue-50 text-blue-600' },
          { label: 'Linked', value: linkedCount, icon: UserCheck, tone: 'bg-emerald-50 text-emerald-600' },
          { label: 'Awaiting link', value: links.length - linkedCount, icon: Unlink, tone: 'bg-amber-50 text-amber-600' },
        ].map(({ label, value, icon: Icon, tone }) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-4">
              <div><p className="text-sm text-muted-foreground">{label}</p><p className="mt-1 text-2xl font-semibold">{value}</p></div>
              <span className={`rounded-lg p-2.5 ${tone}`}><Icon className="h-5 w-5" /></span>
            </CardContent>
          </Card>
        ))}
      </div>

      {loadError && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm">
          <span className="flex items-center gap-2 text-destructive">
            <AlertCircle className="h-4 w-4" /> {loadError}
          </span>
          <Button type="button" variant="outline" size="sm" onClick={() => void loadLinks()}>
            <RefreshCw className="mr-2 h-4 w-4" /> Retry
          </Button>
        </div>
      )}

      <div className="flex flex-wrap items-end justify-between gap-3 rounded-lg border bg-muted/20 p-4">
        <div className="space-y-2">
          <Label htmlFor="link-status">Link status</Label>
          <Select value={statusFilter} onValueChange={(value: 'all' | 'linked' | 'unlinked') => setStatusFilter(value)}>
            <SelectTrigger id="link-status" className="w-48"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All users</SelectItem>
              <SelectItem value="linked">Linked users</SelectItem>
              <SelectItem value="unlinked">Awaiting link</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <p className="text-sm text-muted-foreground">Showing {filteredLinks.length} of {links.length} tenant users</p>
      </div>

      <DataTable
        title="User and employee directory"
        description="Search user accounts and maintain their employee record connections."
        data={filteredLinks}
        columns={columns}
        loading={loading}
        searchPlaceholder="Search user, email, employee or staff number..."
        exportFileName="user_employee_links.csv"
        actions={canManage}
        pageSize={12}
        getRowId={link => link.userId}
        emptyMessage="No user and employee links match the current filters."
        customActions={link => (
          <div className="flex items-center justify-end gap-1">
            <Button type="button" variant="ghost" size="sm" onClick={() => openLinkDialog(link)}>
              <UserPlus className="mr-1 h-4 w-4" /> {link.isLinked ? 'Change' : 'Link'}
            </Button>
            {link.isLinked && (
              <Button type="button" variant="ghost" size="sm" className="text-destructive hover:text-destructive" onClick={() => setUnlinkingUser(link)}>
                <UserMinus className="mr-1 h-4 w-4" /> Unlink
              </Button>
            )}
          </div>
        )}
      />

      <Dialog open={isDialogOpen} onOpenChange={handleDialogOpenChange}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2"><Link2 className="h-5 w-5" /> Link user to employee</DialogTitle>
            <DialogDescription>
              Connect {selectedUser?.fullName || selectedUser?.userName} to one active employee record.
              {selectedUser?.isLinked ? ' Saving will replace the current connection.' : ''}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={saveLink} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="employee-search">Find employee</Label>
              <Input
                id="employee-search"
                value={employeeSearch}
                onChange={event => setEmployeeSearch(event.target.value)}
                placeholder="Search name, employee number, email or position..."
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="employee-select">Employee record</Label>
              <Select value={selectedEmployeeId} onValueChange={setSelectedEmployeeId} disabled={employeesLoading}>
                <SelectTrigger id="employee-select">
                  <SelectValue placeholder={employeesLoading ? 'Loading employees...' : 'Choose an employee'} />
                </SelectTrigger>
                <SelectContent>
                  {availableEmployees.map(employee => (
                    <SelectItem key={employee.id} value={employee.id}>
                      {employeeLabel(employee)}
                      {employee.employeeNumber ? ` · ${employee.employeeNumber}` : ''}
                      {employee.positionTitle ? ` · ${employee.positionTitle}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {!employeesLoading && employees.length > 100 && (
                <p className="text-xs text-muted-foreground">Type a search to narrow the active employee directory.</p>
              )}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleDialogOpenChange(false)}>Cancel</Button>
              <Button type="submit" disabled={!selectedEmployeeId || saving || employeesLoading}>
                {saving ? 'Saving...' : 'Save link'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={Boolean(unlinkingUser)}
        onOpenChange={open => { if (!open) setUnlinkingUser(null); }}
        title="Remove employee link?"
        description={unlinkingUser
          ? `Remove the link between ${unlinkingUser.fullName || unlinkingUser.userName} and ${unlinkingUser.employeeName || 'the employee record'}?`
          : ''}
        confirmText="Remove link"
        variant="destructive"
        isLoading={saving}
        onConfirm={unlinkUser}
      />
    </div>
  );
}
