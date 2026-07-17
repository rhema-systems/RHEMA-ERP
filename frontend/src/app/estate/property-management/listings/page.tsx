'use client';

import React from 'react';
import Link from 'next/link';
import {
  Building2,
  CheckCircle2,
  ImagePlus,
  Loader2,
  MapPin,
  Search,
  Send,
  Star,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
  estateLandManagementService,
  EstateManagedAssetType,
  type EstateManagedAsset,
  type EstateManagedAssetDocument,
} from '@/services/estate-land-management.service';

function assetTypeLabel(value: EstateManagedAssetType) {
  if (value === EstateManagedAssetType.Land) return 'Land';
  if (value === EstateManagedAssetType.Facility) return 'Building';
  return 'Apartment / unit';
}

function formatMoney(value?: number, currency = 'GHS') {
  if (value == null || Number.isNaN(value)) return 'Not priced';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

export default function EstatePropertyListingsPage() {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [documents, setDocuments] = React.useState<EstateManagedAssetDocument[]>(
    []
  );
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [isUploading, setIsUploading] = React.useState(false);
  const [form, setForm] = React.useState({
    isPublishedToExternalPortal: false,
    externalListingType: 'Rent',
    externalListingStatus: 'Published',
    externalListingPrice: '',
    externalListingCurrency: 'GHS',
    externalListingNotes: '',
  });

  const selected = React.useMemo(
    () => assets.find((asset) => asset.id === selectedId) || assets[0],
    [assets, selectedId]
  );

  const loadAssets = React.useCallback(async (query?: string) => {
    setIsLoading(true);
    try {
      const data = await estateLandManagementService.getManagedAssets({
        search: query,
        take: 300,
      });
      setAssets(data);
      setSelectedId((current) =>
        current && data.some((asset) => asset.id === current)
          ? current
          : (data[0]?.id ?? null)
      );
    } catch {
      toast.error('Unable to load managed assets.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  const loadDocuments = React.useCallback(async (assetId: string) => {
    try {
      setDocuments(await estateLandManagementService.getDocuments(assetId));
    } catch {
      setDocuments([]);
    }
  }, []);

  React.useEffect(() => {
    void loadAssets();
  }, [loadAssets]);

  React.useEffect(() => {
    if (!selected) return;

    setForm({
      isPublishedToExternalPortal: selected.isPublishedToExternalPortal,
      externalListingType: selected.externalListingType || 'Rent',
      externalListingStatus: selected.externalListingStatus || 'Published',
      externalListingPrice:
        selected.externalListingPrice == null
          ? ''
          : String(selected.externalListingPrice),
      externalListingCurrency: selected.externalListingCurrency || 'GHS',
      externalListingNotes: selected.externalListingNotes || '',
    });
    void loadDocuments(selected.id);
  }, [loadDocuments, selected]);

  const listingImages = documents.filter((document) => document.isListingImage);
  const publishedCount = assets.filter(
    (asset) => asset.isPublishedToExternalPortal
  ).length;

  const saveListing = async () => {
    if (!selected) return;
    setIsSaving(true);
    try {
      const updated = await estateLandManagementService.updateExternalListing(
        selected.id,
        {
          isPublishedToExternalPortal: form.isPublishedToExternalPortal,
          externalListingType: form.isPublishedToExternalPortal
            ? form.externalListingType
            : 'None',
          externalListingStatus: form.externalListingStatus,
          externalListingPrice: form.externalListingPrice
            ? Number(form.externalListingPrice)
            : null,
          externalListingCurrency: form.externalListingCurrency,
          externalListingNotes: form.externalListingNotes,
        }
      );
      setAssets((current) =>
        current.map((asset) => (asset.id === updated.id ? updated : asset))
      );
      toast.success('Portal listing updated.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to update portal listing.');
    } finally {
      setIsSaving(false);
    }
  };

  const uploadImage = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.currentTarget.value = '';
    if (!file || !selected) return;

    setIsUploading(true);
    try {
      await estateLandManagementService.uploadDocument(
        selected.id,
        file,
        'Listing Image',
        file.name,
        {
          isListingImage: true,
          isPrimaryListingImage: listingImages.length === 0,
        }
      );
      await loadDocuments(selected.id);
      toast.success('Listing image attached.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to upload listing image.');
    } finally {
      setIsUploading(false);
    }
  };

  const setPrimaryImage = async (documentId: string) => {
    if (!selected) return;
    try {
      await estateLandManagementService.setPrimaryListingImage(
        selected.id,
        documentId
      );
      await loadDocuments(selected.id);
      toast.success('Primary listing image updated.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to set primary listing image.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <Badge variant="outline" className="mb-2 w-fit">
            Estate / Property Management
          </Badge>
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            Portal Listings
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Badge variant="secondary" className="px-3 py-2">
            {publishedCount} published
          </Badge>
          <Button asChild variant="outline">
            <Link href="/estate/property-management">
              <Building2 className="mr-2 h-4 w-4" />
              Workspaces
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[380px_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Managed Assets</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              className="flex gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                void loadAssets(search);
              }}
            >
              <div className="relative flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search location, code, name"
                  className="pl-9"
                />
              </div>
              <Button type="submit" variant="outline" size="icon">
                <Search className="h-4 w-4" />
              </Button>
            </form>

            <div className="space-y-2">
              {isLoading ? (
                <div className="flex items-center justify-center gap-2 rounded-md border py-12 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Loading assets
                </div>
              ) : null}
              {!isLoading &&
                assets.map((asset) => {
                  const active = selected?.id === asset.id;
                  return (
                    <button
                      key={asset.id}
                      type="button"
                      onClick={() => setSelectedId(asset.id)}
                      className={`w-full rounded-md border p-3 text-left ${
                        active
                          ? 'border-primary bg-primary/5'
                          : 'bg-background hover:bg-muted'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold">
                            {asset.name}
                          </p>
                          <p className="mt-1 truncate text-xs text-muted-foreground">
                            {asset.assetCode}
                          </p>
                        </div>
                        {asset.isPublishedToExternalPortal ? (
                          <Badge variant="secondary">Portal</Badge>
                        ) : null}
                      </div>
                      <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                        <MapPin className="h-3.5 w-3.5 shrink-0" />
                        <span className="truncate">
                          {asset.location || 'Location not recorded'}
                        </span>
                      </div>
                    </button>
                  );
                })}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {selected?.name || 'Select an asset'}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
            {selected ? (
              <>
                <div className="grid gap-3 md:grid-cols-3">
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Type</div>
                    <div className="mt-1 font-medium">
                      {assetTypeLabel(selected.assetType)}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Price</div>
                    <div className="mt-1 font-medium">
                      {formatMoney(
                        selected.externalListingPrice ||
                          selected.valuationAmount,
                        selected.externalListingCurrency || selected.currency
                      )}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Source</div>
                    <div className="mt-1 font-medium">
                      Estate / Property Management
                    </div>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="space-y-2">
                    <Label>Portal visibility</Label>
                    <Select
                      value={
                        form.isPublishedToExternalPortal
                          ? 'published'
                          : 'draft'
                      }
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          isPublishedToExternalPortal: value === 'published',
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="draft">Not visible</SelectItem>
                        <SelectItem value="published">Visible</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Listing type</Label>
                    <Select
                      value={form.externalListingType}
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          externalListingType: value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Sale">Sale</SelectItem>
                        <SelectItem value="Rent">Rent</SelectItem>
                        <SelectItem value="SaleAndRent">
                          Sale and rent
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Status</Label>
                    <Select
                      value={form.externalListingStatus}
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          externalListingStatus: value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Published">Published</SelectItem>
                        <SelectItem value="Paused">Paused</SelectItem>
                        <SelectItem value="Draft">Draft</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid grid-cols-[1fr_110px] gap-2">
                    <div className="space-y-2">
                      <Label>Price</Label>
                      <Input
                        type="number"
                        min="0"
                        value={form.externalListingPrice}
                        onChange={(event) =>
                          setForm((current) => ({
                            ...current,
                            externalListingPrice: event.target.value,
                          }))
                        }
                      />
                    </div>
                    <div className="space-y-2">
                      <Label>Currency</Label>
                      <Input
                        value={form.externalListingCurrency}
                        onChange={(event) =>
                          setForm((current) => ({
                            ...current,
                            externalListingCurrency:
                              event.target.value.toUpperCase(),
                          }))
                        }
                      />
                    </div>
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Listing notes</Label>
                  <Textarea
                    value={form.externalListingNotes}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        externalListingNotes: event.target.value,
                      }))
                    }
                  />
                </div>

                <div className="flex justify-end">
                  <Button onClick={saveListing} disabled={isSaving}>
                    {isSaving ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="mr-2 h-4 w-4" />
                    )}
                    Save listing
                  </Button>
                </div>

                <div className="rounded-md border p-4">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <div className="font-medium">Listing Images</div>
                      <div className="text-sm text-muted-foreground">
                        {listingImages.length} image
                        {listingImages.length === 1 ? '' : 's'}
                      </div>
                    </div>
                    <div className="relative">
                      <Input
                        type="file"
                        accept="image/*"
                        disabled={isUploading}
                        onChange={uploadImage}
                      />
                      {isUploading ? (
                        <Loader2 className="absolute right-3 top-2.5 h-4 w-4 animate-spin" />
                      ) : (
                        <ImagePlus className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-muted-foreground" />
                      )}
                    </div>
                  </div>

                  <div className="mt-4 space-y-2">
                    {listingImages.length === 0 ? (
                      <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                        No listing images attached.
                      </div>
                    ) : null}
                    {listingImages.map((document) => (
                      <div
                        key={document.id}
                        className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm"
                      >
                        <div className="min-w-0">
                          <div className="truncate font-medium">
                            {document.documentName || document.fileName}
                          </div>
                          {document.isPrimaryListingImage ? (
                            <Badge variant="secondary" className="mt-1">
                              <CheckCircle2 className="mr-1 h-3 w-3" />
                              Primary
                            </Badge>
                          ) : null}
                        </div>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={document.isPrimaryListingImage}
                          onClick={() => void setPrimaryImage(document.id)}
                        >
                          <Star className="mr-1 h-4 w-4" />
                          Primary
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              </>
            ) : (
              <div className="rounded-md border py-16 text-center text-sm text-muted-foreground">
                Select a managed asset.
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
