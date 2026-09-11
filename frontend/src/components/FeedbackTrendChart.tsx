import { useEffect, useRef, useState } from "react";
import { Popover, PopoverContent, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import type { FeedbackTrendBucket } from "../api/client";
import { useFeedbackTrendBucketDetails } from "../api/dashboardQueries";

const CORRECT_COLOR = "#4bb748";
const INCORRECT_COLOR = "#eb0000";

const Wrapper = styled.div`
  width: 100%;
`;

const ChartWrapper = styled.div`
  position: relative;
`;

const Chart = styled.svg`
  display: block;
  width: 100%;
  height: auto;
  margin-top: 0.5rem;
`;

const GridLine = styled.line`
  stroke: #e8e8e8;
  stroke-width: 1;
`;

const AxisLabel = styled.text`
  fill: #565656;
  font-size: 11px;
`;

const TrendLine = styled.polyline<{ $color: string; $dashed?: boolean }>`
  fill: none;
  stroke: ${(p) => p.$color};
  stroke-opacity: 0.7;
  stroke-width: 2.5;
  stroke-dasharray: ${(p) => (p.$dashed ? "7 5" : "none")};
  stroke-linecap: round;
  stroke-linejoin: round;
`;

const CorrectPoint = styled.circle`
  fill: #ffffff;
  stroke: ${CORRECT_COLOR};
  stroke-width: 2;
`;

const IncorrectPoint = styled.line`
  stroke: ${INCORRECT_COLOR};
  stroke-width: 2;
  stroke-linecap: round;
`;

const HitTarget = styled.button<{ $left: number; $width: number; $selected: boolean }>`
  position: absolute;
  top: 5%;
  bottom: 14%;
  left: ${(p) => p.$left}%;
  width: ${(p) => p.$width}%;
  min-width: 4px;
  padding: 0;
  border: 0;
  background: ${(p) => (p.$selected ? "rgba(0, 112, 121, 0.08)" : "transparent")};
  cursor: pointer;
  transform: translateX(-50%);

  &:hover,
  &:focus-visible {
    background: rgba(0, 112, 121, 0.08);
    outline: 2px solid #007079;
    outline-offset: -2px;
  }
`;

const BucketPopover = styled(Popover)<{
  $viewportWidth: number;
  $viewportHeight: number;
  $viewportLeft: number;
  $viewportTop: number;
}>`
  width: min(420px, calc(100vw - 2rem));
  max-height: calc(100dvh - 2rem);
  overflow-y: auto;

  @media (max-width: 700px) {
    position: fixed !important;
    inset: ${(p) => p.$viewportTop + 16}px auto auto
      ${(p) => p.$viewportLeft + 16}px !important;
    width: ${(p) => Math.max(0, p.$viewportWidth - 32)}px;
    max-width: none;
    max-height: ${(p) => Math.max(0, p.$viewportHeight - 32)}px;
    transform: none !important;
  }
`;

const PopoverHeading = styled.div`
  display: grid;
  gap: 0.2rem;
  margin-bottom: 0.5rem;
`;

const DetailTable = styled.table`
  width: 100%;
  border-collapse: collapse;
  font-size: 0.75rem;

  th,
  td {
    padding: 0.4rem 0.5rem;
    border-top: 1px solid #dcdcdc;
    text-align: right;
    white-space: nowrap;
  }

  th:first-child,
  td:first-child {
    max-width: 180px;
    overflow: hidden;
    text-align: left;
    text-overflow: ellipsis;
  }
`;

const Legend = styled.div`
  display: flex;
  gap: 1rem;
  margin-top: 0.75rem;
`;

const Swatch = styled.span<{ $color: string; $dashed?: boolean }>`
  display: inline-block;
  width: 22px;
  height: 0;
  margin-right: 0.35rem;
  border-top: 3px ${(p) => (p.$dashed ? "dashed" : "solid")} ${(p) => p.$color};
  vertical-align: middle;
`;

interface Props {
  data: FeedbackTrendBucket[];
  windowHours: number;
  analysisType?: string;
  timeZone: string;
  formatAnalysisType: (analysisType: string) => string;
}

export default function FeedbackTrendChart({ data, windowHours, analysisType, timeZone, formatAnalysisType }: Props) {
  const [selectedBucket, setSelectedBucket] = useState<string | null>(null);
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [viewport, setViewport] = useState(() => ({
    width: window.visualViewport?.width ?? window.innerWidth,
    height: window.visualViewport?.height ?? window.innerHeight,
    left: window.visualViewport?.offsetLeft ?? 0,
    top: window.visualViewport?.offsetTop ?? 0,
  }));
  const hoverTimer = useRef<number | null>(null);
  const hasReviewedRuns = data.some((bucket) => bucket.correct + bucket.incorrect > 0);
  const selected = data.find((bucket) => bucket.bucketStart === selectedBucket);
  const open = hasReviewedRuns && selected !== undefined && anchorEl !== null;
  const { data: selectedDetails, error, isPending } = useFeedbackTrendBucketDetails(
    open ? selected.bucketStart : null,
    windowHours,
    analysisType,
    timeZone
  );
  const width = 800;
  const height = 190;
  const margin = { top: 10, right: 12, bottom: 30, left: 34 };
  const plotWidth = width - margin.left - margin.right;
  const plotHeight = height - margin.top - margin.bottom;
  const max = Math.max(1, ...data.flatMap((bucket) => [bucket.correct, bucket.incorrect]));
  const x = (index: number) =>
    margin.left + (data.length <= 1 ? plotWidth / 2 : (index / (data.length - 1)) * plotWidth);
  const y = (value: number) => margin.top + plotHeight - (value / max) * plotHeight;
  const points = (value: (bucket: FeedbackTrendBucket) => number) =>
    data.map((bucket, index) => `${x(index)},${y(value(bucket))}`).join(" ");
  const yTicks = [...new Set([0, Math.ceil(max / 2), max])];
  const labelIndexes = new Set(
    Array.from({ length: Math.min(7, data.length) }, (_, index) =>
      Math.round((index * (data.length - 1)) / Math.max(1, Math.min(7, data.length) - 1))
    )
  );

  useEffect(
    () => () => {
      if (hoverTimer.current !== null) window.clearTimeout(hoverTimer.current);
    },
    []
  );

  useEffect(() => {
    const updateViewport = () =>
      setViewport({
        width: window.visualViewport?.width ?? window.innerWidth,
        height: window.visualViewport?.height ?? window.innerHeight,
        left: window.visualViewport?.offsetLeft ?? 0,
        top: window.visualViewport?.offsetTop ?? 0,
      });
    const visualViewport = window.visualViewport;
    window.addEventListener("resize", updateViewport);
    visualViewport?.addEventListener("resize", updateViewport);
    visualViewport?.addEventListener("scroll", updateViewport);
    return () => {
      window.removeEventListener("resize", updateViewport);
      visualViewport?.removeEventListener("resize", updateViewport);
      visualViewport?.removeEventListener("scroll", updateViewport);
    };
  }, []);

  const activateBucket = (bucketStart: string, anchor: HTMLElement) => {
    setSelectedBucket(bucketStart);
    setAnchorEl(anchor);
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

  const formatDate = (iso: string) =>
    new Date(iso).toLocaleDateString([], { dateStyle: "medium", timeZone });

  if (!hasReviewedRuns) {
    return <Typography variant="body_short">No reviewed runs in this window.</Typography>;
  }

  return (
    <Wrapper>
      <ChartWrapper onMouseLeave={closePopover}>
      <Chart viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Correct and incorrect feedback over time">
        {yTicks.map((value) => {
          const position = y(value);
          return (
            <g key={value}>
              <GridLine x1={margin.left} x2={width - margin.right} y1={position} y2={position} />
              <AxisLabel x={margin.left - 7} y={position + 4} textAnchor="end">{value}</AxisLabel>
            </g>
          );
        })}
        <TrendLine $color={CORRECT_COLOR} points={points((bucket) => bucket.correct)} />
        <TrendLine
          $color={INCORRECT_COLOR}
          $dashed
          points={points((bucket) => bucket.incorrect)}
        />
        {data.map((bucket, index) => (
          <g key={bucket.bucketStart}>
            <title>{`${formatDate(bucket.bucketStart)}: ${bucket.correct} correct, ${bucket.incorrect} incorrect`}</title>
            <CorrectPoint cx={x(index)} cy={y(bucket.correct)} r={3.5} />
            <IncorrectPoint
              x1={x(index) - 3.5}
              y1={y(bucket.incorrect) - 3.5}
              x2={x(index) + 3.5}
              y2={y(bucket.incorrect) + 3.5}
            />
            <IncorrectPoint
              x1={x(index) - 3.5}
              y1={y(bucket.incorrect) + 3.5}
              x2={x(index) + 3.5}
              y2={y(bucket.incorrect) - 3.5}
            />
            {labelIndexes.has(index) && (
              <AxisLabel x={x(index)} y={height - 7} textAnchor="middle">
                {new Date(bucket.bucketStart).toLocaleDateString([], { month: "short", day: "numeric", timeZone })}
              </AxisLabel>
            )}
          </g>
        ))}
      </Chart>
      {data.map((bucket, index) => (
        <HitTarget
          key={bucket.bucketStart}
          type="button"
          $left={(x(index) / width) * 100}
          $width={(plotWidth / Math.max(1, data.length) / width) * 100}
          $selected={selectedBucket === bucket.bucketStart}
          aria-label={`${formatDate(bucket.bucketStart)}: ${bucket.correct} correct, ${bucket.incorrect} incorrect`}
          aria-expanded={selectedBucket === bucket.bucketStart}
          onMouseEnter={(event) =>
            activateBucketAfterDelay(bucket.bucketStart, event.currentTarget)
          }
          onFocus={(event) => activateBucket(bucket.bucketStart, event.currentTarget)}
          onClick={(event) => activateBucket(bucket.bucketStart, event.currentTarget)}
        />
      ))}
      <BucketPopover
        $viewportWidth={viewport.width}
        $viewportHeight={viewport.height}
        $viewportLeft={viewport.left}
        $viewportTop={viewport.top}
        open={open}
        anchorEl={anchorEl}
        placement="bottom"
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
              <PopoverHeading>
                <Typography variant="caption" style={{ fontWeight: 600 }}>
                  {formatDate(selected.bucketStart)}
                </Typography>
                <Typography variant="caption">
                  {selected.correct} correct · {selected.incorrect} incorrect
                </Typography>
              </PopoverHeading>
              {error && (
                <Typography variant="caption" role="alert" style={{ color: INCORRECT_COLOR }}>
                  {error.message}
                </Typography>
              )}
              {isPending ? (
                <Typography variant="caption">Loading analysis breakdown…</Typography>
              ) : !selectedDetails ? null : selectedDetails.perAnalysisType.length === 0 ? (
                <Typography variant="caption">No reviewed analyses in this bucket.</Typography>
              ) : (
                <DetailTable>
                  <thead>
                    <tr>
                      <th>Analysis</th>
                      <th>Correct</th>
                      <th>Incorrect</th>
                      <th>Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedDetails.perAnalysisType.map((stat) => (
                      <tr key={stat.analysisType}>
                        <td title={formatAnalysisType(stat.analysisType)}>
                          {formatAnalysisType(stat.analysisType)}
                        </td>
                        <td>{stat.correct}</td>
                        <td>{stat.incorrect}</td>
                        <td>{stat.correct + stat.incorrect}</td>
                      </tr>
                    ))}
                  </tbody>
                </DetailTable>
              )}
            </>
          )}
        </PopoverContent>
      </BucketPopover>
      </ChartWrapper>
      <Legend>
        <Typography variant="caption"><Swatch $color={CORRECT_COLOR} />Correct</Typography>
        <Typography variant="caption"><Swatch $color={INCORRECT_COLOR} $dashed />Incorrect</Typography>
      </Legend>
    </Wrapper>
  );
}
