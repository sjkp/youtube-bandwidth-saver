using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Generic;
using System.Threading;

namespace ytdownload
{
    public class DownloadYoutubeVideo
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public DownloadYoutubeVideo(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        [FunctionName("DownloadYoutubeVideo")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "DownloadYoutubeVideo/{id}")]HttpRequest req, string id,     
            ILogger log)
        {
            log.LogInformation($"Downloading youtube video with id: {id}");

            httpContextAccessor.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            httpContextAccessor.HttpContext.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

            if (string.IsNullOrEmpty(id))
            {
                return new BadRequestObjectResult("Id missing from query");                
            }

            if (StreamLocalFile.FindVideo(id) != null)
            {
                return new OkObjectResult(new {
                    id = id,
                });
            }
                        

            using (DockerClient client = new DockerClientConfiguration()
     .CreateClient())
            {
                var hostConfig = new HostConfig()
                {
                    Mounts = new List<Mount>()
                    {
                         new Mount()
                        {
                            Source = Settings.HostVideoDir, ///mnt
                            Target = "/data",
                            Type = "bind"
                        }
                    }
                };
                var cfg = new Config()
                {
                    Image = "bxggs/youtube-dl",
                    Hostname = "localhost",
                    Cmd = new List<string>() { "-f", "bestvideo[ext=mp4]+bestaudio[ext=m4a]/mp4", $"https://www.youtube.com/watch?v={id}" }
                    //Cmd = new List<string>() { $"https://www.youtube.com/watch?v={id}" }
                };

                await client.Images.CreateImageAsync(new ImagesCreateParameters()
                {
                    FromImage = "bxggs/youtube-dl",
                    Tag = "latest",                            
                }, new AuthConfig() { }, new Progress<JSONMessage>());

                //cfg.ExposedPorts = new Dictionary<string, EmptyStruct>() { { "8080", new EmptyStruct() } };


                var res = await client.Containers.CreateContainerAsync(new CreateContainerParameters(cfg)
                {                  
                    HostConfig = hostConfig
                });

                await client.Containers.StartContainerAsync(res.ID, new ContainerStartParameters()
                {
                    
                });

                var waitResponse = await client.Containers.WaitContainerAsync(res.ID);
                
                var logStream = await client.Containers.GetContainerLogsAsync(res.ID, true, new ContainerLogsParameters()
                {
                    ShowStdout = true,
                    ShowStderr = true,                    
                    //Follow = true,
                    //Timestamps = true
                });


                var logRes = await logStream.ReadOutputToEndAsync(default(CancellationToken));

                



                return new OkObjectResult(new
                {
                    id = id,
                    statusCode = waitResponse.StatusCode,
                    logs = logRes.stdout
                });
            }
                

            
        }
    }
}
