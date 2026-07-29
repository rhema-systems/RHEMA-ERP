'use client';

import React from 'react';
import Link from 'next/link';
import {
  Archive,
  FileText,
  Loader2,
  LockKeyhole,
  Plus,
  Scale,
  ShieldCheck,
  Workflow,
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
import { Textarea } from '@/components/ui/textarea';
import {
  documentManagementService,
  type CentralDocumentAccessRule,
  type CentralDocumentRetentionPolicy,
} from '@/services/document-management.service';

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

export default function DmsAccessRetentionSetupPage() {
  const [accessRules, setAccessRules] = React.useState<
    CentralDocumentAccessRule[]
  >([]);
  const [retentionPolicies, setRetentionPolicies] = React.useState<
    CentralDocumentRetentionPolicy[]
  >([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSavingAccess, setIsSavingAccess] = React.useState(false);
  const [isSavingRetention, setIsSavingRetention] = React.useState(false);
  const [accessForm, setAccessForm] = React.useState(initialAccessForm);
  const [retentionForm, setRetentionForm] =
    React.useState(initialRetentionForm);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const [rules, policies] = await Promise.all([
          documentManagementService.getAccessRules(),
          documentManagementService.getRetentionPolicies(),
        ]);
        if (mounted) {
          setAccessRules(rules);
          setRetentionPolicies(policies);
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
      const created = await documentManagementService.createAccessRule({
        ...accessForm,
        isActive: true,
      });
      setAccessRules((current) => [created, ...current]);
      setAccessForm(initialAccessForm);
    } catch {
      setError('Could not save the access rule.');
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
      const created = await documentManagementService.createRetentionPolicy({
        policyCode: retentionForm.policyCode.trim(),
        name: retentionForm.name.trim(),
        module: retentionForm.module.trim(),
        documentType: retentionForm.documentType.trim(),
        retentionDays: Number(retentionForm.retentionDays) || 2555,
        requiresLegalHoldReview: retentionForm.requiresLegalHoldReview,
        allowArchive: retentionForm.allowArchive,
        allowDestruction: retentionForm.allowDestruction,
        isActive: true,
        notes: retentionForm.notes.trim(),
      });
      setRetentionPolicies((current) => [created, ...current]);
      setRetentionForm(initialRetentionForm);
    } catch {
      setError('Could not save the retention policy.');
    } finally {
      setIsSavingRetention(false);
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
                  <Input
                    id="dms-access-module"
                    value={accessForm.module}
                    onChange={(event) =>
                      updateAccessForm('module', event.target.value)
                    }
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="dms-access-role">Role</Label>
                  <Input
                    id="dms-access-role"
                    value={accessForm.roleName}
                    onChange={(event) =>
                      updateAccessForm('roleName', event.target.value)
                    }
                    placeholder="Estate Officer"
                  />
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

              <Button type="submit" disabled={isSavingAccess}>
                {isSavingAccess ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Plus className="mr-2 h-4 w-4" />
                )}
                Save access rule
              </Button>
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
                  <Input
                    id="dms-retention-module"
                    value={retentionForm.module}
                    onChange={(event) =>
                      updateRetentionForm('module', event.target.value)
                    }
                  />
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

              <Button type="submit" disabled={isSavingRetention}>
                {isSavingRetention ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Plus className="mr-2 h-4 w-4" />
                )}
                Save retention policy
              </Button>
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
                : `${accessRules.length} access rules configured`}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {accessRules.map((rule) => (
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
              </div>
            ))}
            {!isLoading && accessRules.length === 0 ? (
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
                : `${retentionPolicies.length} retention policies configured`}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {retentionPolicies.map((policy) => (
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
              </div>
            ))}
            {!isLoading && retentionPolicies.length === 0 ? (
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
