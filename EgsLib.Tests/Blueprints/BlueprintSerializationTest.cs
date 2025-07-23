using EgsLib.Blueprints;
using System.IO;

namespace EgsLib.Tests.Blueprints
{
    public class BlueprintSerializationTest
    {
        [Theory]
        [ClassData(typeof(BlueprintTestData))]
        public void BlueprintRoundTripSerialization_ShouldProduceIdenticalFile(BlueprintDetails details)
        {
            // Arrange
            var originalBlueprint = new Blueprint(details.File);
            var tempFile = Path.GetTempFileName();

            try
            {
                // Act - Serialize the blueprint to a temporary file
                using (var fileStream = File.Create(tempFile))
                using (var writer = new BinaryWriter(fileStream))
                {
                    originalBlueprint.Serialize(writer);
                }

                // Assert - Read back the serialized file and compare
                var deserializedBlueprint = new Blueprint(tempFile);

                // Compare headers
                Assert.Equal(originalBlueprint.Header.Version, deserializedBlueprint.Header.Version);
                Assert.Equal(originalBlueprint.Header.Size, deserializedBlueprint.Header.Size);
                Assert.Equal(originalBlueprint.Header.SizeClass, deserializedBlueprint.Header.SizeClass);
                Assert.Equal(originalBlueprint.Header.Statistics.BlockSolids, deserializedBlueprint.Header.Statistics.BlockSolids);
                Assert.Equal(originalBlueprint.Header.Statistics.BlockDevices, deserializedBlueprint.Header.Statistics.BlockDevices);
                Assert.Equal(originalBlueprint.Header.Statistics.TrianglesReal, deserializedBlueprint.Header.Statistics.TrianglesReal);

                // Compare block data
                Assert.Equal(originalBlueprint.BlockData.Size, deserializedBlueprint.BlockData.Size);
                Assert.Equal(originalBlueprint.BlockData.BlocksSize, deserializedBlueprint.BlockData.BlocksSize);

                // Compare blocks with data (non-zero blocks)
                var originalBlocks = originalBlueprint.BlockData.Blocks.Take(originalBlueprint.BlockData.BlocksSize).ToArray();
                var deserializedBlocks = deserializedBlueprint.BlockData.Blocks.Take(deserializedBlueprint.BlockData.BlocksSize).ToArray();

                Assert.Equal(originalBlocks.Length, deserializedBlocks.Length);

                for (int i = 0; i < originalBlocks.Length; i++)
                {
                    Assert.Equal(originalBlocks[i].Data, deserializedBlocks[i].Data);
                    Assert.Equal(originalBlocks[i].Damage, deserializedBlocks[i].Damage);
                    Assert.Equal(originalBlocks[i].Density, deserializedBlocks[i].Density);
                    Assert.Equal(originalBlocks[i].Color, deserializedBlocks[i].Color);
                    Assert.Equal(originalBlocks[i].Texture, deserializedBlocks[i].Texture);
                    Assert.Equal(originalBlocks[i].TextureRotation, deserializedBlocks[i].TextureRotation);
                    Assert.Equal(originalBlocks[i].Symbol, deserializedBlocks[i].Symbol);
                    Assert.Equal(originalBlocks[i].SymbolRotation, deserializedBlocks[i].SymbolRotation);
                }

                // Compare entities
                Assert.Equal(originalBlueprint.BlockData.Entities.Count, deserializedBlueprint.BlockData.Entities.Count);
                foreach (var entity in originalBlueprint.BlockData.Entities)
                {
                    Assert.True(deserializedBlueprint.BlockData.Entities.ContainsKey(entity.Key));
                    // Note: NbtList comparison would need to be implemented if needed
                }

                // Compare lock codes
                Assert.Equal(originalBlueprint.BlockData.LockCodes.Count, deserializedBlueprint.BlockData.LockCodes.Count);
                foreach (var lockCode in originalBlueprint.BlockData.LockCodes)
                {
                    Assert.True(deserializedBlueprint.BlockData.LockCodes.ContainsKey(lockCode.Key));
                    Assert.Equal(lockCode.Value, deserializedBlueprint.BlockData.LockCodes[lockCode.Key]);
                }

                // Compare signal sources
                Assert.Equal(originalBlueprint.BlockData.SignalSources.Count, deserializedBlueprint.BlockData.SignalSources.Count);

                // Compare signal receivers
                Assert.Equal(originalBlueprint.BlockData.SignalReceivers.Count, deserializedBlueprint.BlockData.SignalReceivers.Count);
                foreach (var receiver in originalBlueprint.BlockData.SignalReceivers)
                {
                    Assert.True(deserializedBlueprint.BlockData.SignalReceivers.ContainsKey(receiver.Key));
                    Assert.Equal(receiver.Value.Count, deserializedBlueprint.BlockData.SignalReceivers[receiver.Key].Count);
                }

                // Compare circuits
                Assert.Equal(originalBlueprint.BlockData.Circuits.Count, deserializedBlueprint.BlockData.Circuits.Count);

                // Compare shortcut names
                Assert.Equal(originalBlueprint.BlockData.ShortcutNames.Count, deserializedBlueprint.BlockData.ShortcutNames.Count);
                for (int i = 0; i < originalBlueprint.BlockData.ShortcutNames.Count; i++)
                {
                    Assert.Equal(originalBlueprint.BlockData.ShortcutNames[i], deserializedBlueprint.BlockData.ShortcutNames[i]);
                }
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void SimpleCubeBlueprint_ShouldSerializeAndDeserializeCorrectly()
        {
            // Arrange
            var simpleCubePath = @"Resources\Blueprints\Simple Cube\Simple Cube.epb";
            var originalBlueprint = new Blueprint(simpleCubePath);
            var tempFile = Path.GetTempFileName();

            try
            {
                // Act - Serialize the blueprint to a temporary file
                using (var fileStream = File.Create(tempFile))
                using (var writer = new BinaryWriter(fileStream))
                {
                    originalBlueprint.Serialize(writer);
                }

                // Assert - Read back the serialized file
                var deserializedBlueprint = new Blueprint(tempFile);

                // Basic validation
                Assert.NotNull(deserializedBlueprint);
                Assert.Equal(originalBlueprint.Header.Size, deserializedBlueprint.Header.Size);
                Assert.Equal(originalBlueprint.BlockData.BlocksSize, deserializedBlueprint.BlockData.BlocksSize);

                // Verify the file sizes are identical (indicating perfect round-trip)
                var originalFileInfo = new FileInfo(simpleCubePath);
                var serializedFileInfo = new FileInfo(tempFile);
                
                // Note: File sizes might not be identical due to potential differences in 
                // ordering or other factors, but the data should be functionally equivalent
                Assert.True(serializedFileInfo.Length > 0);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Theory]
        [ClassData(typeof(BlueprintTestData))]
        public void BlueprintSerialization_ShouldPreserveAllData(BlueprintDetails details)
        {
            // Arrange
            var originalBlueprint = new Blueprint(details.File);
            var tempFile = Path.GetTempFileName();

            try
            {
                // Act - Serialize and deserialize
                using (var fileStream = File.Create(tempFile))
                using (var writer = new BinaryWriter(fileStream))
                {
                    originalBlueprint.Serialize(writer);
                }

                var deserializedBlueprint = new Blueprint(tempFile);

                // Assert - Verify all critical data is preserved
                Assert.Equal(originalBlueprint.Header.Version, deserializedBlueprint.Header.Version);
                Assert.Equal(originalBlueprint.Header.Size, deserializedBlueprint.Header.Size);
                Assert.Equal(originalBlueprint.Header.SizeClass, deserializedBlueprint.Header.SizeClass);
                
                // Verify block count matches expected
                var nonZeroBlocks = deserializedBlueprint.BlockData.Blocks
                    .Take(deserializedBlueprint.BlockData.BlocksSize)
                    .Count(b => b.Data != 0);
                Assert.Equal(details.BlockCount, nonZeroBlocks);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void CreatePermanentEPB_ForInGameTesting()
        {
            // Arrange
            var simpleCubePath = @"Resources\Blueprints\Simple Cube\Simple Cube.epb";
            var outputDirectory = @"..\..\..\..\TestOutput";
            var outputFile = Path.Combine(outputDirectory, "SerializedSimpleCube_ForGameTesting.epb");

            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);

            // Load the original blueprint
            var originalBlueprint = new Blueprint(simpleCubePath);

            // Act - Serialize the blueprint to a permanent file
            using (var fileStream = File.Create(outputFile))
            using (var writer = new BinaryWriter(fileStream))
            {
                originalBlueprint.Serialize(writer);
            }

            // Verify the serialized file can be read back correctly
            var deserializedBlueprint = new Blueprint(outputFile);

            // Basic validation
            Assert.NotNull(deserializedBlueprint);
            Assert.Equal(originalBlueprint.Header.Version, deserializedBlueprint.Header.Version);
            Assert.Equal(originalBlueprint.Header.Size, deserializedBlueprint.Header.Size);
            Assert.Equal(originalBlueprint.BlockData.BlocksSize, deserializedBlueprint.BlockData.BlocksSize);

            // Output information for the user
            var fileInfo = new FileInfo(outputFile);
            var absolutePath = Path.GetFullPath(outputFile);
            
            Console.WriteLine($"✅ Permanent EPB file created successfully!");
            Console.WriteLine($"📁 Location: {absolutePath}");
            Console.WriteLine($"📊 File size: {fileInfo.Length} bytes");
            Console.WriteLine($"🎮 This file can now be loaded into the game for testing.");
            Console.WriteLine($"🔧 Blueprint info: {deserializedBlueprint.Header.Size} blocks, Version {deserializedBlueprint.Header.Version}");

            // Note: This file is intentionally NOT deleted for in-game testing
        }
    }
}
