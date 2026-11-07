using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using VTOLVRTacviewSPRecorder.Core;
using Xunit;

namespace VTOLVRTacviewSPRecorder.Core.Tests;

public sealed class TacviewCoreTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new DateTimeOffset(2026, 8, 9, 12, 34, 56, 789, TimeSpan.Zero);

    [Fact]
    public void WriterUsesInvariantNumbersUnderGermanCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var text = new StringWriter();
            using var writer = new TacviewAcmiWriter(text, "Invariant", ReferenceTime, leaveOpen: true);

            writer.WriteOwnshipFrame(1.25, Sample(
                longitude: 7.1234567,
                latitude: 51.7654321,
                altitude: 1234.5,
                roll: -4.25,
                pitch: 8.5,
                yaw: 270.75,
                u: 12345.67,
                v: -9876.54,
                heading: 270.75));

            string output = text.ToString();
            Assert.Contains("#1.250\n", output);
            Assert.Contains(
                "T=7.1234567|51.7654321|1234.50|-4.25|8.50|270.75|12345.67|-9876.54|270.75",
                output);
            Assert.DoesNotContain("1234,50", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void WriterEmitsHeaderNineFieldFrameAndOwnshipMetadataOnce()
    {
        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Test, Mission", ReferenceTime, leaveOpen: true);
        AircraftTransform sample = Sample(7.0, 51.0, 100.0, 1.0, 2.0, 3.0, 4000.0, 5000.0, 3.0);

        writer.WriteOwnshipFrame(0.0, sample);
        writer.WriteOwnshipFrame(0.1, sample);

        string output = text.ToString();
        Assert.StartsWith("FileType=text/acmi/tacview\nFileVersion=2.2\n", output);
        Assert.Contains("0,ReferenceTime=2026-08-09T12:34:56.789Z\n", output);
        Assert.Contains("0,DataRecorder=VTOL VR Tacview Singleplayer Recorder\n", output);
        Assert.Contains("0,Title=Test\\, Mission\n", output);
        Assert.Contains(
            "#0.000\n100,T=7.0000000|51.0000000|100.00|1.00|2.00|3.00|4000.00|5000.00|3.00,Type=Air+FixedWing,Name=Ownship,Coalition=Allies,Color=Blue\n",
            output);
        Assert.Contains("#0.100\n100,T=||||||||\n", output);
        Assert.DoesNotContain("ReferenceLongitude", output);
        Assert.DoesNotContain("ReferenceLatitude", output);
        Assert.Equal(1, CountOccurrences(output, "Name=Ownship"));
    }

    [Fact]
    public void VtolGpsAndGlobalCoordinatesMapDirectlyToTacviewFields()
    {
        var attitude = new Attitude(-12.0, 6.5, 273.0);

        AircraftTransform sample = VtolTransformMapper.Map(
            gpsLatitudeX: 51.2345678,
            gpsLongitudeY: 7.8765432,
            gpsAltitudeZ: 1234.5,
            globalWorldX: 45678.9,
            globalWorldZ: -12345.6,
            attitude);

        Assert.Equal(7.8765432, sample.LongitudeDegrees, 10);
        Assert.Equal(51.2345678, sample.LatitudeDegrees, 10);
        Assert.Equal(1234.5, sample.AltitudeMeters, 10);
        Assert.Equal(45678.9, sample.NativeUPositionMeters, 10);
        Assert.Equal(-12345.6, sample.NativeVPositionMeters, 10);
        Assert.Equal(273.0, sample.NativeHeadingDegrees, 10);
    }

    [Fact]
    public void NativeUvCurveIsSerializedWithoutCollapsingAxes()
    {
        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Curve", ReferenceTime, leaveOpen: true);
        var points = new[]
        {
            (U: 0.0, V: 0.0, Heading: 0.0),
            (U: 100.0, V: 0.0, Heading: 90.0),
            (U: 100.0, V: 100.0, Heading: 180.0),
            (U: 0.0, V: 100.0, Heading: 270.0),
            (U: 0.0, V: 0.0, Heading: 0.0)
        };

        for (int index = 0; index < points.Length; index++)
        {
            (double u, double v, double heading) = points[index];
            writer.WriteOwnshipFrame(
                index * 0.1,
                VtolTransformMapper.Map(
                    51.0,
                    7.0,
                    1000.0,
                    u,
                    v,
                    new Attitude(0.0, 0.0, heading)));
        }

        IReadOnlyList<string[]> transforms = ReadExpandedTransforms(text.ToString(), "100");

        Assert.Equal(points.Length, transforms.Count);
        for (int index = 0; index < points.Length; index++)
        {
            Assert.Equal(points[index].U, double.Parse(transforms[index][6], CultureInfo.InvariantCulture), 10);
            Assert.Equal(points[index].V, double.Parse(transforms[index][7], CultureInfo.InvariantCulture), 10);
            Assert.Equal(points[index].Heading, double.Parse(transforms[index][8], CultureInfo.InvariantCulture), 10);
        }

        double firstLegU = double.Parse(transforms[1][6], CultureInfo.InvariantCulture) -
            double.Parse(transforms[0][6], CultureInfo.InvariantCulture);
        double firstLegV = double.Parse(transforms[1][7], CultureInfo.InvariantCulture) -
            double.Parse(transforms[0][7], CultureInfo.InvariantCulture);
        double secondLegU = double.Parse(transforms[2][6], CultureInfo.InvariantCulture) -
            double.Parse(transforms[1][6], CultureInfo.InvariantCulture);
        double secondLegV = double.Parse(transforms[2][7], CultureInfo.InvariantCulture) -
            double.Parse(transforms[1][7], CultureInfo.InvariantCulture);
        Assert.NotEqual(0.0, firstLegU * secondLegV - firstLegV * secondLegU);
    }

    [Fact]
    public void AircraftRegistryReservesOwnshipAndNeverRecyclesIds()
    {
        var registry = new AircraftRegistry();
        object ownshipIdentity = new object();
        object firstIdentity = new object();
        object secondIdentity = new object();
        object replacementIdentity = new object();
        object respawnedOwnshipIdentity = new object();

        TrackedAircraft ownship = registry.Register(ownshipIdentity, true, Metadata("Ownship"));
        TrackedAircraft first = registry.Register(firstIdentity, false, Metadata("Wingman"));
        TrackedAircraft second = registry.Register(secondIdentity, false, Metadata("Bandit"));

        Assert.Equal(TacviewAcmiWriter.OwnshipObjectId, ownship.ObjectId);
        Assert.Equal(0x101UL, first.ObjectId);
        Assert.Equal(0x102UL, second.ObjectId);
        Assert.Same(second, registry.Register(secondIdentity, false, Metadata("Bandit renamed")));
        Assert.Equal("Bandit renamed", second.Metadata.Name);
        Assert.True(registry.Remove(firstIdentity, out TrackedAircraft removed));
        Assert.Same(first, removed);

        TrackedAircraft replacement = registry.Register(replacementIdentity, false, Metadata("Respawn"));
        Assert.Equal(0x103UL, replacement.ObjectId);
        Assert.Equal(3, registry.Count);

        Assert.True(registry.Remove(ownshipIdentity, out TrackedAircraft removedOwnship));
        Assert.Equal(TacviewAcmiWriter.OwnshipObjectId, removedOwnship.ObjectId);
        TrackedAircraft respawnedOwnship = registry.Register(
            respawnedOwnshipIdentity,
            true,
            Metadata("Respawned Ownship"));
        Assert.NotEqual(TacviewAcmiWriter.OwnshipObjectId, respawnedOwnship.ObjectId);
        Assert.Equal(0x104UL, respawnedOwnship.ObjectId);

        registry.Clear();
        Assert.Equal(0, registry.Count);
        Assert.Equal(
            TacviewAcmiWriter.OwnshipObjectId,
            registry.Register(new object(), true, Metadata("New session ownship")).ObjectId);
    }

    [Fact]
    public void RegistryPromotesLateDiscoveredPlayerWithoutReusingItsOldId()
    {
        var registry = new AircraftRegistry();
        object identity = new object();
        TrackedAircraft initiallyRegular = registry.Register(identity, false, Metadata("Late player"));

        Assert.True(registry.PromoteToOwnship(
            identity,
            Metadata("Ownship"),
            out TrackedAircraft previous,
            out TrackedAircraft promoted));

        Assert.Same(initiallyRegular, previous);
        Assert.Equal(0x101UL, previous.ObjectId);
        Assert.True(promoted.IsOwnship);
        Assert.Equal(TacviewAcmiWriter.OwnshipObjectId, promoted.ObjectId);
        Assert.True(registry.TryGet(identity, out TrackedAircraft current));
        Assert.Same(promoted, current);
        Assert.Equal(0x102UL, registry.Register(new object(), false, Metadata("Next")).ObjectId);
    }

    [Fact]
    public void WriterCreatesAlliedEnemyAndUnknownAircraftMetadata()
    {
        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Metadata", ReferenceTime, leaveOpen: true);

        writer.WriteFrame(0.0, new[]
        {
            TacviewObjectUpdate.Upsert(0x101, Sample(), new AircraftMetadata("Air+FixedWing", "F/A-26B", "Allies", "Blue", "Viper 1-1")),
            TacviewObjectUpdate.Upsert(0x102, Sample(), new AircraftMetadata("Air+Rotorcraft", "AH-94", "Enemies", "Red", "Bandit 2-1")),
            TacviewObjectUpdate.Upsert(0x103, Sample(), new AircraftMetadata("Air", "Unknown Aircraft", "Neutral", "White"))
        });

        string output = text.ToString();
        Assert.Contains("101,T=", output);
        Assert.Contains("Type=Air+FixedWing,Name=F/A-26B,Coalition=Allies,Color=Blue,CallSign=Viper 1-1", output);
        Assert.Contains("Type=Air+Rotorcraft,Name=AH-94,Coalition=Enemies,Color=Red,CallSign=Bandit 2-1", output);
        Assert.Contains("Type=Air,Name=Unknown Aircraft,Coalition=Neutral,Color=White", output);
    }

    [Fact]
    public void WriterDeltaEncodesEachObjectIndependently()
    {
        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Deltas", ReferenceTime, leaveOpen: true);
        AircraftMetadata metadata = Metadata("Aircraft");

        writer.WriteFrame(0.0, new[]
        {
            TacviewObjectUpdate.Upsert(0x101, Sample(longitude: 1.0), metadata),
            TacviewObjectUpdate.Upsert(0x102, Sample(longitude: 10.0), metadata)
        });
        writer.WriteFrame(0.1, new[]
        {
            TacviewObjectUpdate.Upsert(0x101, Sample(longitude: 2.0), metadata),
            TacviewObjectUpdate.Upsert(0x102, Sample(longitude: 10.0), metadata)
        });

        string output = text.ToString();
        Assert.Contains("#0.100\n101,T=2.0000000||||||||\n102,T=||||||||\n", output);
    }

    [Fact]
    public void WriterUsesOneFrameHeaderAndFlushForMultipleAircraft()
    {
        var text = new ControlledFlushWriter();
        using var writer = new TacviewAcmiWriter(text, "Batch", ReferenceTime, leaveOpen: true);

        writer.WriteFrame(2.0, new[]
        {
            TacviewObjectUpdate.Upsert(0x102, Sample(), Metadata("Two")),
            TacviewObjectUpdate.Upsert(0x101, Sample(), Metadata("One"))
        });

        Assert.Equal(1, text.FlushCount);
        Assert.Equal(1, CountOccurrences(text.ToString(), "#2.000\n"));
        Assert.True(text.ToString().IndexOf("101,T=", StringComparison.Ordinal) <
                    text.ToString().IndexOf("102,T=", StringComparison.Ordinal));
    }

    [Fact]
    public void WriterEmitsDeleteEventAndPreventsReuse()
    {
        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Delete", ReferenceTime, leaveOpen: true);
        AircraftMetadata metadata = Metadata("Bandit");
        writer.WriteFrame(0.0, new[] { TacviewObjectUpdate.Upsert(0x101, Sample(), metadata) });
        writer.WriteFrame(1.0, new[]
        {
            TacviewObjectUpdate.Delete(0x101, TacviewObjectRemovalReason.LeftArea, "Bandit 2-1")
        });

        Assert.Contains("#1.000\n0,Event=LeftArea|101|Bandit 2-1 left area\n-101\n", text.ToString());
        Assert.Throws<InvalidOperationException>(() => writer.WriteFrame(
            2.0,
            new[] { TacviewObjectUpdate.Upsert(0x101, Sample(), metadata) }));
    }

    [Fact]
    public void WriterUsesDestroyedEventForKilledAircraft()
    {
        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Destroyed", ReferenceTime, leaveOpen: true);
        writer.WriteFrame(0.0, new[]
        {
            TacviewObjectUpdate.Upsert(0x102, Sample(), Metadata("Bandit"))
        });
        writer.WriteFrame(0.5, new[]
        {
            TacviewObjectUpdate.Delete(0x102, TacviewObjectRemovalReason.Destroyed, "Bandit 2-2")
        });

        Assert.Contains("#0.500\n0,Event=Destroyed|102|Bandit 2-2 destroyed\n-102\n", text.ToString());
    }

    [Fact]
    public void SchedulerRunsOwnshipAtTenHzAndOtherAircraftAtFiveHz()
    {
        var registry = new AircraftRegistry();
        TrackedAircraft ownship = registry.Register(new object(), true, Metadata("Ownship"));
        TrackedAircraft ai = registry.Register(new object(), false, Metadata("AI"));
        var scheduler = new AircraftSampleScheduler();

        Assert.True(scheduler.ShouldSample(ownship, 0.0));
        Assert.True(scheduler.ShouldSample(ai, 0.0));
        Assert.False(scheduler.ShouldSample(ownship, 0.099));
        Assert.False(scheduler.ShouldSample(ai, 0.199));
        Assert.True(scheduler.ShouldSample(ownship, 0.1));
        Assert.True(scheduler.ShouldSample(ai, 0.2));
    }

    [Fact]
    public void FailedActorSampleDoesNotBlockOtherAircraft()
    {
        var registry = new AircraftRegistry();
        TrackedAircraft failing = registry.Register(new object(), true, Metadata("Ownship"));
        TrackedAircraft healthy = registry.Register(new object(), false, Metadata("Wingman"));
        var collector = new AircraftFrameCollector(new AircraftSampleScheduler());
        var updates = new List<TacviewObjectUpdate>();
        var skipped = new List<ulong>();

        collector.Collect(
            0.0,
            new[]
            {
                new AircraftSamplingTarget(failing, new ThrowingSource()),
                new AircraftSamplingTarget(healthy, new FixedSource(Sample(longitude: 42.0)))
            },
            updates,
            (aircraft, _) => skipped.Add(aircraft.ObjectId));

        Assert.Equal(new[] { failing.ObjectId }, skipped);
        TacviewObjectUpdate update = Assert.Single(updates);
        Assert.Equal(healthy.ObjectId, update.ObjectId);
        Assert.Equal(42.0, update.Transform.LongitudeDegrees, 10);
    }

    [Fact]
    public void NonFiniteActorSampleDoesNotReachWriterOrBlockOtherAircraft()
    {
        var registry = new AircraftRegistry();
        TrackedAircraft invalid = registry.Register(new object(), false, Metadata("Invalid"));
        TrackedAircraft healthy = registry.Register(new object(), false, Metadata("Healthy"));
        var collector = new AircraftFrameCollector(new AircraftSampleScheduler());
        var updates = new List<TacviewObjectUpdate>();
        var skipped = new List<ulong>();

        collector.Collect(
            0.0,
            new[]
            {
                new AircraftSamplingTarget(invalid, new FixedSource(Sample(longitude: double.NaN))),
                new AircraftSamplingTarget(healthy, new FixedSource(Sample(longitude: 11.0)))
            },
            updates,
            (aircraft, _) => skipped.Add(aircraft.ObjectId));

        Assert.Equal(new[] { invalid.ObjectId }, skipped);
        TacviewObjectUpdate update = Assert.Single(updates);
        Assert.Equal(healthy.ObjectId, update.ObjectId);

        var text = new StringWriter();
        using var writer = new TacviewAcmiWriter(text, "Finite batch", ReferenceTime, leaveOpen: true);
        writer.WriteFrame(0.0, updates);
        Assert.Contains("102,T=11.0000000", text.ToString());
    }

    [Fact]
    public void FileNameIsUtcAndWindowsSafe()
    {
        string fileName = TacviewFileName.Build(ReferenceTime, "  Test:/Mission?  ");

        Assert.Equal("20260809_123456_789Z_Test_Mission_.acmi", fileName);
        Assert.Equal("Mission", TacviewFileName.SanitizeMissionName("<>:*?"));
    }

    [Fact]
    public void SchedulerDropsMissedIntervalsInsteadOfBuildingABacklog()
    {
        var registry = new AircraftRegistry();
        TrackedAircraft ownship = registry.Register(new object(), true, Metadata("Ownship"));
        var scheduler = new AircraftSampleScheduler();

        Assert.True(scheduler.ShouldSample(ownship, 0.0));
        Assert.True(scheduler.ShouldSample(ownship, 60.0));
        Assert.False(scheduler.ShouldSample(ownship, 60.05));
        Assert.True(scheduler.ShouldSample(ownship, 60.1));
    }

    [Fact]
    public void FileWriterFinalizesAtomicallyAndDisposeIsIdempotent()
    {
        string testDirectory = CreateTemporaryDirectory();

        try
        {
            var writer = TacviewAcmiWriter.CreateFile(testDirectory, "Atomic", ReferenceTime);
            string temporaryPath = writer.FinalPath + ".tmp";
            writer.WriteOwnshipFrame(0.0, Sample());

            Assert.True(File.Exists(temporaryPath));
            writer.Dispose();
            string finalPath = writer.FinalPath;
            writer.Dispose();

            Assert.True(File.Exists(finalPath));
            Assert.False(File.Exists(temporaryPath));
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(finalPath).Take(3).ToArray());
        }
        finally
        {
            DeleteTemporaryDirectory(testDirectory);
        }
    }

    [Fact]
    public void AbortingWriterLeavesRecoverableTemporaryFile()
    {
        string testDirectory = CreateTemporaryDirectory();

        try
        {
            var writer = TacviewAcmiWriter.CreateFile(testDirectory, "Incomplete", ReferenceTime);
            string finalPath = writer.FinalPath;
            string temporaryPath = finalPath + ".tmp";
            writer.WriteOwnshipFrame(0.0, Sample());
            writer.Abort();
            writer.Abort();

            Assert.True(File.Exists(temporaryPath));
            Assert.False(File.Exists(finalPath));
        }
        finally
        {
            DeleteTemporaryDirectory(testDirectory);
        }
    }

    [Fact]
    public void UnityEulerAnglesConvertToTacviewSignsAndHeadingConvention()
    {
        Attitude attitude = Attitude.FromUnityEuler(350.0, 90.0, 15.0);

        Assert.Equal(-15.0, attitude.RollDegrees, 10);
        Assert.Equal(10.0, attitude.PitchDegrees, 10);
        Assert.Equal(90.0, attitude.YawDegrees, 10);

        AircraftTransform north = VtolTransformMapper.Map(0, 0, 0, 0, 100, Attitude.FromUnityEuler(0, 0, 0));
        AircraftTransform east = VtolTransformMapper.Map(0, 0, 0, 100, 100, Attitude.FromUnityEuler(0, 90, 0));
        Assert.Equal(0.0, north.NativeHeadingDegrees, 10);
        Assert.Equal(90.0, east.NativeHeadingDegrees, 10);
        Assert.Equal(east.YawDegrees, east.NativeHeadingDegrees, 10);
    }

    [Fact]
    public void LocalManifestIdentifiesV052BetaWithoutWorkshopDependencies()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));
        string manifestPath = Path.Combine(repositoryRoot, "item.json");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;

        Assert.Equal(
            "Tacview Singleplayer Recorder [0.5.2-beta.1]",
            root.GetProperty("Title").GetString());
        Assert.Contains("terrain", root.GetProperty("Description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Community beta", root.GetProperty("Description").GetString());
        Assert.Contains("pooled VTOL VR Bullet", root.GetProperty("Description").GetString());
        Assert.Equal(0, root.GetProperty("PublishFieldId").GetInt32());
        Assert.Empty(root.GetProperty("DependenciesIds").EnumerateArray());
        Assert.Equal(
            "VTOLVRTacviewSPRecorder.dll",
            root.GetProperty("MetaData").GetProperty("DllName").GetString());
    }

    [Fact]
    public void DisposeClosesUnderlyingWriterWhenFlushFails()
    {
        var text = new ControlledFlushWriter { ThrowOnFlush = true };
        var writer = new TacviewAcmiWriter(text, "Flush failure", ReferenceTime);

        Assert.Throws<IOException>(() => writer.Dispose());
        Assert.True(text.WasDisposed);
        writer.Dispose();
    }

    private static AircraftMetadata Metadata(string name)
    {
        return new AircraftMetadata("Air", name, "Allies", "Blue");
    }

    private static AircraftTransform Sample(
        double longitude = 7.0,
        double latitude = 51.0,
        double altitude = 1000.0,
        double roll = 0.0,
        double pitch = 0.0,
        double yaw = 0.0,
        double u = 0.0,
        double v = 0.0,
        double heading = 0.0)
    {
        return new AircraftTransform(
            longitude,
            latitude,
            altitude,
            roll,
            pitch,
            yaw,
            u,
            v,
            heading);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static IReadOnlyList<string[]> ReadExpandedTransforms(string acmi, string objectId)
    {
        var transforms = new List<string[]>();
        string[] previous = null;

        foreach (string line in acmi.Split('\n'))
        {
            if (!line.StartsWith(objectId + ",T=", StringComparison.Ordinal))
            {
                continue;
            }

            string transform = line.Substring(objectId.Length + 3);
            int metadataStart = transform.IndexOf(",", StringComparison.Ordinal);
            if (metadataStart >= 0)
            {
                transform = transform.Substring(0, metadataStart);
            }

            string[] fields = transform.Split('|');
            Assert.Equal(9, fields.Length);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].Length == 0)
                {
                    Assert.NotNull(previous);
                    fields[index] = previous[index];
                }
            }

            transforms.Add(fields);
            previous = fields;
        }

        return transforms;
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "VTOLVRTacviewSPRecorder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedSource : IAircraftTransformSource
    {
        private readonly AircraftTransform _transform;

        public FixedSource(AircraftTransform transform)
        {
            _transform = transform;
        }

        public bool TryGetTransform(out AircraftTransform transform)
        {
            transform = _transform;
            return true;
        }
    }

    private sealed class ThrowingSource : IAircraftTransformSource
    {
        public bool TryGetTransform(out AircraftTransform transform)
        {
            transform = default;
            throw new InvalidOperationException("simulated actor failure");
        }
    }

    private sealed class ControlledFlushWriter : StringWriter
    {
        public bool ThrowOnFlush { get; set; }

        public int FlushCount { get; private set; }

        public bool WasDisposed { get; private set; }

        public override void Flush()
        {
            FlushCount++;
            if (ThrowOnFlush)
            {
                throw new IOException("Simulated flush failure.");
            }

            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
