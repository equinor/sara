using api.Services;

namespace api.Utilities
{
    public static class Sanitize
    {
        public static string SanitizeUserInput(string inputString)
        {
            return inputString.Replace("\n", "").Replace("\r", "");
        }

        public static FetchCO2MeasurementRequest SanitizeUserInput(
            FetchCO2MeasurementRequest inputQuery
        )
        {
            inputQuery.Facility = SanitizeUserInput(inputQuery.Facility);
            inputQuery.TaskStartTime = SanitizeUserInput(inputQuery.TaskStartTime);
            inputQuery.TaskEndTime = SanitizeUserInput(inputQuery.TaskEndTime);
            inputQuery.InspectionName = SanitizeUserInput(inputQuery.InspectionName);

            return inputQuery;
        }
    }
}
