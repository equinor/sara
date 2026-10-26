export function formatAnalysisType(analysisType: string | undefined): string {
  switch (analysisType) {
    case "Fencilla":
      return "Fence detection";
    case "ThermalReading":
      return "Thermal reading";
    case "CLOE":
      return "CLOE";
    case "CO2":
      return "CO2";
    default:
      return analysisType ?? "\u2013";
  }
}
