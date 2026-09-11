import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
import styled from "styled-components";
import { getFeedbackHistory, type FeedbackHistory, type FeedbackParams } from "../../api/client";
import { useConfiguredAnalyses } from "../../api/queries";
import { feedbackTrendDetailsKey, useFeedbackSummary } from "../../api/dashboardQueries";
import DashboardWindowSelector from "../../components/DashboardWindowSelector";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import { ErrorText } from "../../components/Styles";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import FeedbackBreakdownPanel from "./feedback-components/FeedbackBreakdownPanel";
import FeedbackFilters from "./feedback-components/FeedbackFilters";
import FeedbackHistoryTable from "./feedback-components/FeedbackHistoryTable";
import FeedbackSummaryCards from "./feedback-components/FeedbackSummaryCards";
import FeedbackTrendPanel from "./feedback-components/FeedbackTrendPanel";

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

const Toolbar = styled.div`
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
  align-items: center;
  flex-wrap: wrap;
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

export default function FeedbackPage() {
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
        <DashboardWindowSelector
          aria-label="Dashboard time window"
          windows={FEEDBACK_WINDOWS}
          windowHours={windowHours}
          onChange={setWindowHours}
        />
        <Typography variant="caption" style={{ color: tokens.colors.text.static_icons__tertiary.hex }}>
          Trends use analysis run start dates
        </Typography>
      </Toolbar>

      {summaryError && <ErrorText role="alert">{summaryError.message}</ErrorText>}
      <FeedbackSummaryCards summary={summary} loading={summaryLoading} />

      <DashboardGrid>
        <FeedbackTrendPanel
          summary={summary}
          loading={summaryLoading}
          windowHours={windowHours}
          analysisType={filters.analysisType}
          timeZone={timeZone}
        />
        <FeedbackBreakdownPanel summary={summary} loading={summaryLoading} />
      </DashboardGrid>

      <Typography variant="h4">Feedback history</Typography>
      <Typography variant="caption" style={{ color: tokens.colors.text.static_icons__tertiary.hex }}>
        Submitted verdicts only. Date filters use the analysis run start date.
      </Typography>
      <FeedbackFilters
        filters={filters}
        setFilters={setFilters}
        analysisTypes={analysisTypes}
        configuredAnalysesError={configuredAnalyses.error}
      />
      {error && <ErrorText>{error}</ErrorText>}
      <FeedbackHistoryTable items={items} initialLoading={initialLoading} pageSize={pageSize} />

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
