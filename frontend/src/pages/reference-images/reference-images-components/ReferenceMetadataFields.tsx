import { TextField } from "@equinor/eds-core-react"
import type { ReferencePolygonMetadataInput } from "../../../api/client"

interface Props {
    form: ReferencePolygonMetadataInput
    onChange: (form: ReferencePolygonMetadataInput) => void
}

export default function ReferenceMetadataFields({ form, onChange }: Props) {
    return (
        <>
            <TextField
                id="tagId"
                label="Tag ID"
                value={form.tagId}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                    onChange({ ...form, tagId: e.target.value })
                }
            />
            <TextField
                id="installationCode"
                label="Installation Code"
                value={form.installationCode}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                    onChange({ ...form, installationCode: e.target.value })
                }
            />
            <TextField
                id="inspectionDescription"
                label="Inspection Description"
                value={form.inspectionDescription}
                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                    onChange({ ...form, inspectionDescription: e.target.value })
                }
            />
        </>
    )
}
