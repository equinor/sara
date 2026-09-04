import { useEffect, useRef, useState } from "react";
import { Popover, PopoverContent, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { TrendBucket, TrendBucketDetails } from "../api/client";

const SUCCEEDED_COLOR = "#4bb748";
const FAILED_COLOR = "#eb0000";

const Wrapper = styled.div`
  width: 100%;
`;

const Chart = styled.div`
  position: relative;
  padding: 0.25rem 0 0.5rem;
`;

const ChartTrack = styled.div<{ $bucketCount: number }>`
  display: grid;
  grid-template-columns: repeat(${(p) => p.$bucketCount}, minmax(0, 1fr));
  align-items: stretch;
  width: 100%;
`;

const BucketButton = styled.button<{ $selected: boolean; $empty: boolean }>`
  min-width: 0;
  padding: 0 2px;
  border: 0;
  border-radius: 2px;
  background: ${(p) => (p.$selected ? "#e6f3f3" : "transparent")};
  color: inherit;
  cursor: ${(p) => (p.$empty ? "default" : "pointer")};

  &:hover,
  &:focus-visible {
    background: #e6f3f3;
    outline: 2px solid #007079;
    outline-offset: -2px;
  }
`;

const BarArea = styled.span<{ $height: number }>`
  height: ${(p) => p.$height}px;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  background: linear-gradient(to top, #e8e8e8 1px, transparent 1px);
`;

const Bar = styled.span<{ $height: number; $color: string }>`
  display: block;
  min-height: ${(p) => (p.$height > 0 ? 2 : 0)}px;
  height: ${(p) => p.$height}px;
  background: ${(p) => p.$color};
`;

const BucketLabel = styled.span`
  display: block;
  margin-top: 0.35rem;
  font-size: clamp(0.5rem, 0.7vw, 0.68rem);
  line-height: 1.1;
  color: #565656;
  overflow: hidden;
  text-overflow: clip;
  white-space: nowrap;

  @media (max-width: 700px) {
    height: 2.25rem;
    overflow: visible;
    transform: rotate(-55deg);
    transform-origin: top center;
  }
`;

const DetailGrid = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem 1.25rem;
  margin-top: 0.35rem;
`;

const BucketPopover = styled(Popover)`
  min-width: 240px;
  max-width: 360px;
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
  loadDetails: (bucketStart: string) => Promise<TrendBucketDetails>;
  formatAnalysisType: (analysisType: string) => string;
  onBucketClick: (bucket: TrendBucket) => void;
  height?: number;
}

