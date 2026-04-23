import {
    Autocomplete,
    type AutocompleteChanges,
    Button,
    Chip,
    Icon,
    Search,
} from "@equinor/eds-core-react";
import { filter_alt, filter_alt_active } from "@equinor/eds-icons";
import { type ChangeEvent } from "react";
import styled from "styled-components";
import type { PlantDataFilterParams } from "../../api/client";

Icon.add({ filter_alt, filter_alt_active });

const ANONYMIZATION_STATUS_OPTIONS = [
    "NotStarted",
    "Started",
    "ExitSuccess",
    "ExitFailure",
] as const;

const ANALYSIS_TYPE_OPTIONS = [
    "ConstantLevelOiler",
    "Fencilla",
    "ThermalReading",
] as const;

const ANALYSIS_TYPE_LABELS: Record<string, string> = {
    ConstantLevelOiler: "Constant Level Oiler",
    Fencilla: "Fencilla",
    ThermalReading: "Thermal Reading",
};

const STATUS_LABELS: Record<string, string> = {
    NotStarted: "Not Started",
    Started: "Started",
    ExitSuccess: "Completed",
    ExitFailure: "Failed",
};

const StyledFilterBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 0.75rem;
  margin-bottom: 1rem;
`;

const StyledSearch = styled(Search)`
  width: 220px;
  --eds-input-background: white;
`;

interface PlantDataFilterSectionProps {
    filters: PlantDataFilterParams;
    onChange: (filters: PlantDataFilterParams) => void;
}

export default function PlantDataFilterSection({
    filters,
    onChange,
}: PlantDataFilterSectionProps) {
    const hasActiveFilters =
        !!filters.inspectionId ||
        !!filters.tag ||
        !!filters.installationCode ||
        !!filters.anonymizationStatus ||
        !!filters.analysisType ||
        filters.hasIncompleteWorkflows === true;

    const update = (patch: Partial<PlantDataFilterParams>) =>
        onChange({ ...filters, ...patch });

    const clearFilters = () =>
        onChange({
            inspectionId: undefined,
            tag: undefined,
            installationCode: undefined,
            anonymizationStatus: undefined,
            analysisType: undefined,
            hasIncompleteWorkflows: undefined,
        });

    return (
        <div>
            <StyledFilterBar>
                <StyledSearch
                    aria-label="Search by Inspection ID"
                    placeholder="Search Inspection ID"
                    value={filters.inspectionId ?? ""}
                    onChange={(e: ChangeEvent<HTMLInputElement>) =>
                        update({ inspectionId: e.target.value || undefined })
                    }
                />
                <StyledSearch
                    aria-label="Search by tag"
                    placeholder="Search Tag"
                    value={filters.tag ?? ""}
                    onChange={(e: ChangeEvent<HTMLInputElement>) =>
                        update({ tag: e.target.value || undefined })
                    }
                />
                <StyledSearch
                    aria-label="Search by installation code"
                    placeholder="Search Installation Code"
                    value={filters.installationCode ?? ""}
                    onChange={(e: ChangeEvent<HTMLInputElement>) =>
                        update({ installationCode: e.target.value || undefined })
                    }
                />
                <Autocomplete
                    label="Anonymization status"
                    placeholder="Any status"
                    options={ANONYMIZATION_STATUS_OPTIONS.map((s) => STATUS_LABELS[s])}
                    selectedOptions={
                        filters.anonymizationStatus
                            ? [STATUS_LABELS[filters.anonymizationStatus] ?? filters.anonymizationStatus]
                            : []
                    }
                    onOptionsChange={(changes: AutocompleteChanges<string>) => {
                        const selected = changes.selectedItems[0];
                        const key = Object.entries(STATUS_LABELS).find(
                            ([, label]) => label === selected
                        )?.[0];
                        update({ anonymizationStatus: key || undefined });
                    }}
                    style={{ width: 200 }}
                />
                <Autocomplete
                    label="Analysis type"
                    placeholder="Any type"
                    options={ANALYSIS_TYPE_OPTIONS.map((t) => ANALYSIS_TYPE_LABELS[t])}
                    selectedOptions={
                        filters.analysisType
                            ? [ANALYSIS_TYPE_LABELS[filters.analysisType] ?? filters.analysisType]
                            : []
                    }
                    onOptionsChange={(changes: AutocompleteChanges<string>) => {
                        const selected = changes.selectedItems[0];
                        const key = Object.entries(ANALYSIS_TYPE_LABELS).find(
                            ([, label]) => label === selected
                        )?.[0];
                        update({ analysisType: key || undefined });
                    }}
                    style={{ width: 210 }}
                />
                <Chip
                    variant={filters.hasIncompleteWorkflows ? "active" : "default"}
                    onClick={() =>
                        update({
                            hasIncompleteWorkflows: filters.hasIncompleteWorkflows ? undefined : true,
                        })
                    }
                >
                    <Icon name={filters.hasIncompleteWorkflows ? "filter_alt_active" : "filter_alt"} />
                    Incomplete workflows
                </Chip>
                {hasActiveFilters && (
                    <Button variant="ghost" onClick={clearFilters}>
                        Clear filters
                    </Button>
                )}
            </StyledFilterBar>
        </div>
    );
}
