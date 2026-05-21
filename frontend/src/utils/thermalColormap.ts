import { interpolateInferno } from "d3-scale-chromatic";

/**
 * Parse a CSS color string returned by d3 into [r, g, b] bytes.
 * Handles both hex (#rrggbb) and rgb(r, g, b) formats.
 */
function parseColor(css: string): [number, number, number] {
  // Hex format: #rrggbb
  const hex = css.match(/^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i);
  if (hex) return [parseInt(hex[1], 16), parseInt(hex[2], 16), parseInt(hex[3], 16)];
  // rgb() format
  const rgb = css.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
  if (rgb) return [+rgb[1], +rgb[2], +rgb[3]];
  return [0, 0, 0];
}

// Pre-compute a 256-entry LUT so we don't call interpolateInferno per pixel.
const INFERNO_LUT: Uint8Array = (() => {
  const lut = new Uint8Array(256 * 3);
  for (let i = 0; i < 256; i++) {
    const [r, g, b] = parseColor(interpolateInferno(i / 255));
    lut[i * 3] = r;
    lut[i * 3 + 1] = g;
    lut[i * 3 + 2] = b;
  }
  return lut;
})();

/**
 * Apply the Inferno colormap to a Float32Array of temperature values
 * and return an offscreen HTMLCanvasElement ready for use with Konva `<Image>`.
 */
export function applyColormap(
  temperatures: Float32Array,
  width: number,
  height: number,
  minTemp: number,
  maxTemp: number
): HTMLCanvasElement {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext("2d")!;
  const imageData = ctx.createImageData(width, height);
  const pixels = imageData.data; // Uint8ClampedArray [R,G,B,A, ...]

  const range = maxTemp - minTemp || 1; // avoid division by zero

  for (let i = 0; i < temperatures.length; i++) {
    const t = Math.max(0, Math.min(1, (temperatures[i] - minTemp) / range));
    const lutIdx = Math.round(t * 255) * 3;
    const px = i * 4;
    pixels[px] = INFERNO_LUT[lutIdx];
    pixels[px + 1] = INFERNO_LUT[lutIdx + 1];
    pixels[px + 2] = INFERNO_LUT[lutIdx + 2];
    pixels[px + 3] = 255;
  }

  ctx.putImageData(imageData, 0, 0);
  return canvas;
}

/**
 * Create a vertical color bar canvas for use as a legend beside the thermal image.
 * The gradient runs from maxTemp (top) to minTemp (bottom), matching the image colormap.
 */
export function createColorBarCanvas(
  barWidth: number,
  barHeight: number
): HTMLCanvasElement {
  const canvas = document.createElement("canvas");
  canvas.width = barWidth;
  canvas.height = barHeight;
  const ctx = canvas.getContext("2d")!;
  const imageData = ctx.createImageData(barWidth, barHeight);
  const pixels = imageData.data;

  const denom = Math.max(1, barHeight - 1);
  for (let y = 0; y < barHeight; y++) {
    // t=1 at top (hot), t=0 at bottom (cold)
    const t = 1 - y / denom;
    const lutIdx = Math.round(t * 255) * 3;
    const r = INFERNO_LUT[lutIdx];
    const g = INFERNO_LUT[lutIdx + 1];
    const b = INFERNO_LUT[lutIdx + 2];
    for (let x = 0; x < barWidth; x++) {
      const px = (y * barWidth + x) * 4;
      pixels[px] = r;
      pixels[px + 1] = g;
      pixels[px + 2] = b;
      pixels[px + 3] = 255;
    }
  }

  ctx.putImageData(imageData, 0, 0);
  return canvas;
}
