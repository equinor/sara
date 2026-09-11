import { Typography } from "@equinor/eds-core-react"
import styled from "styled-components"
import {
    AnalysisType,
    getThermalInspectionRecords,
    getFencillaInspectionRecords,
    type InspectionRecord,
    type ThermalImageData,
} from "../../../api/client"
import InspectionRecordSelector from "../../../components/InspectionRecordSelector"
import ThermalPolygonDrawingEditor from "../../../components/ThermalPolygonDrawingEditor"
import { FencillaPolygonDrawingEditor } from "../../../components/FencillaImagePolygon"

const StyledImageSection = styled.div`
  margin-top: 1rem;
  position: relative;
`

const LoadingOverlay = styled.div`
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: rgba(255, 255, 255, 0.6);
  z-index: 1;
  border-radius: 0.25rem;
`

interface Props {
    isThermal: boolean
    selectedRecord: InspectionRecord | null
    thermalImageData: ThermalImageData | null
    fencillaImageUrl: string | null
    loadingImage: boolean
    onRecordSelect: (record: InspectionRecord) => void
    onPolygonChange: (polygon: number[][]) => void
}

export default function InspectionReferenceSourceSection({
    isThermal, selectedRecord, thermalImageData, fencillaImageUrl,
    loadingImage, onRecordSelect, onPolygonChange,
}: Props) {
    return (
        <>
            {isThermal ? (
                <InspectionRecordSelector
                    title="Select a thermal inspection record"
                    analysisType={AnalysisType.ThermalReading}
                    fetchRecords={getThermalInspectionRecords}
                    onSelect={onRecordSelect}
                    selectedId={selectedRecord?.id}
                />
            ) : (
                <InspectionRecordSelector
                    title="Select a fencilla inspection record"
                    analysisType={AnalysisType.Fencilla}
                    fetchRecords={getFencillaInspectionRecords}
                    onSelect={onRecordSelect}
                    selectedId={selectedRecord?.id}
                />
            )}

            {(thermalImageData || fencillaImageUrl) && (
                <StyledImageSection>
                    {loadingImage && (
                        <LoadingOverlay>
                            <Typography variant="body_short">
                                Loading source image...
                            </Typography>
                        </LoadingOverlay>
                    )}
                    {isThermal && thermalImageData ? (
                        <ThermalPolygonDrawingEditor
                            temperatures={thermalImageData.temperatures}
                            width={thermalImageData.width}
                            height={thermalImageData.height}
                            minTemperature={thermalImageData.minTemperature}
                            maxTemperature={thermalImageData.maxTemperature}
                            onPolygonChange={onPolygonChange}
                        />
                    ) : (
                        fencillaImageUrl && (
                            <FencillaPolygonDrawingEditor
                                imageUrl={fencillaImageUrl}
                                onPolygonChange={onPolygonChange}
                            />
                        )
                    )}
                </StyledImageSection>
            )}

            {!thermalImageData && !fencillaImageUrl && loadingImage && (
                <StyledImageSection>
                    <Typography variant="body_short">
                        Loading source image...
                    </Typography>
                </StyledImageSection>
            )}
        </>
    )
}
