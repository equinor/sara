import { useEffect, useState, useCallback } from "react"
import { Button, Typography, Icon } from "@equinor/eds-core-react"
import { arrow_back } from "@equinor/eds-icons"
import { useNavigate } from "react-router"
import styled from "styled-components"
import {
    getReferencePolygonMetadataById,
    updateReferencePolygonMetadata,
    deleteReferencePolygonMetadata,
    getReferencePolygonImageData,
    getReferencePolygonImageUrl,
    AnalysisType,
    type ReferencePolygonMetadataInput,
    type ThermalImageData,
    type BlobStorageLocation,
} from "../../../api/client"
import { useResourceDetail, useResourceMutation } from "../../../api/queries"
import { ErrorText } from "../../../components/Styles"
import ReferenceImageSection from "./ReferenceImageSection"
import ReferenceMetadataEditForm from "./ReferenceMetadataEditForm"
import ReferenceMetadataSummary from "./ReferenceMetadataSummary"

const StyledBackNavRowLg = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
`

const StyledHeaderRow = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  max-width: 800px;
  margin-bottom: 1.5rem;
`

const StyledHeaderTitleRow = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
`

const StyledContent = styled.div`
  padding-left: 1.5rem;
`

Icon.add({ arrow_back })

function getBlobDirectory(location: BlobStorageLocation) {
    return location.blobName.split("/").slice(0, -1).join("/")
}

function getErrorMessage(e: unknown, fallback: string) {
    return e instanceof Error ? e.message : fallback
}

export default function ReferenceMetadataDetail({ id }: { id: string | undefined }) {
    const navigate = useNavigate()
    const { data, isLoading: loading, error: queryError, dataUpdatedAt } = useResourceDetail(
        "reference-images", id, getReferencePolygonMetadataById
    )
    const updateMutation = useResourceMutation(
        ({ id, request }: { id: string; request: ReferencePolygonMetadataInput }) =>
            updateReferencePolygonMetadata(id, request)
    )
    const deleteMutation = useResourceMutation(deleteReferencePolygonMetadata, "delete")
    const saving = updateMutation.isPending
    const pending = saving || deleteMutation.isPending
    const [mutationError, setError] = useState<string | null>(null)
    const error = mutationError ?? (queryError
        ? getErrorMessage(queryError, "Failed to fetch reference metadata")
        : null)
    const [form, setForm] = useState<ReferencePolygonMetadataInput | null>(null)
    const editing = form !== null
    const [thermalImage, setThermalImage] = useState<ThermalImageData | null>(
        null
    )
    const [image, setImage] = useState<string | null>(null)
    const [imageLoading, setImageLoading] = useState(false)
    const [imageError, setImageError] = useState<string | null>(null)

    useEffect(() => {
        // Defer refreshes while editing so the polygon editor keeps its local draft.
        if (!id || !data || editing) return
        let cancelled = false
        setImageLoading(true)
        setImageError(null)
        setThermalImage(null)
        setImage(null)

        const loadImage = async () => {
            try {
                if (data.sourceAnalysisType === AnalysisType.ThermalReading) {
                    const result = await getReferencePolygonImageData(id)
                    if (!cancelled) setThermalImage(result)
                } else {
                    const url = await getReferencePolygonImageUrl(id)
                    if (!cancelled) setImage(url)
                }
            } catch (e) {
                if (!cancelled)
                    setImageError(getErrorMessage(e, "Failed to load reference image"))
            } finally {
                if (!cancelled) setImageLoading(false)
            }
        }
        loadImage()

        return () => {
            cancelled = true
        }
    }, [id, data, dataUpdatedAt, editing])

    const navigateBack = () => navigate("/reference-images")

    const startEditing = () => {
        if (!data || pending || imageLoading) return
        setForm({
            tagId: data.tagId,
            installationCode: data.installationCode,
            inspectionDescription: data.inspectionDescription,
            referenceBlobStorageDirectory: {
                blobContainer: data.referenceImageBlobStorageLocation.blobContainer,
                blobName: getBlobDirectory(data.referenceImageBlobStorageLocation),
            },
            polygon: data.polygon.map((point) => ({ ...point })),
            sourceAnalysisType: data.sourceAnalysisType,
        })
    }

    const handleSave = async () => {
        if (!id || !form || pending) return
        if (
            form.sourceAnalysisType === AnalysisType.Fencilla &&
            form.polygon.length < 3
        ) {
            setError("Please draw a polygon with at least 3 vertices before saving.")
            return
        }
        setError(null)
        try {
            await updateMutation.mutateAsync({ id, request: form })
            setForm(null)
        } catch (e) {
            setError(getErrorMessage(e, "Failed to update reference metadata"))
        }
    }

    const handleDelete = async () => {
        if (!id || pending) return
        setError(null)
        try {
            await deleteMutation.mutateAsync(id)
            navigateBack()
        } catch (e) {
            setError(getErrorMessage(e, "Failed to delete reference metadata"))
        }
    }

    const handlePolygonChange = useCallback((polygon: number[][]) => {
        setForm((prev) => prev && ({
            ...prev,
            polygon: polygon.map(([x, y]) => ({ x, y })),
        }))
    }, [])

    if (loading) {
        return (
            <div style={{ paddingTop: "1rem" }}>
                <Typography variant="body_short">Loading...</Typography>
            </div>
        )
    }

    if (!data) {
        return (
            <div style={{ paddingTop: "1rem" }}>
                <StyledBackNavRowLg>
                    <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back" disabled={pending}>
                        <Icon name="arrow_back" />
                    </Button>
                    <Typography variant="h3">Not Found</Typography>
                </StyledBackNavRowLg>
                {error && <ErrorText variant="body_short">{error}</ErrorText>}
            </div>
        )
    }

    const polygon = (form ?? data).polygon.map((c) => [c.x, c.y])

    return (
        <div style={{ paddingTop: "1rem" }}>
            <StyledHeaderRow>
                <StyledHeaderTitleRow>
                    <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back" disabled={pending}>
                        <Icon name="arrow_back" />
                    </Button>
                    <Typography variant="h3">Reference Metadata</Typography>
                </StyledHeaderTitleRow>
            </StyledHeaderRow>

            <StyledContent>
                {error && (
                    <ErrorText variant="body_short" style={{ marginBottom: "1rem" }}>
                        {error}
                    </ErrorText>
                )}

                <ReferenceImageSection
                    form={form}
                    pending={pending}
                    imageLoading={imageLoading}
                    imageError={imageError}
                    thermalImage={thermalImage}
                    image={image}
                    polygon={polygon}
                    onEdit={startEditing}
                    onDelete={handleDelete}
                    onPolygonChange={handlePolygonChange}
                />

                {editing ? (
                    <ReferenceMetadataEditForm
                        form={form}
                        pending={pending}
                        saving={saving}
                        onChange={setForm}
                        onSave={handleSave}
                        onCancel={() => setForm(null)}
                    />
                ) : (
                    <ReferenceMetadataSummary data={data} />
                )}
            </StyledContent>
        </div>
    )
}
