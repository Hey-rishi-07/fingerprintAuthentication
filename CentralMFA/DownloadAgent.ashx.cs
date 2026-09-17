using System;
using System.IO;
using System.Web;

namespace CentralMFA
{
    public class DownloadAgent : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            HttpResponse response = context.Response;
            HttpServerUtility server = context.Server;

            string zipPath = server.MapPath("~/Downloads/MfaScannerAgent_Setup.zip");
            string clientAgentDir = server.MapPath("~/ClientAgent");

            // If zip does not exist yet, build it dynamically
            if (!File.Exists(zipPath) && Directory.Exists(clientAgentDir))
            {
                try
                {
                    string dir = Path.GetDirectoryName(zipPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    // Dynamically create zip if System.IO.Compression.FileSystem is available
                    System.IO.Compression.ZipFile.CreateFromDirectory(clientAgentDir, zipPath);
                }
                catch { }
            }

            if (File.Exists(zipPath))
            {
                FileInfo fi = new FileInfo(zipPath);
                response.Clear();
                response.Buffer = false;
                response.ContentType = "application/zip";
                response.AddHeader("Content-Disposition", "attachment; filename=\"MfaScannerAgent_Setup.zip\"");
                response.AddHeader("Content-Length", fi.Length.ToString());
                response.TransmitFile(zipPath);
                response.End();
            }
            else
            {
                response.StatusCode = 404;
                response.ContentType = "text/plain";
                response.Write("MfaScannerAgent_Setup.zip was not found on the server. Please contact your system administrator.");
            }
        }

        public bool IsReusable
        {
            get { return true; }
        }
    }
}
