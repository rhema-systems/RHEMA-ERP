'use client';

import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import {
  CreateProjectCatalogEntryDto,
  CreateProjectPriorityDto,
  CreateProjectTemplateDto,
  CreateProjectTypeDto,
  ProjectCatalogEntryDto,
  ProjectMasterDataOverviewDto,
  ProjectManagementSettingsDto,
  ProjectPriorityDto,
  ProjectTemplateDto,
  ProjectTypeDto,
  projectService,
  UpdateProjectManagementSettingsDto,
} from '@/services/projectService';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

type AdminTab = 'overview' | 'catalogs' | 'types' | 'priorities' | 'templates' | 'settings';

interface Props {
  initialTab?: AdminTab;
}

const emptyType: CreateProjectTypeDto = { code: '', name: '', description: '', isActive: true, requiresSponsor: false, requiresApproval: true, mandatoryFieldsJson: '' };
const emptyPriority: CreateProjectPriorityDto = { code: '', name: '', colorHex: '#2563eb', sortOrder: 10, isActive: true };
const emptyTemplate: CreateProjectTemplateDto = {
  code: '',
  name: '',
  description: '',
  versionLabel: '1.0',
  templateDefinitionJson: JSON.stringify({ workItems: [{ nodeType: 'Phase', title: 'Initiation' }] }, null, 2),
  isActive: true,
};
const emptySettings: UpdateProjectManagementSettingsDto = {
  projectNumberFormat: 'PRJ-{YYYY}-{SEQ:0000}',
  requireSponsor: false,
  defaultApprovalRequired: true,
  mandatoryFieldsByTypeJson: '',
  notes: '',
};
const emptyCatalog: CreateProjectCatalogEntryDto = {
  catalogType: 'methodologies',
  code: '',
  name: '',
  description: '',
  sortOrder: 10,
  isActive: true,
};

