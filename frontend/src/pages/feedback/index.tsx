import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { Button, Chip, Table, Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import {
  getFeedbackHistory,
  type FeedbackHistory,
  type FeedbackParams,
} from "../../api/client";
import { useConfiguredAnalyses } from "../../api/queries";
import { feedbackTrendDetailsKey, useFeedbackSummary } from "../../api/dashboardQueries";
import FeedbackChip from "../../components/FeedbackChip";
import FeedbackTrendChart from "../../components/FeedbackTrendChart";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatCard from "../../components/StatCard";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";

const FILTER_KEYS: (keyof FeedbackParams & string)[] = [
  "analysisType",
  "isCorrect",
  "startedSince",
  "startedUntil",
];

const FEEDBACK_WINDOWS = [
  { hours: 168, label: "7d" },
  { hours: 720, label: "30d" },
  { hours: 2160, label: "90d" },
] as const;

const CardRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
`;

const Toolbar = styled.div`
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
  align-items: center;
  flex-wrap: wrap;
`;

const WindowToggle = styled.div`
  display: flex;
  gap: 0.5rem;
  align-items: center;
`;

const DashboardGrid = styled.div`
  display: grid;
  grid-template-columns: minmax(0, 1.5fr) minmax(320px, 1fr);
  gap: 1rem;
  margin-bottom: 1.5rem;
  align-items: start;

  @media (max-width: 900px) {
    grid-template-columns: 1fr;
  }
`;

const Panel = styled.section`
  min-width: 0;
  padding: 0.75rem;
  border: 1px solid #dcdcdc;
  border-radius: 4px;
  background: #ffffff;
`;

const SectionTitle = styled(Typography).attrs({ variant: "caption" })`
  display: block;
  margin-bottom: 0.5rem;
  color: #565656;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
`;

const FilterGrid = styled.div`
  display: grid;
  grid-template-columns: minmax(150px, 1fr) minmax(140px, 0.75fr) minmax(185px, 1fr) minmax(185px, 1fr) auto;
  gap: 0.75rem;
  margin: 0.75rem 0 1rem;
  align-items: end;

  @media (max-width: 950px) {
    grid-template-columns: 1fr 1fr;
  }

  @media (max-width: 560px) {
    grid-template-columns: 1fr;
  }
`;

const FilterField = styled.label`
  display: grid;
  gap: 0.3rem;
  min-width: 0;
  color: #3d3d3d;
  font-size: 0.75rem;
  font-weight: 600;
`;

const Control = styled.select`
  width: 100%;
  min-height: 42px;
  padding: 0.5rem 0.65rem;
  border: 1px solid #6f6f6f;
  border-radius: 2px;
  background: #ffffff;
  color: #3d3d3d;
  font: inherit;
`;

const DateInput = styled.input<{ $invalid?: boolean }>`
  width: 100%;
  min-height: 42px;
  box-sizing: border-box;
  padding: 0.5rem 0.65rem;
  border: 1px solid ${(p) => (p.$invalid ? "#eb0000" : "#6f6f6f")};
  border-radius: 2px;
  background: #ffffff;
  color: #3d3d3d;
  font: inherit;
`;

const TableScroller = styled.div`
  overflow-x: auto;
`;

function formatAnalysisType(analysisType: string): string {
  switch (analysisType.toLowerCase()) {
    case "fencilla": return "Fence detection";
    case "thermal-reading": return "Thermal reading";
    case "cloe": return "CLOE";
    case "co2": return "CO2";
    default: return analysisType;
  }
}

function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`;
}

function parseDate(value: string | undefined): Date | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatDateInput(value: Date | null): string {
  if (!value) return "";
  const offset = value.getTimezoneOffset() * 60_000;
  return new Date(value.getTime() - offset).toISOString().slice(0, 16);
}

