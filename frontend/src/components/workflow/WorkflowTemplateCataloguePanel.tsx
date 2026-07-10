'use client';
import * as React from 'react';
import { Download, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { workflowApiService } from '@/services/workflow-api.service';

export function WorkflowTemplateCataloguePanel({ onImported }: { onImported?: () => void }) {
  const [templates, setTemplates] = React.useState<any[]>([]);
  const load = React.useCallback(async () => setTemplates(await workflowApiService.getWorkflowTemplates()), []);
  React.useEffect(() => { void load(); }, [load]);
  const exportTemplate = async (id: string, name: string) => {
    try { const data = await workflowApiService.exportWorkflowTemplate(id); const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], {type:'application/json'}));
      const anchor=document.createElement('a'); anchor.href=url; anchor.download=`${name.replace(/[^a-z0-9]+/gi,'-').toLowerCase()}.workflow.json`; anchor.click(); URL.revokeObjectURL(url); }
    catch (error:any) { toast.error(error?.message || 'Export failed'); }
  };
  const importTemplate = async (file?: File) => { if(!file)return; try { await workflowApiService.importWorkflowTemplate(JSON.parse(await file.text())); await load(); onImported?.(); toast.success('Template imported as draft'); } catch(error:any){toast.error(error?.message||'Import failed');} };
  return <div className="space-y-4"><div className="flex justify-end"><label><Input type="file" accept=".json" className="hidden" onChange={event=>void importTemplate(event.target.files?.[0])}/><Button asChild variant="outline"><span><Upload className="mr-2 h-4 w-4"/>Import</span></Button></label></div>
    <div className="overflow-hidden rounded-md border"><Table><TableHeader><TableRow><TableHead>Name</TableHead><TableHead>Entity</TableHead><TableHead>Version</TableHead><TableHead className="w-12"/></TableRow></TableHeader><TableBody>{templates.map(item=><TableRow key={item.id}><TableCell className="font-medium">{item.name}</TableCell><TableCell>{item.entityType}</TableCell><TableCell>{item.version}</TableCell><TableCell><Button size="icon" variant="ghost" title="Export template" onClick={()=>void exportTemplate(item.id,item.name)}><Download className="h-4 w-4"/></Button></TableCell></TableRow>)}</TableBody></Table></div></div>;
}
