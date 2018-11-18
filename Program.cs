using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VideoLength
{
    public enum OSTypes { Win32, Win64, macOS, Linux32, Linux64 };

    class Program
    {
        public static readonly string[] DownloadLinks =
            {"https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-4.1-win32-static.zip",
            "https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-4.1-win64-static.zip",
            "https://ffmpeg.zeranoe.com/builds/macos64/static/ffmpeg-4.1-macos64-static.zip",
            "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-i686-static.tar.xz",
            "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz"};
        public static readonly string[] FileExtensions =
        {
            "exe", "exe", "", "", ""
        };

        static ParallelOptions options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8
        };

        public const string p7zipLink = "https://github.com/develar/7zip-bin/raw/master/linux/ia32/7za";

        static int _chosenPlatform;
        static string _archivePath;
        static string _directoryName;
        static string _appDirectory;
        static string _ffprobePath;
        static bool _recursive;
        static decimal sum;

        static object monitor = new object();

        static readonly Regex reg = new Regex(@"(\\+)$", RegexOptions.Compiled);
        public static async Task Main(string[] args)
        {
            if (args.Length == 1)
                _recursive = (args[0] == "-r");
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    _chosenPlatform = (int)OSTypes.Linux64;

                    // Selects macOS
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        _chosenPlatform -= 2;
                    // macOS will never be 32-bit, which is why this will
                    // always work and select Linux 32-bit
                    if (!Environment.Is64BitOperatingSystem)
                        _chosenPlatform--;
                    break;

                case PlatformID.Win32NT:
                    // Selects 64 bit
                    _chosenPlatform = (int)OSTypes.Win64;

                    // Selects 32 bit
                    if (!Environment.Is64BitOperatingSystem)
                        _chosenPlatform--;
                    break;
            }
            _appDirectory = Path.GetFullPath(Assembly.GetEntryAssembly().Location);
            _directoryName = Path.Combine(Path.GetDirectoryName(_appDirectory), Path.GetFileNameWithoutExtension(DownloadLinks[_chosenPlatform]));
            if (Directory.Exists(_directoryName))
            {
                Console.WriteLine("FFmpeg already downloaded. If the program fails, FFmpeg might be corrupted. In that case, please delete the FFmpeg folder along with the .zip/.tar.xz file if present and re-launch the program.");
            }
            else
            {
                var downloader = new Downloader();

                Console.WriteLine($"Downloading FFmpeg from {DownloadLinks[_chosenPlatform]}...");
                _archivePath = Path.Combine(Path.GetDirectoryName(_appDirectory), Path.GetFileName(DownloadLinks[_chosenPlatform]));
                downloader.Download(DownloadLinks[_chosenPlatform], _archivePath);

                while (!downloader.DownloadCompleted)
                    Thread.Sleep(1000);

                ExtractFFmpeg();
            }
            GetFFProbePath();
            var files = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", (_recursive) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            Parallel.ForEach(files, async (file) =>
           {
               ProcessStartInfo info = new ProcessStartInfo
               {
                   WorkingDirectory = Directory.GetCurrentDirectory(),
                   CreateNoWindow = false,
                   RedirectStandardOutput = true,
                   FileName = _ffprobePath,
                   Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {SanitizeInput(file)}"
               };
               string result = await Process.Start(info).StandardOutput.ReadToEndAsync();
               decimal num = 0m;
               decimal.TryParse(result, out num);
               if (num != 0m)
               {
                   lock (monitor)
                       sum += num;
               }
           });
            Console.WriteLine($"Total duration: {sum} seconds.");
        }

        public static void GetFFProbePath()
        {
            switch (_chosenPlatform)
            {
                case (int)OSTypes.macOS:
                case (int)OSTypes.Win32:
                case (int)OSTypes.Win64:
                    _ffprobePath = Path.Combine(_directoryName, $"bin{Path.DirectorySeparatorChar}ffprobe.{FileExtensions[_chosenPlatform]}");
                    break;
                case (int)OSTypes.Linux32:
                case (int)OSTypes.Linux64:
                    _ffprobePath = Path.Combine(_directoryName, $"ffprobe.{FileExtensions[_chosenPlatform]}");
                    break;
            }
        }

        public static void ExtractFFmpeg()
        {
            // Linux uses .tar.xz for some reason. Probably better
            // compression ratio.
            if (_chosenPlatform >= (int)OSTypes.Linux32)
            {
                var downloader = new Downloader();

                Console.WriteLine($"Downloading helper tool from {p7zipLink}...");
                string _downloadedPath = Path.Combine(Path.GetDirectoryName(_appDirectory), "7za");
                downloader.Download(p7zipLink, _downloadedPath);

                while (!downloader.DownloadCompleted)
                    Thread.Sleep(1000);

                ProcessStartInfo info = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = _appDirectory,
                    CreateNoWindow = true,
                    FileName = "sh",
                    Arguments = $"-c {SanitizeInput(_downloadedPath)} x {SanitizeInput(_archivePath)} -so | {SanitizeInput(_downloadedPath)} x -aoa -si -ttar"
                };

                Console.WriteLine("Extracting...");
                Process.Start(info).WaitForExit();
                Console.WriteLine("Complete.");
            }
            // Mac and Windows
            else
            {
                Console.WriteLine("Extracting...");
                ZipFile.ExtractToDirectory(_archivePath, Path.GetDirectoryName(_archivePath));
                Console.WriteLine("Complete.");
            }
        }

        public static string SanitizeInput(string s)
        {
            return "\"" + reg.Replace(s, @"$1$1") + "\"";
        }
    }
}
