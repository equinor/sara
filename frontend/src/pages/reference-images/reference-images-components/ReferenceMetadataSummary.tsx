import { Fragment } from "react"
import { Typography } from "@equinor/eds-core-react"
import styled from "styled-components"
import { AnalysisType, type BlobStorageLocation, type ReferencePolygonMetadata } from "../../../api/client"

const StyledDetailGrid = styled.div`
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 0.5rem 1rem;
  margin-bottom: 1.5rem;
`

function formatBlobLocation(location: BlobStorageLocation) {
    return `${location.storageAccount}/${location.blobContainer}/${location.blobName}`
}

const SOURCE_ANALYSIS_TYPE_LABELS: Record<AnalysisType, string> = {
    [AnalysisType.ThermalReading]: "Thermal Reading",
    [AnalysisType.Fencilla]: "Fencilla",
    [AnalysisType.CLOE]: "CLOE",
    [AnalysisType.CO2]: "CO2",
}

export default function ReferenceMetadataSummary({ data }: { data: ReferencePolygonMetadata }) {
    const fields = [
        ["Tag ID", data.tagId],
        ["Installation Code", data.installationCode.toUpperCase()],
        ["Inspection Description", data.inspectionDescription],
        ["Source Analysis Type", SOURCE_ANALYSIS_TYPE_LABELS[data.sourceAnalysisType]],
        ["Date Created", new Date(data.dateCreated).toLocaleString()],
        ["Reference Image", formatBlobLocation(data.referenceImageBlobStorageLocation)],
        ["Reference Polygon", `${data.polygon.length} vertices`],
    ]

    return (
        <StyledDetailGrid>
            {fields.map(([label, value]) => (
                <Fragment key={label}>
                    <Typography variant="body_short_bold">{label}</Typography>
                    <Typography variant="body_short">{value}</Typography>
                </Fragment>
            ))}
        </StyledDetailGrid>
    )
}
