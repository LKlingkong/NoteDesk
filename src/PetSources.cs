using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoteDesk
{
    // Source adapters fetch data only. All adapters use the same validator and renderer.
    internal interface IPetSource
    {
        PetPayload Load();
    }

    internal sealed class PetPayload
    {
        public string Name;
        public string Id;
        public string Origin;
        public bool Animated;
        public byte[] Image;
    }

    internal static class PetPackage
    {
        public const int FrameWidth = 192;
        public const int FrameHeight = 208;
        public const int MaxManifestBytes = 64 * 1024;
        public const int MaxImageBytes = 16 * 1024 * 1024;

        public static Dictionary<string, object> ParseManifest(byte[] bytes)
        {
            var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            if (manifest == null)
                throw new InvalidDataException("桌宠配置文件为空。");
            return manifest;
        }

        private static string ReadString(Dictionary<string, object> manifest, string key)
        {
            object value;
            return manifest.TryGetValue(key, out value) ? value as string : null;
        }

        public static string SpriteFile(Dictionary<string, object> manifest)
        {
            string file = ReadString(manifest, "spritesheetPath") ??
                          ReadString(manifest, "spriteSheetPath") ??
                          ReadString(manifest, "spritesheet") ??
                          ReadString(manifest, "spriteSheet") ?? "spritesheet.webp";
            if (!Regex.IsMatch(file, @"\A[A-Za-z0-9_.-]+\.(webp|png)\z", RegexOptions.IgnoreCase))
                throw new InvalidDataException("图集必须是与 pet.json 同目录的 WebP 或 PNG 文件。");
            return file;
        }

        public static PetPayload FromManifest(Dictionary<string, object> manifest, byte[] image, string origin)
        {
            return new PetPayload
            {
                Id = ReadString(manifest, "id"),
                Name = ReadString(manifest, "displayName") ?? ReadString(manifest, "name"),
                Origin = origin,
                Image = image,
                Animated = true
            };
        }

        public static byte[] ReadFile(string path, int limit)
        {
            using (var stream = File.OpenRead(path))
                return ReadBounded(stream, limit);
        }

        public static byte[] ReadBounded(Stream stream, int limit)
        {
            var buffer = new byte[8192];
            using (var result = new MemoryStream())
            {
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (result.Length + count > limit)
                        throw new InvalidDataException("桌宠资源文件超过大小限制。");
                    result.Write(buffer, 0, count);
                }
                return result.ToArray();
            }
        }

        public static BitmapSource Decode(byte[] image, bool animated)
        {
            if (image == null || image.Length < 12 || image.Length > MaxImageBytes)
                throw new InvalidDataException("桌宠图片无效或超过 16 MB。");
            bool png = image[0] == 137 && image[1] == 80 && image[2] == 78 && image[3] == 71;
            bool webp = Encoding.ASCII.GetString(image, 0, 4) == "RIFF" &&
                        Encoding.ASCII.GetString(image, 8, 4) == "WEBP";
            if (!png && !(animated && webp))
                throw new InvalidDataException("请选择 PNG 图片，或桌宠包内的 WebP 图集。");

            using (var stream = new MemoryStream(image, false))
            {
                try
                {
                    var metadata = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    int width = metadata.PixelWidth;
                    int height = metadata.PixelHeight;
                    if (animated && (width != 8 * FrameWidth || height != 11 * FrameHeight))
                        throw new InvalidDataException("Codex v2 图集须为 1536 × 2288，8 列 × 11 行。");
                    if (width < 1 || height < 1 || width > 4096 || height > 4096)
                        throw new InvalidDataException("桌宠图片边长不得超过 4096 像素。");

                    stream.Position = 0;
                    if (animated)
                    {
                        // Decoded frames still reference their dispatcher-bound decoder when cropped.
                        // Copy to an independent buffer before handing the atlas to the UI thread.
                        var native = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.None).Frames[0];
                        var rgba = new FormatConvertedBitmap(native, PixelFormats.Bgra32, null, 0);
                        int stride = width * 4;
                        var pixels = new byte[stride * height];
                        rgba.CopyPixels(pixels, stride, 0);
                        var detached = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
                        detached.Freeze();
                        return detached;
                    }
                    var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (NotSupportedException)
                {
                    if (webp)
                        throw new InvalidDataException("Windows 无法解码 WebP。请安装 Windows WebP 图像扩展，或导入同尺寸 PNG 图集。");
                    throw;
                }
            }
        }
    }

    internal sealed class LocalPetSource : IPetSource
    {
        private readonly string file;
        private readonly string selectedSprite;

        public LocalPetSource(string file, string selectedSprite = null)
        {
            this.file = file;
            this.selectedSprite = selectedSprite;
        }

        public PetPayload Load()
        {
            if (string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                return new PetPayload { Id = name, Name = name, Image = PetPackage.ReadFile(file, PetPackage.MaxImageBytes) };
            }

            var manifest = PetPackage.ParseManifest(PetPackage.ReadFile(file, PetPackage.MaxManifestBytes));
            string expectedSprite = PetPackage.SpriteFile(manifest);
            string sprite = selectedSprite ?? Path.Combine(Path.GetDirectoryName(file), expectedSprite);
            return PetPackage.FromManifest(manifest, PetPackage.ReadFile(sprite, PetPackage.MaxImageBytes), null);
        }
    }

    internal sealed class GitHubPetSource : IPetSource
    {
        private readonly string owner;
        private readonly string repo;
        private readonly string folder;
        private readonly string origin;

        public GitHubPetSource(string url, string folder)
        {
            Uri uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri) || uri.Scheme != "https" ||
                uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("请输入 https://github.com/作者/仓库 格式的公开仓库地址。");
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length != 2 || parts.Any(part => !Regex.IsMatch(part, @"\A[A-Za-z0-9][A-Za-z0-9_.-]*\z")))
                throw new ArgumentException("请填写仓库首页地址，资源目录填在下方。");
            owner = parts[0];
            repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ?
                parts[1].Substring(0, parts[1].Length - 4) : parts[1];
            this.folder = (folder ?? "").Trim().Replace('\\', '/').Trim('/');
            if (this.folder.Split('/').Any(part => part == "." || part == ".."))
                throw new ArgumentException("资源目录必须是仓库内的相对路径。");
            origin = "https://github.com/" + owner + "/" + repo;
        }

        public PetPayload Load()
        {
            string api = "https://api.github.com/repos/" + owner + "/" + repo;
            var repository = ReadJson<RepositoryInfo>(api, PetPackage.MaxManifestBytes);
            if (repository == null || string.IsNullOrWhiteSpace(repository.default_branch))
                throw new InvalidDataException("无法识别仓库的默认分支。");
            var commit = ReadJson<CommitInfo>(api + "/commits/" + Uri.EscapeDataString(repository.default_branch), 1024 * 1024);
            if (commit == null || !Regex.IsMatch(commit.sha ?? "", @"\A[a-fA-F0-9]{40}\z"))
                throw new InvalidDataException("无法识别仓库的最新提交。");
            var tree = ReadJson<TreeInfo>(api + "/git/trees/" + commit.sha + "?recursive=1", 2 * 1024 * 1024);
            if (tree == null || tree.tree == null)
                throw new InvalidDataException("无法读取仓库文件列表。");
            var entries = tree.tree.Where(entry => entry.type == "blob" &&
                (entry.mode == "100644" || entry.mode == "100755")).ToList();
            var manifests = entries.Where(entry => entry.path == "pet.json" ||
                entry.path.EndsWith("/pet.json", StringComparison.Ordinal)).ToList();
            if (folder.Length != 0)
                manifests = manifests.Where(entry => entry.path == folder + "/pet.json").ToList();
            if (manifests.Count != 1 || (tree.truncated && folder.Length == 0))
                throw new InvalidDataException("请填写包含 pet.json 的资源目录，仓库内应能定位到一个桌宠包。");
            var manifestEntry = manifests[0];
            string prefix = manifestEntry.path.Substring(0, manifestEntry.path.Length - "pet.json".Length);
            string raw = "https://raw.githubusercontent.com/" + owner + "/" + repo + "/" + commit.sha + "/";
            var manifest = PetPackage.ParseManifest(Download(raw + EncodePath(manifestEntry.path), PetPackage.MaxManifestBytes));
            string imagePath = prefix + PetPackage.SpriteFile(manifest);
            if (!entries.Any(entry => entry.path == imagePath))
                throw new InvalidDataException("仓库内缺少 pet.json 指定的同目录图集。");
            return PetPackage.FromManifest(manifest, Download(raw + EncodePath(imagePath), PetPackage.MaxImageBytes), origin);
        }

        private static string EncodePath(string path)
        {
            return string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        }

        private static T ReadJson<T>(string url, int limit)
        {
            return new JavaScriptSerializer().Deserialize<T>(Encoding.UTF8.GetString(Download(url, limit)));
        }

        private static byte[] Download(string url, int limit)
        {
            // The legacy Framework compiler can select older .NET TLS defaults.
            // Let Windows negotiate the protocol for GitHub connections.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "NoteDesk-PetImporter";
            request.Accept = "application/json, application/octet-stream, */*";
            request.AllowAutoRedirect = false;
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > limit)
                        throw new InvalidDataException("GitHub 资源不可用或超过大小限制。");
                    using (var stream = response.GetResponseStream())
                        return PetPackage.ReadBounded(stream, limit);
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.TrustFailure)
                    throw new InvalidDataException(
                        "Windows 无法验证 " + request.RequestUri.Host + " 的 HTTPS 证书。可从原仓库下载桌宠包，解压后本地导入。", ex);
                if (ex.Status == WebExceptionStatus.SecureChannelFailure)
                    throw new InvalidDataException(
                        "无法与 " + request.RequestUri.Host + " 建立 TLS 连接。可从原仓库下载桌宠包，解压后本地导入。", ex);
                throw;
            }
        }

        public sealed class RepositoryInfo { public string default_branch { get; set; } }
        public sealed class CommitInfo { public string sha { get; set; } }
        public sealed class TreeInfo
        {
            public bool truncated { get; set; }
            public List<TreeEntry> tree { get; set; }
        }
        public sealed class TreeEntry
        {
            public string path { get; set; }
            public string type { get; set; }
            public string mode { get; set; }
        }
    }
}
