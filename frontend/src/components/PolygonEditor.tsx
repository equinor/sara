import { useCallback, useEffect, useState } from "react"
import styled from "styled-components"
import { Button } from "@equinor/eds-core-react"
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

    useEffect(() => {
        onPolygonChange(isClosed && vertices.length >= 3 ? vertices : [])
    }, [vertices, isClosed, onPolygonChange])

    // pointerX/pointerY are in display (scaled) space; scale converts to image space.
    const handleStageClick = useCallback(
        (pointerX: number, pointerY: number, scale: number) => {
            if (isClosed) return

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
    }, [])

    const handleUndoLastPoint = useCallback(() => {
        if (isClosed) {
            setIsClosed(false)
            return
        }
        setVertices((prev) => prev.slice(0, -1))
    }, [isClosed])

    const handleClosePolygon = useCallback(() => {
        setIsClosed((prev) => prev || vertices.length >= 3)
    }, [vertices])

    const instructions = isClosed
        ? "Polygon drawn. Drag vertices to adjust, or clear to redraw."
        : vertices.length === 0
            ? "Click on the image to start drawing a polygon."
            : `${vertices.length} point${vertices.length > 1 ? "s" : ""} placed. Click near the first point or press "Close polygon" to finish.`

    return {
        vertices,
        isClosed,
        instructions,
        handleStageClick,
        handleVertexDragEnd,
        handleClear,
        handleUndoLastPoint,
        handleClosePolygon,
    }
}

export interface PolygonEditorControlsProps {
    vertices: number[][]
    isClosed: boolean
    onClosePolygon: () => void
    onUndoLastPoint: () => void
    onClear: () => void
}

export function PolygonEditorControls({
    vertices,
    isClosed,
    onClosePolygon,
    onUndoLastPoint,
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
    onVertexDragEnd?: (index: number, imageX: number, imageY: number) => void
}

export function PolygonOverlay({
    vertices,
    scale,
    isClosed,
    editable,
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
                        fill={index === 0 && !isClosed ? "red" : "lime"}
                        stroke="white"
                        strokeWidth={1}
                        draggable={isClosed}
                        onDragEnd={(e) =>
                            onVertexDragEnd?.(index, e.target.x() / scale, e.target.y() / scale)
                        }
                    />
                ))}
        </>
    )
}
