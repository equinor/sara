import { useCallback, useEffect, useState } from "react"
import type { ReactNode } from "react"
import styled from "styled-components"
import { Button, Typography } from "@equinor/eds-core-react"
import { Line, Circle } from "react-konva"
import type { KonvaEventObject } from "konva/lib/Node"

const CLOSE_THRESHOLD = 12
const VERTEX_RADIUS = 6

export const EditorContainer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const ControlsRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
  align-items: center;
`

export function usePolygonEditor(
    onPolygonChange: (polygon: number[][]) => void,
    initialPolygon: number[][] = []
) {
    const [vertices, setVertices] = useState<number[][]>(initialPolygon)
    const [isClosed, setIsClosed] = useState(initialPolygon.length >= 3)
    const [selectedVertexIndex, setSelectedVertexIndex] = useState<number | null>(null)

    useEffect(() => {
        onPolygonChange(isClosed && vertices.length >= 3 ? vertices : [])
    }, [vertices, isClosed, onPolygonChange])

    // pointerX/pointerY are in display (scaled) space; scale converts to image space.
    const handleStageClick = useCallback(
        (pointerX: number, pointerY: number, scale: number) => {
            if (isClosed) return
            setSelectedVertexIndex(null)

            if (vertices.length >= 3) {
                const [firstX, firstY] = vertices[0]
                const distance = Math.hypot(
                    pointerX - firstX * scale,
                    pointerY - firstY * scale
                )
                if (distance < CLOSE_THRESHOLD) {
                    setIsClosed(true)
                    return
                }
            }

            setVertices((prev) => [...prev, [pointerX / scale, pointerY / scale]])
        },
        [isClosed, vertices]
    )

    const handleVertexDragEnd = useCallback((index: number, x: number, y: number) => {
        setVertices((prev) => {
            const updated = [...prev]
            updated[index] = [x, y]
            return updated
        })
    }, [])

    const handleClear = useCallback(() => {
        setVertices([])
        setIsClosed(false)
        setSelectedVertexIndex(null)
    }, [])

    const handleUndoLastPoint = useCallback(() => {
        if (isClosed) {
            setIsClosed(false)
            setSelectedVertexIndex(null)
            return
        }
        setVertices((prev) => prev.slice(0, -1))
        setSelectedVertexIndex(null)
    }, [isClosed])

    const handleRemoveSelectedVertex = useCallback(() => {
        if (selectedVertexIndex === null) return
        setVertices((prev) => prev.filter((_, index) => index !== selectedVertexIndex))
        if (vertices.length <= 3) setIsClosed(false)
        setSelectedVertexIndex(null)
    }, [selectedVertexIndex, vertices.length])

    const handleClosePolygon = useCallback(() => {
        setIsClosed((prev) => prev || vertices.length >= 3)
    }, [vertices])

    const instructions = isClosed
        ? "Polygon drawn. Drag vertices to adjust, or select a vertex to remove it."
        : vertices.length === 0
            ? "Click on the image to start drawing a polygon."
            : `${vertices.length} point${vertices.length > 1 ? "s" : ""} placed. Click near the first point or press "Close polygon" to finish.`

    return {
        vertices,
        isClosed,
        selectedVertexIndex,
        instructions,
        handleStageClick,
        handleVertexDragEnd,
        handleClear,
        handleUndoLastPoint,
        handleRemoveSelectedVertex,
        handleClosePolygon,
        setSelectedVertexIndex,
    }
}

type PolygonEditorState = ReturnType<typeof usePolygonEditor>

export interface PolygonDrawingEditorProps {
    initialPolygon?: number[][]
    onPolygonChange: (polygon: number[][]) => void
    renderStage: (editor: PolygonEditorState) => ReactNode
}

export function PolygonDrawingEditor({
    initialPolygon,
    onPolygonChange,
    renderStage,
}: PolygonDrawingEditorProps) {
    const editor = usePolygonEditor(onPolygonChange, initialPolygon)

    return (
        <EditorContainer>
            <Typography variant="body_short">{editor.instructions}</Typography>
            {renderStage(editor)}
            <PolygonEditorControls
                vertices={editor.vertices}
                isClosed={editor.isClosed}
                onClosePolygon={editor.handleClosePolygon}
                onUndoLastPoint={editor.handleUndoLastPoint}
                selectedVertexIndex={editor.selectedVertexIndex}
                onRemoveSelectedVertex={editor.handleRemoveSelectedVertex}
                onClear={editor.handleClear}
            />
        </EditorContainer>
    )
}

export interface PolygonEditorControlsProps {
    vertices: number[][]
    isClosed: boolean
    onClosePolygon: () => void
    onUndoLastPoint: () => void
    selectedVertexIndex: number | null
    onRemoveSelectedVertex: () => void
    onClear: () => void
}

export function PolygonEditorControls({
    vertices,
    isClosed,
    onClosePolygon,
    onUndoLastPoint,
    selectedVertexIndex,
    onRemoveSelectedVertex,
    onClear,
}: PolygonEditorControlsProps) {
    return (
        <ControlsRow>
            {!isClosed && vertices.length >= 3 && (
                <Button variant="outlined" onClick={onClosePolygon}>
                    Close polygon
                </Button>
            )}
            {vertices.length > 0 && (
                <Button variant="outlined" onClick={onUndoLastPoint}>
                    {isClosed ? "Reopen polygon" : "Undo last point"}
                </Button>
            )}
            {selectedVertexIndex !== null && (
                <Button variant="outlined" color="danger" onClick={onRemoveSelectedVertex}>
                    Remove vertex
                </Button>
            )}
            {vertices.length > 0 && (
                <Button variant="ghost" onClick={onClear}>
                    Clear
                </Button>
            )}
        </ControlsRow>
    )
}

export function createStageClickHandler(
    editable: boolean,
    scale: number,
    onStageClick?: (pointerX: number, pointerY: number, scale: number) => void
) {
    return (e: KonvaEventObject<MouseEvent>) => {
        if (!editable) return
        const pointer = e.target.getStage()?.getPointerPosition()
        if (!pointer) return
        onStageClick?.(pointer.x, pointer.y, scale)
    }
}

export interface PolygonOverlayProps {
    vertices: number[][]
    scale: number
    isClosed: boolean
    editable: boolean
    selectedVertexIndex?: number | null
    onVertexSelect?: (index: number) => void
    onVertexDragEnd?: (index: number, imageX: number, imageY: number) => void
}

export function PolygonOverlay({
    vertices,
    scale,
    isClosed,
    editable,
    selectedVertexIndex,
    onVertexSelect,
    onVertexDragEnd,
}: PolygonOverlayProps) {
    return (
        <>
            {vertices.length >= 2 && (
                <Line
                    points={vertices.flat().map((v) => v * scale)}
                    closed={isClosed}
                    stroke="lime"
                    strokeWidth={2}
                    fill={isClosed ? "rgba(0, 255, 0, 0.15)" : undefined}
                />
            )}
            {editable &&
                vertices.map((vertex, index) => (
                    <Circle
                        key={index}
                        x={vertex[0] * scale}
                        y={vertex[1] * scale}
                        radius={VERTEX_RADIUS}
                        fill={selectedVertexIndex === index ? "orange" : index === 0 && !isClosed ? "red" : "lime"}
                        stroke="white"
                        strokeWidth={1}
                        draggable={isClosed}
                        onClick={(e) => {
                            e.cancelBubble = true
                            onVertexSelect?.(index)
                        }}
                        onTap={(e) => {
                            e.cancelBubble = true
                            onVertexSelect?.(index)
                        }}
                        onDragEnd={(e) =>
                            onVertexDragEnd?.(index, e.target.x() / scale, e.target.y() / scale)
                        }
                    />
                ))}
        </>
    )
}
