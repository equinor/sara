import { useMemo, useState } from "react"
import { useQuery } from "@tanstack/react-query"
import {
    Button,
    ButtonGroup,
    Icon,
    Table,
    Typography,
} from "@equinor/eds-core-react"
import { add, refresh } from "@equinor/eds-icons"
import { useNavigate } from "react-router"
import styled from "styled-components"
import {
    deleteReferencePolygonMetadata,
    getReferencePolygonMetadata,
    AnalysisType,
} from "../../api/client"
import { useResourceMutation } from "../../api/queries"
import IdCell from "../../components/IdCell"
import SegmentedToggle from "../../components/SegmentedToggle"

Icon.add({ add, refresh })

const StyledPageHeader = styled.div`
  display: flex;
  justify-content: space-between;
  margin-bottom: 1rem;
`

const StyledPage = styled.div`
  padding-top: 1rem;
`

const StyledTableToolbar = styled.div`
  display: flex;
  justify-content: space-between;
  max-width: 1200px;
  margin-bottom: 1rem;
`

const StyledActions = styled.div`
  display: flex;
  gap: 0.5rem;
`

const StyledTable = styled(Table)`
  width: 1200px;
`

const SOURCE_ANALYSIS_TYPE_LABELS: Record<AnalysisType, string> = {
    [AnalysisType.ThermalReading]: "Thermal Reading",
    [AnalysisType.Fencilla]: "Fencilla",
    [AnalysisType.CLOE]: "CLOE",
    [AnalysisType.CO2]: "CO2",
}

type SourceTypeFilter = "All" | AnalysisType.ThermalReading | AnalysisType.Fencilla

const SOURCE_TYPE_FILTERS: { value: SourceTypeFilter; label: string }[] = [
    { value: "All", label: "All" },
    { value: AnalysisType.ThermalReading, label: "Thermal Reading" },
    { value: AnalysisType.Fencilla, label: "Fencilla" },
]

export default function ReferencePolygonImagesPage() {
    const navigate = useNavigate()
    const { data = [], isPending: loading, isFetching, error: queryError, refetch } = useQuery({
        queryKey: ["sara", "reference-images", "list"],
        queryFn: ({ signal }) => getReferencePolygonMetadata(signal),
    })
    const deleteMutation = useResourceMutation(deleteReferencePolygonMetadata, "delete")
    const [mutationError, setError] = useState<string | null>(null)
    const error = mutationError ?? (queryError
        ? queryError instanceof Error ? queryError.message : "Failed to fetch reference metadata"
        : null)
    const [sourceTypeFilter, setSourceTypeFilter] = useState<SourceTypeFilter>("All")

    const sortedData = useMemo(
        () =>
            [...data]
                .filter(
                    (metadata) =>
                        sourceTypeFilter === "All" ||
                        metadata.sourceAnalysisType === sourceTypeFilter
                )
                .sort(
                    (left, right) =>
                        new Date(right.dateCreated).getTime() - new Date(left.dateCreated).getTime()
                ),
        [data, sourceTypeFilter]
    )

    const handleDelete = async (id: string) => {
        if (deleteMutation.isPending) return
        setError(null)
        try {
            await deleteMutation.mutateAsync(id)
        } catch (e) {
            setError(
                e instanceof Error
                    ? e.message
                    : "Failed to delete reference metadata"
            )
        }
    }

    return (
        <StyledPage>
            <StyledPageHeader>
                <Typography variant="h3">Reference Metadata</Typography>
            </StyledPageHeader>

            {error && (
                <Typography
                    variant="body_short"
                    style={{ marginBottom: "1rem", color: "#eb0000" }}
                >
                    {error}
                </Typography>
            )}

            <StyledTableToolbar>
                <SegmentedToggle
                    options={SOURCE_TYPE_FILTERS}
                    value={sourceTypeFilter}
                    onChange={setSourceTypeFilter}
                />
                <ButtonGroup>
                    <Button
                        variant="ghost_icon"
                        onClick={() => {
                            setError(null)
                            void refetch()
                        }}
                        aria-label="Refresh"
                        disabled={isFetching}
                    >
                        <Icon name="refresh" />
                    </Button>
                    <Button onClick={() => navigate("/reference-images/new")}>
                        <Icon name="add" />
                        New Reference Metadata
                    </Button>
                </ButtonGroup>
            </StyledTableToolbar>

            {loading ? (
                <Typography variant="body_short">Loading...</Typography>
            ) : (
                <StyledTable>
                    <Table.Head>
                        <Table.Row>
                            <Table.Cell>ID</Table.Cell>
                            <Table.Cell>Installation</Table.Cell>
                            <Table.Cell>Tag</Table.Cell>
                            <Table.Cell>Inspection Description</Table.Cell>
                            <Table.Cell>Source Type</Table.Cell>
                            <Table.Cell>Actions</Table.Cell>
                        </Table.Row>
                    </Table.Head>
                    <Table.Body>
                        {sortedData.map((metadata) => (
                            <Table.Row
                                key={metadata.id}
                                onClick={() => navigate(`/reference-images/${metadata.id}`)}
                                style={{ cursor: "pointer" }}
                            >
                                <Table.Cell>
                                    <IdCell id={metadata.id} />
                                </Table.Cell>
                                <Table.Cell>{metadata.installationCode.toUpperCase()}</Table.Cell>
                                <Table.Cell>{metadata.tagId}</Table.Cell>
                                <Table.Cell>{metadata.inspectionDescription}</Table.Cell>
                                <Table.Cell>
                                    {SOURCE_ANALYSIS_TYPE_LABELS[metadata.sourceAnalysisType]}
                                </Table.Cell>
                                <Table.Cell>
                                    <StyledActions>
                                        <Button
                                            variant="ghost"
                                            color="danger"
                                            disabled={deleteMutation.isPending}
                                            onClick={(event) => {
                                                event.stopPropagation()
                                                handleDelete(metadata.id)
                                            }}
                                        >
                                            Delete
                                        </Button>
                                    </StyledActions>
                                </Table.Cell>
                            </Table.Row>
                        ))}
                        {sortedData.length === 0 && (
                            <Table.Row>
                                <Table.Cell colSpan={6}>
                                    <Typography variant="body_short">
                                        No reference metadata found.
                                    </Typography>
                                </Table.Cell>
                            </Table.Row>
                        )}
                    </Table.Body>
                </StyledTable>
            )}
        </StyledPage>
    )
}
