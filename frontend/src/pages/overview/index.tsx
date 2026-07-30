import { useCallback, type ReactNode } from "react";
import { useNavigate, useSearchParams } from "react-router";
import {
  Button,
  Chip,
  Table,
  Typography,
} from "@equinor/eds-core-react";
import {
  DASHBOARD_WINDOWS,
  getDashboardSummary,
  getWorkflows,
  retryWorkflow,
  type DashboardSummary,
  type Workflow,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import StatCard from "../../components/StatCard";
import StatusChip from "../../components/StatusChip";
import TrendChart from "../../components/TrendChart";
import { useAutoRefresh } from "../../utils/useAutoRefresh";
import styled from "styled-components";

const REFRESH_MS = 60000;

const CardRow = styled.div`
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 0.75rem;
`;

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(420px, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
  align-items: start;
`;

const SectionTitle = styled(Typography).attrs({ variant: "caption" })`
  display: block;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #6f6f6f;
  margin-bottom: 0.4rem;
`;

const WindowToggle = styled.div`
  display: flex;
  gap: 0.5rem;
  align-items: center;
`;

const Panel = styled.div<{ $accent?: string }>`
  border: 1px solid #dcdcdc;
  ${(p) => (p.$accent ? `border-left: 3px solid ${p.$accent};` : "")}
  border-radius: 4px;
  padding: 0.6rem 0.8rem;
  background: #ffffff;
`;

const DenseTable = styled(Table)`
  width: 100%;
  font-size: 0.8rem;

  td,
  th {
    padding: 0.25rem 0.5rem;
  }
`;

/** A titled panel used as a grid cell. */
function Block({
  title,
  accent,
  children,
}: {
  title: ReactNode;
  accent?: string;
  children: ReactNode;
}) {
  return (
    <Panel $accent={accent}>
      <SectionTitle>{title}</SectionTitle>
      {children}
    </Panel>
  );
}


interface OverviewData {
  summary: DashboardSummary;
  latest: Workflow[];
  failures: Workflow[];
}

function fmtDuration(seconds: number | null): string {
  if (seconds == null) return "–";
  if (seconds < 60) return `${Math.round(seconds)}s`;
  if (seconds < 3600) return `${(seconds / 60).toFixed(1)}m`;
  return `${(seconds / 3600).toFixed(1)}h`;
}

function fmtTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString() : "–";
}

export default function OverviewPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const windowHours = (() => {
    const fromUrl = Number(searchParams.get("window"));
    if (DASHBOARD_WINDOWS.some((w) => w.hours === fromUrl)) return fromUrl;
    const stored = Number(localStorage.getItem("overview.window"));
    return DASHBOARD_WINDOWS.some((w) => w.hours === stored) ? stored : 24;
  })();

  const fetcher = useCallback(async (): Promise<OverviewData> => {
    const [summary, latest, failures] = await Promise.all([
      getDashboardSummary(windowHours),
      getWorkflows(1, 5, {}),
      getWorkflows(1, 5, { status: "Failed" }),
    ]);
    return {
      summary,
      latest: latest.items,
      failures: failures.items,
    };
  }, [windowHours]);

  const { data, loading, error, lastUpdated, refetch } = useAutoRefresh<OverviewData>(
    fetcher,
    REFRESH_MS,
    [windowHours]
  );

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
      await retryWorkflow(id);
      await refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Retry failed");
    }
  };

  const summary = data?.summary;
  const hourly = windowHours <= 24;

  // Show not-completed (InProgress/Pending) rows first, preserving recency within each group.
  const latest = data
    ? [...data.latest].sort(
        (a, b) => Number(a.completedAt != null) - Number(b.completedAt != null)
      )
    : [];

  return (
    <PageHeader title="Overview" loading={loading} onRefresh={refetch}>
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
        <WindowToggle>
          {DASHBOARD_WINDOWS.map((w) => (
            <Chip
              key={w.hours}
              variant={w.hours === windowHours ? "active" : "default"}
              onClick={() => setWindow(w.hours)}
              style={{ cursor: "pointer" }}
            >
              {w.label}
            </Chip>
          ))}
        </WindowToggle>
        {lastUpdated && (
          <Typography variant="caption" style={{ color: "#6f6f6f" }}>
            Updated {lastUpdated.toLocaleTimeString()} · auto-refresh 60s
          </Typography>
        )}
      </div>

      {error && (
        <Typography
          variant="body_short"
          style={{ color: "#eb0000", marginBottom: "1rem" }}
        >
          {error}
        </Typography>
      )}

      {!summary ? (
        <Typography variant="body_short">Loading dashboard…</Typography>
      ) : (
        <>
          {/* Headline metrics + analysis-group health in one strip */}
          <CardRow>
            <StatCard
              title={`Succeeded (${DASHBOARD_WINDOWS.find((w) => w.hours === windowHours)?.label})`}
              value={summary.workflowStatusCounts.succeeded}
              tone="success"
            />
            <StatCard
              title="Failed"
              value={summary.workflowStatusCounts.failed}
              tone={summary.workflowStatusCounts.failed > 0 ? "error" : "default"}
            />
            <StatCard
              title="In Progress"
              value={summary.currentlyRunning.workflows}
              tone="info"
              subtitle={`${summary.currentlyRunning.runs} run(s)`}
            />
            <StatCard
              title="Success Rate"
              value={`${Math.round(summary.successRate * 100)}%`}
              tone={
                summary.successRate >= 0.9
                  ? "success"
                  : summary.successRate >= 0.6
                    ? "warning"
                    : "error"
              }
            />
            <StatCard
              title="Inspections Ingested"
              value={summary.inspectionRecordsIngested}
              tone="default"
            />
            <StatCard
              title="Groups Pending"
              value={summary.analysisGroupCounts.pending}
              tone="info"
            />
            <StatCard
              title="Groups Complete"
              value={summary.analysisGroupCounts.complete}
              tone="success"
            />
            <StatCard
              title="Groups Timed Out"
              value={summary.analysisGroupCounts.timedOut}
              tone={summary.analysisGroupCounts.timedOut > 0 ? "error" : "default"}
            />
          </CardRow>

          {/* Trend – full width, short */}
          <Panel style={{ marginBottom: "1rem" }}>
            <SectionTitle>Succeeded vs Failed over time</SectionTitle>
            <TrendChart data={summary.trend} hourly={hourly} />
          </Panel>

          <Grid>
            {/* Recent failures */}
            {data!.failures.length > 0 && (
              <Block title="Recent failures" accent="#eb0000">
                <DenseTable>
                  <Table.Head>
                    <Table.Row>
                      <Table.Cell>ID</Table.Cell>
                      <Table.Cell>Type</Table.Cell>
                      <Table.Cell>Error</Table.Cell>
                      <Table.Cell>Actions</Table.Cell>
                    </Table.Row>
                  </Table.Head>
                  <Table.Body>
                    {data!.failures.map((w) => (
                      <Table.Row
                        key={w.id}
                        onClick={() => navigate(`/workflows/${w.id}`)}
                        style={{ cursor: "pointer" }}
                      >
                        <Table.Cell>
                          <IdCell id={w.id} />
                        </Table.Cell>
                        <Table.Cell>{w.workflowType}</Table.Cell>
                        <Table.Cell
                          style={{
                            maxWidth: 220,
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {w.errorMessage ?? "–"}
                        </Table.Cell>
                        <Table.Cell>
                          <Button
                            variant="ghost"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleRetry(w.id);
                            }}
                          >
                            Retry
                          </Button>
                        </Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </DenseTable>
              </Block>
            )}

            {/* Latest workflows */}
            <Block title="Latest workflows">
              <DenseTable>
                <Table.Head>
                  <Table.Row>
                    <Table.Cell>ID</Table.Cell>
                    <Table.Cell>Type</Table.Cell>
                    <Table.Cell>Status</Table.Cell>
                    <Table.Cell>Completed</Table.Cell>
                  </Table.Row>
                </Table.Head>
                <Table.Body>
                  {latest.length === 0 ? (
                    <Table.Row>
                      <Table.Cell colSpan={4}>No workflows.</Table.Cell>
                    </Table.Row>
                  ) : (
                    latest.map((w) => (
                      <Table.Row
                        key={w.id}
                        onClick={() => navigate(`/workflows/${w.id}`)}
                        style={{ cursor: "pointer" }}
                      >
                        <Table.Cell>
                          <IdCell id={w.id} />
                        </Table.Cell>
                        <Table.Cell>{w.workflowType}</Table.Cell>
                        <Table.Cell>
                          <StatusChip status={w.status} />
                        </Table.Cell>
                        <Table.Cell>{fmtTime(w.completedAt ?? null)}</Table.Cell>
                      </Table.Row>
                    ))
                  )}
                </Table.Body>
              </DenseTable>
            </Block>

            {/* Per-workflow-type breakdown */}
            <Block title="By workflow type">
              <DenseTable>
                <Table.Head>
                  <Table.Row>
                    <Table.Cell>Type</Table.Cell>
                    <Table.Cell>Total</Table.Cell>
                    <Table.Cell>OK</Table.Cell>
                    <Table.Cell>Fail</Table.Cell>
                    <Table.Cell>Fail %</Table.Cell>
                    <Table.Cell>Avg</Table.Cell>
                  </Table.Row>
                </Table.Head>
                <Table.Body>
                  {summary.perWorkflowType.length === 0 ? (
                    <Table.Row>
                      <Table.Cell colSpan={6}>No completed workflows in window.</Table.Cell>
                    </Table.Row>
                  ) : (
                    summary.perWorkflowType.map((s) => (
                      <Table.Row key={s.workflowType}>
                        <Table.Cell>{s.workflowType}</Table.Cell>
                        <Table.Cell>{s.total}</Table.Cell>
                        <Table.Cell>{s.succeeded}</Table.Cell>
                        <Table.Cell>{s.failed}</Table.Cell>
                        <Table.Cell
                          style={{ color: s.failureRate > 0 ? "#eb0000" : undefined }}
                        >
                          {Math.round(s.failureRate * 100)}%
                        </Table.Cell>
                        <Table.Cell>{fmtDuration(s.averageDurationSeconds)}</Table.Cell>
                      </Table.Row>
                    ))
                  )}
                </Table.Body>
              </DenseTable>
            </Block>

            {/* Stuck / long-running */}
            {summary.stuck.length > 0 && (
              <Block
                title={`Possibly stuck workflows (${summary.stuck.length})`}
                accent="#ff9200"
              >
                <DenseTable>
                  <Table.Head>
                    <Table.Row>
                      <Table.Cell>ID</Table.Cell>
                      <Table.Cell>Type</Table.Cell>
                      <Table.Cell>Running for</Table.Cell>
                    </Table.Row>
                  </Table.Head>
                  <Table.Body>
                    {summary.stuck.map((s) => (
                      <Table.Row
                        key={s.id}
                        onClick={() => navigate(`/workflows/${s.id}`)}
                        style={{ cursor: "pointer" }}
                      >
                        <Table.Cell>
                          <IdCell id={s.id} />
                        </Table.Cell>
                        <Table.Cell>{s.workflowType}</Table.Cell>
                        <Table.Cell>{Math.round(s.minutesRunning)} min</Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </DenseTable>
              </Block>
            )}
          </Grid>
        </>
      )}
    </PageHeader>
  );
}
