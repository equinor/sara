import { useMemo, useRef, useEffect, useState, useCallback } from "react";
import { Stage, Layer, Image as KonvaImage, Line, Circle } from "react-konva";
import styled from "styled-components";
import { Button, Typography } from "@equinor/eds-core-react";
import { applyColormap, createColorBarCanvas } from "../utils/thermalColormap";
import type { KonvaEventObject } from "konva/lib/Node";

export interface PolygonDrawingEditorProps {
  temperatures: Float32Array;
  width: number;
  height: number;
  minTemperature: number;
  maxTemperature: number;
  maxDisplayWidth?: number;
  onPolygonChange: (polygon: number[][]) => void;
}

const Container = styled.div`
  display: flex;
  gap: 1rem;
  align-items: stretch;
`;

const ColorScaleContainer = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  position: relative;
  width: 3rem;
  flex-shrink: 0;
`;

const ColorBarWrapper = styled.div`
  height: 100%;
  display: flex;
  border-radius: 0.25rem;
  border: 1px solid #dcdcdc;
  overflow: hidden;
`;

interface ColorLabelProps {
  variant?: string;
  children?: React.ReactNode;
  className?: string;
}

const ColorLabel = styled(Typography as React.ComponentType<ColorLabelProps>)`
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  white-space: nowrap;
`;

const ColorLabelMax = styled(ColorLabel)`
  bottom: 100%;
  margin-bottom: 0.25rem;
`;

const ColorLabelMin = styled(ColorLabel)`
  top: 100%;
  margin-top: 0.25rem;
`;

const ControlsRow = styled.div`
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
  align-items: center;
`;

const EditorContainer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`;

const COLOR_BAR_WIDTH = 16;
const VERTEX_RADIUS = 6;
const CLOSE_THRESHOLD = 12;

export default function PolygonDrawingEditor({
  temperatures,
  width,
  height,
  minTemperature,
  maxTemperature,
  maxDisplayWidth = 800,
  onPolygonChange,
}: PolygonDrawingEditorProps) {
  const [vertices, setVertices] = useState<number[][]>([]);
  const [isClosed, setIsClosed] = useState(false);

  const thermalCanvas = useMemo(
    () => applyColormap(temperatures, width, height, minTemperature, maxTemperature),
    [temperatures, width, height, minTemperature, maxTemperature]
  );

  const scale = Math.min(1, maxDisplayWidth / width);
  const displayWidth = Math.round(width * scale);
  const displayHeight = Math.round(height * scale);

  const colorBarRef = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const el = colorBarRef.current;
    if (!el) return;
    const barCanvas = createColorBarCanvas(COLOR_BAR_WIDTH, displayHeight);
    el.width = COLOR_BAR_WIDTH;
    el.height = displayHeight;
    const ctx = el.getContext("2d")!;
    ctx.drawImage(barCanvas, 0, 0);
  }, [displayHeight]);

  // Notify parent when polygon changes
  useEffect(() => {
    if (isClosed && vertices.length >= 3) {
      onPolygonChange(vertices);
    } else {
      onPolygonChange([]);
    }
  }, [vertices, isClosed, onPolygonChange]);

  const handleStageClick = useCallback(
    (e: KonvaEventObject<MouseEvent>) => {
      if (isClosed) return;

      const stage = e.target.getStage();
      if (!stage) return;

      const pointer = stage.getPointerPosition();
      if (!pointer) return;

      // Convert display coordinates to image coordinates
      const imageX = pointer.x / scale;
      const imageY = pointer.y / scale;

      // Check if clicking near the first vertex to close polygon
      if (vertices.length >= 3) {
        const firstVertex = vertices[0];
        const dx = pointer.x - firstVertex[0] * scale;
        const dy = pointer.y - firstVertex[1] * scale;
        const distance = Math.sqrt(dx * dx + dy * dy);
        if (distance < CLOSE_THRESHOLD) {
          setIsClosed(true);
          return;
        }
      }

      setVertices((prev) => [...prev, [imageX, imageY]]);
    },
    [isClosed, vertices, scale]
  );

  const handleVertexDragEnd = useCallback(
    (index: number, e: KonvaEventObject<DragEvent>) => {
      const newX = e.target.x() / scale;
      const newY = e.target.y() / scale;
      setVertices((prev) => {
        const updated = [...prev];
        updated[index] = [newX, newY];
        return updated;
      });
    },
    [scale]
  );

  const handleClear = useCallback(() => {
    setVertices([]);
    setIsClosed(false);
    onPolygonChange([]);
  }, [onPolygonChange]);

  const handleUndoLastPoint = useCallback(() => {
    if (isClosed) {
      setIsClosed(false);
      return;
    }
    setVertices((prev) => prev.slice(0, -1));
  }, [isClosed]);

  const handleClosePolygon = useCallback(() => {
    if (vertices.length >= 3) {
      setIsClosed(true);
    }
  }, [vertices]);

  return (
    <EditorContainer>
      <Typography variant="body_short" style={{ color: "#6f6f6f" }}>
        {isClosed
          ? "Polygon drawn. Drag vertices to adjust, or clear to redraw."
          : vertices.length === 0
            ? "Click on the image to start drawing a polygon."
            : `${vertices.length} point${vertices.length > 1 ? "s" : ""} placed. Click near the first point or press "Close polygon" to finish.`}
      </Typography>
      <Container>
        <Stage
          width={displayWidth}
          height={displayHeight}
          onClick={handleStageClick}
          style={{ cursor: isClosed ? "default" : "crosshair" }}
        >
          <Layer>
            <KonvaImage
              image={thermalCanvas}
              width={displayWidth}
              height={displayHeight}
            />
            {/* Draw the polygon lines */}
            {vertices.length >= 2 && (
              <Line
                points={vertices.flat().map((v) => v * scale)}
                closed={isClosed}
                stroke="lime"
                strokeWidth={2}
                fill={isClosed ? "rgba(0, 255, 0, 0.15)" : undefined}
              />
            )}
            {/* Draw vertices as circles */}
            {vertices.map((vertex, index) => (
              <Circle
                key={index}
                x={vertex[0] * scale}
                y={vertex[1] * scale}
                radius={VERTEX_RADIUS}
                fill={index === 0 && !isClosed ? "red" : "lime"}
                stroke="white"
                strokeWidth={1}
                draggable={isClosed}
                onDragEnd={(e) => handleVertexDragEnd(index, e)}
              />
            ))}
          </Layer>
        </Stage>

        <ColorScaleContainer>
          <ColorBarWrapper>
            <canvas
              ref={colorBarRef}
              aria-hidden="true"
              style={{ display: "block", width: COLOR_BAR_WIDTH }}
            />
          </ColorBarWrapper>
          <ColorLabelMax variant="caption">
            {maxTemperature.toFixed(1)}
          </ColorLabelMax>
          <ColorLabelMin variant="caption">
            {minTemperature.toFixed(1)}
          </ColorLabelMin>
        </ColorScaleContainer>
      </Container>

      <ControlsRow>
        {!isClosed && vertices.length >= 3 && (
          <Button variant="outlined" onClick={handleClosePolygon}>
            Close polygon
          </Button>
        )}
        {vertices.length > 0 && (
          <Button variant="outlined" onClick={handleUndoLastPoint}>
            {isClosed ? "Reopen polygon" : "Undo last point"}
          </Button>
        )}
        {vertices.length > 0 && (
          <Button variant="ghost" onClick={handleClear}>
            Clear
          </Button>
        )}
      </ControlsRow>
    </EditorContainer>
  );
}
