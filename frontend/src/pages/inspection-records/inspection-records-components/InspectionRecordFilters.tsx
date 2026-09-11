import type { InspectionRecordParams } from "../../../api/client";
import { FilterBar, FilterSearch } from "../../../components/Styles";

interface InspectionRecordFiltersProps {
  filters: InspectionRecordParams;
  setFilters: (filters: Partial<InspectionRecordParams>) => void;
}

export default function InspectionRecordFilters({ filters, setFilters }: InspectionRecordFiltersProps) {
  return (
    <FilterBar>
      <FilterSearch
        id="inspection-records-inspection-id"
        label="Inspection ID"
        placeholder="Inspection ID"
        value={filters.inspectionId ?? ""}
        onChange={(e) =>
          setFilters({ inspectionId: (e.target as HTMLInputElement).value })
        }
      />
      <FilterSearch
        id="inspection-records-tag"
        label="Tag"
        placeholder="Tag"
        value={filters.tag ?? ""}
        onChange={(e) => setFilters({ tag: (e.target as HTMLInputElement).value })}
      />
      <FilterSearch
        id="inspection-records-installation"
        label="Installation"
        placeholder="Installation"
        value={filters.installationCode ?? ""}
        onChange={(e) =>
          setFilters({ installationCode: (e.target as HTMLInputElement).value })
        }
      />
    </FilterBar>
  );
}
