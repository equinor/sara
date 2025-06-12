﻿using System.Text.RegularExpressions;

namespace api.MQTT
{
    /// <summary>
    ///     This class contains a list of topics linked to their message models
    /// </summary>
    public static class MqttTopics
    {
        /// <summary>
        ///     A dictionary linking MQTT topics to their respective message models
        /// </summary>
        public static readonly Dictionary<string, Type> TopicsToMessages = new()
        {
            { "isar/+/inspection_result", typeof(IsarInspectionResultMessage) },
            { "isar/+/inspection_value", typeof(IsarInspectionValueMessage) },
            { "sara/visualization_available", typeof(SaraVisualizationAvailableMessage) },
        };

        public static readonly Dictionary<Type, string> MessagesToTopics = new()
        {
            { typeof(IsarInspectionResultMessage), "isar/+/inspection_result" },
            { typeof(IsarInspectionValueMessage), "isar/+/inspection_value" },
            { typeof(SaraVisualizationAvailableMessage), "sara/visualization_available" },
        };

        /// <summary>
        ///     Searches a dictionary for a specific topic name and returns the corresponding value from the wildcarded dictionary
        /// </summary>
        /// <remarks>
        ///     Will throw <see cref="InvalidOperationException"></see> if there are more than one matches for the topic.
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="dict"></param>
        /// <param name="topic"></param>
        /// <returns>
        ///     The value corresponding to the wildcarded topic
        /// </returns>
        /// <exception cref="InvalidOperationException">There was more than one topic pattern matching the provided topic</exception>
        public static T? GetItemByTopic<T>(this Dictionary<string, T> dict, string topic)
        {
            return (
                from p in dict
                where Regex.IsMatch(topic, ConvertTopicToRegex(p.Key))
                select p.Value
            ).SingleOrDefault();
        }

        public static string? GetTopicByItem<T>(this Dictionary<T, string> dict, T item)
            where T : Type
        {
            var topic = (from p in dict where item == p.Key select p.Value).SingleOrDefault();

            return topic ?? null;
        }

        /// <summary>
        ///     Converts from mqtt topic wildcards to the corresponding
        ///     regex expression to allow for searching the dictionary for the topic of a message
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        private static string ConvertTopicToRegex(string topic)
        {
            return topic.Replace('#', '*').Replace("+", "[^/\n]*", StringComparison.Ordinal);
        }
    }
}
