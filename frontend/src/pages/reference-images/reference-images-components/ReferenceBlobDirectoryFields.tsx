import { TextField, Typography } from "@equinor/eds-core-react"
import styled from "styled-components"
import type { ReferencePolygonMetadataInput } from "../../../api/client"

const StyledBlobSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
  border: 1px solid #dcdcdc;
  border-radius: 0.5rem;
`

interface Props {
    form: ReferencePolygonMetadataInput
    onChange: (form: ReferencePolygonMetadataInput) => void
}

export default function ReferenceBlobDirectoryFields({ form, onChange }: Props) {
    return (
        <StyledBlobSection>
            <Typography variant="h6">Reference Blob Storage Directory</Typography>
            <TextField
                id="blobContainer"
                label="Blob Container"
                value={form.referenceBlobStorageDirectory.blobContainer}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                    onChange({
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
                    onChange({
                        ...form,
                        referenceBlobStorageDirectory: {
                            ...form.referenceBlobStorageDirectory,
                            blobName: e.target.value,
                        },
                    })
                }
            />
        </StyledBlobSection>
    )
}
