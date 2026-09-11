import type { AnalysisParams } from "../../../api/client";
import { FilterBar, FilterSearch } from "../../../components/Styles";

interface AnalysesFiltersProps {
  filters: AnalysisParams;
  setFilters: (filters: Partial<AnalysisParams>) => void;
}

export default function AnalysesFilters({ filters, setFilters }: AnalysesFiltersProps) {
  return (
    <FilterBar>
      <FilterSearch
        id="analyses-name"
        label="Name"
        placeholder="Name"
        value={filters.name ?? ""}
        onChange={(e) => setFilters({ name: (e.target as HTMLInputElement).value })}
      />
      <FilterSearch
        id="analyses-group-id"
        label="Group ID"
        placeholder="Group ID (uuid)"
        value={filters.analysisGroupId ?? ""}
        onChange={(e) =>
          setFilters({ analysisGroupId: (e.target as HTMLInputElement).value })
        }
      />
      <FilterSearch
        id="analyses-inspection-record-id"
        label="Inspection Record ID"
        placeholder="Inspection Record ID (uuid)"
        value={filters.inspectionRecordId ?? ""}
        onChange={(e) =>
          setFilters({ inspectionRecordId: (e.target as HTMLInputElement).value })
        }
      />
    </FilterBar>
  );
}
