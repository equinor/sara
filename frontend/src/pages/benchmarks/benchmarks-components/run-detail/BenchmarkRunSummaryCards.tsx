import styled from "styled-components";
import type {
  BenchmarkEvaluation,
  BenchmarkMetrics,
} from "../../../../api/benchmarkClient";
import StatCard from "../../../../components/StatCard";
import {
  formatNumber,
  formatPercent,
  summarizeBinaryEvaluation,
} from "../benchmarkFormatting";

const StatsGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.75rem;
  margin-bottom: 1rem;
`;

interface BenchmarkRunSummaryCardsProps {
  metrics: BenchmarkMetrics;
  primaryEvaluation?: BenchmarkEvaluation;
}

export default function BenchmarkRunSummaryCards({
  metrics,
  primaryEvaluation,
}: BenchmarkRunSummaryCardsProps) {
  const summary = primaryEvaluation
    ? summarizeBinaryEvaluation(primaryEvaluation)
    : null;

  return (
    <StatsGrid>
      <StatCard
        title="Throughput"
        value={`${formatNumber(metrics.processing.per_second, 2)} ${metrics.processing.unit}/s`}
        subtitle={`${metrics.processing.count} in ${formatNumber(metrics.processing.seconds, 1)}s`}
      />
      <StatCard
        title="Precision"
        value={summary ? formatPercent(summary.precision) : "–"}
        subtitle={summary ? `TP ${summary.tp} / FP ${summary.fp}` : undefined}
      />
      <StatCard
        title="Recall"
        value={summary ? formatPercent(summary.recall) : "–"}
        subtitle={summary ? `TP ${summary.tp} / FN ${summary.fn}` : undefined}
      />
      <StatCard
        title="Mean IoU"
        value={primaryEvaluation ? formatNumber(primaryEvaluation.mean_iou, 3) : "–"}
        subtitle={
          primaryEvaluation ? `${primaryEvaluation.matched_count} matched` : undefined
        }
      />
    </StatsGrid>
  );
}
