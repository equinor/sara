import type { WorkflowParams, WorkflowStatus } from "../../../api/client";
import { FilterBar, FilterSearch, FilterSelect } from "../../../components/Styles";

const STATUSES: WorkflowStatus[] = ["Pending", "InProgress", "Succeeded", "Failed"];

interface WorkflowsFiltersProps {
  filters: WorkflowParams;
  setFilters: (filters: Partial<WorkflowParams>) => void;
}

export default function WorkflowsFilters({ filters, setFilters }: WorkflowsFiltersProps) {
  return (
    <FilterBar>
      <FilterSearch
        id="workflows-type"
        label="Workflow Type"
        placeholder="Workflow Type"
        value={filters.workflowType ?? ""}
        onChange={(e) => setFilters({ workflowType: (e.target as HTMLInputElement).value })}
      />
      <FilterSearch
        id="workflows-analysis-run-id"
        label="Analysis Run ID"
        placeholder="Analysis Run ID (uuid)"
        value={filters.analysisRunId ?? ""}
        onChange={(e) => setFilters({ analysisRunId: (e.target as HTMLInputElement).value })}
      />
      <FilterSelect
        id="workflows-status"
        label="Status"
        value={filters.status ?? ""}
        onChange={(e) =>
          setFilters({ status: (e.target.value || undefined) as WorkflowStatus | undefined })
        }
      >
        <option value="">All statuses</option>
        {STATUSES.map((s) => (
          <option key={s} value={s}>
            {s}
          </option>
        ))}
      </FilterSelect>
    </FilterBar>
  );
}
