import styled from "styled-components";
import type { FeedbackParams } from "../../../api/client";
import { ClearFiltersButton, ErrorText, FilterSelect } from "../../../components/Styles";
import DateTimeFilterInput from "../../../components/DateTimeFilterInput";
import { formatAnalysisType } from "../../../utils/formatAnalysisType";

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

export default function FeedbackFilters({ filters, setFilters, analysisTypes, configuredAnalysesError }: {
  filters: FeedbackParams;
  setFilters: (filters: Partial<FeedbackParams>) => void;
  analysisTypes: string[];
  configuredAnalysesError: Error | null;
}) {
  const invalidRange = Boolean(filters.startedSince && filters.startedUntil &&
    new Date(filters.startedSince) > new Date(filters.startedUntil));
  const hasFilters = Object.values(filters).some(Boolean);

  return (
    <>
      <FilterGrid>
        <FilterSelect
          id="feedback-analysis-type"
          label="Analysis"
          value={filters.analysisType ?? ""}
          onChange={(event) => setFilters({ analysisType: event.target.value || undefined })}
        >
          <option value="">All analyses</option>
          {analysisTypes.map((type) => <option key={type} value={type}>{formatAnalysisType(type)}</option>)}
        </FilterSelect>
        <FilterSelect
          id="feedback-result"
          label="Result"
          value={filters.isCorrect ?? ""}
          onChange={(event) => setFilters({ isCorrect: event.target.value || undefined })}
        >
          <option value="">All results</option>
          <option value="true">Correct</option>
          <option value="false">Incorrect</option>
        </FilterSelect>
        <DateTimeFilterInput
          id="feedback-started-since"
          label="Started since"
          value={filters.startedSince}
          onChange={(startedSince) => setFilters({ startedSince })}
        />
        <DateTimeFilterInput
          id="feedback-started-until"
          label="Started until"
          value={filters.startedUntil}
          variant={invalidRange ? "error" : undefined}
          aria-invalid={invalidRange}
          aria-describedby={invalidRange ? "feedback-date-error" : undefined}
          onChange={(startedUntil) => setFilters({ startedUntil })}
        />
        {hasFilters && (
          <ClearFiltersButton
            variant="ghost"
            onClick={() => setFilters({ analysisType: undefined, isCorrect: undefined, startedSince: undefined, startedUntil: undefined })}
          >
            Clear filters
          </ClearFiltersButton>
        )}
      </FilterGrid>
      {configuredAnalysesError && (
        <ErrorText variant="body_short" role="alert">
          Failed to load configured analyses: {configuredAnalysesError.message}
        </ErrorText>
      )}
      {invalidRange && <ErrorText id="feedback-date-error" role="alert">Started until must not be earlier than started since.</ErrorText>}
    </>
  );
}
