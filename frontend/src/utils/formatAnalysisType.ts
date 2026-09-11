export function formatAnalysisType(analysisType: string | undefined): string {
  switch (analysisType?.toLowerCase()) {
    case "fencilla":
      return "Fence detection";
    case "thermal-reading":
      return "Thermal reading";
    case "cloe":
      return "CLOE";
    case "co2":
      return "CO2";
    default:
      return analysisType ?? "\u2013";
  }
}
