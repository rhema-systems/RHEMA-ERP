import React, { useState, useEffect } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { UserPlus, UserMinus, Link, Unlink } from 'lucide-react';
import { useToast } from '../../hooks/use-toast';
import { apiService } from '../../services/api.service';

// Define Employee interface locally for all employees (not just maintenance technicians)
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


interface LinkRequest {
  userId: string;
  employeeId: string;
}

const UserEmployeeLinks: React.FC = () => {
  const [links, setLinks] = useState<UserEmployeeLink[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedUser, setSelectedUser] = useState<UserEmployeeLink | null>(null);
  const [selectedEmployeeId, setSelectedEmployeeId] = useState<string>('');
  const { toast } = useToast();

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [linksData, employeesData] = await Promise.all([
        apiService.get<UserEmployeeLink[]>('/useremployeelink/user-employee-links'),
        apiService.get<Employee[]>('/employees?pageSize=1000') // Fetch all employees, not just maintenance technicians
      ]);

      setLinks(linksData);
      // Filter to only active employees
      setEmployees(employeesData.filter(e => e.isActive));
    } catch (error) {
      console.error('Error loading data:', error);
      toast({
        title: 'Error',
        description: 'Error loading data',
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  const linkUserToEmployee = async (userId: string, employeeId: string) => {
    try {
      const result = await apiService.post<{success: boolean; message: string}>(
        '/useremployeelink/link-user-to-employee', 
        { userId, employeeId } as LinkRequest
      );

      toast({
        title: 'Success',
        description: result.message,
        variant: 'success',
      });
      setIsDialogOpen(false);
      setSelectedUser(null);
      setSelectedEmployeeId('');
      loadData(); // Reload data
    } catch (error) {
      console.error('Error linking user:', error);
      toast({
        title: 'Error',
        description: 'Error linking user to employee',
        variant: 'destructive',
      });
    }
  };

  const unlinkUserFromEmployee = async (userId: string) => {
    try {
      const result = await apiService.post<{success: boolean; message: string}>(
        '/useremployeelink/unlink-user-from-employee', 
        { userId }
      );

      toast({
        title: 'Success',
        description: result.message,
        variant: 'success',
      });
      loadData(); // Reload data
    } catch (error) {
      console.error('Error unlinking user:', error);
      toast({
        title: 'Error',
        description: 'Error unlinking user from employee',
        variant: 'destructive',
      });
    }
  };

  const openLinkDialog = (user: UserEmployeeLink) => {
    setSelectedUser(user);
    setSelectedEmployeeId(user.employeeId || '');
    setIsDialogOpen(true);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedUser && selectedEmployeeId) {
      linkUserToEmployee(selectedUser.userId, selectedEmployeeId);
    }
  };

  if (loading) {
    return (
      <Card className="w-full">
        <CardHeader>
          <CardTitle>User-Employee Links</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-center py-4">Loading...</div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Link className="h-5 w-5" />
          User-Employee Links
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          Manage links between user accounts and employee records
        </p>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {links.map((link) => (
            <div
              key={link.userId}
              className="flex items-center justify-between p-4 border rounded-lg"
            >
              <div className="flex-1">
                <div className="flex items-center gap-3">
                  <div>
                    <h4 className="font-medium">{link.fullName}</h4>
                    <p className="text-sm text-muted-foreground">@{link.userName}</p>
                    <p className="text-sm text-muted-foreground">{link.email}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    {link.isLinked ? (
                      <Badge variant="secondary" className="flex items-center gap-1">
                        <Link className="h-3 w-3" />
                        Linked
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="flex items-center gap-1">
                        <Unlink className="h-3 w-3" />
                        Not Linked
                      </Badge>
                    )}
                  </div>
                </div>
                {link.isLinked && (
                  <div className="mt-2 p-2 bg-muted rounded">
                    <p className="text-sm font-medium">
                      Employee: {link.employeeName} ({link.employeeNumber})
                    </p>
                  </div>
                )}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => openLinkDialog(link)}
                  className="flex items-center gap-1"
                >
                  <UserPlus className="h-4 w-4" />
                  {link.isLinked ? 'Change Link' : 'Link'}
                </Button>
                {link.isLinked && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => unlinkUserFromEmployee(link.userId)}
                    className="flex items-center gap-1"
                  >
                    <UserMinus className="h-4 w-4" />
                    Unlink
                  </Button>
                )}
              </div>
            </div>
          ))}
        </div>

        <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Link User to Employee</DialogTitle>
              <DialogDescription>
                {selectedUser && (
                  <>
                    Link user "{selectedUser.fullName}" to an employee record.
                    {selectedUser.isLinked && (
                      <span className="block mt-1 text-orange-600">
                        This will replace the existing link.
                      </span>
                    )}
                  </>
                )}
              </DialogDescription>
            </DialogHeader>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <Label htmlFor="employee">Select Employee</Label>
                <Select value={selectedEmployeeId} onValueChange={setSelectedEmployeeId}>
                  <SelectTrigger>
                    <SelectValue placeholder="Choose an employee..." />
                  </SelectTrigger>
                  <SelectContent>
                    {employees.map((employee) => (
                      <SelectItem key={employee.id} value={employee.id}>
                        {employee.fullName || `${employee.firstName} ${employee.lastName}`}
                        {employee.employeeNumber && ` - ${employee.employeeNumber}`}
                        {employee.positionTitle && ` (${employee.positionTitle})`}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="flex justify-end gap-3">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setIsDialogOpen(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={!selectedEmployeeId}>
                  Link User
                </Button>
              </div>
            </form>
          </DialogContent>
        </Dialog>
      </CardContent>
    </Card>
  );
};

export default UserEmployeeLinks;