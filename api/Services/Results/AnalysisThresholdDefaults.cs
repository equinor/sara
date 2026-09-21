using api.Configurations;
using api.Database.Models;

namespace api.Services.Results;

public static class AnalysisThresholdDefaults
{
    public static List<AnalysisThreshold> Build(AnalysisOptions options, string analysisType)
    {
        if (!options.Analyses.TryGetValue(analysisType, out var analysisConfig))
            return [];

        return analysisConfig
            .Results.Where(entry => entry.Value.DefaultThreshold is not null)
            .Select(entry =>
            {
                var defaults = entry.Value.DefaultThreshold!;
                return new AnalysisThreshold
                {
                    Key = entry.Key,
                    LowerAlert = defaults.LowerAlert,
                    LowerWarning = defaults.LowerWarning,
                    UpperWarning = defaults.UpperWarning,
                    UpperAlert = defaults.UpperAlert,
                    AlertWhenTrue = defaults.AlertWhenTrue,
                    MinConfidence = defaults.MinConfidence,
                    Source = ThresholdSource.Config,
                };
            })
            .ToList();
    }
}
