using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;

namespace ytdownload
{

    public class StreamLocalFile
    {
        public static string FindVideo(string path) => Directory.GetFiles(Settings.DataDir, $"*{path}*").Where(s => Path.GetExtension(s) == ".mp4").FirstOrDefault();

        public StreamLocalFile(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        [FunctionName("ExistsLocalFile")]
        public async Task<IActionResult> Exists(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "ExistsLocalFile/{path}")] HttpRequest req, string path,
            ILogger log)
        {
            log.LogInformation($"Checking if youtube video with id {path} exists");
            httpContextAccessor.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            httpContextAccessor.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            var file = FindVideo(path);

            if (file != null)
            {
                return new OkResult();
            }
            log.LogInformation($"File with id {path} not found");
            return new NotFoundResult();
        }

        private static MediaTypeHeaderValue GetMediaType(string path)
        {
            switch(Path.GetExtension(path))
            {
                case ".mp4":
                    return new MediaTypeHeaderValue("video/mp4");
                case ".webm":
                    return new MediaTypeHeaderValue("video/webm");
                default:
                    throw new ArgumentOutOfRangeException($"mimetype not found for {path}");
            }
        }

        [FunctionName("StreamLocalFile")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "StreamLocalFile/{path}")] HttpRequest req, string path,
            ILogger log)
        {
            log.LogInformation($"Streaming video with id {path}");

            var file = FindVideo(path);

            if (file != null)
            {
                var b = File.OpenRead(file);
                //var response = new FileStreamResult(b, "video/mp4");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(b),
                    
                };

                response.StatusCode = HttpStatusCode.OK;
                response.Content.Headers.ContentType = GetMediaType(file);
                response.Content.Headers.ContentLength = b.Length;
                response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, b.Length);
                response.Headers.Add("Accept-Ranges", "0-" + b.Length);
                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        public const int ReadStreamBufferSize = 1024 * 1024;
        private readonly IHttpContextAccessor httpContextAccessor;

        private static bool TryReadRangeItem(RangeItemHeaderValue range, long contentLength,
           out long start, out long end)
        {
            if (range.From != null)
            {
                start = range.From.Value;
                if (range.To != null)
                    end = range.To.Value;
                else
                    end = contentLength - 1;
            }
            else
            {
                end = contentLength - 1;
                if (range.To != null)
                    start = contentLength - range.To.Value;
                else
                    start = 0;
            }
            return (start < contentLength && end < contentLength);
        }

        private static async Task CreatePartialContent(Stream inputStream, Stream outputStream,
            long start, long end)
        {
            int count = 0;
            long remainingBytes = end - start + 1;
            long position = start;
            byte[] buffer = new byte[ReadStreamBufferSize];

            inputStream.Position = start;
            do
            {
                try
                {
                    if (remainingBytes > ReadStreamBufferSize)
                        count = inputStream.Read(buffer, 0, ReadStreamBufferSize);
                    else
                        count = inputStream.Read(buffer, 0, (int)remainingBytes);
                    await outputStream.WriteAsync(buffer, 0, count);
                }
                catch (Exception error)
                {
                    throw;
                }
                position = inputStream.Position;
                remainingBytes = end - position + 1;
            } while (position <= end);
        }

        [FunctionName("StreamLocalVideo")]
        public static async Task<HttpResponseMessage> Video(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "StreamLocalVideo/{path}")] HttpRequest req, string path,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var file = FindVideo(path);

            if (file != null)
            {
                var fileInfo = new FileInfo(file);

                long totalLength = fileInfo.Length;

                req.Headers.TryGetValue("Range", out var range);
                RangeHeaderValue.TryParse(range, out var rangeHeader);
                
                HttpResponseMessage response = new HttpResponseMessage();
                response.Headers.AcceptRanges.Add("bytes");
                
                if (range.Count() == 0)
                {
                    response.StatusCode = HttpStatusCode.OK;
                    response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                    =>
                    {
                        using (outputStream) // Copy the file to output stream straightforward. 
                        using (Stream inputStream = fileInfo.OpenRead())
                        {
                            try
                            {
                                await inputStream.CopyToAsync(outputStream, ReadStreamBufferSize);
                            }
                            catch (Exception error)
                            {
                                throw;
                            }
                        }
                    }, "video/mp4");

                    response.Content.Headers.ContentLength = totalLength;
                    return response;
                }



                if (rangeHeader.Unit != "bytes" || rangeHeader.Ranges.Count > 1 ||
        !TryReadRangeItem(rangeHeader.Ranges.First(), totalLength, out var start, out var end))
                {
                    response.StatusCode = HttpStatusCode.RequestedRangeNotSatisfiable;
                    response.Content = new StreamContent(Stream.Null);  // No content for this status.
                    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(totalLength);
                    response.Content.Headers.ContentType = GetMediaType(file); 

                    return response;
                }

                var contentRange = new ContentRangeHeaderValue(start, end, totalLength);

                // We are now ready to produce partial content.
                response.StatusCode = HttpStatusCode.PartialContent;
                response.Content = new PushStreamContent(async (outputStream, httpContent, transpContext)
                =>
                {
                    using (outputStream) // Copy the file to output stream in indicated range.
                    using (Stream inputStream = fileInfo.OpenRead())
                        await CreatePartialContent(inputStream, outputStream, start, end);

                }, GetMediaType(file));

                response.Content.Headers.ContentLength = end - start + 1;
                response.Content.Headers.ContentRange = contentRange;

                return response;
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
