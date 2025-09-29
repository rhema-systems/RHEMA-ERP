'use client';

import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import { Button } from '../ui/button';
import { Progress } from '../ui/progress';
import { Clock, AlertTriangle } from 'lucide-react';

interface SessionTimeoutDialogProps {
  isOpen: boolean;
  remainingSeconds: number;
  onExtendSession: () => void;
  onLogout: () => void;
}

export function SessionTimeoutDialog({
  isOpen,
  remainingSeconds,
  onExtendSession,
  onLogout,
}: SessionTimeoutDialogProps) {
  const [timeLeft, setTimeLeft] = useState(remainingSeconds);

  useEffect(() => {
    setTimeLeft(remainingSeconds);
  }, [remainingSeconds]);

  useEffect(() => {
    if (!isOpen || timeLeft <= 0) return;

    const timer = setInterval(() => {
      setTimeLeft((prev) => {
        const newTime = prev - 1;
        if (newTime <= 0) {
          clearInterval(timer);
          onLogout();
          return 0;
        }
        return newTime;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [isOpen, timeLeft, onLogout]);

  const formatTime = (seconds: number) => {
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${minutes}:${secs.toString().padStart(2, '0')}`;
  };

  const progressPercentage = (timeLeft / remainingSeconds) * 100;
  const isUrgent = timeLeft <= 30;

  return (
    <Dialog open={isOpen} onOpenChange={() => {}}>
      <DialogContent className="sm:max-w-md" onClick={(e) => e.stopPropagation()}>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle className={`h-5 w-5 ${isUrgent ? 'text-red-500' : 'text-yellow-500'}`} />
            Session Expiring Soon
          </DialogTitle>
          <DialogDescription>
            Your session will expire due to inactivity. You will be automatically logged out unless you choose to continue.
          </DialogDescription>
        </DialogHeader>
        
        <div className="space-y-4">
          <div className="flex items-center justify-center gap-2">
            <Clock className={`h-8 w-8 ${isUrgent ? 'text-red-500' : 'text-blue-500'}`} />
            <span className={`text-3xl font-mono font-bold ${isUrgent ? 'text-red-500' : 'text-blue-600'}`}>
              {formatTime(timeLeft)}
            </span>
          </div>
          
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span>Time remaining</span>
              <span className={isUrgent ? 'text-red-500 font-semibold' : 'text-gray-600'}>
                {formatTime(timeLeft)}
              </span>
            </div>
            <Progress 
              value={progressPercentage} 
              className="h-2"
              style={{
                ['--progress-foreground' as any]: isUrgent ? 'hsl(var(--destructive))' : 'hsl(var(--primary))'
              }}
            />
          </div>
          
          {isUrgent && (
            <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3">
              <p className="text-red-700 dark:text-red-300 text-sm font-medium">
                ⚠️ Less than 30 seconds remaining!
              </p>
            </div>
          )}
        </div>

        <DialogFooter className="flex gap-2">
          <Button
            variant="outline"
            onClick={onLogout}
            className="flex-1"
          >
            Logout Now
          </Button>
          <Button
            onClick={onExtendSession}
            className="flex-1"
            disabled={timeLeft <= 0}
          >
            Continue Session
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}