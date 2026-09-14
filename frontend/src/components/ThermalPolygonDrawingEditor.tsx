import { PolygonDrawingEditor } from "./PolygonEditor"
import ThermalImageStage from "./ThermalImageStage"

export interface ThermalPolygonDrawingEditorProps {
  temperatures: Float32Array
  width: number
  height: number
  minTemperature: number
  maxTemperature: number
  maxDisplayWidth?: number
  initialPolygon?: number[][]
  onPolygonChange: (polygon: number[][]) => void
}

export default function ThermalPolygonDrawingEditor({
  temperatures,
  width,
  height,
  minTemperature,
  maxTemperature,
  maxDisplayWidth = 800,
  initialPolygon,
  onPolygonChange,
}: ThermalPolygonDrawingEditorProps) {
  return (
    <PolygonDrawingEditor
      initialPolygon={initialPolygon}
      onPolygonChange={onPolygonChange}
      renderStage={(editor) => (
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
          selectedVertexIndex={editor.selectedVertexIndex}
          onStageClick={editor.handleStageClick}
          onVertexSelect={editor.setSelectedVertexIndex}
          onVertexDragEnd={editor.handleVertexDragEnd}
        />
      )}
    />
  )
}
