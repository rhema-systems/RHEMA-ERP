'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Plus, Search, Edit, Trash2, FileText, File, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { tenderDocumentTypeService, TenderDocumentType, CreateTenderDocumentTypeDto, UpdateTenderDocumentTypeDto } from '@/services/tenderDocumentTypeService';

export default function DocumentTypesPage() {
  const [searchTerm, setSearchTerm] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [documentTypes, setDocumentTypes] = useState<TenderDocumentType[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedDocType, setSelectedDocType] = useState<TenderDocumentType | null>(null);
  const [formData, setFormData] = useState<CreateTenderDocumentTypeDto>({
    documentName: '',
    documentCode: '',
    category: 'General',
    description: '',
    isRequired: true,
    maxFileSizeMB: 10,
    allowedFileTypes: 'PDF',
    isActive: true,
    displayOrder: 0,
  });

  useEffect(() => {
    loadDocumentTypes();
  }, []);

  const loadDocumentTypes = async () => {
    try {
      setLoading(true);
      const data = await tenderDocumentTypeService.getAll();
      setDocumentTypes(data);
    } catch (error) {
      toast.error('Failed to load document types');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    try {
      await tenderDocumentTypeService.create(formData);
      toast.success('Document type created successfully');
      setIsCreateDialogOpen(false);
      resetForm();
      loadDocumentTypes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to create document type');
    }
  };

  const handleUpdate = async () => {
    if (!selectedDocType) return;
    try {
      const updateData: UpdateTenderDocumentTypeDto = {
        documentName: formData.documentName,
        description: formData.description,
        isRequired: formData.isRequired,
        maxFileSizeMB: formData.maxFileSizeMB,
        allowedFileTypes: formData.allowedFileTypes,
        isActive: formData.isActive,
        displayOrder: formData.displayOrder,
      };
      await tenderDocumentTypeService.update(selectedDocType.id, updateData);
      toast.success('Document type updated successfully');
      setIsEditDialogOpen(false);
      setSelectedDocType(null);
      resetForm();
      loadDocumentTypes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to update document type');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this document type?')) return;
    try {
      await tenderDocumentTypeService.delete(id);
      toast.success('Document type deleted successfully');
      loadDocumentTypes();
    } catch (error: any) {
      toast.error(error.message || 'Failed to delete document type');
    }
  };

  const handleEdit = (docType: TenderDocumentType) => {
    setSelectedDocType(docType);
    setFormData({
      documentName: docType.documentName,
      documentCode: docType.documentCode,
      category: docType.category,
      description: docType.description || '',
      isRequired: docType.isRequired,
      maxFileSizeMB: docType.maxFileSizeMB,
      allowedFileTypes: docType.allowedFileTypes,
      isActive: docType.isActive,
      displayOrder: docType.displayOrder,
    });
    setIsEditDialogOpen(true);
  };

  const resetForm = () => {
    setFormData({
      documentName: '',
      documentCode: '',
      category: 'General',
      description: '',
      isRequired: true,
      maxFileSizeMB: 10,
      allowedFileTypes: 'PDF',
      isActive: true,
      displayOrder: 0,
    });
  };

  const filteredDocumentTypes = documentTypes.filter(docType => {
    const matchesSearch = docType.documentName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         docType.documentCode.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesCategory = categoryFilter === 'all' || docType.category === categoryFilter;
    return matchesSearch && matchesCategory;
  });

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Document Types</h1>
          <p className="text-muted-foreground">
            Manage document types and requirements for tender submissions
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Add Document Type
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[600px]">
            <DialogHeader>
              <DialogTitle>Add Document Type</DialogTitle>
              <DialogDescription>
                Create a new document type for tender requirements.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="grid gap-2">
                <Label htmlFor="name">Document Name</Label>
                <Input
                  id="name"
                  placeholder="e.g., Company Registration Certificate"
                  value={formData.documentName}
                  onChange={(e) => setFormData({ ...formData, documentName: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="code">Document Code</Label>
                <Input
                  id="code"
                  placeholder="e.g., DOC-REG-001"
                  value={formData.documentCode}
                  onChange={(e) => setFormData({ ...formData, documentCode: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="category">Category</Label>
                <Select value={formData.category} onValueChange={(value) => setFormData({ ...formData, category: value })}>
                  <SelectTrigger id="category">
                    <SelectValue placeholder="Select category" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="General">General</SelectItem>
                    <SelectItem value="Legal">Legal</SelectItem>
                    <SelectItem value="Financial">Financial</SelectItem>
                    <SelectItem value="Technical">Technical</SelectItem>
                    <SelectItem value="Experience">Experience</SelectItem>
                    <SelectItem value="Other">Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <Label htmlFor="maxSize">Max File Size (MB)</Label>
                  <Input
                    id="maxSize"
                    type="number"
                    placeholder="e.g., 10"
                    value={formData.maxFileSizeMB}
                    onChange={(e) => setFormData({ ...formData, maxFileSizeMB: parseInt(e.target.value) || 0 })}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="fileTypes">Allowed File Types</Label>
                  <Input
                    id="fileTypes"
                    placeholder="e.g., PDF,DOC,DOCX"
                    value={formData.allowedFileTypes}
                    onChange={(e) => setFormData({ ...formData, allowedFileTypes: e.target.value })}
                  />
                </div>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="description">Description</Label>
                <Textarea
                  id="description"
                  placeholder="Document description..."
                  rows={3}
                  value={formData.description}
                  onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                />
              </div>
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  id="isRequired"
                  className="rounded h-4 w-4"
                  checked={formData.isRequired}
                  onChange={(e) => setFormData({ ...formData, isRequired: e.target.checked })}
                />
                <Label htmlFor="isRequired" className="cursor-pointer">Required document</Label>
              </div>
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => { setIsCreateDialogOpen(false); resetForm(); }}>Cancel</Button>
              <Button onClick={handleCreate}>Create Document Type</Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search document types..."
                className="pl-8"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <Select value={categoryFilter} onValueChange={setCategoryFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Categories</SelectItem>
                <SelectItem value="Legal">Legal</SelectItem>
                <SelectItem value="Financial">Financial</SelectItem>
                <SelectItem value="Technical">Technical</SelectItem>
                <SelectItem value="Experience">Experience</SelectItem>
                <SelectItem value="General">General</SelectItem>
                <SelectItem value="Other">Other</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {/* Document Types List */}
      <Card>
        <CardHeader>
          <CardTitle>Document Types</CardTitle>
          <CardDescription>
            {loading ? 'Loading...' : `${filteredDocumentTypes.length} document type${filteredDocumentTypes.length !== 1 ? 's' : ''} found`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="flex justify-center py-8">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <div className="space-y-4">
              {filteredDocumentTypes.length === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  No document types found. {searchTerm || categoryFilter !== 'all' ? 'Try adjusting your filters.' : 'Create your first document type to get started.'}
                </div>
              ) : (
                filteredDocumentTypes.map((docType) => (
                  <div key={docType.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                    <div className="flex items-start justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex items-center space-x-3">
                          <FileText className="h-5 w-5 text-muted-foreground" />
                          <h3 className="font-semibold">{docType.documentName}</h3>
                          <Badge variant="outline">{docType.documentCode}</Badge>
                          <Badge>{docType.category}</Badge>
                          {docType.isRequired && (
                            <Badge variant="destructive">Required</Badge>
                          )}
                          <Badge className={docType.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}>
                            {docType.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm text-muted-foreground">
                          <div>
                            <span className="font-medium">Max Size:</span> {docType.maxFileSizeMB} MB
                          </div>
                          <div>
                            <span className="font-medium">File Types:</span> {docType.allowedFileTypes}
                          </div>
                        </div>

                        {docType.description && (
                          <p className="text-sm text-muted-foreground">{docType.description}</p>
                        )}
                      </div>

                      <div className="flex items-center space-x-2 ml-4">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEdit(docType)}
                        >
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDelete(docType.id)}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Edit Document Type</DialogTitle>
            <DialogDescription>
              Update the document type details.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="edit-name">Document Name</Label>
              <Input
                id="edit-name"
                value={formData.documentName}
                onChange={(e) => setFormData({ ...formData, documentName: e.target.value })}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-code">Document Code (Read-only)</Label>
              <Input
                id="edit-code"
                value={formData.documentCode}
                disabled
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="edit-maxSize">Max File Size (MB)</Label>
                <Input
                  id="edit-maxSize"
                  type="number"
                  value={formData.maxFileSizeMB}
                  onChange={(e) => setFormData({ ...formData, maxFileSizeMB: parseInt(e.target.value) || 0 })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="edit-fileTypes">Allowed File Types</Label>
                <Input
                  id="edit-fileTypes"
                  value={formData.allowedFileTypes}
                  onChange={(e) => setFormData({ ...formData, allowedFileTypes: e.target.value })}
                />
              </div>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="edit-description">Description</Label>
              <Textarea
                id="edit-description"
                rows={3}
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              />
            </div>
            <div className="flex items-center space-x-4">
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  id="edit-isRequired"
                  checked={formData.isRequired}
                  onChange={(e) => setFormData({ ...formData, isRequired: e.target.checked })}
                  className="h-4 w-4"
                />
                <Label htmlFor="edit-isRequired">Required</Label>
              </div>
              <div className="flex items-center space-x-2">
                <input
                  type="checkbox"
                  id="edit-isActive"
                  checked={formData.isActive}
                  onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                  className="h-4 w-4"
                />
                <Label htmlFor="edit-isActive">Active</Label>
              </div>
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => { setIsEditDialogOpen(false); setSelectedDocType(null); resetForm(); }}>Cancel</Button>
            <Button onClick={handleUpdate}>Update Document Type</Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

