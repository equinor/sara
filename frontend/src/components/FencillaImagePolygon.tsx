import { useState, useEffect } from "react"
import { Stage, Layer, Image as KonvaImage } from "react-konva"
import { Typography } from "@equinor/eds-core-react"
import { usePolygonEditor, EditorContainer, PolygonEditorControls, createStageClickHandler, PolygonOverlay } from "./PolygonEditor"
import { ErrorText } from "./Styles"

function useHtmlImage(url: string) {
    const [image, setImage] = useState<HTMLImageElement | null>(null)
    const [error, setError] = useState(false)

    useEffect(() => {
        setImage(null)
        setError(false)
        const img = new Image()
        img.onload = () => setImage(img)
        img.onerror = () => setError(true)
        img.src = url
        return () => {
            img.onload = null
            img.onerror = null
        }
    }, [url])

    return { image, error }
}

interface FencillaImageStageProps {
    imageUrl: string
    maxDisplayWidth: number
    vertices: number[][]
    isClosed: boolean
    editable: boolean
    onStageClick?: (pointerX: number, pointerY: number, scale: number) => void
    onVertexDragEnd?: (index: number, imageX: number, imageY: number) => void
}

function FencillaImageStage({
    imageUrl,
    maxDisplayWidth,
    vertices,
    isClosed,
    editable,
    onStageClick,
    onVertexDragEnd,
}: FencillaImageStageProps) {
    const { image, error } = useHtmlImage(imageUrl)

    if (error) {
        return (
            <ErrorText variant="body_short">
                Failed to load image.
            </ErrorText>
        )
    }

    if (!image) {
        return <Typography variant="body_short">Loading image...</Typography>
    }

    const scale = Math.min(1, maxDisplayWidth / image.naturalWidth)
    const displayWidth = Math.round(image.naturalWidth * scale)
    const displayHeight = Math.round(image.naturalHeight * scale)

    const handleClick = createStageClickHandler(editable, scale, onStageClick)

    return (
        <Stage
            width={displayWidth}
            height={displayHeight}
            onClick={handleClick}
            style={{ cursor: editable && !isClosed ? "crosshair" : "default" }}
        >
            <Layer>
                <KonvaImage image={image} width={displayWidth} height={displayHeight} />
                <PolygonOverlay
                    vertices={vertices}
                    scale={scale}
                    isClosed={isClosed}
                    editable={editable}
                    onVertexDragEnd={onVertexDragEnd}
                />
            </Layer>
        </Stage>
    )
}

export interface FencillaImageViewerProps {
    imageUrl: string
    polygon?: number[][]
    maxDisplayWidth?: number
}

export function FencillaImageViewer({
    imageUrl,
    polygon,
    maxDisplayWidth = 800,
}: FencillaImageViewerProps) {
    return (
        <FencillaImageStage
            imageUrl={imageUrl}
            maxDisplayWidth={maxDisplayWidth}
            vertices={polygon ?? []}
            isClosed
            editable={false}
        />
    )
}

export interface FencillaPolygonDrawingEditorProps {
    imageUrl: string
    maxDisplayWidth?: number
    initialPolygon?: number[][]
    onPolygonChange: (polygon: number[][]) => void
}

export function FencillaPolygonDrawingEditor({
    imageUrl,
    maxDisplayWidth = 800,
    initialPolygon,
    onPolygonChange,
}: FencillaPolygonDrawingEditorProps) {
    const editor = usePolygonEditor(onPolygonChange, initialPolygon)

    return (
        <EditorContainer>
            <Typography variant="body_short">{editor.instructions}</Typography>

            <FencillaImageStage
                imageUrl={imageUrl}
                maxDisplayWidth={maxDisplayWidth}
                vertices={editor.vertices}
                isClosed={editor.isClosed}
                editable
                onStageClick={editor.handleStageClick}
                onVertexDragEnd={editor.handleVertexDragEnd}
            />

            <PolygonEditorControls
                vertices={editor.vertices}
                isClosed={editor.isClosed}
                onClosePolygon={editor.handleClosePolygon}
                onUndoLastPoint={editor.handleUndoLastPoint}
                onClear={editor.handleClear}
            />
        </EditorContainer>
    )
}
