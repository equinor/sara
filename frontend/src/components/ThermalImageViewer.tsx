import { useMemo, useRef, useEffect } from "react";
import { Stage, Layer, Image as KonvaImage } from "react-konva";
import styled from "styled-components";
import { Typography } from "@equinor/eds-core-react";
import { applyColormap, createColorBarCanvas } from "../utils/thermalColormap";

export interface ThermalImageViewerProps {
  temperatures: Float32Array;
  width: number;
  height: number;
  minTemperature: number;
  maxTemperature: number;
  /** Maximum display width in CSS pixels. The image scales to fit. */
  maxDisplayWidth?: number;
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

// Cast to avoid TS2590 – eds Typography's variant union is too complex for styled-components inference.
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

const COLOR_BAR_WIDTH = 16;

export default function ThermalImageViewer({
  temperatures,
  width,
  height,
  minTemperature,
  maxTemperature,
  maxDisplayWidth = 800,
}: ThermalImageViewerProps) {
  const thermalCanvas = useMemo(
    () => applyColormap(temperatures, width, height, minTemperature, maxTemperature),
    [temperatures, width, height, minTemperature, maxTemperature]
  );

  // Scale to fit maxDisplayWidth while preserving aspect ratio.
  const scale = Math.min(1, maxDisplayWidth / width);
  const displayWidth = Math.round(width * scale);
  const displayHeight = Math.round(height * scale);

  // Color bar canvas rendered into a <canvas> element outside the Stage.
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

  return (
    <Container>
      <Stage width={displayWidth} height={displayHeight}>
        <Layer>
          <KonvaImage
            image={thermalCanvas}
            width={displayWidth}
            height={displayHeight}
          />
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
  );
}
