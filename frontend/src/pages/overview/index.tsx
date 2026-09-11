import { tokens } from "@equinor/eds-tokens";
import { useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router";
import { Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import { DASHBOARD_WINDOWS, retryWorkflow } from "../../api/client";
import { useResourceMutation } from "../../api/queries";
import { dashboardTrendDetailsKey, useOverview } from "../../api/dashboardQueries";
import DashboardWindowSelector from "../../components/DashboardWindowSelector";
import PageHeader from "../../components/PageHeader";
import { ErrorText } from "../../components/Styles";
import LatestAnalysisRunsPanel from "./overview-components/LatestAnalysisRunsPanel";
import OverviewBreakdownPanel from "./overview-components/OverviewBreakdownPanel";
import OverviewSummaryCards from "./overview-components/OverviewSummaryCards";
import OverviewTrendPanel from "./overview-components/OverviewTrendPanel";
import RecentFailuresPanel from "./overview-components/RecentFailuresPanel";
import StuckWorkflowsPanel from "./overview-components/StuckWorkflowsPanel";

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(420px, 100%), 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
  align-items: start;
`;

export default function OverviewPage() {
  const queryClient = useQueryClient();
  const retryMutation = useResourceMutation(retryWorkflow);
  const [searchParams, setSearchParams] = useSearchParams();

  const windowHours = (() => {
    const fromUrl = Number(searchParams.get("window"));
    if (DASHBOARD_WINDOWS.some((w) => w.hours === fromUrl)) return fromUrl;
    const stored = Number(localStorage.getItem("overview.window"));
    return DASHBOARD_WINDOWS.some((w) => w.hours === stored) ? stored : 168;
  })();

  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const { data, isFetching, isPending, error, dataUpdatedAt, refetch } = useOverview(windowHours, timeZone);
  const refresh = async () => {
    await Promise.all([
      refetch(),
      queryClient.invalidateQueries({ queryKey: dashboardTrendDetailsKey }),
    ]);
  };

  const setWindow = (hours: number) => {
    try {
      localStorage.setItem("overview.window", String(hours));
    } catch {
      /* ignore */
    }
    const next = new URLSearchParams(searchParams);
    next.set("window", String(hours));
    setSearchParams(next, { replace: true });
  };

  const handleRetry = async (id: string) => {
    if (!window.confirm("Retry this workflow?")) return;
    try {
      await retryMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Retry failed");
    }
  };

  const summary = data?.summary;

  return (
    <PageHeader title="Overview" loading={isFetching} onRefresh={refresh}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "1rem",
          flexWrap: "wrap",
          gap: "0.5rem",
        }}
      >
        <DashboardWindowSelector windows={DASHBOARD_WINDOWS} windowHours={windowHours} onChange={setWindow} />
        {dataUpdatedAt > 0 && (
          <Typography variant="caption" style={{ color: tokens.colors.text.static_icons__tertiary.hex }}>
            Updated {new Date(dataUpdatedAt).toLocaleTimeString()} · auto-refresh 60s
          </Typography>
        )}
      </div>

      {error && (
        <ErrorText variant="body_short" role="alert" style={{ marginBottom: "1rem" }}>
          {error.message}
        </ErrorText>
      )}

      {isPending && <Typography variant="body_short">Loading dashboard…</Typography>}
      {summary && (
        <>
          <OverviewSummaryCards summary={summary} windowHours={windowHours} />
          <OverviewTrendPanel trend={summary.trend} windowHours={windowHours} timeZone={timeZone} />
          <Grid>
            <RecentFailuresPanel failures={data!.failures} retryPending={retryMutation.isPending} onRetry={handleRetry} />
            <LatestAnalysisRunsPanel runs={data!.latestRuns} />
            <OverviewBreakdownPanel
              title="By workflow type"
              rows={summary.perWorkflowType}
              emptyMessage="No completed workflows in window."
            />
            <OverviewBreakdownPanel
              title="By analysis type"
              rows={summary.perAnalysisType}
              emptyMessage="No completed analyses in window."
            />
            <StuckWorkflowsPanel workflows={summary.stuck} />
          </Grid>
        </>
      )}
    </PageHeader>
  );
}
