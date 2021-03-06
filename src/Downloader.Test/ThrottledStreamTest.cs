﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ThrottledStreamTest
    {
        private delegate void ThrottledStreamWrite(Stream stream, byte[] buffer, int offset, int count);
        private delegate int ThrottledStreamRead(Stream stream, byte[] buffer, int offset, int count);

        [TestMethod]
        public void TestStreamReadSpeed()
        {
            TestStreamReadSpeed((stream, buffer, offset, count) => stream.Read(buffer, offset, count));
        }

        [TestMethod]
        public void TestStreamReadAsyncSpeed()
        {
            TestStreamReadSpeed((stream, buffer, offset, count) => {
                var task = stream.ReadAsync(buffer, offset, count);
                task.Wait();
                return task.Result;
            });
        }

        private void TestStreamReadSpeed(ThrottledStreamRead readMethod)
        {
            // arrange
            var size = 1024;
            var maxBytesPerSecond = 256; // 256 Byte/s
            var slowExpectedTime = (size / maxBytesPerSecond) * 1000; // 4000 Milliseconds
            var fastExpectedTime = slowExpectedTime * 0.75; // 3000 Milliseconds
            var randomBytes = DummyData.GenerateRandomBytes(size);
            var buffer = new byte[maxBytesPerSecond/8];
            var readSize = 1;
            using Stream stream = new ThrottledStream(new MemoryStream(randomBytes), maxBytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = readMethod(stream, buffer, 0, buffer.Length);
            }
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= fastExpectedTime, $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds <= slowExpectedTime, $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public void TestStreamWriteSpeed()
        {
            TestStreamWriteSpeed((stream, buffer, offset, count) => {
                stream.Write(buffer, offset, count);
            });
        }

        [TestMethod]
        public void TestStreamWriteAsyncSpeed()
        {
            TestStreamWriteSpeed((stream, buffer, offset, count) => {
                stream.WriteAsync(buffer, offset, count).Wait();
            });
        }

        private void TestStreamWriteSpeed(ThrottledStreamWrite writeMethod)
        {
            // arrange
            var size = 1024;
            var bytesPerSecond = 256; // 256 B/s
            var tolerance = 50; // 50 ms
            var expectedTime = (size / bytesPerSecond) * 1000; // 4000 Milliseconds
            var randomBytes = DummyData.GenerateRandomBytes(size);
            using Stream stream = new ThrottledStream(new MemoryStream(), bytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            writeMethod(stream, randomBytes, 0, randomBytes.Length);
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds + tolerance >= expectedTime, 
                $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public void TestNegativeBandwidth()
        {
            // arrange
            int maximumBytesPerSecond = -1;

            // act
            void CreateThrottledStream()
            {
                using var throttledStream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);
            }

            // assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(CreateThrottledStream);
        }

        [TestMethod]
        public void TestZeroBandwidth()
        {
            // arrange
            int maximumBytesPerSecond = 0;

            // act 
            using var throttledStream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);

            // assert
            Assert.AreEqual(long.MaxValue, throttledStream.BandwidthLimit);
        }

        [TestMethod]
        public void TestStreamIntegrityWithSpeedMoreThanSize()
        {
            TestStreamIntegrity(500, 1024);
        }

        [TestMethod]
        public void TestStreamIntegrityWithMaximumSpeed()
        {
            TestStreamIntegrity(4096, long.MaxValue);
        }

        [TestMethod]
        public void TestStreamIntegrityWithSpeedLessThanSize()
        {
            TestStreamIntegrity(247, 77);
        }

        private void TestStreamIntegrity(int streamSize, long maximumBytesPerSecond)
        {
            // arrange
            byte[] data = DummyData.GenerateOrderedBytes(streamSize);
            byte[] copiedData = new byte[streamSize];
            using Stream stream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);

            // act
            stream.Write(data, 0, data.Length);
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(copiedData, 0, copiedData.Length);

            // assert
            Assert.AreEqual(streamSize, data.Length);
            Assert.AreEqual(streamSize, copiedData.Length);
            Assert.IsTrue(data.SequenceEqual(copiedData));
        }
    }
}
