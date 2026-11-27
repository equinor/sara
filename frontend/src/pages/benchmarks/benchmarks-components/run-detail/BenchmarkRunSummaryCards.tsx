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
        title="Person Recall"
        value={
          primaryEvaluation ? formatPercent(primaryEvaluation.person_recall) : "–"
        }
        subtitle={
          primaryEvaluation
            ? `${primaryEvaluation.covered_people} / ${primaryEvaluation.total_people} people`
            : undefined
        }
      />
      <StatCard
        title="Avg Coverage"
        value={
          primaryEvaluation
            ? formatPercent(primaryEvaluation.mean_person_coverage)
            : "–"
        }
        subtitle={
          primaryEvaluation
            ? `Min avg ${formatPercent(primaryEvaluation.mean_min_person_coverage)}`
            : undefined
        }
      />
      <StatCard
        title="Bg Masked"
        value={
          primaryEvaluation
            ? formatPercent(primaryEvaluation.mean_background_masked_fraction)
            : "–"
        }
        subtitle={summary ? `FP ${summary.fp} / TN ${summary.tn}` : undefined}
      />
      <StatCard
        title="Mean IoU"
        value={primaryEvaluation ? formatNumber(primaryEvaluation.mean_iou, 3) : "–"}
        subtitle={
          summary ? `Images TP ${summary.tp} / FN ${summary.fn}` : undefined
        }
      />
    </StatsGrid>
  );
}
