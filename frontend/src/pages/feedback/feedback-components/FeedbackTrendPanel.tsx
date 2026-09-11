import { Typography } from "@equinor/eds-core-react";
import type { FeedbackSummary } from "../../../api/client";
import DashboardPanel from "../../../components/DashboardPanel";
import FeedbackTrendChart from "../../../components/FeedbackTrendChart";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";

export default function FeedbackTrendPanel({ summary, loading, windowHours, analysisType, timeZone }: {
  summary: FeedbackSummary | undefined;
  loading: boolean;
  windowHours: number;
  analysisType: string | undefined;
  timeZone: string;
}) {
  return (
    <DashboardPanel title="Correctness trend" variant="feedback">
      {loading ? (
        <Typography variant="body_short">{"Loading trend\u2026"}</Typography>
      ) : summary ? (
        <FeedbackTrendChart
          key={`${windowHours}-${analysisType ?? ""}`}
          data={summary.trend}
          windowHours={windowHours}
          analysisType={analysisType}
          timeZone={timeZone}
          formatAnalysisType={formatAnalysisType}
        />
      ) : null}
    </DashboardPanel>
  );
}
