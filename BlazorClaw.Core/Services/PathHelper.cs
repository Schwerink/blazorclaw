using BlazorClaw.Core.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BlazorClaw.Core.Services
{
    public class PathHelper(IWebHostEnvironment env, HttpClient httpClient, IHttpContextAccessor contextAccessor, IConfiguration conf)
    {
        private Uri? baseUrl;
        public Uri GetBaseUrl()
        {
            baseUrl ??= conf.GetValue<Uri>("Web:BaseUrl");
            if (baseUrl == null && contextAccessor.HttpContext != null)
            {
                var ub = new UriBuilder
                {
                    Scheme = contextAccessor.HttpContext.Request.Scheme,
                    Host = contextAccessor.HttpContext.Request.Host.Host,
                    Port = contextAccessor.HttpContext.Request.Host.Port ?? -1
                };
                baseUrl = ub.Uri;
            }
            return baseUrl ?? new Uri("http://localhost");
        }
        public Uri GetUrl(string relativePath)
        {
            return new Uri(GetBaseUrl(), relativePath);
        }

        public Uri GetMediaUrl(string mediaFile)
        {
            mediaFile = Path.GetFileName(mediaFile);
            return new Uri(GetBaseUrl(), $"/uploads/{mediaFile}");
        }

        public string GetBaseFolder() => env.ContentRootPath;
        public string GetMediaFolder()
        {
            var path = Path.Combine(GetBaseFolder(), "uploads");
            Directory.CreateDirectory(path);
            return path;
        }

        public  MediaFile? GetMediaFile(string fileName)
        {
            var filenameOnly = Path.GetFileNameWithoutExtension(fileName);
            if ((filenameOnly?.Length ?? 0) < 36) return null;
            if (!Guid.TryParse(filenameOnly![..36], out _)) return null;

            var file = Path.Combine(GetMediaFolder(), Path.GetFileName(fileName));
            if (!System.IO.File.Exists(file)) return null;
            return new(file);
        }


        public async Task<string?> SaveMediaFileAsync(string data)
        {
            try
            {
                if (data.StartsWith("data:"))
                {
                    // Split the string to escape the real data

                    var b64 = data.Split(",".ToCharArray(), 2);
                    var mime = b64[0].Substring(5).Split(';').First();
                    // Convert the base 64 String to byte array
                    byte[] byteArray = Convert.FromBase64String(b64[1]);
                    if (string.IsNullOrEmpty(mime) || !mime.Contains('/'))
                    {
                        mime = Mimetype.DetectMimeType(byteArray);
                    }
                    var ext = Mimetype.GetExtensionFromMimeType(mime) ?? ".dat";
                    var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");
                    await File.WriteAllBytesAsync(filename, byteArray);
                    return filename;
                }
                if (data.StartsWith("http://") || data.StartsWith("https://"))
                {
                    var uri = new Uri(data);
                    if (uri.Host == GetBaseUrl().Host) return data;

                    var ext = Path.GetExtension(uri.AbsolutePath);
                    if (string.IsNullOrWhiteSpace(ext)) ext = "";
                    var filenameBase = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
                    var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}--{filenameBase}{ext}");

                    using var strm = await httpClient.GetStreamAsync(uri);
                    using (var fStrm = File.OpenWrite(filename))
                    {
                        await strm.CopyToAsync(fStrm);
                    }
                    if (ext == "")
                    {
                        var newExt = string.Empty;
                        using (var fStrm = File.OpenRead(filename))
                        {
                            var buff = new byte[1024];
                            fStrm.Read(buff, 0, buff.Length);
                            var mime = Mimetype.DetectMimeType(buff);
                            newExt = Mimetype.GetExtensionFromMimeType(mime);
                        }
                        if (!string.IsNullOrWhiteSpace(newExt) && newExt != ext)
                        {
                            var newFilename = filename + newExt;
                            File.Move(filename, newFilename);
                            filename = newFilename;
                        }
                    }
                    return filename;
                }
                if (data.StartsWith("file://"))
                {
                    var uri = new Uri(data);
                    if (uri.IsFile) return uri.LocalPath;
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<string?> SaveMediaFileAsync(Tuple<Stream, string>? tuple)
        {
            if (tuple == null) return null;
            var ext = Mimetype.GetExtensionFromMimeType(tuple.Item2);
            if (string.IsNullOrWhiteSpace(ext) && tuple.Item1.CanSeek)
            {
                var mime = Mimetype.DetectMimeType(tuple.Item1);
                if (!string.IsNullOrWhiteSpace(mime)) ext = Mimetype.GetExtensionFromMimeType(mime);
            }
            if (string.IsNullOrWhiteSpace(ext)) ext = ".dat";
            var filename = Path.Combine(GetMediaFolder(), $"{Guid.NewGuid()}{ext}");

            using var s = tuple.Item1;
            using var fs = File.OpenWrite(filename);
            await s.CopyToAsync(fs);
            return filename;
        }
    }

    public class MediaFile
    {
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; }
        public string FullPath { get; set; }

        private Stream? data = null;

        public MediaFile(string fullpath)
        {
            FullPath = fullpath;
            FileName = Path.GetFileName(fullpath);
            MimeType = Mimetype.GetMimeTypeFromExtension(FileName) ?? Mimetype.Binary;
            if (FileName.Length > 39)
            {
                if (FileName[36] == '-' && FileName[37] == '-')
                {
                    FileName = FileName[38..];
                }
            }
        }

        public Stream GetStream()
        {
            data = File.OpenRead(FullPath);
            return data;
        }
    }
}