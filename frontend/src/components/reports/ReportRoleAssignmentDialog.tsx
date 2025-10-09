'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '../ui/card';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import { Checkbox } from '../ui/checkbox';
import { Label } from '../ui/label';
import { Separator } from '../ui/separator';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '../ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table';
import { ConfirmationDialog } from '../ui/confirmation-dialog';
import { useToast } from '../../hooks/use-toast';
import { reportsService, ReportRoleAssignment, CreateReportRoleAssignmentDto, UpdateReportRoleAssignmentDto } from '../../services/reports';
import { adminApiService, Role } from '../../services/admin-api.service';
import {
  UserCheck,
  Shield,
  Eye,
  Play,
  Download,
  Edit,
  Clock,
  Trash2,
  Plus,
  Loader2,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Users,
  Settings
} from 'lucide-react';

interface ReportRoleAssignmentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  reportId: string | null;
  reportName?: string;
}

interface PermissionConfig {
  key: keyof Pick<CreateReportRoleAssignmentDto, 'canRead' | 'canExecute' | 'canExport' | 'canEdit' | 'canSchedule'>;
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
}

const permissionConfigs: PermissionConfig[] = [
  {
    key: 'canRead',
    label: 'View',
    description: 'Can view report results and metadata',
    icon: Eye,
    color: 'text-blue-600'
  },
  {
    key: 'canExecute',
    label: 'Execute',
    description: 'Can run the report and generate results',
    icon: Play,
    color: 'text-green-600'
  },
  {
    key: 'canExport',
    label: 'Export',
    description: 'Can export report data to various formats',
    icon: Download,
    color: 'text-purple-600'
  },
  {
    key: 'canEdit',
    label: 'Edit',
    description: 'Can modify report configuration and settings',
    icon: Edit,
    color: 'text-orange-600'
  },
  {
    key: 'canSchedule',
    label: 'Schedule',
    description: 'Can create and manage scheduled executions',
    icon: Clock,
    color: 'text-indigo-600'
  }
];

