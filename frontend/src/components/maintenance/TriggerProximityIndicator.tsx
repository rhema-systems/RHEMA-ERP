import React, { useEffect, useState } from 'react';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { AlertTriangle, Clock, Gauge, Activity } from 'lucide-react';
import { assetUsageTrackingService } from '@/services/assetUsageTrackingService';

interface TriggerProximityIndicatorProps {
  schedule: {
    id: string;
    primaryTriggerType?: string;
    nextDue?: string;
    mileageTrigger?: number;
    operatingHoursTrigger?: number;
    cycleTrigger?: number;
    assetId: string;
  };
}

export function TriggerProximityIndicator({ schedule }: TriggerProximityIndicatorProps) {
  const [proximityData, setProximityData] = useState<{
    percentage: number;
    status: 'safe' | 'warning' | 'critical';
    message: string;
    icon: React.ReactNode;
  } | null>(null);

  useEffect(() => {
    calculateProximity();
  }, [schedule]);

  const calculateProximity = async () => {
    try {
      if (schedule.primaryTriggerType === 'Time' && schedule.nextDue) {
        const daysUntilDue = Math.ceil(
          (new Date(schedule.nextDue).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
        );
        
        let percentage = 0;
        let status: 'safe' | 'warning' | 'critical' = 'safe';
        let message = '';

        if (daysUntilDue < 0) {
          percentage = 100;
          status = 'critical';
          message = `Overdue by ${Math.abs(daysUntilDue)} days`;
        } else if (daysUntilDue === 0) {
          percentage = 100;
          status = 'critical';
          message = 'Due today';
        } else if (daysUntilDue <= 3) {
          percentage = 90;
          status = 'critical';
          message = `Due in ${daysUntilDue} days`;
        } else if (daysUntilDue <= 7) {
          percentage = 70;
          status = 'warning';
          message = `Due in ${daysUntilDue} days`;
        } else if (daysUntilDue <= 14) {
          percentage = 50;
          status = 'warning';
          message = `Due in ${daysUntilDue} days`;
        } else {
          percentage = 30;
          status = 'safe';
          message = `Due in ${daysUntilDue} days`;
        }

        setProximityData({
          percentage,
          status,
          message,
          icon: <Clock className="h-4 w-4" />
        });
      } else if (
        schedule.primaryTriggerType === 'Usage' &&
        (schedule.mileageTrigger || schedule.operatingHoursTrigger || schedule.cycleTrigger)
      ) {
        // Fetch current usage data
        const summary = await assetUsageTrackingService.getAssetUsageSummary(schedule.assetId);
        
        let percentage = 0;
        let status: 'safe' | 'warning' | 'critical' = 'safe';
        let message = '';
        const icon: React.ReactNode = <Gauge className="h-4 w-4" />;

        if (schedule.mileageTrigger && summary.currentMileage) {
          const progress = (summary.currentMileage / schedule.mileageTrigger) * 100;
          percentage = Math.min(progress, 100);
          
          if (progress >= 100) {
            status = 'critical';
            message = `Mileage trigger met (${summary.currentMileage.toLocaleString()} / ${schedule.mileageTrigger.toLocaleString()})`;
          } else if (progress >= 90) {
            status = 'critical';
            message = `${(100 - progress).toFixed(0)}% to mileage trigger`;
          } else if (progress >= 75) {
            status = 'warning';
            message = `${(100 - progress).toFixed(0)}% to mileage trigger`;
          } else {
            status = 'safe';
            message = `${progress.toFixed(0)}% of mileage trigger`;
          }
        } else if (schedule.operatingHoursTrigger && summary.currentOperatingHours) {
          const progress = (summary.currentOperatingHours / schedule.operatingHoursTrigger) * 100;
          percentage = Math.min(progress, 100);
          
          if (progress >= 100) {
            status = 'critical';
            message = `Operating hours trigger met (${summary.currentOperatingHours.toLocaleString()} / ${schedule.operatingHoursTrigger.toLocaleString()})`;
          } else if (progress >= 90) {
            status = 'critical';
            message = `${(100 - progress).toFixed(0)}% to hours trigger`;
          } else if (progress >= 75) {
            status = 'warning';
            message = `${(100 - progress).toFixed(0)}% to hours trigger`;
          } else {
            status = 'safe';
            message = `${progress.toFixed(0)}% of hours trigger`;
          }
        } else if (schedule.cycleTrigger && summary.currentCycles) {
          const progress = (summary.currentCycles / schedule.cycleTrigger) * 100;
          percentage = Math.min(progress, 100);
          
          if (progress >= 100) {
            status = 'critical';
            message = `Cycle trigger met (${summary.currentCycles.toLocaleString()} / ${schedule.cycleTrigger.toLocaleString()})`;
          } else if (progress >= 90) {
            status = 'critical';
            message = `${(100 - progress).toFixed(0)}% to cycle trigger`;
          } else if (progress >= 75) {
            status = 'warning';
            message = `${(100 - progress).toFixed(0)}% to cycle trigger`;
          } else {
            status = 'safe';
            message = `${progress.toFixed(0)}% of cycle trigger`;
          }
        } else {
          message = 'No usage data available';
        }

        setProximityData({
          percentage,
          status,
          message,
          icon
        });
      } else if (schedule.primaryTriggerType === 'Condition') {
        // For condition-based triggers, show monitoring status
        setProximityData({
          percentage: 50,
          status: 'safe',
          message: 'Condition monitoring active',
          icon: <Activity className="h-4 w-4" />
        });
      } else {
        // Default for combined or unknown trigger types
        setProximityData({
          percentage: 50,
          status: 'safe',
          message: 'Monitoring active',
          icon: <Clock className="h-4 w-4" />
        });
      }
    } catch (error) {
      console.error('Error calculating trigger proximity:', error);
      // Show default safe state on error
      setProximityData({
        percentage: 50,
        status: 'safe',
        message: 'Unable to calculate proximity',
        icon: <AlertTriangle className="h-4 w-4" />
      });
    }
  };

  if (!proximityData) {
    return (
      <div className="flex items-center space-x-2">
        <div className="w-24 h-2 bg-gray-200 rounded-full animate-pulse" />
      </div>
    );
  }

  const getProgressColor = () => {
    switch (proximityData.status) {
      case 'critical':
        return 'bg-red-500';
      case 'warning':
        return 'bg-yellow-500';
      default:
        return 'bg-green-500';
    }
  };

  const getStatusBadge = () => {
    switch (proximityData.status) {
      case 'critical':
        return <Badge className="bg-red-100 text-red-800 flex items-center gap-1">
          <AlertTriangle className="h-3 w-3" />
          Critical
        </Badge>;
      case 'warning':
        return <Badge className="bg-yellow-100 text-yellow-800 flex items-center gap-1">
          <AlertTriangle className="h-3 w-3" />
          Warning
        </Badge>;
      default:
        return null;
    }
  };

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div className="flex items-center space-x-2 cursor-help">
            <div className="flex items-center space-x-1 text-muted-foreground">
              {proximityData.icon}
            </div>
            <div className="flex-1 min-w-[120px]">
              <div className="flex items-center space-x-2">
                <div className="flex-1">
                  <div className="relative w-full h-2 bg-gray-200 rounded-full overflow-hidden">
                    <div
                      className={`absolute left-0 top-0 h-full transition-all duration-300 ${getProgressColor()}`}
                      style={{ width: `${proximityData.percentage}%` }}
                    />
                  </div>
                </div>
                {proximityData.status !== 'safe' && (
                  <span className="text-xs font-medium">{proximityData.percentage.toFixed(0)}%</span>
                )}
              </div>
            </div>
            {getStatusBadge()}
          </div>
        </TooltipTrigger>
        <TooltipContent>
          <p className="text-sm">{proximityData.message}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
