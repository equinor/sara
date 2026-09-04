import { useNavigate } from "react-router";
import type { MouseEvent } from "react";
import { Button, Search, Table, Typography } from "@equinor/eds-core-react";
import {
  deleteAnalysisRun,
  getAnalysisRuns,
  type AnalysisRun,
  type AnalysisRunParams,
  type AnalysisRunStatus,
} from "../../api/client";
import IdCell from "../../components/IdCell";
import PageHeader from "../../components/PageHeader";
import PaginationFooter from "../../components/PaginationFooter";
import StatusChip from "../../components/StatusChip";
import TableSkeleton from "../../components/TableSkeleton";
import { PAGE_SIZE_OPTIONS, usePagedList } from "../../utils/usePagedList";
import { argoWorkflowUrl } from "../../utils/argo";
import styled from "styled-components";

const FilterPanel = styled.div`
  margin-bottom: 1rem;
  padding-bottom: 0.25rem;
`;

const FilterGrid = styled.div`
  display: grid;
  grid-template-columns: minmax(220px, 1.5fr) minmax(150px, 0.75fr) minmax(185px, 1fr) auto minmax(185px, 1fr) auto;
  gap: 0.75rem;
  align-items: end;

  @media (max-width: 900px) {
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

const StatusSelect = styled.select`
  width: 100%;
  min-height: 42px;
  padding: 0.5rem 2rem 0.5rem 0.65rem;
  border: 1px solid #6f6f6f;
  border-radius: 2px;
  background: #ffffff;
  color: #3d3d3d;
  font: inherit;
  font-size: 0.875rem;

  &:focus-visible {
    outline: 2px solid #007079;
    outline-offset: 1px;
  }
`;

const DateTimeInput = styled.input<{ $invalid?: boolean }>`
  width: 100%;
  min-height: 42px;
  box-sizing: border-box;
  padding: 0.5rem 0.65rem;
  border: 1px solid ${(p) => (p.$invalid ? "#eb0000" : "#6f6f6f")};
  border-radius: 2px;
  background: #ffffff;
  color: #3d3d3d;
  font: inherit;
  font-size: 0.875rem;

  &:focus-visible {
    outline: 2px solid ${(p) => (p.$invalid ? "#eb0000" : "#007079")};
    outline-offset: 1px;
  }
`;

const RangeSeparator = styled.span`
  align-self: end;
  padding-bottom: 0.65rem;
  color: #6f6f6f;
  font-size: 0.8rem;

  @media (max-width: 900px) {
    display: none;
  }
`;

const ValidationMessage = styled.span`
  grid-column: 3 / 6;
  color: #eb0000;
  font-size: 0.75rem;

  @media (max-width: 900px) {
    grid-column: 1 / -1;
  }
`;

const ClearButton = styled(Button)`
  min-height: 42px;
  white-space: nowrap;
`;

const FILTER_KEYS: (keyof AnalysisRunParams & string)[] = [
  "analysisId",
  "status",
  "startedSince",
  "startedUntil",
];
const STATUSES: AnalysisRunStatus[] = ["Pending", "InProgress", "Succeeded", "Failed"];

export default function AnalysisRunsPage() {
  const navigate = useNavigate();
  const {
    response,
    loading,
    error,
    pageNumber,
    pageSize,
    filters,
    setPage,
    setPageSize,
    setFilters,
    refetch,
  } = usePagedList<AnalysisRun, AnalysisRunParams>(
    "analysisRuns.pageSize",
    FILTER_KEYS,
    getAnalysisRuns
  );

  const items = response?.items ?? [];
  const showSkeleton = loading || (response === null && error === null);
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
    if (!window.confirm("Delete this run and its workflows?")) return;
    try {
      await deleteAnalysisRun(id);
      await refetch();
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
          <FilterField>
            Analysis ID
            <Search
              placeholder="UUID"
              value={filters.analysisId ?? ""}
              onChange={(e) => setFilters({ analysisId: (e.target as HTMLInputElement).value })}
            />
          </FilterField>
          <FilterField>
            Status
            <StatusSelect
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
            </StatusSelect>
          </FilterField>
          <FilterField>
            Started since
            <DateTimeInput
              type="datetime-local"
              value={formatDateTimeLocal(startedSince)}
              onChange={(event) => updateDateFilter("startedSince", event.target.value)}
            />
          </FilterField>
          <RangeSeparator>to</RangeSeparator>
          <FilterField>
            Started until
            <DateTimeInput
              type="datetime-local"
              value={formatDateTimeLocal(startedUntil)}
              onChange={(event) => updateDateFilter("startedUntil", event.target.value)}
              aria-invalid={invalidRange}
              $invalid={invalidRange}
            />
          </FilterField>
          {hasFilters && (
            <ClearButton
              variant="ghost"
              onClick={() =>
                setFilters({
                  analysisId: undefined,
                  status: undefined,
                  startedSince: undefined,
                  startedUntil: undefined,
                })
              }
            >
              Clear filters
            </ClearButton>
          )}
          {invalidRange && (
            <ValidationMessage>Started until must not be earlier than started since.</ValidationMessage>
          )}
        </FilterGrid>
      </FilterPanel>

      {error && (
        <Typography variant="body_short" style={{ color: "#eb0000", marginBottom: "1rem" }}>
          {error}
        </Typography>
      )}

      <Table style={{ width: "100%" }}>
        <Table.Head>
          <Table.Row>
            <Table.Cell>ID</Table.Cell>
            <Table.Cell>Analysis</Table.Cell>
            <Table.Cell>Run #</Table.Cell>
            <Table.Cell>Status</Table.Cell>
            <Table.Cell>Started</Table.Cell>
            <Table.Cell>Completed</Table.Cell>
            <Table.Cell>#Workflows</Table.Cell>
            <Table.Cell>Argo</Table.Cell>
            <Table.Cell>Actions</Table.Cell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {showSkeleton ? (
            <TableSkeleton columns={9} rows={pageSize} />
          ) : items.length === 0 ? (
            <Table.Row>
              <Table.Cell colSpan={9}>No runs.</Table.Cell>
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