export default function ProjectManagementAdminPage({ initialTab = 'overview' }: Props) {
  const [tab, setTab] = useState<AdminTab>(initialTab);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [types, setTypes] = useState<ProjectTypeDto[]>([]);
  const [priorities, setPriorities] = useState<ProjectPriorityDto[]>([]);
  const [templates, setTemplates] = useState<ProjectTemplateDto[]>([]);
  const [masterDataOverview, setMasterDataOverview] = useState<ProjectMasterDataOverviewDto | null>(null);
  const [catalogEntries, setCatalogEntries] = useState<ProjectCatalogEntryDto[]>([]);
  const [settings, setSettings] = useState<ProjectManagementSettingsDto | null>(null);
  const [typeForm, setTypeForm] = useState<CreateProjectTypeDto>(emptyType);
  const [priorityForm, setPriorityForm] = useState<CreateProjectPriorityDto>(emptyPriority);
  const [templateForm, setTemplateForm] = useState<CreateProjectTemplateDto>(emptyTemplate);
  const [catalogForm, setCatalogForm] = useState<CreateProjectCatalogEntryDto>(emptyCatalog);
  const [settingsForm, setSettingsForm] = useState<UpdateProjectManagementSettingsDto>(emptySettings);
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null);
  const [editingPriorityId, setEditingPriorityId] = useState<string | null>(null);
  const [editingTemplateId, setEditingTemplateId] = useState<string | null>(null);
  const [editingCatalogId, setEditingCatalogId] = useState<string | null>(null);
  const [selectedCatalogType, setSelectedCatalogType] = useState<string>('methodologies');
  const [seedingCatalogs, setSeedingCatalogs] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [loadedOverview, loadedTypes, loadedPriorities, loadedTemplates, loadedSettings] = await Promise.all([
        projectService.getMasterDataOverview(),
        projectService.getProjectTypes(),
        projectService.getProjectPriorities(),
        projectService.getProjectTemplates(),
        projectService.getSettings(),
      ]);
      setMasterDataOverview(loadedOverview);
      const nextCatalogType = loadedOverview.recommendedCatalogs[0]?.key || selectedCatalogType;
      setSelectedCatalogType(nextCatalogType);
      setCatalogForm((prev) => ({ ...prev, catalogType: nextCatalogType }));
      setTypes(loadedTypes);
      setPriorities(loadedPriorities);
      setTemplates(loadedTemplates);
      setSettings(loadedSettings);
      setSettingsForm({
        projectNumberFormat: loadedSettings.projectNumberFormat,
        requireSponsor: loadedSettings.requireSponsor,
        defaultApprovalRequired: loadedSettings.defaultApprovalRequired,
        defaultProjectTypeId: loadedSettings.defaultProjectTypeId,
        defaultProjectPriorityId: loadedSettings.defaultProjectPriorityId,
        defaultTemplateId: loadedSettings.defaultTemplateId,
        mandatoryFieldsByTypeJson: loadedSettings.mandatoryFieldsByTypeJson || '',
        notes: loadedSettings.notes || '',
      });
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project administration data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const loadCatalogEntries = async (catalogType: string) => {
    try {
      setCatalogEntries(await projectService.getCatalogEntries(catalogType));
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project catalog entries');
    }
  };

  useEffect(() => {
    if (!selectedCatalogType) return;
    setCatalogForm((prev) => ({ ...prev, catalogType: selectedCatalogType }));
    loadCatalogEntries(selectedCatalogType);
  }, [selectedCatalogType]);

  const saveType = async () => {
    try {
      setSaving(true);
      if (editingTypeId) {
        await projectService.updateProjectType(editingTypeId, typeForm);
      } else {
        await projectService.createProjectType(typeForm);
      }
      setTypeForm(emptyType);
      setEditingTypeId(null);
      await loadData();
      toast.success('Project type saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project type');
    } finally {
      setSaving(false);
    }
  };

  const savePriority = async () => {
    try {
      setSaving(true);
      if (editingPriorityId) {
        await projectService.updateProjectPriority(editingPriorityId, priorityForm);
      } else {
        await projectService.createProjectPriority(priorityForm);
      }
      setPriorityForm(emptyPriority);
      setEditingPriorityId(null);
      await loadData();
      toast.success('Project priority saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project priority');
    } finally {
      setSaving(false);
    }
  };

  const saveTemplate = async () => {
    try {
      setSaving(true);
      if (editingTemplateId) {
        await projectService.updateProjectTemplate(editingTemplateId, templateForm);
      } else {
        await projectService.createProjectTemplate(templateForm);
      }
      setTemplateForm(emptyTemplate);
      setEditingTemplateId(null);
      await loadData();
      toast.success('Project template saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project template');
    } finally {
      setSaving(false);
    }
  };

  const saveSettings = async () => {
    try {
      setSaving(true);
      const updated = await projectService.updateSettings(settingsForm);
      setSettings(updated);
      toast.success('Project settings updated');
    } catch (error: any) {
      toast.error(error.message || 'Failed to update project settings');
    } finally {
      setSaving(false);
    }
  };

  const saveCatalogEntry = async () => {
    try {
      setSaving(true);
      if (editingCatalogId) {
        await projectService.updateCatalogEntry(editingCatalogId, catalogForm);
      } else {
        await projectService.createCatalogEntry(catalogForm);
      }
      setCatalogForm({ ...emptyCatalog, catalogType: selectedCatalogType });
      setEditingCatalogId(null);
      await Promise.all([loadCatalogEntries(selectedCatalogType), loadData()]);
      toast.success('Project catalog entry saved');
    } catch (error: any) {
      toast.error(error.message || 'Failed to save project catalog entry');
    } finally {
      setSaving(false);
    }
  };

  const removeType = async (id: string) => {
    if (!window.confirm('Delete this project type?')) return;
    try {
      await projectService.deleteProjectType(id);
      if (editingTypeId === id) {
        setEditingTypeId(null);
        setTypeForm(emptyType);
      }
      await loadData();
      toast.success('Project type deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project type');
    }
  };

  const removePriority = async (id: string) => {
    if (!window.confirm('Delete this project priority?')) return;
    try {
      await projectService.deleteProjectPriority(id);
      if (editingPriorityId === id) {
        setEditingPriorityId(null);
        setPriorityForm(emptyPriority);
      }
      await loadData();
      toast.success('Project priority deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project priority');
    }
  };

  const removeTemplate = async (id: string) => {
    if (!window.confirm('Delete this project template?')) return;
    try {
      await projectService.deleteProjectTemplate(id);
      if (editingTemplateId === id) {
        setEditingTemplateId(null);
        setTemplateForm(emptyTemplate);
      }
      await loadData();
      toast.success('Project template deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project template');
    }
  };

  const removeCatalogEntry = async (id: string) => {
    if (!window.confirm('Delete this project catalog entry?')) return;
    try {
      await projectService.deleteCatalogEntry(id);
      if (editingCatalogId === id) {
        setEditingCatalogId(null);
        setCatalogForm({ ...emptyCatalog, catalogType: selectedCatalogType });
      }
      await Promise.all([loadCatalogEntries(selectedCatalogType), loadData()]);
      toast.success('Project catalog entry deleted');
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete project catalog entry');
    }
  };

  const seedCatalogDefaults = async () => {
    try {
      setSeedingCatalogs(true);
      await projectService.seedCatalogDefaults(selectedCatalogType);
      await Promise.all([loadCatalogEntries(selectedCatalogType), loadData()]);
      toast.success('Recommended defaults seeded');
    } catch (error: any) {
      toast.error(error.message || 'Failed to seed recommended defaults');
    } finally {
      setSeedingCatalogs(false);
    }
  };

  if (loading) {
    return <div className="py-20 text-center text-muted-foreground">Loading project management setup...</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Project Management Setup</h1>
          <p className="text-muted-foreground">Configure defaults, templates, catalogs, priorities, and project types.</p>
        </div>
        <Button onClick={saveSettings} disabled={saving}>
          Save Settings
        </Button>
      </div>

      <Tabs value={tab} onValueChange={(value) => setTab(value as AdminTab)} className="space-y-6">
        <TabsList className="grid w-full grid-cols-6">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="catalogs">Catalogs</TabsTrigger>
          <TabsTrigger value="types">Types</TabsTrigger>
          <TabsTrigger value="priorities">Priorities</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-6">
            <Card><CardHeader><CardTitle className="text-base">Types</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectTypeCount ?? types.length}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Priorities</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectPriorityCount ?? priorities.length}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Templates</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.projectTemplateCount ?? templates.length}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Portfolios</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.portfolioCount ?? 0}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Programs</CardTitle></CardHeader><CardContent className="text-3xl font-semibold">{masterDataOverview?.programCount ?? 0}</CardContent></Card>
            <Card><CardHeader><CardTitle className="text-base">Approval Default</CardTitle></CardHeader><CardContent><Badge>{settings?.defaultApprovalRequired ? 'Required' : 'Optional'}</Badge></CardContent></Card>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Project Setup Catalogs</CardTitle>
              <CardDescription>Manage tenant-specific project setup catalogs.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 lg:grid-cols-2">
              {(masterDataOverview?.recommendedCatalogs || []).map((group) => (
                <div key={group.key} className="rounded-lg border p-4">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <div className="font-semibold">{group.displayName}</div>
                      <div className="text-sm text-muted-foreground">
                        {masterDataOverview?.catalogCoverage.find((item) => item.catalogType === group.key)?.configuredCount ?? 0} configured of {group.items.length} recommended.
                      </div>
                    </div>
                    <Badge variant="outline">{group.items.length} items</Badge>
                  </div>
                  <div className="mt-4 flex flex-wrap gap-2">
                    {group.items.map((item) => (
                      <Badge key={`${group.key}-${item.code}`} variant="secondary">
                        {item.name}
                      </Badge>
                    ))}
                  </div>
                </div>
              ))}
              {!(masterDataOverview?.recommendedCatalogs?.length) ? (
                <div className="text-sm text-muted-foreground">No catalog groups are available yet.</div>
              ) : null}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Setup Coverage</CardTitle>
              <CardDescription>Review configured setup records and available catalog groups.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="rounded-lg border p-4">
                <div className="font-medium">Operational setup</div>
                <div className="mt-2 text-sm text-muted-foreground">Types, priorities, templates, numbering rules, portfolios, and programs are available as active setup records.</div>
              </div>
              <div className="rounded-lg border p-4">
                <div className="font-medium">Additional setup areas</div>
                <div className="mt-2 text-sm text-muted-foreground">Methodologies, lifecycle statuses, risk ratings, cost categories, expense categories, billing types, and resource roles can be maintained here as tenant-specific records.</div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="catalogs">
          <Card>
            <CardHeader>
              <CardTitle>Project Catalogs</CardTitle>
              <CardDescription>Manage tenant-specific methodologies, lifecycle statuses, stages, billing types, cost categories, and related project setup catalogs.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-4">
                <div className="flex flex-col gap-4 md:flex-row md:items-end">
                  <div className="grid gap-2 md:flex-1">
                    <Label>Catalog Type</Label>
                    <Select value={selectedCatalogType} onValueChange={setSelectedCatalogType}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {(masterDataOverview?.recommendedCatalogs || []).map((group) => (
                          <SelectItem key={group.key} value={group.key}>{group.displayName}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <Button variant="outline" onClick={seedCatalogDefaults} disabled={seedingCatalogs}>
                    Seed Recommended
                  </Button>
                </div>
                <div className="space-y-3">
                  {catalogEntries.length === 0 ? <div className="text-sm text-muted-foreground">No entries exist yet for this catalog type.</div> : null}
                  {catalogEntries.map((item) => (
                    <div key={item.id} className="rounded-lg border p-4">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-semibold">{item.name}</span>
                            <Badge variant="outline">{item.code}</Badge>
                            <Badge variant={item.isActive ? 'secondary' : 'outline'}>{item.isActive ? 'Active' : 'Inactive'}</Badge>
                          </div>
                          <div className="text-sm text-muted-foreground">Sort order {item.sortOrder}</div>
                          {item.description ? <div className="mt-2 text-sm text-muted-foreground">{item.description}</div> : null}
                        </div>
                        <div className="flex gap-2">
                          <Button variant="outline" size="sm" onClick={() => {
                            setEditingCatalogId(item.id);
                            setCatalogForm({
                              catalogType: item.catalogType,
                              code: item.code,
                              name: item.name,
                              description: item.description || '',
                              sortOrder: item.sortOrder,
                              isActive: item.isActive,
                            });
                          }}>Edit</Button>
                          <Button variant="ghost" size="icon" onClick={() => removeCatalogEntry(item.id)}><Trash2 className="h-4 w-4" /></Button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingCatalogId ? 'Edit Catalog Entry' : 'New Catalog Entry'}</div>
                <div className="grid gap-2">
                  <Label>Catalog Type</Label>
                  <Select value={catalogForm.catalogType} onValueChange={(value) => { setSelectedCatalogType(value); setCatalogForm((prev) => ({ ...prev, catalogType: value })); }}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {(masterDataOverview?.recommendedCatalogs || []).map((group) => (
                        <SelectItem key={group.key} value={group.key}>{group.displayName}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Code</Label><Input value={catalogForm.code} onChange={(e) => setCatalogForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={catalogForm.name} onChange={(e) => setCatalogForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Description</Label><Textarea rows={3} value={catalogForm.description || ''} onChange={(e) => setCatalogForm((prev) => ({ ...prev, description: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Sort Order</Label><Input type="number" value={catalogForm.sortOrder ?? 0} onChange={(e) => setCatalogForm((prev) => ({ ...prev, sortOrder: Number(e.target.value || '0') }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={catalogForm.isActive !== false} onCheckedChange={(checked) => setCatalogForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={saveCatalogEntry} disabled={saving || !catalogForm.catalogType || !catalogForm.code || !catalogForm.name}>Save Entry</Button>
                  {editingCatalogId && <Button variant="outline" onClick={() => { setEditingCatalogId(null); setCatalogForm({ ...emptyCatalog, catalogType: selectedCatalogType }); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="types">
          <Card>
            <CardHeader>
              <CardTitle>Project Types</CardTitle>
              <CardDescription>Lifecycle and governance behavior by project type.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-3">
                {types.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-semibold">{item.name}</span>
                          <Badge variant="outline">{item.code}</Badge>
                        </div>
                        <div className="text-sm text-muted-foreground">{item.description || 'No description'}</div>
                      </div>
                      <div className="flex gap-2">
                        <Button variant="outline" size="sm" onClick={() => {
                          setEditingTypeId(item.id);
                          setTypeForm({ code: item.code, name: item.name, description: item.description || '', isActive: item.isActive, requiresSponsor: item.requiresSponsor, requiresApproval: item.requiresApproval, mandatoryFieldsJson: item.mandatoryFieldsJson || '' });
                        }}>Edit</Button>
                        <Button variant="ghost" size="icon" onClick={() => removeType(item.id)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingTypeId ? 'Edit Type' : 'New Type'}</div>
                <div className="grid gap-2"><Label>Code</Label><Input value={typeForm.code} onChange={(e) => setTypeForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={typeForm.name} onChange={(e) => setTypeForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Description</Label><Textarea rows={3} value={typeForm.description} onChange={(e) => setTypeForm((prev) => ({ ...prev, description: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Mandatory Fields JSON</Label><Textarea rows={5} value={typeForm.mandatoryFieldsJson} onChange={(e) => setTypeForm((prev) => ({ ...prev, mandatoryFieldsJson: e.target.value }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Require Sponsor</span><Switch checked={!!typeForm.requiresSponsor} onCheckedChange={(checked) => setTypeForm((prev) => ({ ...prev, requiresSponsor: checked }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Require Approval</span><Switch checked={typeForm.requiresApproval !== false} onCheckedChange={(checked) => setTypeForm((prev) => ({ ...prev, requiresApproval: checked }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={typeForm.isActive !== false} onCheckedChange={(checked) => setTypeForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={saveType} disabled={saving || !typeForm.code || !typeForm.name}>Save Type</Button>
                  {editingTypeId && <Button variant="outline" onClick={() => { setEditingTypeId(null); setTypeForm(emptyType); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="priorities">
          <Card>
            <CardHeader>
              <CardTitle>Project Priorities</CardTitle>
              <CardDescription>Default urgency levels used in project and task planning.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-3">
                {priorities.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4 flex items-center justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold">{item.name}</span>
                        <Badge variant="outline">{item.code}</Badge>
                        <span className="h-3 w-3 rounded-full border" style={{ backgroundColor: item.colorHex || '#64748b' }} />
                      </div>
                      <div className="text-sm text-muted-foreground">Sort order {item.sortOrder}</div>
                    </div>
                    <div className="flex gap-2">
                      <Button variant="outline" size="sm" onClick={() => {
                        setEditingPriorityId(item.id);
                        setPriorityForm({ code: item.code, name: item.name, colorHex: item.colorHex || '#2563eb', sortOrder: item.sortOrder, isActive: item.isActive });
                      }}>Edit</Button>
                      <Button variant="ghost" size="icon" onClick={() => removePriority(item.id)}><Trash2 className="h-4 w-4" /></Button>
                    </div>
                  </div>
                ))}
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingPriorityId ? 'Edit Priority' : 'New Priority'}</div>
                <div className="grid gap-2"><Label>Code</Label><Input value={priorityForm.code} onChange={(e) => setPriorityForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={priorityForm.name} onChange={(e) => setPriorityForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Color</Label><Input value={priorityForm.colorHex} onChange={(e) => setPriorityForm((prev) => ({ ...prev, colorHex: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Sort Order</Label><Input type="number" value={priorityForm.sortOrder} onChange={(e) => setPriorityForm((prev) => ({ ...prev, sortOrder: Number(e.target.value || '0') }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={priorityForm.isActive !== false} onCheckedChange={(checked) => setPriorityForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={savePriority} disabled={saving || !priorityForm.code || !priorityForm.name}>Save Priority</Button>
                  {editingPriorityId && <Button variant="outline" onClick={() => { setEditingPriorityId(null); setPriorityForm(emptyPriority); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="templates">
          <Card>
            <CardHeader>
              <CardTitle>Project Templates</CardTitle>
              <CardDescription>Reusable WBS and milestone definitions stored as JSON.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
              <div className="space-y-3">
                {templates.map((item) => (
                  <div key={item.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-semibold">{item.name}</span>
                          <Badge variant="outline">{item.code}</Badge>
                          <Badge variant="secondary">{item.versionLabel}</Badge>
                        </div>
                        <div className="text-sm text-muted-foreground">{item.description || 'No description'}</div>
                      </div>
                      <div className="flex gap-2">
                        <Button variant="outline" size="sm" onClick={() => {
                          setEditingTemplateId(item.id);
                          setTemplateForm({ code: item.code, name: item.name, description: item.description || '', projectTypeId: item.projectTypeId, versionLabel: item.versionLabel, templateDefinitionJson: item.templateDefinitionJson || '', isActive: item.isActive });
                        }}>Edit</Button>
                        <Button variant="ghost" size="icon" onClick={() => removeTemplate(item.id)}><Trash2 className="h-4 w-4" /></Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
              <div className="rounded-lg border p-4 space-y-4">
                <div className="font-semibold">{editingTemplateId ? 'Edit Template' : 'New Template'}</div>
                <div className="grid gap-2"><Label>Code</Label><Input value={templateForm.code} onChange={(e) => setTemplateForm((prev) => ({ ...prev, code: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Name</Label><Input value={templateForm.name} onChange={(e) => setTemplateForm((prev) => ({ ...prev, name: e.target.value }))} /></div>
                <div className="grid gap-2">
                  <Label>Project Type</Label>
                  <Select value={templateForm.projectTypeId || 'none'} onValueChange={(value) => setTemplateForm((prev) => ({ ...prev, projectTypeId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="General template" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">General template</SelectItem>
                      {types.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Version</Label><Input value={templateForm.versionLabel} onChange={(e) => setTemplateForm((prev) => ({ ...prev, versionLabel: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Description</Label><Textarea rows={3} value={templateForm.description} onChange={(e) => setTemplateForm((prev) => ({ ...prev, description: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Template Definition JSON</Label><Textarea rows={10} value={templateForm.templateDefinitionJson} onChange={(e) => setTemplateForm((prev) => ({ ...prev, templateDefinitionJson: e.target.value }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-3"><span className="text-sm">Active</span><Switch checked={templateForm.isActive !== false} onCheckedChange={(checked) => setTemplateForm((prev) => ({ ...prev, isActive: checked }))} /></div>
                <div className="flex gap-2">
                  <Button onClick={saveTemplate} disabled={saving || !templateForm.code || !templateForm.name}>Save Template</Button>
                  {editingTemplateId && <Button variant="outline" onClick={() => { setEditingTemplateId(null); setTemplateForm(emptyTemplate); }}>Cancel</Button>}
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="settings">
          <Card>
            <CardHeader>
              <CardTitle>Project Settings</CardTitle>
              <CardDescription>Tenant-aware defaults for numbering, workflow, and baseline records.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="grid gap-2"><Label>Project Number Format</Label><Input value={settingsForm.projectNumberFormat} onChange={(e) => setSettingsForm((prev) => ({ ...prev, projectNumberFormat: e.target.value }))} /></div>
                <div className="grid gap-2">
                  <Label>Default Project Type</Label>
                  <Select value={settingsForm.defaultProjectTypeId || 'none'} onValueChange={(value) => setSettingsForm((prev) => ({ ...prev, defaultProjectTypeId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="No default type" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default type</SelectItem>
                      {types.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Default Priority</Label>
                  <Select value={settingsForm.defaultProjectPriorityId || 'none'} onValueChange={(value) => setSettingsForm((prev) => ({ ...prev, defaultProjectPriorityId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="No default priority" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default priority</SelectItem>
                      {priorities.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Default Template</Label>
                  <Select value={settingsForm.defaultTemplateId || 'none'} onValueChange={(value) => setSettingsForm((prev) => ({ ...prev, defaultTemplateId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="No default template" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default template</SelectItem>
                      {templates.map((item) => <SelectItem key={item.id} value={item.id}>{item.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <div className="flex items-center justify-between border rounded-md p-4"><span className="text-sm">Require Sponsor</span><Switch checked={settingsForm.requireSponsor} onCheckedChange={(checked) => setSettingsForm((prev) => ({ ...prev, requireSponsor: checked }))} /></div>
                <div className="flex items-center justify-between border rounded-md p-4"><span className="text-sm">Default Approval Required</span><Switch checked={settingsForm.defaultApprovalRequired} onCheckedChange={(checked) => setSettingsForm((prev) => ({ ...prev, defaultApprovalRequired: checked }))} /></div>
              </div>
              <div className="grid gap-2"><Label>Mandatory Fields By Type JSON</Label><Textarea rows={6} value={settingsForm.mandatoryFieldsByTypeJson} onChange={(e) => setSettingsForm((prev) => ({ ...prev, mandatoryFieldsByTypeJson: e.target.value }))} /></div>
              <div className="grid gap-2"><Label>Notes</Label><Textarea rows={4} value={settingsForm.notes} onChange={(e) => setSettingsForm((prev) => ({ ...prev, notes: e.target.value }))} /></div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
