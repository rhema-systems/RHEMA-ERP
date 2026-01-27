'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { performanceTrackingService, type PerformanceTrendDto } from '@/services/performanceTrackingService';
import { TrendingUp } from 'lucide-react';

interface PerformanceTrendsChartProps {
  businessPartnerId: string;
}

export function PerformanceTrendsChart({ businessPartnerId }: PerformanceTrendsChartProps) {
  const [loading, setLoading] = useState(false);
  const [period, setPeriod] = useState('Monthly');
  const [count, setCount] = useState(12);
  const [trends, setTrends] = useState<PerformanceTrendDto[]>([]);

  useEffect(() => {
    loadTrends();
  }, [businessPartnerId, period, count]);

  const loadTrends = async () => {
    try {
      setLoading(true);
      const data = await performanceTrackingService.getPerformanceTrends(businessPartnerId, period, count);
      setTrends(data);
    } catch (error) {
      console.error('Error loading performance trends:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <TrendingUp className="w-5 h-5" />
            <CardTitle>Performance Trends</CardTitle>
          </div>
          <div className="flex gap-2">
            <Select value={period} onValueChange={setPeriod}>
              <SelectTrigger className="w-32">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Monthly">Monthly</SelectItem>
                <SelectItem value="Quarterly">Quarterly</SelectItem>
                <SelectItem value="Yearly">Yearly</SelectItem>
              </SelectContent>
            </Select>
            <Select value={count.toString()} onValueChange={(v) => setCount(parseInt(v))}>
              <SelectTrigger className="w-32">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="6">Last 6</SelectItem>
                <SelectItem value="12">Last 12</SelectItem>
                <SelectItem value="24">Last 24</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
        <CardDescription>
          Track performance metrics over time
        </CardDescription>
      </CardHeader>
      <CardContent>
        {loading ? (
          <div className="h-80 flex items-center justify-center text-gray-500">
            Loading trends...
          </div>
        ) : trends.length === 0 ? (
          <div className="h-80 flex items-center justify-center text-gray-500">
            No trend data available
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={400}>
            <LineChart data={trends} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="period"
                tick={{ fontSize: 12 }}
                angle={-45}
                textAnchor="end"
                height={80}
              />
              <YAxis
                tick={{ fontSize: 12 }}
                domain={[0, 100]}
                label={{ value: 'Percentage (%)', angle: -90, position: 'insideLeft' }}
              />
              <Tooltip
                contentStyle={{ backgroundColor: 'white', border: '1px solid #ccc', borderRadius: '8px' }}
                formatter={(value: number) => `${value.toFixed(1)}%`}
              />
              <Legend />
              <Line
                type="monotone"
                dataKey="onTimeDeliveryRate"
                stroke="#10b981"
                strokeWidth={2}
                name="On-Time Delivery"
                dot={{ r: 4 }}
                activeDot={{ r: 6 }}
              />
              <Line
                type="monotone"
                dataKey="qualityAcceptanceRate"
                stroke="#3b82f6"
                strokeWidth={2}
                name="Quality Acceptance"
                dot={{ r: 4 }}
                activeDot={{ r: 6 }}
              />
              <Line
                type="monotone"
                dataKey="overallScore"
                stroke="#8b5cf6"
                strokeWidth={2}
                name="Overall Score"
                dot={{ r: 4 }}
                activeDot={{ r: 6 }}
              />
            </LineChart>
          </ResponsiveContainer>
        )}
      </CardContent>
    </Card>
  );
}

