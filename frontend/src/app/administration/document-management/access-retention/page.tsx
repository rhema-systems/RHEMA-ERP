'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
  Archive,
  FileText,
  Loader2,
  LockKeyhole,
  Pencil,
  Plus,
  Power,
  Scale,
  ShieldCheck,
  Workflow,
  X,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  documentManagementService,
  type CentralDocumentAccessRule,
  type CentralDocumentRetentionPolicy,
} from '@/services/document-management.service';
import { adminApiService } from '@/services/admin-api.service';

const governanceAreas = [
  {
    title: 'Access Profiles',
    description:
      'Define open internal, module restricted, finance restricted, legal restricted, HR restricted, management-only, and custom restricted profiles.',
    icon: LockKeyhole,
  },
  {
    title: 'Retention Rules',
    description:
      'Set statutory, legal, finance, operational, permanent, archive, and destruction-review periods by document template.',
    icon: Archive,
  },
  {
    title: 'Legal Holds',
    description:
      'Hold documents from archive or destruction where matters, disputes, investigations, or court processes require preservation.',
    icon: Scale,
  },
  {
    title: 'Audit Controls',
    description:
      'Track repository access, version publication, annotation review, comment closure, retention decisions, and source-module handoffs.',
    icon: ShieldCheck,
  },
];

const initialAccessForm = {
  accessProfile: 'Module restricted',
  module: 'Estate / Facilities',
  roleName: '',
  permissionKey: 'document-management.module',
  canView: true,
  canUpload: false,
  canAnnotate: false,
  canApprove: false,
  canArchive: false,
};

const anyRoleValue = '__any_role__';

const dmsModuleRoleOptions = [
  {
    module: 'Estate / Facilities',
    roles: [
      'Estate Officer',
      'Estate Manager',
      'Head of Estate',
      'Land Registry Officer',
      'Survey Officer',
      'Acquisition Committee',
      'Executive Approver',
      'Facilities Officer',
      'Facilities Manager',
      'Property Manager',
    ],
  },
  {
    module: 'Estate / Property Management',
    roles: [
      'Estate Officer',
      'Estate Manager',
      'Head of Estate',
      'Property Manager',
      'Executive Approver',
      'Records Officer',
    ],
  },
  {
    module: 'Legal Department',
    roles: [
      'Legal Admin Assistant',
      'Legal Officer',
      'Legal Manager',
      'Head of Legal',
      'Records Officer',
    ],
  },
  {
    module: 'DocumentManagement',
    roles: [
      'Document Control Officer',
      'Records Officer',
      'Admin',
      'SystemAdmin',
      'TenantAdmin',
      'SuperAdmin',
    ],
  },
  {
    module: 'Finance',
    roles: [
      'Finance Officer',
      'Finance Manager',
      'Accounts Payable',
      'Accounts Receivable',
    ],
  },
  {
    module: 'HR',
    roles: ['HR Officer', 'HR Manager', 'HR Admin', 'TenantAdmin'],
  },
  {
    module: 'Procurement',
    roles: [
      'Procurement Officer',
      'Procurement Manager',
      'Tender Committee',
      'Records Officer',
    ],
  },
  {
    module: 'Planning',
    roles: ['Planning Officer', 'Planning Manager'],
  },
  {
    module: 'Project',
    roles: ['Project Officer', 'Project Manager', 'PMO'],
  },
];

const initialRetentionForm = {
  policyCode: '',
  name: '',
  module: 'Estate / Facilities',
  documentType: '',
  retentionDays: '2555',
  requiresLegalHoldReview: false,
  allowArchive: true,
  allowDestruction: false,
  notes: '',
};

