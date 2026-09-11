import { Typography } from "@equinor/eds-core-react";
import { tokens } from "@equinor/eds-tokens";
import styled from "styled-components";
import type { AnalysisRunParams, AnalysisRunStatus } from "../../../api/client";
import { ClearFiltersButton, ErrorText, FilterSearch, FilterSelect } from "../../../components/Styles";
import DateTimeFilterInput from "../../../components/DateTimeFilterInput";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";

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

const STATUSES: AnalysisRunStatus[] = ["Pending", "InProgress", "Succeeded", "Failed"];

interface AnalysisRunsFiltersProps {
  filters: AnalysisRunParams;
  setFilters: (filters: Partial<AnalysisRunParams>) => void;
  analysisTypes: string[];
  configuredAnalysesError: Error | null;
}

export default function AnalysisRunsFilters({
  filters, setFilters, analysisTypes, configuredAnalysesError,
}: AnalysisRunsFiltersProps) {
  const invalidRange = Boolean(filters.startedSince && filters.startedUntil &&
    new Date(filters.startedSince) > new Date(filters.startedUntil));
  const hasFilters = Object.values(filters).some((value) => value != null && value !== "");

  return (
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
        <DateTimeFilterInput
          id="analysis-runs-started-since"
          label="Started since"
          value={filters.startedSince}
          onChange={(startedSince) => setFilters({ startedSince })}
        />
        <RangeSeparator>to</RangeSeparator>
        <DateTimeFilterInput
          id="analysis-runs-started-until"
          label="Started until"
          value={filters.startedUntil}
          onChange={(startedUntil) => setFilters({ startedUntil })}
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
      {configuredAnalysesError && (
        <ErrorText variant="body_short" role="alert">
          Failed to load configured analyses: {configuredAnalysesError.message}
        </ErrorText>
      )}
    </FilterPanel>
  );
}
