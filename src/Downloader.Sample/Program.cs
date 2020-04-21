﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using ShellProgressBar;

namespace Downloader.Sample
{
    class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }

        static void Main(string[] args)
        {
            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };
            var childOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGreen,
                ProgressCharacter = '─'
            };
            ConsoleProgress = new ProgressBar(10000, "Downloading 10MB.zip file", options);

            var downloadOpt = new DownloadConfiguration()
            {
                ParallelDownload = true, // download parts of file as parallel or not
                BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes
                ChunkCount = 8, // file parts to download
                MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
                OnTheFlyDownload = true, // caching in-memory mode
                Timeout = 1000 // timeout (millisecond) per stream block reader
            };
            var ds = new DownloadService(downloadOpt);
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            var file = Path.Combine(Path.GetTempPath(), "zip_10MB.zip");
            ds.DownloadFileAsync("https://file-examples.com/wp-content/uploads/2017/02/zip_10MB.zip", file);
            Console.WriteLine();
            Console.ReadKey();
        }

        private static async void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ConsoleProgress.Tick();
            await Task.Delay(1000);
            Console.WriteLine();
            Console.WriteLine();

            if (e.Cancelled)
            {
                Console.WriteLine("Download canceled!");
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error);
            }
            else
            {
                Console.WriteLine("Download completed successfully.");
                Console.Title = "100%";
            }
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            var nonZeroSpeed = e.BytesPerSecondSpeed == 0 ? 0.0001 : e.BytesPerSecondSpeed;
            var estimateTime = (int)((e.TotalBytesToReceive - e.BytesReceived) / nonZeroSpeed);
            var isMins = estimateTime >= 60;
            var timeLeftUnit = "seconds";
            if (isMins)
            {
                timeLeftUnit = "mins";
                estimateTime /= 60;
            }

            Console.Title = $"{e.ProgressPercentage:N3}%  -  {CalcMemoryMensurableUnit(e.BytesPerSecondSpeed)}/s  -  " +
                            $"[{CalcMemoryMensurableUnit(e.BytesReceived)} of {CalcMemoryMensurableUnit(e.TotalBytesToReceive)}], {estimateTime} {timeLeftUnit} left";
            ConsoleProgress.Tick((int)(e.ProgressPercentage * 100));
        }

        public static string CalcMemoryMensurableUnit(long bigUnSignedNumber, bool isShort = true)
        {
            var kb = bigUnSignedNumber / 1024; // · 1024 Bytes = 1 Kilobyte 
            var mb = kb / 1024; // · 1024 Kilobytes = 1 Megabyte 
            var gb = mb / 1024; // · 1024 Megabytes = 1 Gigabyte 
            var tb = gb / 1024; // · 1024 Gigabytes = 1 Terabyte 

            var b = isShort ? "B" : "Bytes";
            var k = isShort ? "KB" : "Kilobytes";
            var m = isShort ? "MB" : "Megabytes";
            var g = isShort ? "GB" : "Gigabytes";
            var t = isShort ? "TB" : "Terabytes";

            return tb > 1 ? $"{tb:N0}{t}" :
                   gb > 1 ? $"{gb:N0}{g}" :
                   mb > 1 ? $"{mb:N0}{m}" :
                   kb > 1 ? $"{kb:N0}{k}" :
                   $"{bigUnSignedNumber:N0}{b}";
        }
    }
}