export default function ReportRoleAssignmentDialog({
  open,
  onOpenChange,
  reportId,
  reportName
}: ReportRoleAssignmentDialogProps) {
  const [activeTab, setActiveTab] = useState('assign');
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [permissions, setPermissions] = useState<CreateReportRoleAssignmentDto>({
    reportId: '',
    roleId: '',
    canRead: true,
    canExecute: false,
    canExport: false,
    canEdit: false,
    canSchedule: false
  });
  const [editingAssignment, setEditingAssignment] = useState<ReportRoleAssignment | null>(null);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [assignmentToDelete, setAssignmentToDelete] = useState<string | null>(null);

  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch available roles
  const { data: roles = [], isLoading: rolesLoading } = useQuery({
    queryKey: ['roles'],
    queryFn: () => adminApiService.getRoles(),
    enabled: open
  });

  // Fetch existing role assignments for the report
  const {
    data: assignments = [],
    isLoading: assignmentsLoading,
    refetch: refetchAssignments
  } = useQuery({
    queryKey: ['report-role-assignments', reportId],
    queryFn: () => {
      console.log('🔍 Fetching role assignments for reportId:', reportId);
      return reportId ? reportsService.getReportRoleAssignments(reportId) : Promise.resolve([]);
    },
    enabled: open && !!reportId,
    onSuccess: (data) => {
      console.log('✅ Role assignments fetched successfully:', data);
      console.log('📊 Assignment count:', data?.length || 0);
      if (data?.length > 0) {
        console.log('📋 First assignment details:', data[0]);
      }
    },
    onError: (error) => {
      console.error('❌ Error fetching role assignments:', error);
    }
  });

  // Reset form when dialog opens/closes or report changes
  useEffect(() => {
    if (open && reportId) {
      setPermissions({
        reportId,
        roleId: '',
        canRead: true,
        canExecute: false,
        canExport: false,
        canEdit: false,
        canSchedule: false
      });
      setSelectedRoleId(null);
      setEditingAssignment(null);
    }
  }, [open, reportId]);

  // Create role assignment mutation
  const createAssignmentMutation = useMutation({
    mutationFn: reportsService.createRoleAssignment,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['report-role-assignments'] });
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      refetchAssignments();
      toast({
        title: 'Role Assignment Created',
        description: 'Role has been successfully assigned to the report.',
      });
      // Reset form
      setSelectedRoleId(null);
      setPermissions(prev => ({
        ...prev,
        roleId: '',
        canRead: true,
        canExecute: false,
        canExport: false,
        canEdit: false,
        canSchedule: false
      }));
    },
    onError: (error: any) => {
      toast({
        title: 'Assignment Failed',
        description: error.response?.data?.message || 'Failed to assign role to report',
        variant: 'destructive',
      });
    }
  });

  // Update role assignment mutation
  const updateAssignmentMutation = useMutation({
    mutationFn: ({ assignmentId, updateDto }: { assignmentId: string; updateDto: UpdateReportRoleAssignmentDto }) =>
      reportsService.updateRoleAssignment(assignmentId, updateDto),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['report-role-assignments'] });
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      refetchAssignments();
      setEditingAssignment(null);
      toast({
        title: 'Assignment Updated',
        description: 'Role assignment has been successfully updated.',
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Update Failed',
        description: error.response?.data?.message || 'Failed to update role assignment',
        variant: 'destructive',
      });
    }
  });

  // Delete role assignment mutation
  const deleteAssignmentMutation = useMutation({
    mutationFn: reportsService.deleteRoleAssignment,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['report-role-assignments'] });
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      refetchAssignments();
      setShowDeleteDialog(false);
      setAssignmentToDelete(null);
      toast({
        title: 'Assignment Removed',
        description: 'Role assignment has been successfully removed.',
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Delete Failed',
        description: error.response?.data?.message || 'Failed to remove role assignment',
        variant: 'destructive',
      });
    }
  });

  const handleRoleSelect = (role: Role) => {
    setSelectedRoleId(role.id);
    setPermissions(prev => ({ ...prev, roleId: role.id }));
  };

  const handlePermissionChange = (permission: string, checked: boolean) => {
    setPermissions(prev => ({
      ...prev,
      [permission]: checked
    }));
  };

  const handleCreateAssignment = () => {
    if (!selectedRoleId || !reportId) return;

    createAssignmentMutation.mutate(permissions);
  };

  const handleEditAssignment = (assignment: ReportRoleAssignment) => {
    setEditingAssignment(assignment);
    setActiveTab('assign');
    setSelectedRoleId(assignment.roleId);
    setPermissions({
      reportId: assignment.reportId,
      roleId: assignment.roleId,
      canRead: assignment.canRead,
      canExecute: assignment.canExecute,
      canExport: assignment.canExport,
      canEdit: assignment.canEdit,
      canSchedule: assignment.canSchedule
    });
  };

  const handleUpdateAssignment = () => {
    if (!editingAssignment) return;

    const updateDto: UpdateReportRoleAssignmentDto = {
      canRead: permissions.canRead,
      canExecute: permissions.canExecute,
      canExport: permissions.canExport,
      canEdit: permissions.canEdit,
      canSchedule: permissions.canSchedule
    };

    updateAssignmentMutation.mutate({
      assignmentId: editingAssignment.id,
      updateDto
    });
  };

  const handleDeleteAssignment = (assignmentId: string) => {
    setAssignmentToDelete(assignmentId);
    setShowDeleteDialog(true);
  };

  const confirmDelete = () => {
    if (assignmentToDelete) {
      deleteAssignmentMutation.mutate(assignmentToDelete);
    }
  };

  const getPermissionBadge = (assignment: ReportRoleAssignment, permissionKey: string, key?: string) => {
    const hasPermission = assignment[permissionKey as keyof ReportRoleAssignment] as boolean;
    const config = permissionConfigs.find(p => p.key === permissionKey);
    
    if (!config) return null;

    const Icon = config.icon;
    return (
      <Badge
        key={key || permissionKey}
        variant={hasPermission ? "default" : "secondary"}
        className={`text-xs ${hasPermission ? '' : 'opacity-50'}`}
      >
        <Icon className="h-3 w-3 mr-1" />
        {config.label}
      </Badge>
    );
  };

  const availableRoles = roles.filter(role => 
    !assignments.some(assignment => assignment.roleId === role.id) || 
    (editingAssignment && role.id === editingAssignment.roleId)
  );

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-4xl max-h-[90vh] flex flex-col">
          <DialogHeader className="flex-shrink-0">
            <DialogTitle className="flex items-center space-x-2">
              <UserCheck className="h-5 w-5" />
              <span>Manage Role Assignments</span>
            </DialogTitle>
            <DialogDescription>
              {reportName ? `Configure role access and permissions for "${reportName}"` : 'Configure role access and permissions for this report'}
            </DialogDescription>
          </DialogHeader>

          <div className="flex-1 overflow-hidden flex flex-col">
            <Tabs value={activeTab} onValueChange={setActiveTab} className="flex-1 flex flex-col">
              <TabsList className="grid w-full grid-cols-2 flex-shrink-0">
                <TabsTrigger value="assign">Assign Roles</TabsTrigger>
                <TabsTrigger value="manage">Manage Assignments</TabsTrigger>
              </TabsList>

              <div className="flex-1 overflow-hidden flex flex-col">
                <div className="flex-1 overflow-y-auto px-1">
                  <TabsContent value="assign" className="space-y-6 mt-4 h-full">
                  <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    {/* Role Selection */}
                    <Card>
                      <CardHeader>
                        <CardTitle className="text-lg flex items-center space-x-2">
                          <Users className="h-5 w-5" />
                          <span>Select Role</span>
                        </CardTitle>
                        <CardDescription>
                          Choose a role to assign to this report
                        </CardDescription>
                      </CardHeader>
                      <CardContent>
                        {rolesLoading ? (
                          <div className="space-y-3">
                            {[...Array(3)].map((_, i) => (
                              <div key={i} className="h-16 bg-muted rounded-lg animate-pulse" />
                            ))}
                          </div>
                        ) : (
                          <div className="space-y-3">
                            {availableRoles.map((role) => (
                              <Card
                                key={role.id}
                                className={`cursor-pointer transition-colors hover:bg-muted/50 ${
                                  selectedRoleId === role.id ? 'ring-2 ring-primary bg-muted/50' : ''
                                }`}
                                onClick={() => handleRoleSelect(role)}
                              >
                                <CardHeader className="pb-2">
                                  <div className="flex items-center justify-between">
                                    <div className="flex items-center space-x-3">
                                      <div className="p-2 rounded-lg bg-blue-100 text-blue-700">
                                        <Shield className="h-4 w-4" />
                                      </div>
                                      <div>
                                        <CardTitle className="text-base">{role.name}</CardTitle>
                                        <CardDescription className="text-sm">
                                          {role.description}
                                        </CardDescription>
                                      </div>
                                    </div>
                                    {selectedRoleId === role.id && (
                                      <CheckCircle className="h-5 w-5 text-primary" />
                                    )}
                                  </div>
                                </CardHeader>
                              </Card>
                            ))}
                            {availableRoles.length === 0 && (
                              <div className="text-center py-8">
                                <Users className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                                <h3 className="text-lg font-medium mb-2">No Available Roles</h3>
                                <p className="text-muted-foreground">
                                  All roles have been assigned to this report or no roles exist.
                                </p>
                              </div>
                            )}
                          </div>
                        )}
                      </CardContent>
                    </Card>

                    {/* Permissions Configuration */}
                    <Card>
                      <CardHeader>
                        <CardTitle className="text-lg flex items-center space-x-2">
                          <Settings className="h-5 w-5" />
                          <span>Configure Permissions</span>
                        </CardTitle>
                        <CardDescription>
                          Set what actions the selected role can perform
                        </CardDescription>
                      </CardHeader>
                      <CardContent>
                        {selectedRoleId ? (
                          <div className="space-y-4">
                            {permissionConfigs.map((config) => {
                              const Icon = config.icon;
                              return (
                                <div key={config.key} className="flex items-start space-x-3 p-3 rounded-lg border">
                                  <Checkbox
                                    id={config.key}
                                    checked={permissions[config.key]}
                                    onCheckedChange={(checked) => 
                                      handlePermissionChange(config.key, checked as boolean)
                                    }
                                    className="mt-1"
                                  />
                                  <div className="flex-1">
                                    <div className="flex items-center space-x-2 mb-1">
                                      <Icon className={`h-4 w-4 ${config.color}`} />
                                      <Label htmlFor={config.key} className="font-medium">
                                        {config.label}
                                      </Label>
                                    </div>
                                    <p className="text-sm text-muted-foreground">
                                      {config.description}
                                    </p>
                                  </div>
                                </div>
                              );
                            })}
                          </div>
                        ) : (
                          <div className="text-center py-8">
                            <Settings className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                            <h3 className="text-lg font-medium mb-2">Select a Role</h3>
                            <p className="text-muted-foreground">
                              Choose a role first to configure its permissions.
                            </p>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  </div>
                  </TabsContent>

                  <TabsContent value="manage" className="mt-4 h-full">
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg">Current Role Assignments</CardTitle>
                      <CardDescription>
                        View and manage existing role assignments for this report
                      </CardDescription>
                    </CardHeader>
                    <CardContent>
                      {assignmentsLoading ? (
                        <div className="space-y-3">
                          {[...Array(3)].map((_, i) => (
                            <div key={i} className="h-20 bg-muted rounded-lg animate-pulse" />
                          ))}
                        </div>
                      ) : assignments.length > 0 ? (
                        <div className="space-y-4">
                          {assignments.map((assignment) => (
                            <Card key={assignment.id} className="border-l-4 border-l-primary/50">
                              <CardHeader className="pb-3">
                                <div className="flex items-center justify-between">
                                  <div className="flex items-center space-x-3">
                                    <div className="p-2 rounded-lg bg-blue-100 text-blue-700">
                                      <Shield className="h-4 w-4" />
                                    </div>
                                    <div>
                                      <CardTitle className="text-base">{assignment.roleName}</CardTitle>
                                      <CardDescription className="text-sm">
                                        Assigned by {assignment.assignedBy} on{' '}
                                        {new Date(assignment.assignedAt).toLocaleDateString()}
                                      </CardDescription>
                                    </div>
                                  </div>
                                  <div className="flex items-center space-x-2">
                                    <Button
                                      variant="outline"
                                      size="sm"
                                      onClick={() => handleEditAssignment(assignment)}
                                    >
                                      <Edit className="h-4 w-4 mr-1" />
                                      Edit
                                    </Button>
                                    <Button
                                      variant="outline"
                                      size="sm"
                                      onClick={() => handleDeleteAssignment(assignment.id)}
                                      className="text-red-600 hover:text-red-700"
                                    >
                                      <Trash2 className="h-4 w-4" />
                                    </Button>
                                  </div>
                                </div>
                              </CardHeader>
                              <CardContent>
                                <div className="flex flex-wrap gap-2">
                                  {permissionConfigs.map((config) => 
                                    getPermissionBadge(assignment, config.key, config.key)
                                  )}
                                </div>
                              </CardContent>
                            </Card>
                          ))}
                        </div>
                      ) : (
                        <div className="text-center py-12">
                          <UserCheck className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                          <h3 className="text-lg font-medium mb-2">No Role Assignments</h3>
                          <p className="text-muted-foreground mb-4">
                            This report hasn't been assigned to any roles yet.
                          </p>
                          <Button onClick={() => setActiveTab('assign')}>
                            <Plus className="h-4 w-4 mr-2" />
                            Assign First Role
                          </Button>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                  </TabsContent>
                </div>
                
                {/* Fixed Action Buttons Footer - only show on assign tab */}
                {activeTab === 'assign' && selectedRoleId && (
                  <div className="flex-shrink-0 border-t bg-background p-4">
                    <div className="flex justify-end space-x-3">
                      <Button
                        variant="outline"
                        onClick={() => {
                          setSelectedRoleId(null);
                          setEditingAssignment(null);
                        }}
                      >
                        Cancel
                      </Button>
                      <Button
                        onClick={editingAssignment ? handleUpdateAssignment : handleCreateAssignment}
                        disabled={
                          createAssignmentMutation.isPending || 
                          updateAssignmentMutation.isPending ||
                          !selectedRoleId
                        }
                      >
                        {(createAssignmentMutation.isPending || updateAssignmentMutation.isPending) && (
                          <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                        )}
                        {editingAssignment ? 'Update Assignment' : 'Create Assignment'}
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            </Tabs>
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        open={showDeleteDialog}
        onOpenChange={setShowDeleteDialog}
        title="Remove Role Assignment"
        description="Are you sure you want to remove this role assignment? Users with this role will lose access to this report. This action cannot be undone."
        confirmText="Remove Assignment"
        cancelText="Cancel"
        variant="destructive"
        onConfirm={confirmDelete}
        isLoading={deleteAssignmentMutation.isPending}
      />
    </>
  );
}