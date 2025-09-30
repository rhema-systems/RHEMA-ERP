"use client";

import React, { useState, useMemo } from 'react';
import { DataTable, DataTableColumn, DataTableAction } from '@/components/ui/DataTable';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarImage, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import {
  UserIcon,
  MailIcon,
  EditIcon,
  Trash2Icon,
  EyeIcon,
  UserPlusIcon,
  ShieldIcon,
  CheckCircleIcon,
  XCircleIcon,
} from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

// Sample user data type
export interface User {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  lastLogin?: string;
  createdAt: string;
  department?: string;
  phoneNumber?: string;
}

// Sample data
const sampleUsers: User[] = [
  {
    id: '1',
    userName: 'john.doe',
    email: 'john.doe@company.com',
    firstName: 'John',
    lastName: 'Doe',
    role: 'Manager',
    isActive: true,
    lastLogin: '2024-01-29T10:30:00Z',
    createdAt: '2024-01-15T09:00:00Z',
    department: 'Engineering',
    phoneNumber: '+1234567890',
  },
  {
    id: '2',
    userName: 'jane.smith',
    email: 'jane.smith@company.com',
    firstName: 'Jane',
    lastName: 'Smith',
    role: 'Employee',
    isActive: true,
    lastLogin: '2024-01-29T14:15:00Z',
    createdAt: '2024-01-10T11:30:00Z',
    department: 'Marketing',
    phoneNumber: '+1234567891',
  },
  {
    id: '3',
    userName: 'bob.wilson',
    email: 'bob.wilson@company.com',
    firstName: 'Bob',
    lastName: 'Wilson',
    role: 'TenantAdmin',
    isActive: false,
    lastLogin: '2024-01-25T16:45:00Z',
    createdAt: '2024-01-05T08:15:00Z',
    department: 'IT',
    phoneNumber: '+1234567892',
  },
  {
    id: '4',
    userName: 'alice.brown',
    email: 'alice.brown@company.com',
    firstName: 'Alice',
    lastName: 'Brown',
    role: 'Employee',
    isActive: true,
    createdAt: '2024-01-20T13:20:00Z',
    department: 'Sales',
    phoneNumber: '+1234567893',
  },
  {
    id: '5',
    userName: 'charlie.davis',
    email: 'charlie.davis@company.com',
    firstName: 'Charlie',
    lastName: 'Davis',
    role: 'SuperAdmin',
    isActive: true,
    lastLogin: '2024-01-29T09:00:00Z',
    createdAt: '2024-01-01T10:00:00Z',
    department: 'Executive',
    phoneNumber: '+1234567894',
  },
];

