'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Loader2, Settings2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { estatePropertyManagementService } from '@/services/estate-property-management.service';
import type { FacilitiesProcedureWorkspace } from '@/services/estate-facilities.service';
import { BillingServiceChargeWorkspace } from './BillingServiceChargeWorkspace';
import { GroundRentAdministrationWorkspace } from './GroundRentAdministrationWorkspace';
import { LeaseSetupWorkspace } from './LeaseSetupWorkspace';
import { ListingApplicationWorkspace } from './ListingApplicationWorkspace';
import { MoveInHandoverWorkspace } from './MoveInHandoverWorkspace';
import { OccupancyAvailabilityWorkspace } from './OccupancyAvailabilityWorkspace';
import { PropertyUnitRegister } from './PropertyUnitRegister';
import { RecordsIndexWorkspace } from './RecordsIndexWorkspace';
import { TenantOccupantWorkspace } from './TenantOccupantWorkspace';

export default function PropertyManagementWorkspacePage() {
  const router = useRouter();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType)
    ? params.entityType[0]
    : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [workspace, setWorkspace] =
    React.useState<FacilitiesProcedureWorkspace | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadWorkspace = async () => {
      if (!entityType) {
        setLoadError('Workspace was not found.');
        setIsLoading(false);
        return;
      }

      try {
        const data =
          await estatePropertyManagementService.getProcedureWorkspace(
            entityType
          );
        if (mounted) {
          setWorkspace(data);
          setLoadError(data ? null : 'Workspace was not returned by the API.');
        }
      } catch {
        if (mounted) {
          setWorkspace(null);
          setLoadError('Unable to load workspace from the API.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadWorkspace();

    return () => {
      mounted = false;
    };
  }, [entityType]);

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading property management workspace
        </div>
      </div>
    );
  }

  if (!workspace || loadError) {
    return (
      <div className="space-y-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/estate/property-management')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Property Management
        </Button>
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Workspace unavailable</CardTitle>
            <CardDescription>{loadError}</CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  const { procedure } = workspace;
  const isPropertyUnitRegister =
    procedure.entityType === 'EstatePropertyManagementPropertyUnit';
  const isLeaseManagement =
    procedure.entityType === 'EstatePropertyManagementLease';
  const isGroundRentAdministration =
    procedure.entityType === 'EstatePropertyManagementGroundRent';
  const isListingApplication =
    procedure.entityType === 'EstatePropertyManagementListingApplication';
  const isOccupancyAvailability =
    procedure.entityType === 'EstatePropertyManagementOccupancyAvailability';
  const isTenantOccupant =
    procedure.entityType === 'EstatePropertyManagementTenantOccupant';
  const isBillingServiceCharge =
    procedure.entityType === 'EstatePropertyManagementBillingServiceCharge';
  const isMoveInHandover =
    procedure.entityType === 'EstatePropertyManagementMoveInMoveOutHandover';
  const isRecordsIndex =
    procedure.entityType === 'EstatePropertyManagementDocumentRecordIndex';
  const workspaceLabel = isPropertyUnitRegister
    ? 'Property register'
    : isLeaseManagement
      ? 'Lease register'
      : isGroundRentAdministration
        ? 'Ground rent register'
        : isOccupancyAvailability
          ? 'Operational board'
          : isTenantOccupant
            ? 'Tenant register'
            : isBillingServiceCharge
              ? 'Billing board'
              : isMoveInHandover
                ? 'Handover board'
                : isRecordsIndex
                  ? 'Records register'
                  : procedure.workspaceType;

  return (
    <div className="space-y-6">
      <div className="space-y-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/estate/property-management')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Property Management
        </Button>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            {procedure.title}
          </h1>
          {workspaceLabel ? (
            <Badge variant="secondary">{workspaceLabel}</Badge>
          ) : null}
          {isListingApplication ? (
            <Badge variant="outline">Central workflow</Badge>
          ) : null}
          {isListingApplication ? (
            <Button asChild variant="outline" size="sm" className="gap-2">
              <Link href="/administration/workflow?entityType=EstatePropertyManagementListingApplication">
                <Settings2 className="h-4 w-4" />
                Workflow setup
              </Link>
            </Button>
          ) : null}
        </div>
      </div>

      {isPropertyUnitRegister ? (
        <PropertyUnitRegister />
      ) : isLeaseManagement ? (
        <LeaseSetupWorkspace />
      ) : isGroundRentAdministration ? (
        <GroundRentAdministrationWorkspace />
      ) : isListingApplication ? (
        <ListingApplicationWorkspace />
      ) : isOccupancyAvailability ? (
        <OccupancyAvailabilityWorkspace />
      ) : isTenantOccupant ? (
        <TenantOccupantWorkspace />
      ) : isBillingServiceCharge ? (
        <BillingServiceChargeWorkspace />
      ) : isMoveInHandover ? (
        <MoveInHandoverWorkspace />
      ) : isRecordsIndex ? (
        <RecordsIndexWorkspace />
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>Dedicated workspace required</CardTitle>
            <CardDescription>
              This Property Management entity is not mapped to an operational
              workspace yet.
            </CardDescription>
          </CardHeader>
        </Card>
      )}
    </div>
  );
}
