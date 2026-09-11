import type { ReactNode } from "react"
import { Button, Typography } from "@equinor/eds-core-react"
import styled from "styled-components"
import { AnalysisType, type ReferencePolygonMetadataInput } from "../../../api/client"
import SegmentedToggle from "../../../components/SegmentedToggle"
import ReferenceMetadataFields from "./ReferenceMetadataFields"
import ReferenceBlobDirectoryFields from "./ReferenceBlobDirectoryFields"

const StyledFormContainer = styled.div`
  max-width: 640px;
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const StyledActionRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
`

const SOURCE_ANALYSIS_TYPES: { value: AnalysisType; label: string }[] = [
    { value: AnalysisType.ThermalReading, label: "Thermal Reading" },
    { value: AnalysisType.Fencilla, label: "Fencilla" },
]

const CREATE_MODES: { value: "manual" | "inspection"; label: string }[] = [
    { value: "manual", label: "Manual blob path" },
    { value: "inspection", label: "From inspection record" },
]

interface Props {
    form: ReferencePolygonMetadataInput
    creating: boolean
    fromInspection: boolean
    canSubmitFromInspection: boolean
    onChange: (form: ReferencePolygonMetadataInput) => void
    onSourceAnalysisTypeChange: (sourceAnalysisType: AnalysisType) => void
    onModeChange: (mode: "manual" | "inspection") => void
    onCreateManual: () => void
    onCreateFromInspection: () => void
    onCancel: () => void
    children: ReactNode
}

export default function CreateReferenceMetadataForm({
    form, creating, fromInspection, canSubmitFromInspection, onChange,
    onSourceAnalysisTypeChange, onModeChange, onCreateManual,
    onCreateFromInspection, onCancel, children,
}: Props) {
    return (
        <StyledFormContainer inert={creating}>
            <div>
                <Typography variant="body_short_bold" style={{ marginBottom: "0.5rem" }}>
                    Source Analysis Type
                </Typography>
                <SegmentedToggle
                    options={SOURCE_ANALYSIS_TYPES}
                    value={form.sourceAnalysisType}
                    onChange={onSourceAnalysisTypeChange}
                />
            </div>

            <ReferenceMetadataFields form={form} onChange={onChange} />

            <SegmentedToggle
                options={CREATE_MODES}
                value={fromInspection ? "inspection" : "manual"}
                onChange={onModeChange}
            />

            {!fromInspection && (
                <>
                    <ReferenceBlobDirectoryFields form={form} onChange={onChange} />
                    <StyledActionRow>
                        <Button onClick={onCreateManual} disabled={creating}>
                            {creating ? "Creating..." : "Create"}
                        </Button>
                        <Button variant="ghost" onClick={onCancel} disabled={creating}>
                            Cancel
                        </Button>
                    </StyledActionRow>
                </>
            )}

            {fromInspection && (
                <>
                    {children}
                    <StyledActionRow>
                        <Button
                            onClick={onCreateFromInspection}
                            disabled={creating || !canSubmitFromInspection}
                        >
                            {creating ? "Creating..." : "Create"}
                        </Button>
                        <Button variant="ghost" onClick={onCancel} disabled={creating}>
                            Cancel
                        </Button>
                    </StyledActionRow>
                </>
            )}
        </StyledFormContainer>
    )
}
