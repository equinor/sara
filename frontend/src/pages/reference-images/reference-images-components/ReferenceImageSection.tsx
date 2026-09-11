import { Button, Typography } from "@equinor/eds-core-react"
import styled from "styled-components"
import { AnalysisType, type ReferencePolygonMetadataInput, type ThermalImageData } from "../../../api/client"
import ThermalImageViewer from "../../../components/ThermalImageViewer"
import { FencillaImageViewer, FencillaPolygonDrawingEditor } from "../../../components/FencillaImagePolygon"
import { ErrorText } from "../../../components/Styles"

const StyledImageSection = styled.div`
  margin-bottom: 1.5rem;
`

const StyledHeaderRow = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  max-width: 800px;
  margin-bottom: 1.5rem;
`

const StyledActionRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
`

interface Props {
    form: ReferencePolygonMetadataInput | null
    pending: boolean
    imageLoading: boolean
    imageError: string | null
    thermalImage: ThermalImageData | null
    image: string | null
    polygon: number[][]
    onEdit: () => void
    onDelete: () => void
    onPolygonChange: (polygon: number[][]) => void
}

export default function ReferenceImageSection({
    form, pending, imageLoading, imageError, thermalImage, image,
    polygon, onEdit, onDelete, onPolygonChange,
}: Props) {
    const editing = form !== null

    return (
        <StyledImageSection inert={pending}>
            <StyledHeaderRow style={{ marginBottom: "0.75rem" }}>
                <Typography variant="h5">Reference Image</Typography>
                {!editing && (
                    <StyledActionRow style={{ marginTop: 0 }}>
                        <Button onClick={onEdit} disabled={pending || imageLoading}>Edit</Button>
                        <Button variant="outlined" color="danger" onClick={onDelete} disabled={pending}>
                            Delete
                        </Button>
                    </StyledActionRow>
                )}
            </StyledHeaderRow>
            {imageLoading && (
                <Typography variant="body_short">Loading image...</Typography>
            )}
            {imageError && (
                <ErrorText variant="body_short">{imageError}</ErrorText>
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
                editing && form.sourceAnalysisType === AnalysisType.Fencilla ? (
                    <FencillaPolygonDrawingEditor
                        imageUrl={image}
                        initialPolygon={polygon}
                        onPolygonChange={onPolygonChange}
                    />
                ) : (
                    <FencillaImageViewer imageUrl={image} polygon={polygon} />
                )
            )}
        </StyledImageSection>
    )
}