function formatSaveError(error: unknown, fallback: string) {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

export default function DmsAccessRetentionSetupPage() {
  const searchParams = useSearchParams();
  const setupScope = searchParams.get('q')?.trim() ?? '';
  const scopedModule = setupScope.toLowerCase().includes('legal')
    ? 'Legal Department'
    : setupScope.toLowerCase().includes('estate')
      ? 'Estate / Facilities'
      : initialAccessForm.module;
  const [accessRules, setAccessRules] = React.useState<
    CentralDocumentAccessRule[]
  >([]);
  const [retentionPolicies, setRetentionPolicies] = React.useState<
    CentralDocumentRetentionPolicy[]
  >([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSavingAccess, setIsSavingAccess] = React.useState(false);
  const [isSavingRetention, setIsSavingRetention] = React.useState(false);
  const [accessForm, setAccessForm] = React.useState({
    ...initialAccessForm,
    module: scopedModule,
  });
  const [retentionForm, setRetentionForm] = React.useState({
    ...initialRetentionForm,
    module: scopedModule,
  });
  const [identityRoles, setIdentityRoles] = React.useState<string[]>([]);
  const [editingAccessId, setEditingAccessId] = React.useState<string | null>(
    null
  );
  const [editingRetentionId, setEditingRetentionId] = React.useState<
    string | null
  >(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const [rules, policies, roles] = await Promise.all([
          documentManagementService.getAccessRules(),
          documentManagementService.getRetentionPolicies(),
          adminApiService.getRoles().catch(() => []),
        ]);
        if (mounted) {
          setAccessRules(rules);
          setRetentionPolicies(policies);
          setIdentityRoles(
            roles
              .map((role) => role.name)
              .filter(Boolean)
              .sort((a, b) => a.localeCompare(b))
          );
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, []);

  const updateAccessForm = (
    field: keyof typeof initialAccessForm,
    value: string | boolean
  ) => {
    setAccessForm((current) => ({ ...current, [field]: value }));
  };

  const selectedModuleRoles = identityRoles.length
    ? identityRoles
    : dmsModuleRoleOptions.find((option) => option.module === accessForm.module)
        ?.roles || [];

  const scopeMatches = React.useCallback(
    (module?: string | null) =>
      !setupScope || module?.toLowerCase().includes(setupScope.toLowerCase()),
    [setupScope]
  );
  const visibleAccessRules = accessRules.filter((rule) =>
    scopeMatches(rule.module)
  );
  const visibleRetentionPolicies = retentionPolicies.filter((policy) =>
    scopeMatches(policy.module)
  );

  const updateAccessModule = (module: string) => {
    const availableRoles = identityRoles.length
      ? identityRoles
      : dmsModuleRoleOptions.find((option) => option.module === module)?.roles || [];
    setAccessForm((current) => ({
      ...current,
      module,
      roleName: !current.roleName || availableRoles.includes(current.roleName) ? current.roleName : '',
    }));
  };

  const updateRetentionForm = (
    field: keyof typeof initialRetentionForm,
    value: string | boolean
  ) => {
    setRetentionForm((current) => ({ ...current, [field]: value }));
  };

  const saveAccessRule = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setIsSavingAccess(true);
    try {
      const payload = {
        ...accessForm,
        isActive: editingAccessId
          ? (accessRules.find((rule) => rule.id === editingAccessId)
              ?.isActive ?? true)
          : true,
      };
      const saved = editingAccessId
        ? await documentManagementService.updateAccessRule(
            editingAccessId,
            payload
          )
        : await documentManagementService.createAccessRule(payload);
      setAccessRules((current) =>
        editingAccessId
          ? current.map((rule) => (rule.id === editingAccessId ? saved : rule))
          : [saved, ...current]
      );
      setEditingAccessId(null);
      setAccessForm({ ...initialAccessForm, module: scopedModule });
    } catch (saveError) {
      setError(formatSaveError(saveError, 'Could not save the access rule.'));
    } finally {
      setIsSavingAccess(false);
    }
  };

  const saveRetentionPolicy = async (
    event: React.FormEvent<HTMLFormElement>
  ) => {
    event.preventDefault();
    setError(null);

    if (!retentionForm.policyCode.trim() || !retentionForm.name.trim()) {
      setError('Policy code and name are required.');
      return;
    }

    setIsSavingRetention(true);
    try {
      const payload = {
        policyCode: retentionForm.policyCode.trim(),
        name: retentionForm.name.trim(),
        module: retentionForm.module.trim(),
        documentType: retentionForm.documentType.trim(),
        retentionDays: Number(retentionForm.retentionDays) || 2555,
        requiresLegalHoldReview: retentionForm.requiresLegalHoldReview,
        allowArchive: retentionForm.allowArchive,
        allowDestruction: retentionForm.allowDestruction,
        isActive: editingRetentionId
          ? (retentionPolicies.find(
              (policy) => policy.id === editingRetentionId
            )?.isActive ?? true)
          : true,
        notes: retentionForm.notes.trim(),
      };
      const saved = editingRetentionId
        ? await documentManagementService.updateRetentionPolicy(
            editingRetentionId,
            payload
          )
        : await documentManagementService.createRetentionPolicy(payload);
      setRetentionPolicies((current) =>
        editingRetentionId
          ? current.map((policy) =>
              policy.id === editingRetentionId ? saved : policy
            )
          : [saved, ...current]
      );
      setEditingRetentionId(null);
      setRetentionForm({ ...initialRetentionForm, module: scopedModule });
    } catch (saveError) {
      setError(
        formatSaveError(saveError, 'Could not save the retention policy.')
      );
    } finally {
      setIsSavingRetention(false);
    }
  };

  const editAccessRule = (rule: CentralDocumentAccessRule) => {
    setEditingAccessId(rule.id);
    setAccessForm({
      accessProfile: rule.accessProfile,
      module: rule.module || scopedModule,
      roleName: rule.roleName || '',
      permissionKey: rule.permissionKey || '',
      canView: rule.canView,
      canUpload: rule.canUpload,
      canAnnotate: rule.canAnnotate,
      canApprove: rule.canApprove,
      canArchive: rule.canArchive,
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const editRetentionPolicy = (policy: CentralDocumentRetentionPolicy) => {
    setEditingRetentionId(policy.id);
    setRetentionForm({
      policyCode: policy.policyCode,
      name: policy.name,
      module: policy.module || scopedModule,
      documentType: policy.documentType || '',
      retentionDays: String(policy.retentionDays),
      requiresLegalHoldReview: policy.requiresLegalHoldReview,
      allowArchive: policy.allowArchive,
      allowDestruction: policy.allowDestruction,
      notes: policy.notes || '',
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const toggleAccessRule = async (rule: CentralDocumentAccessRule) => {
    setError(null);
    try {
      const saved = await documentManagementService.updateAccessRule(rule.id, {
        accessProfile: rule.accessProfile,
        module: rule.module || undefined,
        roleName: rule.roleName || undefined,
        permissionKey: rule.permissionKey || undefined,
        canView: rule.canView,
        canUpload: rule.canUpload,
        canAnnotate: rule.canAnnotate,
        canApprove: rule.canApprove,
        canArchive: rule.canArchive,
        isActive: !rule.isActive,
      });
      setAccessRules((current) =>
        current.map((item) => (item.id === rule.id ? saved : item))
      );
    } catch (saveError) {
      setError(formatSaveError(saveError, 'Could not update the access rule.'));
    }
  };

  const toggleRetentionPolicy = async (
    policy: CentralDocumentRetentionPolicy
  ) => {
    setError(null);
    try {
      const saved = await documentManagementService.updateRetentionPolicy(
        policy.id,
        {
          policyCode: policy.policyCode,
          name: policy.name,
          module: policy.module || undefined,
          documentType: policy.documentType || undefined,
          retentionDays: policy.retentionDays,
          requiresLegalHoldReview: policy.requiresLegalHoldReview,
          allowArchive: policy.allowArchive,
          allowDestruction: policy.allowDestruction,
          isActive: !policy.isActive,
          notes: policy.notes || undefined,
        }
      );
      setRetentionPolicies((current) =>
        current.map((item) => (item.id === policy.id ? saved : item))
      );
    } catch (saveError) {
      setError(
        formatSaveError(saveError, 'Could not update the retention policy.')
      );
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Administration / DMS setup
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              DMS Access & Retention
            </h1>
            <p className="mt-2 max-w-4xl text-sm text-muted-foreground">
              Configure Central DMS confidentiality, repository access,
              retention, legal hold, archival, and audit governance.
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/document-management/CentralDocumentGovernance">
              <FileText className="mr-2 h-4 w-4" />
              Governance workspace
            </Link>
          </Button>
          <Button asChild>
            <Link href="/administration/workflow?q=Document%20Management">
              <Workflow className="mr-2 h-4 w-4" />
              Workflow setup
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle className="text-base">Create Access Rule</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={saveAccessRule}>
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="dms-access-profile">Access profile</Label>
                  <Input
                    id="dms-access-profile"
                    value={accessForm.accessProfile}
                    onChange={(event) =>
                      updateAccessForm('accessProfile', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-access-module">Module</Label>
                  <Select
                    value={accessForm.module}
                    onValueChange={updateAccessModule}
                  >
                    <SelectTrigger id="dms-access-module">
                      <SelectValue placeholder="Select module" />
                    </SelectTrigger>
                    <SelectContent>
                      {dmsModuleRoleOptions.map((option) => (
                        <SelectItem key={option.module} value={option.module}>
                          {option.module}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-access-role">Role</Label>
                  <Select
                    value={accessForm.roleName || anyRoleValue}
                    onValueChange={(value) =>
                      updateAccessForm(
                        'roleName',
                        value === anyRoleValue ? '' : value
                      )
                    }
                  >
                    <SelectTrigger id="dms-access-role">
                      <SelectValue placeholder="Select role" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={anyRoleValue}>
                        Any configured role
                      </SelectItem>
                      {selectedModuleRoles.map((role) => (
                        <SelectItem key={role} value={role}>
                          {role}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-access-permission">Permission key</Label>
                  <Input
                    id="dms-access-permission"
                    value={accessForm.permissionKey}
                    onChange={(event) =>
                      updateAccessForm('permissionKey', event.target.value)
                    }
                  />
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                {[
                  ['canView', 'View'],
                  ['canUpload', 'Upload'],
                  ['canAnnotate', 'Annotate'],
                  ['canApprove', 'Approve'],
                  ['canArchive', 'Archive'],
                ].map(([field, label]) => (
                  <label
                    key={field}
                    className="flex items-center gap-2 rounded-md border bg-background p-3 text-sm"
                  >
                    <Checkbox
                      checked={
                        accessForm[field as keyof typeof initialAccessForm] as
                          | boolean
                          | undefined
                      }
                      onCheckedChange={(checked) =>
                        updateAccessForm(
                          field as keyof typeof initialAccessForm,
                          Boolean(checked)
                        )
                      }
                    />
                    {label}
                  </label>
                ))}
              </div>

              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={isSavingAccess}>
                  {isSavingAccess ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Plus className="mr-2 h-4 w-4" />
                  )}
                  {editingAccessId ? 'Update access rule' : 'Save access rule'}
                </Button>
                {editingAccessId ? (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => {
                      setEditingAccessId(null);
                      setAccessForm({
                        ...initialAccessForm,
                        module: scopedModule,
                      });
                    }}
                  >
                    <X className="mr-2 h-4 w-4" />
                    Cancel
                  </Button>
                ) : null}
              </div>
            </form>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle className="text-base">Create Retention Policy</CardTitle>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={saveRetentionPolicy}>
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="dms-retention-code">Policy code</Label>
                  <Input
                    id="dms-retention-code"
                    value={retentionForm.policyCode}
                    onChange={(event) =>
                      updateRetentionForm('policyCode', event.target.value)
                    }
                    placeholder="EST-LEASE-7Y"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-retention-name">Name</Label>
                  <Input
                    id="dms-retention-name"
                    value={retentionForm.name}
                    onChange={(event) =>
                      updateRetentionForm('name', event.target.value)
                    }
                    placeholder="Estate lease records"
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-retention-module">Module</Label>
                  <Select
                    value={retentionForm.module}
                    onValueChange={(value) =>
                      updateRetentionForm('module', value)
                    }
                  >
                    <SelectTrigger id="dms-retention-module">
                      <SelectValue placeholder="Select module" />
                    </SelectTrigger>
                    <SelectContent>
                      {dmsModuleRoleOptions.map((option) => (
                        <SelectItem key={option.module} value={option.module}>
                          {option.module}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-retention-type">Document type</Label>
                  <Input
                    id="dms-retention-type"
                    value={retentionForm.documentType}
                    onChange={(event) =>
                      updateRetentionForm('documentType', event.target.value)
                    }
                    placeholder="Lease agreement"
                  />
                </div>
                <div className="space-y-2 md:col-span-2">
                  <Label htmlFor="dms-retention-days">Retention days</Label>
                  <Input
                    id="dms-retention-days"
                    type="number"
                    min={1}
                    value={retentionForm.retentionDays}
                    onChange={(event) =>
                      updateRetentionForm('retentionDays', event.target.value)
                    }
                  />
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-3">
                {[
                  ['requiresLegalHoldReview', 'Legal hold review'],
                  ['allowArchive', 'Allow archive'],
                  ['allowDestruction', 'Allow destruction'],
                ].map(([field, label]) => (
                  <label
                    key={field}
                    className="flex items-center gap-2 rounded-md border bg-background p-3 text-sm"
                  >
                    <Checkbox
                      checked={
                        retentionForm[
                          field as keyof typeof initialRetentionForm
                        ] as boolean | undefined
                      }
                      onCheckedChange={(checked) =>
                        updateRetentionForm(
                          field as keyof typeof initialRetentionForm,
                          Boolean(checked)
                        )
                      }
                    />
                    {label}
                  </label>
                ))}
              </div>

              <div className="space-y-2">
                <Label htmlFor="dms-retention-notes">Notes</Label>
                <Textarea
                  id="dms-retention-notes"
                  value={retentionForm.notes}
                  onChange={(event) =>
                    updateRetentionForm('notes', event.target.value)
                  }
                />
              </div>

              <div className="flex flex-wrap gap-2">
                <Button type="submit" disabled={isSavingRetention}>
                  {isSavingRetention ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Plus className="mr-2 h-4 w-4" />
                  )}
                  {editingRetentionId
                    ? 'Update retention policy'
                    : 'Save retention policy'}
                </Button>
                {editingRetentionId ? (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => {
                      setEditingRetentionId(null);
                      setRetentionForm({
                        ...initialRetentionForm,
                        module: scopedModule,
                      });
                    }}
                  >
                    <X className="mr-2 h-4 w-4" />
                    Cancel
                  </Button>
                ) : null}
              </div>
            </form>
          </CardContent>
        </Card>
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {governanceAreas.map((area) => {
          const Icon = area.icon;
          return (
            <Card
              key={area.title}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader>
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
                <CardTitle className="text-base">{area.title}</CardTitle>
              </CardHeader>
            </Card>
          );
        })}
      </div>

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <CardTitle>Setup Links</CardTitle>
        </CardHeader>
        <CardContent>
          <Button asChild variant="outline">
            <Link href="/administration/document-management/metadata-templates">
              Open metadata setup
            </Link>
          </Button>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Configured Access Rules</CardTitle>
            <CardDescription>
              {isLoading
                ? 'Loading access rules'
                : `${visibleAccessRules.length} access rules configured`}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {visibleAccessRules.map((rule) => (
              <div
                key={rule.id}
                className="rounded-md border bg-background p-3"
              >
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="text-sm font-medium">
                      {rule.accessProfile}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {rule.module || 'All modules'} /{' '}
                      {rule.roleName || 'Any role'}
                    </div>
                  </div>
                  <Badge variant={rule.isActive ? 'default' : 'outline'}>
                    {rule.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  {[
                    [rule.canView, 'View'],
                    [rule.canUpload, 'Upload'],
                    [rule.canAnnotate, 'Annotate'],
                    [rule.canApprove, 'Approve'],
                    [rule.canArchive, 'Archive'],
                  ]
                    .filter(([enabled]) => enabled)
                    .map(([, label]) => (
                      <Badge key={String(label)} variant="secondary">
                        {label}
                      </Badge>
                    ))}
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => editAccessRule(rule)}
                  >
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => void toggleAccessRule(rule)}
                  >
                    <Power className="mr-2 h-4 w-4" />
                    {rule.isActive ? 'Deactivate' : 'Activate'}
                  </Button>
                </div>
              </div>
            ))}
            {!isLoading && visibleAccessRules.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No access rules configured yet.
              </p>
            ) : null}
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Configured Retention Policies</CardTitle>
            <CardDescription>
              {isLoading
                ? 'Loading retention policies'
                : `${visibleRetentionPolicies.length} retention policies configured`}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {visibleRetentionPolicies.map((policy) => (
              <div
                key={policy.id}
                className="rounded-md border bg-background p-3"
              >
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="text-sm font-medium">{policy.name}</div>
                    <div className="text-xs text-muted-foreground">
                      {policy.policyCode} / {policy.module || 'All modules'} /{' '}
                      {policy.documentType || 'All document types'}
                    </div>
                  </div>
                  <Badge variant={policy.isActive ? 'default' : 'outline'}>
                    {policy.retentionDays} days
                  </Badge>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  {policy.requiresLegalHoldReview ? (
                    <Badge variant="secondary">Legal hold review</Badge>
                  ) : null}
                  {policy.allowArchive ? (
                    <Badge variant="secondary">Archive allowed</Badge>
                  ) : null}
                  {policy.allowDestruction ? (
                    <Badge variant="secondary">Destruction allowed</Badge>
                  ) : null}
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => editRetentionPolicy(policy)}
                  >
                    <Pencil className="mr-2 h-4 w-4" />
                    Edit
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => void toggleRetentionPolicy(policy)}
                  >
                    <Power className="mr-2 h-4 w-4" />
                    {policy.isActive ? 'Deactivate' : 'Activate'}
                  </Button>
                </div>
              </div>
            ))}
            {!isLoading && visibleRetentionPolicies.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                No retention policies configured yet.
              </p>
            ) : null}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
