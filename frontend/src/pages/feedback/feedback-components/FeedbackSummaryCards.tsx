import styled from "styled-components";
import type { FeedbackSummary } from "../../../api/client";
import StatCard from "../../../components/StatCard";

const CardRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
`;

export default function FeedbackSummaryCards({ summary, loading }: {
  summary: FeedbackSummary | undefined;
  loading: boolean;
}) {
  return (
    <CardRow>
      <StatCard
        title="Correctness"
        value={loading ? "\u2026" : summary?.reviewed ? `${Math.round(summary.correctnessRate * 100)}%` : "\u2013"}
        tone="success"
        subtitle="of reviewed runs"
      />
      <StatCard
        title="Reviewed"
        value={loading ? "\u2026" : summary?.reviewed.toLocaleString() ?? "\u2013"}
        tone="info"
        subtitle={summary ? `${Math.round(summary.reviewRate * 100)}% review coverage` : undefined}
      />
      <StatCard title="Correct" value={loading ? "\u2026" : summary?.correct ?? "\u2013"} tone="success" />
      <StatCard title="Incorrect" value={loading ? "\u2026" : summary?.incorrect ?? "\u2013"} tone="error" />
    </CardRow>
  );
}
