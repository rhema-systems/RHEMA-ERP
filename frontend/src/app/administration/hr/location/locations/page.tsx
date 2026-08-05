'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Pencil, Trash2, MapPin } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { locationService } from '@/services/hr/location.service';
import type { Location } from '@/types/hr/location';

export default function LocationsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<Location | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const locations = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (l) =>
        l.name.toLowerCase().includes(term) ||
        l.code.toLowerCase().includes(term) ||
        (l.city ?? '').toLowerCase().includes(term),
    );
  }, [data, search]);

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await locationService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'locations'] });
      toast({ title: 'Deleted', description: `"${deleteTarget.name}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete location.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Locations"
        description="Physical work locations, organized by your location hierarchy."
        actions={
          <Button onClick={() => router.push('/administration/hr/location/locations/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Location
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Locations</CardTitle>
            <div className="relative w-64">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search locations…"
                className="pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Level</TableHead>
                  <TableHead>Parent</TableHead>
                  <TableHead>City</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      <TableCell><Skeleton className="h-4 w-[180px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[120px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[80px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[70px]" /></TableCell>
                      <TableCell><Skeleton className="h-8 w-8" /></TableCell>
                    </TableRow>
                  ))
                ) : locations.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={MapPin}
                        title={search ? 'No matching locations' : 'No locations yet'}
                        description={search ? 'Try a different search.' : 'Create your first location.'}
                        action={
                          !search ? (
                            <Button size="sm" onClick={() => router.push('/administration/hr/location/locations/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Location
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  locations.map((loc) => (
                    <TableRow
                      key={loc.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/administration/hr/location/locations/${loc.id}/edit`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{loc.name}</span>
                          {loc.code && <span className="text-xs text-muted-foreground">{loc.code}</span>}
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">{loc.levelName ?? '—'}</TableCell>
                      <TableCell className="text-muted-foreground">{loc.parentLocationName ?? '—'}</TableCell>
                      <TableCell className="text-muted-foreground">{loc.city ?? '—'}</TableCell>
                      <TableCell>
                        <StatusBadge active={loc.isActive} />
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/administration/hr/location/locations/${loc.id}/edit`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Edit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(loc);
                              }}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Delete
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete location"
        description={
          deleteTarget
            ? `Are you sure you want to delete "${deleteTarget.name}"? This cannot be undone.`
            : ''
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDelete}
      />
    </div>
  );
}
