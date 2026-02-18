using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;

namespace CrossRemoverProxy.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProxyController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProxyController> _logger;

        public ProxyController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ProxyController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL parameter is required");

            // decode URL اگر لازم باشد
            url = Uri.UnescapeDataString(url);
            if (url.StartsWith("vlc://")) 
            { 
                return await StreamVideo(url);
            }
            return await ProcessRequest(url, HttpMethod.Get);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] UrlRequest url)
        {
            if (string.IsNullOrWhiteSpace(url.Url))
                return BadRequest("URL parameter is required");

            // decode URL اگر لازم باشد
           var urll = Uri.UnescapeDataString(url.Url);
            if (urll.StartsWith("vlc://"))
            {
                return await StreamVideo(urll);
            }
            return await ProcessRequest(urll, HttpMethod.Post);
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromQuery] string url)
        {
            return await ProcessRequest(url, HttpMethod.Put);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] string url)
        {
            return await ProcessRequest(url, HttpMethod.Delete);
        }

        [HttpOptions]
        public IActionResult Options([FromQuery] string url)
        {
            // Handle preflight requests
            Response.Headers.Append("Access-Control-Allow-Origin", "*");
            Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
            return Ok();
        }
        [HttpGet("subtitle")]
        public IActionResult StreamSubtitle([FromQuery] string url)
        {
            url = Uri.UnescapeDataString(url).Replace("vlc://", "http://");

            var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

            var args =
                $"-loglevel error " +
                $"-analyzeduration 10000000 " +
                $"-probesize 10000000 " +
                $"-i \"{url}\" " +
                $"-map 0:s? " +          // اگر نبود، کرش نکن
                $"-f webvtt pipe:1";

            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false, // ❗️ مهم
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            ffmpeg.Start();

            HttpContext.RequestAborted.Register(() =>
            {
                try
                {
                    if (!ffmpeg.HasExited)
                        ffmpeg.Kill(true);
                }
                catch { }
            });

            return File(
                ffmpeg.StandardOutput.BaseStream,
                "text/vtt; charset=utf-8"
            );
        }
        private async Task<IActionResult> StreamVideo(string videoUrl)
        {
            var realUrl = videoUrl.Replace("vlc://", "http://");

            var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");

            var args =
                $"-loglevel error " +
                $"-i \"{realUrl}\" " +
                $"-c:v copy " +
                $"-c:a copy " +
                $"-c:s mov_text " +
                $"-movflags frag_keyframe+empty_moov+default_base_moof " +
                $"-f mp4 pipe:1";

            var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            ffmpeg.Start();

            // اگر کاربر صفحه را بست → FFmpeg هم کشته شود
            HttpContext.RequestAborted.Register(() =>
            {
                try
                {
                    if (!ffmpeg.HasExited)
                        ffmpeg.Kill(true);
                }
                catch { }
            });

            return File(
                ffmpeg.StandardOutput.BaseStream,
                "video/mp4",
                enableRangeProcessing: true
            );
        }

        private async Task<IActionResult> ProcessRequest(string url, HttpMethod method)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest("URL parameter is required");

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                return BadRequest("Invalid URL format");

            var uri = new Uri(url);

            try
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression =
                        System.Net.DecompressionMethods.GZip |
                        System.Net.DecompressionMethods.Deflate |
                        System.Net.DecompressionMethods.Brotli
                };

                using var httpClient = new HttpClient(handler);

                var request = new HttpRequestMessage(method, uri);

                request.Headers.TryAddWithoutValidation(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36"
                );

                request.Headers.TryAddWithoutValidation(
                    "Accept",
                    "*/*"
                );

                foreach (var header in Request.Headers)
                {
                    if (
                        header.Key.StartsWith("host", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.StartsWith("origin", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.StartsWith("referer", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.StartsWith("content-length", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.StartsWith("transfer-encoding", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.StartsWith("connection", StringComparison.OrdinalIgnoreCase) ||
                        header.Key.StartsWith("accept-encoding", StringComparison.OrdinalIgnoreCase)
                    )
                        continue;

                    request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                if (method == HttpMethod.Post || method == HttpMethod.Put)
                {
                    using var reader = new StreamReader(Request.Body);
                    var body = await reader.ReadToEndAsync();
                    request.Content = new StringContent(
                        body,
                        Encoding.UTF8,
                        Request.ContentType ?? "application/json"
                    );
                }

                using var response = await httpClient.SendAsync(request);

                var contentType = response.Content.Headers.ContentType?.ToString()
                                  ?? "application/octet-stream";

                // پاک‌سازی کامل Headerهای خروجی
                Response.Headers.Clear();
                Response.Headers["Access-Control-Allow-Origin"] = "*";
                Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
                Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";

                // --- TEXT (JSON / HTML / CSS / JS / XML)
                if (contentType.StartsWith("text", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Contains("json") ||
                    contentType.Contains("xml") ||
                    contentType.Contains("javascript"))
                {
                    var text = await response.Content.ReadAsStringAsync();
                    return Content(text, contentType);
                }

                // --- BINARY (image / pdf / video / zip / ...)
                var bytes = await response.Content.ReadAsByteArrayAsync();
                return File(bytes, contentType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while requesting {Url}", url);
                return StatusCode(502, "Bad Gateway");
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504, "Gateway Timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while requesting {Url}", url);
                return StatusCode(500, "Internal Server Error");
            }
        }


    }
    public class UrlRequest
    {
        public string Url { get; set; }
    }
}