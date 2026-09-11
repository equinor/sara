import { Button } from "@equinor/eds-core-react"
import styled from "styled-components"
import type { ReferencePolygonMetadataInput } from "../../../api/client"
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

interface Props {
    form: ReferencePolygonMetadataInput
    pending: boolean
    saving: boolean
    onChange: (form: ReferencePolygonMetadataInput) => void
    onSave: () => void
    onCancel: () => void
}

export default function ReferenceMetadataEditForm({
    form, pending, saving, onChange, onSave, onCancel,
}: Props) {
    return (
        <StyledFormContainer inert={pending}>
            <ReferenceMetadataFields form={form} onChange={onChange} />
            <ReferenceBlobDirectoryFields form={form} onChange={onChange} />
            <StyledActionRow>
                <Button onClick={onSave} disabled={pending}>
                    {saving ? "Saving..." : "Save"}
                </Button>
                <Button variant="ghost" onClick={onCancel} disabled={pending}>
                    Cancel
                </Button>
            </StyledActionRow>
        </StyledFormContainer>
    )
}
