import { Button, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { BenchmarkImageResult } from "../../../../api/benchmarkClient";
import StatusChip from "../../../../components/StatusChip";
import { formatNumber, formatPercent, isImagePassed } from "../benchmarkFormatting";

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
  summary?: BenchmarkImageResult | null;
  hasPrev: boolean;
  hasNext: boolean;
  onBack: () => void;
  onPrev: () => void;
  onNext: () => void;
}

export default function BenchmarkImageHeaderBar({
  runId,
  summary,
  hasPrev,
  hasNext,
  onBack,
  onPrev,
  onNext,
}: BenchmarkImageHeaderBarProps) {
  return (
    <HeaderBar>
      <HeaderGroup>
        <Button variant="ghost" onClick={onBack}>
          ← Back to {runId}
        </Button>
        {summary && (
          <StatusChip status={isImagePassed(summary) ? "Succeeded" : "Failed"} />
        )}
        {summary && (
          <Typography variant="body_short">
            People: {summary.covered_people}/{summary.gt_people} · Min coverage:{" "}
            {formatPercent(summary.min_person_coverage)} · Bg masked:{" "}
            {formatPercent(summary.background_masked_fraction)} · IoU:{" "}
            {formatNumber(summary.mean_iou, 3)}
            {summary.false_positive ? " · FP" : ""}
          </Typography>
        )}
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
