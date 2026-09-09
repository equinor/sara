import { useEffect, useState, useCallback } from "react"
import {
    Button,
    Typography,
    TextField,
    Icon,
} from "@equinor/eds-core-react"
import { arrow_back } from "@equinor/eds-icons"
import { useNavigate, useParams } from "react-router"
import styled from "styled-components"
import {
    getReferencePolygonMetadataById,
    updateReferencePolygonMetadata,
    deleteReferencePolygonMetadata,
    getReferencePolygonImageData,
    getReferencePolygonImageUrl,
    AnalysisType,
    type ReferencePolygonMetadata,
    type ReferencePolygonMetadataInput,
    type ThermalImageData,
    type BlobStorageLocation,
} from "../../api/client"
import ThermalImageViewer from "../../components/ThermalImageViewer"
import { FencillaImageViewer, FencillaPolygonDrawingEditor } from "../../components/FencillaImagePolygon"

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

const StyledDetailGrid = styled.div`
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 0.5rem 1rem;
  margin-bottom: 1.5rem;
`

const StyledFormContainer = styled.div`
  max-width: 640px;
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const StyledBlobSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
  border: 1px solid #dcdcdc;
  border-radius: 0.5rem;
`

const StyledActionRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
`

const StyledImageSection = styled.div`
  margin-bottom: 1.5rem;
