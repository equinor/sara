import { Button, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type {
  BenchmarkImageResult,
  BenchmarkPerImageColumn,
} from "../../../../api/benchmarkClient";
import StatusChip from "../../../../components/StatusChip";
import { formatMetricValue } from "../benchmarkFormatting";

const HeaderBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
`;

const HeaderGroup = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
`;

interface BenchmarkImageHeaderBarProps {
  runId: string;
  columns: BenchmarkPerImageColumn[];
  summary?: BenchmarkImageResult | null;
  hasPrev: boolean;
  hasNext: boolean;
  onBack: () => void;
  onPrev: () => void;
  onNext: () => void;
}

export default function BenchmarkImageHeaderBar({
  runId,
  columns,
  summary,
  hasPrev,
  hasNext,
  onBack,
  onPrev,
  onNext,
}: BenchmarkImageHeaderBarProps) {
  const summaryText = summary
    ? [
        ...columns.map(
          (col) => `${col.label}: ${formatMetricValue(summary.values[col.key], col.format)}`
        ),
        ...(summary.false_positive ? ["FP"] : []),
      ].join(" · ")
    : "";

  return (
    <HeaderBar>
      <HeaderGroup>
        <Button variant="ghost" onClick={onBack}>
          ← Back to {runId}
        </Button>
        {summary && (
          <StatusChip status={summary.passed ? "Succeeded" : "Failed"} />
        )}
        {summaryText && <Typography variant="body_short">{summaryText}</Typography>}
      </HeaderGroup>

      <HeaderGroup>
        <Button variant="outlined" disabled={!hasPrev} onClick={onPrev}>
          ← Previous
        </Button>
        <Button variant="outlined" disabled={!hasNext} onClick={onNext}>
          Next →
        </Button>
      </HeaderGroup>
    </HeaderBar>
  );
}
