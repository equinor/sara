import { Button, ButtonGroup, Icon } from "@equinor/eds-core-react"
import { add, refresh } from "@equinor/eds-icons"
import styled from "styled-components"
import { AnalysisType } from "../../../api/client"
import SegmentedToggle from "../../../components/SegmentedToggle"

Icon.add({ add, refresh })

const StyledTableToolbar = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  justify-content: space-between;
  max-width: 1200px;
  margin-bottom: 1rem;
`

export type SourceTypeFilter = "All" | AnalysisType.ThermalReading | AnalysisType.Fencilla

const SOURCE_TYPE_FILTERS: { value: SourceTypeFilter; label: string }[] = [
    { value: "All", label: "All" },
    { value: AnalysisType.ThermalReading, label: "Thermal Reading" },
    { value: AnalysisType.Fencilla, label: "Fencilla" },
]

interface Props {
    sourceTypeFilter: SourceTypeFilter
    isFetching: boolean
    onFilterChange: (filter: SourceTypeFilter) => void
    onRefresh: () => void
    onCreate: () => void
}

export default function ReferenceMetadataToolbar({
    sourceTypeFilter, isFetching, onFilterChange, onRefresh, onCreate,
}: Props) {
    return (
        <StyledTableToolbar>
            <SegmentedToggle
                options={SOURCE_TYPE_FILTERS}
                value={sourceTypeFilter}
                onChange={onFilterChange}
            />
            <ButtonGroup>
                <Button
                    variant="ghost_icon"
                    onClick={onRefresh}
                    aria-label="Refresh"
                    disabled={isFetching}
                >
                    <Icon name="refresh" />
                </Button>
                <Button onClick={onCreate}>
                    <Icon name="add" />
                    New Reference Metadata
                </Button>
            </ButtonGroup>
        </StyledTableToolbar>
    )
}