export function UserManagementTable() {
  const [users, setUsers] = useState<User[]>(sampleUsers);
  const [loading, setLoading] = useState(false);

  // Define table columns
  const columns = useMemo<DataTableColumn<User>[]>(() => [
    {
      accessorKey: 'userName',
      header: 'User',
      cell: ({ row }) => {
        const user = row.original;
        const initials = `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`;
        
        return (
          <div className="flex items-center space-x-3">
            <Avatar className="h-8 w-8">
              <AvatarImage src={`https://api.dicebear.com/7.x/initials/svg?seed=${user.userName}`} />
              <AvatarFallback className="text-xs">{initials}</AvatarFallback>
            </Avatar>
            <div className="flex flex-col">
              <span className="font-medium">{`${user.firstName} ${user.lastName}`}</span>
              <span className="text-xs text-muted-foreground">@{user.userName}</span>
            </div>
          </div>
        );
      },
      enableSorting: true,
      enableHiding: false,
    },
    {
      accessorKey: 'email',
      header: 'Email',
      cell: ({ row }) => (
        <div className="flex items-center space-x-2">
          <MailIcon className="h-3 w-3 text-muted-foreground" />
          <span>{row.getValue('email')}</span>
        </div>
      ),
      enableSorting: true,
    },
    {
      accessorKey: 'role',
      header: 'Role',
      cell: ({ row }) => {
        const role = row.getValue('role') as string;
        const roleColors = {
          'SuperAdmin': 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
          'TenantAdmin': 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200',
          'Manager': 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
          'Employee': 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
        };
        
        return (
          <Badge 
            variant="secondary" 
            className={roleColors[role as keyof typeof roleColors] || ''}
          >
            <ShieldIcon className="mr-1 h-3 w-3" />
            {role}
          </Badge>
        );
      },
      enableSorting: true,
      filterFn: 'equals',
    },
    {
      accessorKey: 'isActive',
      header: 'Status',
      cell: ({ row }) => {
        const isActive = row.getValue('isActive') as boolean;
        return (
          <Badge variant={isActive ? 'default' : 'secondary'}>
            {isActive ? (
              <><CheckCircleIcon className="mr-1 h-3 w-3" />Active</>
            ) : (
              <><XCircleIcon className="mr-1 h-3 w-3" />Inactive</>
            )}
          </Badge>
        );
      },
      enableSorting: true,
      filterFn: 'equals',
    },
    {
      accessorKey: 'department',
      header: 'Department',
      enableSorting: true,
      filterFn: 'includesString',
    },
    {
      accessorKey: 'lastLogin',
      header: 'Last Login',
      cell: ({ row }) => {
        const lastLogin = row.getValue('lastLogin') as string;
        if (!lastLogin) return <span className="text-muted-foreground">Never</span>;
        
        return (
          <span className="text-sm">
            {formatDistanceToNow(new Date(lastLogin), { addSuffix: true })}
          </span>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'createdAt',
      header: 'Created',
      cell: ({ row }) => {
        const createdAt = new Date(row.getValue('createdAt'));
        return (
          <span className="text-sm">
            {createdAt.toLocaleDateString()}
          </span>
        );
      },
      enableSorting: true,
    },
  ], []);

  // Define row actions
  const rowActions = useMemo<DataTableAction<User>[]>(() => [
    {
      id: 'view',
      label: 'View Details',
      icon: EyeIcon,
      onClick: (row) => {
        console.log('View user:', row.original);
        // Handle view action
      },
      variant: 'ghost',
    },
    {
      id: 'edit',
      label: 'Edit User',
      icon: EditIcon,
      onClick: (row) => {
        console.log('Edit user:', row.original);
        // Handle edit action
      },
      variant: 'ghost',
    },
    {
      id: 'delete',
      label: 'Delete User',
      icon: Trash2Icon,
      onClick: (row) => {
        console.log('Delete user:', row.original);
        // Handle delete action
      },
      variant: 'ghost',
      disabled: (row) => row.original.role === 'SuperAdmin',
    },
  ], []);

  // Handle toolbar actions
  const handleCreateUser = () => {
    console.log('Create new user');
    // Handle create action
  };

  const handleRefresh = () => {
    setLoading(true);
    // Simulate API call
    setTimeout(() => {
      setLoading(false);
      console.log('Refreshed user list');
    }, 1000);
  };

  const handleDeleteSelected = (selectedUsers: User[]) => {
    console.log('Delete selected users:', selectedUsers);
    // Handle bulk delete
    setUsers(prev => prev.filter(user => !selectedUsers.some(selected => selected.id === user.id)));
  };

  const handleRowClick = (row: any) => {
    console.log('Row clicked:', row.original);
  };

  const handleRowSelectionChange = (selectedUsers: User[]) => {
    console.log('Selection changed:', selectedUsers);
  };

  return (
    <DataTable
      data={users}
      columns={columns}
      title="User Management"
      description="Manage users, roles, and permissions in your organization"
      loading={loading}
      
      // Row selection
      enableRowSelection={true}
      onRowSelectionChange={handleRowSelectionChange}
      
      // Pagination
      enablePagination={true}
      pageSize={10}
      pageSizeOptions={[5, 10, 25, 50, 100]}
      
      // Sorting and filtering
      enableSorting={true}
      enableGlobalFilter={true}
      enableColumnFilters={true}
      searchPlaceholder="Search users..."
      
      // Export
      enableExport={true}
      exportFileName="users"
      exportFormats={['csv', 'excel', 'json']}
      
      // Actions
      rowActions={rowActions}
      toolbarActions={{
        create: handleCreateUser,
        refresh: handleRefresh,
        delete: handleDeleteSelected,
        customActions: [
          {
            id: 'invite',
            label: 'Invite Users',
            icon: UserPlusIcon,
            onClick: (selectedUsers) => {
              console.log('Invite users:', selectedUsers);
            },
            variant: 'outline',
            requiresSelection: false,
          },
        ],
      }}
      
      // Event handlers
      onRowClick={handleRowClick}
      
      // Styling
      striped={true}
      hoverable={true}
      showBorder={true}
      emptyStateMessage="No users found. Create your first user to get started."
      
      // Advanced features
      enableColumnVisibility={true}
      className="w-full"
    />
  );
}

export default UserManagementTable;