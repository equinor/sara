import { useNavigate } from "react-router";
import type { TrendBucket } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import TrendChart from "../../../components/TrendChart";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";

export default function OverviewTrendPanel({ trend, windowHours, timeZone }: {
  trend: TrendBucket[];
  windowHours: number;
  timeZone: string;
}) {
  const navigate = useNavigate();
  return (
    <DashboardPanel title="Succeeded vs Failed over time" style={{ marginBottom: "1rem" }}>
      <TrendChart
        key={windowHours}
        data={trend}
        hourly={windowHours <= 24}
        windowHours={windowHours}
        timeZone={timeZone}
        formatAnalysisType={formatAnalysisType}
        onBucketClick={(bucket) => {
          const inclusiveEnd = new Date(
            new Date(bucket.bucketEnd).getTime() - 1
          ).toISOString();
          const query = new URLSearchParams({
            pageSize: "25",
            page: "1",
            startedSince: bucket.bucketStart,
            startedUntil: inclusiveEnd,
          });
          navigate(`/analysis-runs?${query}`);
        }}
      />
    </DashboardPanel>
  );
}
