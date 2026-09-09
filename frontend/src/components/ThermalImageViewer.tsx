import ThermalImageStage from "./ThermalImageStage";

export interface ThermalImageViewerProps {
  temperatures: Float32Array;
  width: number;
  height: number;
  minTemperature: number;
  maxTemperature: number;
  /** Maximum display width in CSS pixels. The image scales to fit. */
  maxDisplayWidth?: number;
  /** Polygon coordinates in original image pixel space ([[x,y], ...]). */
  polygon?: number[][];
}

export default function ThermalImageViewer({
  temperatures,
  width,
  height,
  minTemperature,
  maxTemperature,
  maxDisplayWidth = 800,
  polygon,
}: ThermalImageViewerProps) {
  return (
    <ThermalImageStage
      temperatures={temperatures}
      width={width}
      height={height}
      minTemperature={minTemperature}
      maxTemperature={maxTemperature}
      maxDisplayWidth={maxDisplayWidth}
      vertices={polygon ?? []}
      isClosed
      editable={false}
    />
  );
}
