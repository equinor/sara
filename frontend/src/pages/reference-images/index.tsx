import { useMemo, useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { Typography } from "@equinor/eds-core-react"
import { useNavigate } from "react-router"
import styled from "styled-components"
import {
    deleteReferencePolygonMetadata,
    getReferencePolygonMetadata,
} from "../../api/client"
import { useResourceMutation } from "../../api/queries"
import { ErrorText } from "../../components/Styles"
import ReferenceMetadataTable from "./reference-images-components/ReferenceMetadataTable"
import ReferenceMetadataToolbar, { type SourceTypeFilter } from "./reference-images-components/ReferenceMetadataToolbar"

const StyledPageHeader = styled.div`
  display: flex;
  justify-content: space-between;
  margin-bottom: 1rem;
`

const StyledPage = styled.div`
  padding-top: 1rem;
`

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
                <ErrorText
                    variant="body_short"
                    style={{ marginBottom: "1rem" }}
                >
                    {error}
                </ErrorText>
            )}

            <ReferenceMetadataToolbar
                sourceTypeFilter={sourceTypeFilter}
                isFetching={isFetching}
                onFilterChange={setSourceTypeFilter}
                onRefresh={() => {
                    setError(null)
                    void refetch()
                }}
                onCreate={() => navigate("/reference-images/new")}
            />

            {loading ? (
                <Typography variant="body_short">Loading...</Typography>
            ) : (
                <ReferenceMetadataTable
                    data={sortedData}
                    deleting={deleteMutation.isPending}
                    onSelect={(id) => navigate(`/reference-images/${id}`)}
                    onDelete={handleDelete}
                />
            )}
        </StyledPage>
    )
}
