﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    public abstract class DownloadIntegrationTest
    {
        protected DownloadConfiguration Config { get; set; }

        [TestInitialize]
        public abstract void InitialTest();

        [TestMethod]
        public void Download1KbWithFilenameTest()
        {
            // arrange
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };

            // act
            var downloadTask = downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl);
            downloadTask.Wait();
            using var memoryStream = downloadTask.Result;

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(memoryStream);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, memoryStream.Length);
            Assert.IsTrue(DownloadTestHelper.AreEqual(DownloadTestHelper.File1Kb, memoryStream));
        }

        [TestMethod]
        public void Download16KbWithoutFilenameTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl,
                new DirectoryInfo(DownloadTestHelper.TempDirectory)).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DownloadTestHelper.TempDirectory));
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DownloadTestHelper.AreEqual(DownloadTestHelper.File16Kb, File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void DownloadProgressChangedTest()
        {
            // arrange
            var downloader = new DownloadService(Config);
            var progressChangedCount = (int)Math.Ceiling((double)DownloadTestHelper.FileSize16Kb / Config.BufferBlockSize);
            var progressCounter = 0;
            downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl).Wait();

            // assert
            // Note: some times received bytes on read stream method was less than block size!
            Assert.IsTrue(progressChangedCount <= progressCounter);
        }

        [TestMethod]
        public void StopResumeDownloadTest()
        {
            // arrange
            var expectedStopCount = 5;
            var stopCount = 0;
            var cancellationsOccurrenceCount = 0;
            var downloadFileExecutionCounter = 0;
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled && e.Error != null)
                {
                    cancellationsOccurrenceCount++;
                }
                else
                {
                    downloadCompletedSuccessfully = true;
                }
            };
            downloader.DownloadStarted += delegate {
                if (expectedStopCount > stopCount)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    stopCount++;
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File150KbUrl, Path.GetTempFileName()).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedStopCount, stopCount);
            Assert.AreEqual(expectedStopCount, cancellationsOccurrenceCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void StopResumeDownloadFromLastPositionTest()
        {
            // arrange
            var expectedStopCount = 5;
            var stopCount = 0;
            var downloadFileExecutionCounter = 0;
            var totalDownloadSize = 0L;
            var config = (DownloadConfiguration)Config.Clone();
            config.BufferBlockSize = 1024;
            var downloader = new DownloadService(Config);
            downloader.DownloadProgressChanged += (s, e) => {
                totalDownloadSize += e.ReceivedBytes.Length;
                if (expectedStopCount > stopCount)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    stopCount++;
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, totalDownloadSize);
        }

        [TestMethod]
        public void SpeedLimitTest()
        {
            // arrange
            double averageSpeed = 0;
            var progressCounter = 0;
            Config.MaximumBytesPerSecond = 128; // 128 Byte/s
            var downloader = new DownloadService(Config);
            downloader.DownloadProgressChanged += (s, e) => {
                averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
                progressCounter++;
            };

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= Config.MaximumBytesPerSecond, $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");
        }

        [TestMethod]
        public void DownloadOnMemoryStreamSizeTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Result;

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, stream.Length);
        }

        [TestMethod]
        public void DownloadOnMemoryStreamTypeTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Result;

            // assert
            Assert.IsTrue(stream is MemoryStream);
        }

        [TestMethod]
        public void DownloadOnMemoryStreamContentTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = (MemoryStream)downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Result;

            // assert
            Assert.IsTrue(DownloadTestHelper.File1Kb.SequenceEqual(stream.ToArray()));
        }
    }
}