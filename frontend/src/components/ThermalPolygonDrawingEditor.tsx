import { Typography } from "@equinor/eds-core-react"
import { usePolygonEditor, EditorContainer, PolygonEditorControls } from "./PolygonEditor"
import ThermalImageStage from "./ThermalImageStage"

export interface ThermalPolygonDrawingEditorProps {
  temperatures: Float32Array
  width: number
  height: number
  minTemperature: number
  maxTemperature: number
  maxDisplayWidth?: number
  onPolygonChange: (polygon: number[][]) => void
}

export default function ThermalPolygonDrawingEditor({
  temperatures,
  width,
  height,
  minTemperature,
  maxTemperature,
  maxDisplayWidth = 800,
  onPolygonChange,
}: ThermalPolygonDrawingEditorProps) {
  const editor = usePolygonEditor(onPolygonChange)

  return (
    <EditorContainer>
      <Typography variant="body_short" style={{ color: "#6f6f6f" }}>
        {editor.instructions}
      </Typography>

      <ThermalImageStage
        temperatures={temperatures}
        width={width}
        height={height}
        minTemperature={minTemperature}
        maxTemperature={maxTemperature}
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