`

Icon.add({ arrow_back })

function formatBlobLocation(location: BlobStorageLocation) {
    return `${location.storageAccount}/${location.blobContainer}/${location.blobName}`
}

function getBlobDirectory(location: BlobStorageLocation) {
    return location.blobName.split("/").slice(0, -1).join("/")
}

function getErrorMessage(e: unknown, fallback: string) {
    return e instanceof Error ? e.message : fallback
}

const SOURCE_ANALYSIS_TYPE_LABELS: Record<AnalysisType, string> = {
    [AnalysisType.ThermalReading]: "Thermal Reading",
    [AnalysisType.Fencilla]: "Fencilla",
    [AnalysisType.CLOE]: "CLOE",
    [AnalysisType.CO2]: "CO2",
}

function DetailRow({ label, value }: { label: string; value: string }) {
    return (
        <>
            <Typography variant="body_short_bold">{label}</Typography>
            <Typography variant="body_short">{value}</Typography>
        </>
    )
}

export default function ReferencePolygonMetadataDetailPage() {
    const { id } = useParams<{ id: string }>()
    const navigate = useNavigate()
    const [data, setData] = useState<ReferencePolygonMetadata | null>(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [editing, setEditing] = useState(false)
    const [saving, setSaving] = useState(false)
    const [form, setForm] = useState<ReferencePolygonMetadataInput>({
        tagId: "",
        installationCode: "",
        inspectionDescription: "",
        referenceBlobStorageDirectory: { blobContainer: "", blobName: "" },
        polygon: [],
        sourceAnalysisType: AnalysisType.ThermalReading,
    })
    const [thermalImage, setThermalImage] = useState<ThermalImageData | null>(
        null
    )
    const [image, setImage] = useState<string | null>(null)
    const [imageLoading, setImageLoading] = useState(false)
    const [imageError, setImageError] = useState<string | null>(null)

    const fetchData = useCallback(async () => {
        if (!id) return
        setLoading(true)
        setError(null)
        try {
            const result = await getReferencePolygonMetadataById(id)
            setData(result)
        } catch (e) {
            setError(getErrorMessage(e, "Failed to fetch reference metadata"))
        } finally {
            setLoading(false)
        }
    }, [id])

    useEffect(() => {
        fetchData()
    }, [fetchData])

    useEffect(() => {
        if (!id || !data) return
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
    }, [id, data])

    const navigateBack = () => navigate("/reference-images")

    const startEditing = () => {
        if (!data) return
        setForm({
            tagId: data.tagId,
            installationCode: data.installationCode,
            inspectionDescription: data.inspectionDescription,
            referenceBlobStorageDirectory: {
                blobContainer: data.referenceImageBlobStorageLocation.blobContainer,
                blobName: getBlobDirectory(data.referenceImageBlobStorageLocation),
            },
            polygon: data.polygon,
            sourceAnalysisType: data.sourceAnalysisType,
        })
        setEditing(true)
    }

    const handleSave = async () => {
        if (!id) return
        if (
            form.sourceAnalysisType === AnalysisType.Fencilla &&
            form.polygon.length < 3
        ) {
            setError("Please draw a polygon with at least 3 vertices before saving.")
            return
        }
        setSaving(true)
        setError(null)
        try {
            await updateReferencePolygonMetadata(id, form)
            setEditing(false)
            await fetchData()
        } catch (e) {
            setError(getErrorMessage(e, "Failed to update reference metadata"))
        } finally {
            setSaving(false)
        }
    }

    const handleDelete = async () => {
        if (!id) return
        try {
            await deleteReferencePolygonMetadata(id)
            navigateBack()
        } catch (e) {
            setError(getErrorMessage(e, "Failed to delete reference metadata"))
        }
    }

    const handlePolygonChange = useCallback((polygon: number[][]) => {
        setForm((prev) => ({
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
                    <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
                        <Icon name="arrow_back" />
                    </Button>
                    <Typography variant="h3">Not Found</Typography>
                </StyledBackNavRowLg>
                {error && (
                    <Typography
                        variant="body_short"
                        style={{ color: "#eb0000" }}
                    >
                        {error}
                    </Typography>
                )}
            </div>
        )
    }

    const polygon = data.polygon.map((c) => [c.x, c.y])

    return (
        <div style={{ paddingTop: "1rem" }}>
            <StyledHeaderRow>
                <StyledHeaderTitleRow>
                    <Button variant="ghost_icon" onClick={navigateBack} aria-label="Back">
                        <Icon name="arrow_back" />
                    </Button>
                    <Typography variant="h3">Reference Metadata</Typography>
                </StyledHeaderTitleRow>
            </StyledHeaderRow>

            <StyledContent>
                {error && (
                    <Typography
                        variant="body_short"
                        style={{ marginBottom: "1rem", color: "#eb0000" }}
                    >
                        {error}
                    </Typography>
                )}

                <StyledImageSection>
                    <StyledHeaderRow style={{ marginBottom: "0.75rem" }}>
                        <Typography variant="h5">Reference Image</Typography>
                        {!editing && (
                            <StyledActionRow style={{ marginTop: 0 }}>
                                <Button onClick={startEditing}>Edit</Button>
                                <Button variant="outlined" color="danger" onClick={handleDelete}>
                                    Delete
                                </Button>
                            </StyledActionRow>
                        )}
                    </StyledHeaderRow>
                    {imageLoading && (
                        <Typography variant="body_short">Loading image...</Typography>
                    )}
                    {imageError && (
                        <Typography variant="body_short" style={{ color: "#eb0000" }}>
                            {imageError}
                        </Typography>
                    )}
                    {thermalImage && (
                        <ThermalImageViewer
                            temperatures={thermalImage.temperatures}
                            width={thermalImage.width}
                            height={thermalImage.height}
                            minTemperature={thermalImage.minTemperature}
                            maxTemperature={thermalImage.maxTemperature}
                            polygon={polygon}
                        />
                    )}
                    {image && (
                        editing && data.sourceAnalysisType === AnalysisType.Fencilla ? (
                            <FencillaPolygonDrawingEditor
                                imageUrl={image}
                                initialPolygon={polygon}
                                onPolygonChange={handlePolygonChange}
                            />
                        ) : (
                            <FencillaImageViewer imageUrl={image} polygon={polygon} />
                        )
                    )}
                </StyledImageSection>

                {editing ? (
                    <StyledFormContainer>
                        <TextField
                            id="tagId"
                            label="Tag ID"
                            value={form.tagId}
                            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                setForm({ ...form, tagId: e.target.value })
                            }
                        />
                        <TextField
                            id="installationCode"
                            label="Installation Code"
                            value={form.installationCode}
                            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                setForm({ ...form, installationCode: e.target.value })
                            }
                        />
                        <TextField
                            id="inspectionDescription"
                            label="Inspection Description"
                            value={form.inspectionDescription}
                            onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                setForm({ ...form, inspectionDescription: e.target.value })
                            }
                        />

                        <StyledBlobSection>
                            <Typography variant="h6">Reference Blob Storage Directory</Typography>
                            <TextField
                                id="blobContainer"
                                label="Blob Container"
                                value={form.referenceBlobStorageDirectory.blobContainer}
                                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                    setForm({
                                        ...form,
                                        referenceBlobStorageDirectory: {
                                            ...form.referenceBlobStorageDirectory,
                                            blobContainer: e.target.value,
                                        },
                                    })
                                }
                            />
                            <TextField
                                id="blobDirectory"
                                label="Blob Directory"
                                value={form.referenceBlobStorageDirectory.blobName}
                                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                    setForm({
                                        ...form,
                                        referenceBlobStorageDirectory: {
                                            ...form.referenceBlobStorageDirectory,
                                            blobName: e.target.value,
                                        },
                                    })
                                }
                            />
                        </StyledBlobSection>

                        <StyledActionRow>
                            <Button onClick={handleSave} disabled={saving}>
                                {saving ? "Saving..." : "Save"}
                            </Button>
                            <Button variant="ghost" onClick={() => setEditing(false)}>
                                Cancel
                            </Button>
                        </StyledActionRow>
                    </StyledFormContainer>
                ) : (
                    <StyledDetailGrid>
                        <DetailRow label="Tag ID" value={data.tagId} />
                        <DetailRow
                            label="Installation Code"
                            value={data.installationCode.toUpperCase()}
                        />
                        <DetailRow label="Inspection Description" value={data.inspectionDescription} />
                        <DetailRow
                            label="Source Analysis Type"
                            value={SOURCE_ANALYSIS_TYPE_LABELS[data.sourceAnalysisType]}
                        />
                        <DetailRow
                            label="Date Created"
                            value={new Date(data.dateCreated).toLocaleString()}
                        />
                        <DetailRow
                            label="Reference Image"
                            value={formatBlobLocation(data.referenceImageBlobStorageLocation)}
                        />
                        <DetailRow
                            label="Reference Polygon"
                            value={`${data.polygon.length} vertices`}
                        />
                    </StyledDetailGrid>
                )}
            </StyledContent>
        </div>
    )
}
