import { useQuery } from "@tanstack/react-query";
import {
  getAnalysisRuns,
  getDashboardSummary,
  getFeedbackSummary,
  getFeedbackTrendBucketDetails,
  getTrendBucketDetails,
  getWorkflows,
} from "./client";

export const dashboardTrendDetailsKey = ["sara", "dashboard", "trend-details"] as const;
export const feedbackTrendDetailsKey = ["sara", "feedback-trend-details"] as const;

export function useOverview(windowHours: number, timeZone: string) {
  return useQuery({
    queryKey: ["sara", "dashboard", "overview", { windowHours, timeZone }],
    queryFn: async ({ signal }) => {
      const startedSince = new Date(Date.now() - windowHours * 60 * 60 * 1000).toISOString();
      const [summary, latestRuns, failures] = await Promise.all([
        getDashboardSummary(windowHours, timeZone, signal),
        getAnalysisRuns(1, 5, {}, signal),
        getWorkflows(1, 5, { status: "Failed", startedSince }, signal),
      ]);
      return { summary, latestRuns: latestRuns.items, failures: failures.items };
    },
    staleTime: 0,
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: "always",
  });
}

export function useFeedbackSummary(windowHours: number, analysisType: string | undefined, timeZone: string) {
  return useQuery({
    queryKey: ["sara", "feedback-summary", { windowHours, analysisType, timeZone }],
    queryFn: ({ signal }) => getFeedbackSummary(windowHours, analysisType, timeZone, signal),
    staleTime: 0,
    refetchInterval: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}

export function useTrendBucketDetails(bucketStart: string | null, windowHours: number, timeZone: string) {
  return useQuery({
    queryKey: [...dashboardTrendDetailsKey, { windowHours, bucketStart, timeZone }],
    queryFn: ({ signal }) => getTrendBucketDetails(bucketStart!, windowHours, timeZone, signal),
    enabled: bucketStart !== null,
    staleTime: 0,
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: "always",
  });
}

export function useFeedbackTrendBucketDetails(
  bucketStart: string | null,
  windowHours: number,
  analysisType: string | undefined,
  timeZone: string
) {
  return useQuery({
    queryKey: [...feedbackTrendDetailsKey, { windowHours, bucketStart, analysisType, timeZone }],
    queryFn: ({ signal }) =>
      getFeedbackTrendBucketDetails(bucketStart!, windowHours, analysisType, timeZone, signal),
    enabled: bucketStart !== null,
    staleTime: 0,
    refetchInterval: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
}
