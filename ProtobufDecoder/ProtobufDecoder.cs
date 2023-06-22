using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtobufDecoder
{
    /// <summary>
    /// https://developers.google.com/protocol-buffers/docs/encoding
    /// </summary>
    internal class ProtobufDecoder
    {
        private readonly Dictionary<byte, string> fieldTypes = new Dictionary<byte, string>();

        public ProtobufDecoder()
        {
            fieldTypes.Add(0, "Varint (int32, int64, uint32, uint64, sint32, sint64, bool, enum)");
            fieldTypes.Add(1, "64-bit (fixed64, sfixed64, double)");
            fieldTypes.Add(2, "Length-delimited (string, bytes, embedded messages, packed repeated fields)");
            fieldTypes.Add(3, "Start group groups (deprecated)");
            fieldTypes.Add(4, "End group groups  (deprecated)");
            fieldTypes.Add(5, "32-bit (fixed32, sfixed32, float)");
        }

        public void Decode(byte[] data, int root = 0)
        {
            byte fieldType = byte.MaxValue;
            byte fieldId = byte.MaxValue;

            List<byte> fieldData = new List<byte>();
            bool isFirstByte = true;
            bool isLastByte = false;

            foreach (byte b in data)
            {
                if (!isFirstByte)
                {
                    fieldData.Add(b);
                }

                if (fieldType != byte.MaxValue)
                {
                    switch (fieldType)
                    {
                        case 0:
                            isLastByte = b >> 7 == 0;
                            break;
                        case 1:
                            {
                                byte len = (byte)fieldData.Count;
                                isLastByte = len == 8;
                            }
                            break;
                        case 2:
                            {
                                byte len = fieldData.FirstOrDefault();
                                isLastByte = len == 0 || len == fieldData.Count - 1;
                            }
                            break;
                        case 5:
                            {
                                byte len = (byte)fieldData.Count;
                                isLastByte = len == 4;
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }

                if (isFirstByte)
                {
                    ParseFieldTypeAndId(b, out fieldType, out fieldId);
                    isFirstByte = false;
                }
                else if (isLastByte)
                {
                    DecodeField(fieldType, fieldId, fieldData.ToArray(), root);
                    fieldData.Clear();
                    fieldType = byte.MaxValue;
                    isFirstByte = true;
                }

            }
        }

        public void DecodeField(byte fieldType, byte fieldId, byte[] fieldData, int root)
        {
            fieldTypes.TryGetValue(fieldType, out string fieldTypeStr);
            Console.WriteLine();
            Console.WriteLine($"{string.Empty.PadRight(root)}fieldId={fieldId}");
            Console.WriteLine($"{string.Empty.PadRight(root)}fieldType={fieldTypeStr}");
            switch (fieldType)
            {
                case 0:
                    ParseVarint(fieldData, out long longVal);
                    Console.WriteLine($"{string.Empty.PadRight(root)}val=({longVal});");
                    break;
                case 1:
                    Parse64Bit(fieldData, out double doubleVal);
                    Console.WriteLine($"{string.Empty.PadRight(root)}val=({doubleVal});");
                    break;
                case 2:
                    ParseLengthDelimited(fieldData, out List<byte[]> valuesBytes);
                    foreach (byte[] value in valuesBytes)
                    {
                        string str = Encoding.UTF8.GetString(value);
                        Console.WriteLine($"{string.Empty.PadRight(root)}val=({Encoding.UTF8.GetString(value)});");
                        try
                        {
                            Decode(value, root + 4);
                        }
                        catch { }
                    }
                    break;
                case 5:
                    Parse32Bit(fieldData, out float floatVal);
                    Console.WriteLine($"{string.Empty.PadRight(root)}val=({floatVal});");
                    break;
            }
        }

        private void ParseFieldTypeAndId(byte firstByte, out byte fieldType, out byte fieldId)
        {
            fieldType = (byte)(firstByte & 0x7);
            fieldId = (byte)(firstByte >> 3);
        }

        private void ParseVarint(byte[] fieldData, out long value)
        {
            byte[] valueBytes = new byte[fieldData.Length];
            for (int i = 0; i < fieldData.Length; i++)
            {
                byte msb = (byte)(fieldData[i] >> 7);
                valueBytes[i] = (byte)(fieldData[i] & 0x7F);
                if (msb == 0)
                {
                    break;
                }
            }

            Array.Reverse(valueBytes, 0, valueBytes.Length);

            value = 0;
            foreach (byte b in valueBytes)
            {
                value = (value << 7) + (b & 0xFF);
            }
        }

        private void Parse64Bit(byte[] fieldData, out double value)
        {
            value = BitConverter.ToDouble(fieldData, 0);
        }

        private void Parse32Bit(byte[] fieldData, out float value)
        {
            value = BitConverter.ToSingle(fieldData, 0);
        }

        private void ParseLengthDelimited(byte[] fieldData, out List<byte[]> valuesBytes)
        {
            byte[] fieldDataCopy = new byte[fieldData.Length];
            Array.Copy(fieldData, 0, fieldDataCopy, 0, fieldData.Length);

            valuesBytes = new List<byte[]>();

            int startIndex = 0;
            int endIndex = fieldDataCopy.Length;
            while (startIndex < endIndex)
            {
                byte valueLen = fieldDataCopy[startIndex]; //10010 
                byte[] fieldBytes = new byte[valueLen];
                Array.Copy(fieldData, startIndex + 1, fieldBytes, 0, valueLen);
                valuesBytes.Add(fieldBytes);
                startIndex += valueLen + 2;
            }
        }
    }
}