export default function FeedbackPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [windowHours, setWindowHours] = useState(720);
  const configuredAnalyses = useConfiguredAnalyses();
  const analysisTypes = (configuredAnalyses.data ?? []).map((item) => item.name).sort();
  const {
    response,
    loading,
    initialLoading,
    error,
    pageNumber,
    pageSize,
    filters,
    setPage,
    setPageSize,
    setFilters,
    refetch,
  } = usePagedList<FeedbackHistory, FeedbackParams>(
    "feedback",
    "feedback.pageSize",
    FILTER_KEYS,
    getFeedbackHistory
  );

  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const summaryQuery = useFeedbackSummary(windowHours, filters.analysisType, timeZone);
  const { data: summary, isPending: summaryLoading, error: summaryError } = summaryQuery;

  const startedSince = parseDate(filters.startedSince);
  const startedUntil = parseDate(filters.startedUntil);
  const invalidRange = startedSince !== null && startedUntil !== null && startedSince > startedUntil;
  const hasFilters = Object.values(filters).some(Boolean);
  const items = response?.items ?? [];
  const refresh = async () => {
    await Promise.all([
      summaryQuery.refetch(),
      refetch(),
      queryClient.invalidateQueries({ queryKey: feedbackTrendDetailsKey }),
    ]);
  };

  return (
    <PageHeader title="Feedback" loading={loading || summaryQuery.isFetching} onRefresh={refresh}>
      <Toolbar>
        <WindowToggle aria-label="Dashboard time window">
          {FEEDBACK_WINDOWS.map((item) => (
            <Chip
              key={item.hours}
              variant={windowHours === item.hours ? "active" : "default"}
              onClick={() => setWindowHours(item.hours)}
              style={{ cursor: "pointer" }}
            >
              {item.label}
            </Chip>
          ))}
        </WindowToggle>
        <Typography variant="caption" style={{ color: "#6f6f6f" }}>
          Trends use analysis run start dates
        </Typography>
      </Toolbar>

      {summaryError && <Typography role="alert" style={{ color: "#eb0000" }}>{summaryError.message}</Typography>}

      <CardRow>
        <StatCard
          title="Correctness"
          value={
            summaryLoading
              ? "…"
              : summary?.reviewed
                ? formatPercent(summary.correctnessRate)
                : "–"
          }
          tone="success"
          subtitle="of reviewed runs"
        />
        <StatCard
          title="Reviewed"
          value={summaryLoading ? "…" : summary?.reviewed.toLocaleString() ?? "–"}
          tone="info"
          subtitle={summary ? `${formatPercent(summary.reviewRate)} review coverage` : undefined}
        />
        <StatCard title="Correct" value={summaryLoading ? "…" : summary?.correct ?? "–"} tone="success" />
        <StatCard title="Incorrect" value={summaryLoading ? "…" : summary?.incorrect ?? "–"} tone="error" />
      </CardRow>

      <DashboardGrid>
        <Panel>
          <SectionTitle>Correctness trend</SectionTitle>
          {summaryLoading ? (
            <Typography variant="body_short">Loading trend…</Typography>
          ) : summary ? (
            <FeedbackTrendChart
              key={`${windowHours}-${filters.analysisType ?? ""}`}
              data={summary.trend}
              windowHours={windowHours}
              analysisType={filters.analysisType}
              timeZone={timeZone}
              formatAnalysisType={formatAnalysisType}
            />
          ) : null}
        </Panel>
        <Panel>
          <SectionTitle>Breakdown by analysis type</SectionTitle>
          <Table style={{ width: "100%" }}>
            <Table.Head>
              <Table.Row>
                <Table.Cell>Analysis</Table.Cell>
                <Table.Cell>Reviewed</Table.Cell>
                <Table.Cell>Correctness</Table.Cell>
                <Table.Cell>Coverage</Table.Cell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {summaryLoading ? (
                <TableSkeleton columns={4} rows={3} />
              ) : !summary ? (
                <Table.Row><Table.Cell colSpan={4}>Summary unavailable.</Table.Cell></Table.Row>
              ) : summary.perAnalysisType.length === 0 ? (
                <Table.Row><Table.Cell colSpan={4}>No runs in this window.</Table.Cell></Table.Row>
              ) : summary?.perAnalysisType.map((stat) => (
                <Table.Row key={stat.analysisType}>
                  <Table.Cell>{formatAnalysisType(stat.analysisType)}</Table.Cell>
                  <Table.Cell>{stat.reviewed}</Table.Cell>
                  <Table.Cell>{formatPercent(stat.correctnessRate)}</Table.Cell>
                  <Table.Cell>{formatPercent(stat.reviewRate)}</Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </Panel>
      </DashboardGrid>

      <Typography variant="h4">Feedback history</Typography>
      <Typography variant="caption" style={{ color: "#6f6f6f" }}>
        Submitted verdicts only. Date filters use the analysis run start date.
      </Typography>
      <FilterGrid>
        <FilterField>
          Analysis
          <Control
            value={filters.analysisType ?? ""}
            onChange={(event) => setFilters({ analysisType: event.target.value || undefined })}
          >
            <option value="">All analyses</option>
            {analysisTypes.map((type) => <option key={type} value={type}>{formatAnalysisType(type)}</option>)}
          </Control>
        </FilterField>
        <FilterField>
          Result
          <Control
            value={filters.isCorrect ?? ""}
            onChange={(event) => setFilters({ isCorrect: event.target.value || undefined })}
          >
            <option value="">All results</option>
            <option value="true">Correct</option>
            <option value="false">Incorrect</option>
          </Control>
        </FilterField>
        <FilterField>
          Started since
          <DateInput
            type="datetime-local"
            value={formatDateInput(startedSince)}
            onChange={(event) => setFilters({ startedSince: event.target.value ? new Date(event.target.value).toISOString() : undefined })}
          />
        </FilterField>
        <FilterField>
          Started until
          <DateInput
            type="datetime-local"
            value={formatDateInput(startedUntil)}
            $invalid={invalidRange}
            aria-invalid={invalidRange}
            onChange={(event) => setFilters({ startedUntil: event.target.value ? new Date(event.target.value).toISOString() : undefined })}
          />
        </FilterField>
        {hasFilters && (
          <Button
            variant="ghost"
            onClick={() => setFilters({ analysisType: undefined, isCorrect: undefined, startedSince: undefined, startedUntil: undefined })}
          >
            Clear filters
          </Button>
        )}
      </FilterGrid>
      {configuredAnalyses.error && (
        <Typography variant="body_short" role="alert" style={{ color: "#eb0000" }}>
          Failed to load configured analyses: {configuredAnalyses.error.message}
        </Typography>
      )}
      {invalidRange && <Typography style={{ color: "#eb0000" }}>Started until must not be earlier than started since.</Typography>}
      {error && <Typography style={{ color: "#eb0000" }}>{error}</Typography>}

      <TableScroller>
        <Table style={{ width: "100%", minWidth: "850px" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>Result</Table.Cell>
              <Table.Cell>Analysis</Table.Cell>
              <Table.Cell>Run #</Table.Cell>
              <Table.Cell>Started</Table.Cell>
              <Table.Cell>Action</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {initialLoading ? (
              <TableSkeleton columns={5} rows={pageSize} />
            ) : items.length === 0 ? (
              <Table.Row><Table.Cell colSpan={5}>No submitted feedback matches these filters.</Table.Cell></Table.Row>
            ) : items.map((item) => (
              <Table.Row
                key={item.id}
                onClick={() => navigate(`/analysis-runs/${item.analysisRunId}`)}
                style={{ cursor: "pointer" }}
              >
                <Table.Cell><FeedbackChip isCorrect={item.isCorrect} /></Table.Cell>
                <Table.Cell>{formatAnalysisType(item.analysisType)}</Table.Cell>
                <Table.Cell>{item.runNumber}</Table.Cell>
                <Table.Cell>{item.startedAt ? new Date(item.startedAt).toLocaleString() : "–"}</Table.Cell>
                <Table.Cell><Button variant="ghost">View run</Button></Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      </TableScroller>

      <PaginationFooter
        hasResponse={response !== null}
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={response?.totalCount ?? null}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        disabled={loading}
        loading={loading}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
        resetKey={`${pageSize}-${pageNumber}`}
      />
    </PageHeader>
  );
}
