using System;
using System.Collections.Generic;
using System.Text;

namespace ytdownload
{
    public class Settings
    {
        public static string DataDir => Environment.GetEnvironmentVariable("LOCAL_VIDEODIR");
        public static string HostVideoDir => Environment.GetEnvironmentVariable("HOST_VIDEODIR");
    }
}
