using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using XCMAConverter.Level5.Compression;
using XCMAConverter.Tools;
using XCMAConverter.Level5.Compression.NoCompression;
using XCMAConverter.Level5.Compression.LZ10;

namespace XCMAConverter.Level5.Camera
{
    public class XCMA
    {
        public uint HashName;

        public Dictionary<int, Dictionary<int, float[]>> CamValues;

        public XCMA(Stream stream)
        {
            CamValues = new Dictionary<int, Dictionary<int, float[]>>();

            using (BinaryDataReader data = new BinaryDataReader(stream))
            {
                XCMASupport.Header header1 = data.ReadStruct<XCMASupport.Header>();
                XCMASupport.Header2 header2 = data.ReadStruct<XCMASupport.Header2>();
                XCMASupport.Header3 header3 = data.ReadStruct<XCMASupport.Header3>();

                HashName = header2.Hash;

                ReadCamData(data, 3);
                ReadCamData(data, 3);
                ReadCamData(data, 1);
                ReadCamData(data, 1);
            }
        }

        public XCMA(string[] lines)
        {
            HashName = uint.Parse(lines[0].Split(':')[1].Trim(), NumberStyles.AllowHexSpecifier);
            CamValues = new Dictionary<int, Dictionary<int, float[]>>();

            int outerKey = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!line.StartsWith("\t"))
                {
                    outerKey = int.Parse(line.Replace(":", ""));
                    CamValues[outerKey] = new Dictionary<int, float[]>();
                }
                else
                {
                    int innerKey = int.Parse(line.Split(':')[0].Trim());
                    string[] values = line.Split('(')[1].Split(')')[0].Split(',');
                    float[] floats = new float[values.Length];
                    for (int j = 0; j < values.Length; j++)
                    {
                        floats[j] = float.Parse(values[j], CultureInfo.InvariantCulture);
                    }
                    CamValues[outerKey][innerKey] = floats;
                }
            }
        }

        private int GetFrameCount()
        {
            int maxKey = 0;

            if (CamValues.Count > 0)
            {
                foreach (Dictionary<int, float[]> item in CamValues.Values)
                {
                    if (item.Keys.Max() > maxKey)
                    {
                        maxKey = item.Keys.Max();
                    }
                }
            }

            return maxKey;
        }

        private void ReadCamData(BinaryDataReader data, int valuesCount)
        {
            int index = CamValues.Keys.Count();

            CamValues.Add(index, new Dictionary<int, float[]>());

            XCMASupport.CameraHeader camHeader = data.ReadStruct<XCMASupport.CameraHeader>();

            using (BinaryDataReader camData = new BinaryDataReader(Compressor.Decompress(data.GetSection(camHeader.CameraDataLength - camHeader.CameraDataOffset))))
            {
                byte unk1 = camData.ReadValue<byte>();
                byte unk2 = camData.ReadValue<byte>();
                byte framesCount = camData.ReadValue<byte>();
                byte unk3 = camData.ReadValue<byte>();

                int[] framesIndexes = new int[framesCount];
                for (int j = 0; j < framesCount; j++)
                {
                    framesIndexes[j] = camData.ReadValue<short>();
                }

                camData.Seek((uint)camHeader.CameraStartDataOffset);
                for (int k = 0; k < framesCount; k++)
                {
                    CamValues[index].Add(framesIndexes[k], camData.ReadMultipleStruct<float>(valuesCount).ToArray());
                }
            }
        }

        public void Save(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                BinaryDataWriter writer = new BinaryDataWriter(stream);

                XCMASupport.Header header1 = new XCMASupport.Header
                {
                    Magic = 0x414D4358,
                    Offset = 0x20,
                    Unk1 = 0x18,
                    Unk2 = 0x01,
                    Unk3 = 0x01,
                    Unk4 = 0x01,
                    Unk5 = 0x01,
                    Unk6 = 0x00
                };

                XCMASupport.Header2 header2 = new XCMASupport.Header2
                {
                    Hash = HashName,
                    Unk1 = 0x0,
                    FramesCount = GetFrameCount(),
                    Unk2 = 0x02,
                    Unk3 = 0x3F000000,
                    Unk4 = 0x00,
                    Unk5 = 0x0C,
                    Unk6 = 0x1C,
                    Unk7 = 0x50,
                    Unk8 = 0x00
                };

                XCMASupport.Pattern pattern1 = new XCMASupport.Pattern
                {
                    Unk1 = 0x0201,
                    Unk2 = 0x0300,
                    Unk3 = 0x00,
                    FramesCount = (short)GetFrameCount(),
                };

                XCMASupport.Pattern pattern2 = new XCMASupport.Pattern
                {
                    Unk1 = 0x0201,
                    Unk2 = 0x0100,
                    Unk3 = 0x00,
                    FramesCount = (short)GetFrameCount(),
                };

                XCMASupport.Header3 header3 = new XCMASupport.Header3
                {
                    Hash1 = 0xC55BEBD1,
                    Hash2 = 0xC55BEBD1,
                    Hash3 = 0xC55BEBD1,
                    Unk1 = 0X28,
                    Unk2 = 0x30,
                    Unk3 = 0x38,
                    Unk4 = 0x40,
                    Unk5 = 0x48,
                    Unk6 = 0x00,
                    UnkPattern = new XCMASupport.Pattern[4] { pattern1, pattern1, pattern2, pattern2 },
                    Unk7 = new byte[8]
                };

                writer.WriteStruct(header1);
                writer.WriteStruct(header2);
                writer.WriteStruct(header3);

                for (int i = 0; i < 4; i++)
                {
                    using (MemoryStream camDataStream = new MemoryStream())
                    {
                        long camDataStartOffset = 0;

                        using (BinaryDataWriter camDataWriter = new BinaryDataWriter(camDataStream))
                        {
                            camDataWriter.Write((byte)0xFF);
                            camDataWriter.Write((byte)0xFF);
                            camDataWriter.Write((byte)CamValues[i].Values.Count());
                            camDataWriter.Write((byte)0x20);

                            for (int j = 0; j < CamValues[i].Values.Count; j++)
                            {
                                camDataWriter.Write((short)CamValues[i].ElementAt(j).Key);
                            }

                            camDataWriter.WriteAlignment(4);

                            camDataStartOffset = camDataWriter.Position;

                            for (int j = 0; j < CamValues[i].Values.Count; j++)
                            {
                                for (int k = 0; k < CamValues[i].ElementAt(j).Value.Count(); k++)
                                {
                                    camDataWriter.WriteStruct<float>(CamValues[i].ElementAt(j).Value[k]);
                                }
                            }
                        }

                        byte[] compressedCamData = new NoCompression().Compress(camDataStream.ToArray());

                        writer.Write(0x10);
                        writer.Write(0x04);
                        writer.Write((int)camDataStartOffset);
                        writer.Write(compressedCamData.Length + 0x10);
                        writer.Write(compressedCamData);
                    }
                }
            }
        }
    }
}
