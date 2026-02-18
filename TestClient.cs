using System;
using System.Net.Http;
using System.Threading.Tasks;

class TestClient
{
    static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient();
        
        // Add headers that mimic a real browser
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

        try
        {
            Console.WriteLine("Testing direct access to the target URL...");
            var response = await httpClient.GetAsync("https://flzios.ir/list.php?q=sales&send=%D8%AC%D8%B3%D8%AA%D8%AC%D9%88+%DA%A9%D9%86");
            Console.WriteLine($"Status Code: {(int)response.StatusCode} ({response.StatusCode})");
            
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response Length: {content.Length} characters");
            Console.WriteLine("First 500 characters of response:");
            Console.WriteLine(content.Substring(0, Math.Min(500, content.Length)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}