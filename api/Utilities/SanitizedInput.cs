namespace api.Utilities
{
    public static class Sanitize
    {
        public static string SanitizeUserInput(string inputString)
        {
            return inputString.Replace("\n", "").Replace("\r", "");
        }
    }
}
