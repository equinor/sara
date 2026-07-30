import { useMemo } from "react";
import { Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { TrendBucket } from "../api/client";

const SUCCEEDED_COLOR = "#4bb748";
const FAILED_COLOR = "#eb0000";

const Wrapper = styled.div`
  width: 100%;
`;

const Legend = styled.div`
  display: flex;
  gap: 1rem;
  margin-top: 0.5rem;
  align-items: center;
`;

const Swatch = styled.span<{ $color: string }>`
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 2px;
  background: ${(p) => p.$color};
  margin-right: 0.35rem;
`;

interface Props {
  data: TrendBucket[];
  hourly: boolean;
  height?: number;
}

/**
 * Stacked bar chart (succeeded + failed per time bucket) rendered as inline SVG
 * using a 0..100 viewBox so it scales fluidly to its container width.
 */
export default function TrendChart({ data, hourly, height = 96 }: Props) {
  const max = useMemo(
    () => Math.max(1, ...data.map((d) => d.succeeded + d.failed)),
    [data]
  );

  if (data.length === 0) {
    return (
      <Typography variant="body_short" style={{ color: "#6f6f6f" }}>
        No data in this window.
      </Typography>
    );
  }

  const gap = 0.2;
  const slot = 100 / data.length;
  const barWidth = slot - gap;

  const fmt = (iso: string) => {
    const d = new Date(iso);
    return hourly
      ? d.toLocaleTimeString([], { hour: "2-digit" })
      : d.toLocaleDateString([], { month: "short", day: "numeric" });
  };

  return (
    <Wrapper>
      <svg
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        style={{ width: "100%", height, display: "block" }}
        role="img"
        aria-label="Workflow success and failure trend"
      >
        {data.map((d, i) => {
          const x = i * slot + gap / 2;
          const succH = (d.succeeded / max) * 100;
          const failH = (d.failed / max) * 100;
          const total = d.succeeded + d.failed;
          return (
            <g key={d.bucketStart}>
              <rect
                x={x}
                y={100 - succH}
                width={barWidth}
                height={succH}
                fill={SUCCEEDED_COLOR}
              />
              <rect
                x={x}
                y={100 - succH - failH}
                width={barWidth}
                height={failH}
                fill={FAILED_COLOR}
              />
              <title>{`${fmt(d.bucketStart)} — ${d.succeeded} succeeded, ${d.failed} failed (${total} total)`}</title>
            </g>
          );
        })}
      </svg>
      <Legend>
        <Typography variant="caption">
          <Swatch $color={SUCCEEDED_COLOR} />
          Succeeded
        </Typography>
        <Typography variant="caption">
          <Swatch $color={FAILED_COLOR} />
          Failed
        </Typography>
        <Typography variant="caption" style={{ marginLeft: "auto", color: "#6f6f6f" }}>
          {fmt(data[0].bucketStart)} – {fmt(data[data.length - 1].bucketStart)}
        </Typography>
      </Legend>
    </Wrapper>
  );
}
