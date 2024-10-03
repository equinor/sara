using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using api.Models;
using System.Text.Json;

namespace api.Services;

public class AnonymizerService
{
    private static readonly HttpClient client = new();

    public async Task TriggerAnonymizerFunc(InspectionData data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://<your-function-app-name>.azurewebsites.net/api/AnonymizerFunc", content);

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