export default function TrendChart({
  data,
  hourly,
  loadDetails,
  formatAnalysisType,
  onBucketClick,
  height = 110,
}: Props) {
  const [selectedBucket, setSelectedBucket] = useState<string | null>(null);
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [details, setDetails] = useState<Record<string, TrendBucketDetails>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const loading = useRef(new Set<string>());
  const hoverTimer = useRef<number | null>(null);
  const max = Math.max(1, ...data.map((bucket) => bucket.succeeded + bucket.failed));

  if (data.length === 0) {
    return (
      <Typography variant="body_short" style={{ color: "#6f6f6f" }}>
        No data in this window.
      </Typography>
    );
  }

  const formatLabel = (iso: string) => {
    const date = new Date(iso);
    return hourly
      ? date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
      : date.toLocaleDateString([], { month: "short", day: "numeric" });
  };

  const formatRange = (start: string, end: string) => {
    const startDate = new Date(start);
    const endDate = new Date(end);
    if (hourly) {
      return `${startDate.toLocaleDateString()} ${startDate.toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
      })} – ${endDate.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
    }
    return startDate.toLocaleDateString([], { dateStyle: "medium" });
  };

  useEffect(
    () => () => {
      if (hoverTimer.current !== null) window.clearTimeout(hoverTimer.current);
    },
    []
  );

  const activateBucket = (bucketStart: string, anchor: HTMLElement) => {
    setSelectedBucket(bucketStart);
    setAnchorEl(anchor);
    if (details[bucketStart] || loading.current.has(bucketStart)) return;

    loading.current.add(bucketStart);
    loadDetails(bucketStart)
      .then((result) => setDetails((current) => ({ ...current, [bucketStart]: result })))
      .catch((error) =>
        setErrors((current) => ({
          ...current,
          [bucketStart]: error instanceof Error ? error.message : "Failed to load details",
        }))
      )
      .finally(() => loading.current.delete(bucketStart));
  };

  const activateBucketAfterDelay = (bucketStart: string, anchor: HTMLElement) => {
    if (hoverTimer.current !== null) window.clearTimeout(hoverTimer.current);
    hoverTimer.current = window.setTimeout(() => activateBucket(bucketStart, anchor), 150);
  };

  const closePopover = () => {
    if (hoverTimer.current !== null) window.clearTimeout(hoverTimer.current);
    hoverTimer.current = null;
    setAnchorEl(null);
    setSelectedBucket(null);
  };

  const selected = data.find((bucket) => bucket.bucketStart === selectedBucket);
  const selectedDetails = selectedBucket ? details[selectedBucket] : undefined;

  return (
    <Wrapper>
      <Chart onMouseLeave={closePopover}>
        <ChartTrack $bucketCount={data.length}>
          {data.map((bucket) => {
            const empty = bucket.succeeded + bucket.failed === 0;
            const succeededHeight = (bucket.succeeded / max) * height;
            const failedHeight = (bucket.failed / max) * height;
            return (
              <BucketButton
                key={bucket.bucketStart}
                type="button"
                $selected={selectedBucket === bucket.bucketStart}
                $empty={empty}
                onMouseEnter={(event) =>
                  activateBucketAfterDelay(bucket.bucketStart, event.currentTarget)
                }
                onFocus={(event) => activateBucket(bucket.bucketStart, event.currentTarget)}
                onClick={() => {
                  if (!empty) onBucketClick(bucket);
                }}
                aria-label={`${formatRange(bucket.bucketStart, bucket.bucketEnd)}: ${bucket.succeeded} succeeded, ${bucket.failed} failed`}
              >
                <BarArea $height={height}>
                  <Bar $height={failedHeight} $color={FAILED_COLOR} />
                  <Bar $height={succeededHeight} $color={SUCCEEDED_COLOR} />
                </BarArea>
                <BucketLabel>{formatLabel(bucket.bucketStart)}</BucketLabel>
              </BucketButton>
            );
          })}
        </ChartTrack>
        <BucketPopover
          open={selected !== undefined && anchorEl !== null}
          anchorEl={anchorEl}
          placement="top"
          onClose={closePopover}
          onMouseEnter={() => {
            if (hoverTimer.current !== null) window.clearTimeout(hoverTimer.current);
          }}
          onMouseLeave={closePopover}
          aria-live="polite"
        >
          <PopoverContent>
            {selected && (
              <>
            <Typography variant="caption" style={{ fontWeight: 600 }}>
              {formatRange(selected.bucketStart, selected.bucketEnd)} · {selected.succeeded} succeeded, {selected.failed} failed
            </Typography>
            {selectedBucket && errors[selectedBucket] ? (
              <Typography variant="caption" style={{ color: FAILED_COLOR, display: "block" }}>
                {errors[selectedBucket]}
              </Typography>
            ) : !selectedDetails ? (
              <Typography variant="caption" style={{ display: "block" }}>
                Loading analysis breakdown…
              </Typography>
            ) : selectedDetails.perAnalysisType.length === 0 ? (
              <Typography variant="caption" style={{ display: "block" }}>
                No completed analyses in this bucket.
              </Typography>
            ) : (
              <DetailGrid>
                {selectedDetails.perAnalysisType.map((stat) => (
                  <Typography key={stat.analysisType} variant="caption">
                    <strong>{formatAnalysisType(stat.analysisType)}</strong>: {stat.succeeded} succeeded, {stat.failed} failed
                  </Typography>
                ))}
              </DetailGrid>
            )}
              </>
            )}
          </PopoverContent>
        </BucketPopover>
      </Chart>

      <Legend>
        <Typography variant="caption">
          <Swatch $color={SUCCEEDED_COLOR} />
          Succeeded analyses
        </Typography>
        <Typography variant="caption">
          <Swatch $color={FAILED_COLOR} />
          Failed analyses
        </Typography>
      </Legend>
    </Wrapper>
  );
}
