import { useNavigate } from "react-router";
import type { MouseEvent } from "react";
import { Button, Table, Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
import {
  deleteAnalysisRun,
  getAnalysisRuns,
  type AnalysisRun,
  type AnalysisRunParams,
  type AnalysisRunStatus,
} from "../../api/client";
import { useConfiguredAnalyses, useResourceMutation } from "../../api/queries";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { ClearFiltersButton, DateTimeInput, ErrorText, FilterSearch, FilterSelect, TableScroller } from "../../components/Styles";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import { argoWorkflowUrl } from "../../utils/argo";
import { formatElapsedDuration } from "../../utils/duration";
import styled from "styled-components";

const FilterPanel = styled.div`
  margin-bottom: 1rem;
  padding-bottom: 0.25rem;
`;

const FilterGrid = styled.div`
  display: grid;
  grid-template-columns: minmax(200px, 1.25fr) minmax(150px, 0.8fr) minmax(140px, 0.7fr) minmax(185px, 1fr) auto minmax(185px, 1fr) auto;
  gap: 0.75rem;
  align-items: end;

  @media (max-width: 1100px) {
    grid-template-columns: 1fr 1fr;
  }

  @media (max-width: 560px) {
    grid-template-columns: 1fr;
  }
`;

const RangeSeparator = styled.span`
  align-self: end;
  padding-bottom: 0.65rem;
  color: ${tokens.colors.text.static_icons__tertiary.hex};
  font-size: 0.8rem;

  @media (max-width: 1100px) {
    display: none;
  }
`;

const ValidationMessage = styled.span`
  grid-column: 4 / 7;
  color: ${tokens.colors.interactive.danger__text.hex};
  font-size: 0.75rem;

  @media (max-width: 1100px) {
    grid-column: 1 / -1;
  }
`;

const FILTER_KEYS: (keyof AnalysisRunParams & string)[] = [
  "analysisId",
  "analysisType",
  "status",
  "startedSince",
  "startedUntil",
];
const STATUSES: AnalysisRunStatus[] = ["Pending", "InProgress", "Succeeded", "Failed"];

function formatAnalysisType(analysisType: string): string {
  switch (analysisType.toLowerCase()) {
    case "fencilla":
      return "Fence detection";
    case "thermal-reading":
      return "Thermal reading";
    case "cloe":
      return "CLOE";
    case "co2":
      return "CO2";
    default:
      return analysisType;
  }
}

export default function AnalysisRunsPage() {
  const navigate = useNavigate();
  const deleteMutation = useResourceMutation(deleteAnalysisRun, "delete");
  const configuredAnalyses = useConfiguredAnalyses();
  const analysisTypes = (configuredAnalyses.data ?? [])
    .map((analysis) => analysis.name)
    .sort((a, b) => a.localeCompare(b));
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
  } = usePagedList<AnalysisRun, AnalysisRunParams>(
    "analysis-runs",
    "analysisRuns.pageSize",
    FILTER_KEYS,
    getAnalysisRuns
  );

  const items = response?.items ?? [];
  const parseFilterDate = (value: string | undefined): Date | null => {
    if (!value) return null;
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  };
  const startedSince = parseFilterDate(filters.startedSince);
  const startedUntil = parseFilterDate(filters.startedUntil);
  const invalidRange =
    startedSince !== null && startedUntil !== null && startedSince > startedUntil;
  const hasFilters = Object.values(filters).some((value) => value != null && value !== "");
  const formatDateTimeLocal = (date: Date | null): string => {
    if (!date) return "";
    const offset = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
  };
  const updateDateFilter = (key: "startedSince" | "startedUntil", value: string) => {
    setFilters({ [key]: value ? new Date(value).toISOString() : undefined });
  };

  const handleDelete = async (id: string) => {
    if (deleteMutation.isPending) return;
    if (!window.confirm("Delete this run and its workflows?")) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Delete failed");
    }
  };

  return (
    <PageHeader title="Analysis Runs" loading={loading} onRefresh={refetch}>
      <FilterPanel>
        <Typography variant="caption" style={{ display: "block", marginBottom: "0.65rem" }}>
          Filters
        </Typography>
        <FilterGrid>
          <FilterSearch
            id="analysis-runs-analysis-id"
            label="Analysis ID"
            placeholder="UUID"
            value={filters.analysisId ?? ""}
            onChange={(e) => setFilters({ analysisId: (e.target as HTMLInputElement).value })}
          />
          <FilterSelect
            id="analysis-runs-analysis-type"
            label="Analysis name"
            value={filters.analysisType ?? ""}
            onChange={(event) =>
              setFilters({ analysisType: event.target.value || undefined })
            }
          >
            <option value="">All analyses</option>
            {analysisTypes.map((analysisType) => (
              <option key={analysisType} value={analysisType}>
                {formatAnalysisType(analysisType)}
              </option>
            ))}
          </FilterSelect>
          <FilterSelect
            id="analysis-runs-status"
            label="Status"
            value={filters.status ?? ""}
            onChange={(e) =>
              setFilters({
                status: (e.target.value || undefined) as AnalysisRunStatus | undefined,
              })
            }
          >
            <option value="">All statuses</option>
            {STATUSES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </FilterSelect>
          <DateTimeInput
            id="analysis-runs-started-since"
            label="Started since"
            value={formatDateTimeLocal(startedSince)}
            onChange={(event) => updateDateFilter("startedSince", event.target.value)}
          />
          <RangeSeparator>to</RangeSeparator>
          <DateTimeInput
            id="analysis-runs-started-until"
            label="Started until"
            value={formatDateTimeLocal(startedUntil)}
            onChange={(event) => updateDateFilter("startedUntil", event.target.value)}
            aria-invalid={invalidRange}
            variant={invalidRange ? "error" : undefined}
            aria-describedby={invalidRange ? "analysis-runs-date-error" : undefined}
          />
          {hasFilters && (
            <ClearFiltersButton
              variant="ghost"
              onClick={() =>
                setFilters({
                  analysisId: undefined,
                  analysisType: undefined,
                  status: undefined,
                  startedSince: undefined,
                  startedUntil: undefined,
                })
              }
            >
              Clear filters
            </ClearFiltersButton>
          )}
          {invalidRange && (
            <ValidationMessage id="analysis-runs-date-error" role="alert">Started until must not be earlier than started since.</ValidationMessage>
          )}
        </FilterGrid>
        {configuredAnalyses.error && (
          <ErrorText variant="body_short" role="alert">
            Failed to load configured analyses: {configuredAnalyses.error.message}
          </ErrorText>
        )}
      </FilterPanel>

      {error && (
        <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
          {error}
        </ErrorText>
      )}

      <TableScroller>
        <Table style={{ width: "100%" }}>
          <Table.Head>
            <Table.Row>
              <Table.Cell>ID</Table.Cell>
              <Table.Cell>Analysis</Table.Cell>
              <Table.Cell>Run #</Table.Cell>
              <Table.Cell>Status</Table.Cell>
              <Table.Cell>Started</Table.Cell>
              <Table.Cell>Completed</Table.Cell>
              <Table.Cell>Duration</Table.Cell>
              <Table.Cell>#Workflows</Table.Cell>
              <Table.Cell>Argo</Table.Cell>
              <Table.Cell>Actions</Table.Cell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {initialLoading ? (
              <TableSkeleton columns={10} rows={pageSize} />
            ) : items.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={10}>No runs.</Table.Cell>
              </Table.Row>
            ) : (
              items.map((r) => (
                <Table.Row
                  key={r.id}
                  onClick={() => navigate(`/analysis-runs/${r.id}`)}
                  style={{ cursor: "pointer" }}
                >
                  <Table.Cell>
                    <IdCell id={r.id} />
                  </Table.Cell>
                  <Table.Cell>
                    {r.analysis ? (
                      <Button
                        variant="ghost"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigate(`/analyses/${r.analysisId}`);
                        }}
                      >
                        {r.analysis.analysisType}
                      </Button>
                    ) : (
                      r.analysisId
                    )}
                  </Table.Cell>
                  <Table.Cell>{r.runNumber}</Table.Cell>
                  <Table.Cell>
                    <StatusChip status={r.status} />
                  </Table.Cell>
                  <Table.Cell>
                    {r.startedAt ? new Date(r.startedAt).toLocaleString() : "–"}
                  </Table.Cell>
                  <Table.Cell>
                    {r.completedAt ? new Date(r.completedAt).toLocaleString() : "–"}
                  </Table.Cell>
                  <Table.Cell>{formatElapsedDuration(r.startedAt, r.completedAt)}</Table.Cell>
                  <Table.Cell>{(r.workflows ?? []).length}</Table.Cell>
                  <Table.Cell>
                    {argoWorkflowUrl(r.workflows?.[0]?.argoWorkflowName) && (
                      <Typography
                        link
                        href={argoWorkflowUrl(r.workflows?.[0]?.argoWorkflowName)!}
                        target="_blank"
                        rel="noopener noreferrer"
                        onClick={(e: MouseEvent) => e.stopPropagation()}
                      >
                        View
                      </Typography>
                    )}
                  </Table.Cell>
                  <Table.Cell>
                    <Button
                      variant="ghost"
                      color="danger"
                      disabled={deleteMutation.isPending}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(r.id);
                      }}
                    >
                      Delete
                    </Button>
                  </Table.Cell>
                </Table.Row>
              ))
            )}
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
