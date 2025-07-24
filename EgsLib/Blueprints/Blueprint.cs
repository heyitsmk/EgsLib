using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Linq;

namespace EgsLib.Blueprints
{
    public class Blueprint
    {
        #region File Info
        public string FilePath { get; }

        public string FileName { get; }

        public long FileSize { get; }

        public DateTime FileLastWritten { get; }
        #endregion

        public BlueprintHeader Header { get; }

        public BlueprintBlockData BlockData { get; }

        /// <summary>
        /// Additional data that appears after the block data (possibly checksum or terrain data)
        /// </summary>
        public byte[] TrailingData { get; private set; } = new byte[0];

        /// <summary>
        /// Byte and boolean that appear after the ZIP length (as read by the game)
        /// </summary>
        private byte _zipByte;
        private bool _zipBoolean;

        public Blueprint(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new ArgumentNullException(nameof(file));

            if (!File.Exists(file))
                throw new FileNotFoundException("Blueprint file does not exist");

            var fileInfo = new FileInfo(file);

            // Save file info
            FilePath = fileInfo.FullName;
            FileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
            FileSize = fileInfo.Length;

            // Read file & cache LastWriteTime
            var bytes = ReadFileBytes(fileInfo, out DateTime lastWriteTime);
            FileLastWritten = lastWriteTime;

            // Parse file
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                Header = new BlueprintHeader(FileName, reader);
                BlockData = ReadBlockData(reader);
                ReadTerrainData(reader);
            }
        }

        public void Serialize(BinaryWriter bw)
        {
            Header.Serialize(bw);
            SerializeBlockData(bw);
            SerializeTrailingData(bw);
        }

        private static byte[] ReadFileBytes(FileInfo file, out DateTime lastWriteTime)
        {
            byte[] bytes;

            using (var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                lastWriteTime = file.LastWriteTime;

                var fileLength = fs.Length;
                if (fileLength > int.MaxValue)
                    throw new IOException("File is too large");

                var index = 0;
                var count = (int)fileLength;
                bytes = new byte[fileLength];

                while (count > 0)
                {
                    var n = fs.Read(bytes, index, count);
                    if (n == 0)
                        throw new EndOfStreamException();

                    index += n;
                    count -= n;
                }
            }

            return bytes;
        }

        private BlueprintBlockData ReadBlockData(BinaryReader reader)
        {
            // Older versions read until the end of file while newer ones support terrain data after block data
            int length;
            if (Header.Version > 22)
            {
                length = reader.ReadInt32();
                _zipByte = reader.ReadByte();      // Read the byte
                _zipBoolean = reader.ReadBoolean(); // Read the boolean
            }
            else
            {
                length = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            }

            var bytes = reader.ReadBytes(length);

            // Older versions are missing the zip header because ???
            bytes[0] = (byte)'P';
            bytes[1] = (byte)'K';

            var compressed = new MemoryStream(bytes, writable: false);
            using (var zipFile = new ZipFile(compressed, leaveOpen: false))
            {
                var entry = zipFile.Cast<ZipEntry>()
                    .FirstOrDefault(e => e.IsFile && e.Name == "0");

                // TODO: Better error handling for missing block data, low priority since this shouldn't be possible
                if (entry == null)
                    return null;

                Stream stream = null;
                BinaryReader zipReader = null;

                try
                {
                    stream = zipFile.GetInputStream(entry);
                    zipReader = new BinaryReader(stream);

                    return new BlueprintBlockData(zipReader, Header);
                }
                catch (ZipException)
                {
                    // Thrown on malformed zip entry which seems to be an issue with some files
                    // Notably: CV_New, HV_New, SV_New
                    return null;
                }
                finally
                {
                    zipReader?.Dispose();
                    stream?.Dispose();
                }
            }
        }

        private void SerializeBlockData(BinaryWriter writer)
        {
            // Serialize block data to byte[]
            byte[] blockDataBytes;
            using (var blockDataStream = new MemoryStream())
            using (var blockDataWriter = new BinaryWriter(blockDataStream))
            {
                BlockData.Serialize(blockDataWriter);
                blockDataWriter.Flush();
                blockDataBytes = blockDataStream.ToArray();
            }

            // Create a ZIP archive in memory with one entry "0"
            byte[] compressedBytes;
            using (var compressedStream = new MemoryStream())
            {
                using (var zipFile = ZipFile.Create(compressedStream))
                {
                    zipFile.BeginUpdate();
                    zipFile.Add(new ByteArrayDataSource(blockDataBytes), "0");
                    zipFile.CommitUpdate();
                }
                compressedBytes = compressedStream.ToArray();
            }

            if (Header.Version > 22)
            {
                // Write length + 2 "garbage" bytes
                var length = compressedBytes.Length;
                if (length <= 0)
                {
                    throw new InvalidOperationException($"Invalid compressed bytes length: {length}");
                }
                writer.Write(length);
                writer.Write(_zipByte);    // Write the byte 
                writer.Write(_zipBoolean); // Write the boolean
                writer.Write(compressedBytes); // Full ZIP archive
            }
            else
            {
                // Remove the first 2 bytes ("PK") for legacy versions
                var dataWithoutPK = new byte[compressedBytes.Length - 2];
                Array.Copy(compressedBytes, 2, dataWithoutPK, 0, dataWithoutPK.Length);
                writer.Write(dataWithoutPK);
            }
        }

        private void SerializeTrailingData(BinaryWriter writer)
        {
            if (TrailingData.Length > 0)
            {
                writer.Write(TrailingData);
            }
        }


        private void ReadTerrainData(BinaryReader reader)
        {
            // Capture any remaining data in the stream (could be checksum, terrain data, etc.)
            var remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remainingBytes > 0)
            {
                TrailingData = reader.ReadBytes((int)remainingBytes);
            }
            else
            {
                TrailingData = new byte[0];
            }
        }
    }

    public class ByteArrayDataSource : IStaticDataSource
    {
        private readonly byte[] _data;

        public ByteArrayDataSource(byte[] data)
        {
            _data = data;
        }

        public Stream GetSource()
        {
            return new MemoryStream(_data, writable: false);
        }
    }
}
