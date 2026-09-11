import { useParams } from "react-router"
import ReferenceMetadataDetail from "./reference-images-components/ReferenceMetadataDetail"

export default function ReferencePolygonMetadataDetailPage() {
    const { id } = useParams<{ id: string }>()
    return <ReferenceMetadataDetail key={id} id={id} />
}
