using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using api.Models;

namespace api.Services;

public class TriggerAnonymizerRequest(string inspectionId, Uri rawDataUri, Uri anonymizedUri)
{
    public string InspectionId { get; } = inspectionId;
    public Uri RawDataUri { get; } = rawDataUri;
    public Uri AnonymizedUri { get; } = anonymizedUri;
}

public class AnonymizerService
{
    private static readonly HttpClient client = new();

    public async Task TriggerAnonymizerFunc(InspectionData data)
    {
        if (data.RawDataUri == null)
            throw new ArgumentNullException(nameof(data), "RawDataUri cannot be null.");

        if (data.AnonymizedUri == null)
            throw new ArgumentNullException(nameof(data), "AnonymizedUri cannot be null.");

        var postRequestData = new TriggerAnonymizerRequest(data.InspectionId, data.RawDataUri, data.AnonymizedUri);
        var json = JsonSerializer.Serialize(postRequestData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("http://trigger-anonymizer-source:8080/trigger-anonymizer", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Function triggered successfully.");
        }
        else
        {
            Console.WriteLine("Failed to trigger function.");
        }
    }
}
