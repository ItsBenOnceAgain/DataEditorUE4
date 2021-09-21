using DataEditorUE4.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DataEditorUE4.Utilities
{
    public static class DataTableFileWriter
    {
        public const int MAX_ASCII_CODE = 127;
        public static void WriteTableToFile(UEDataTable table, string uassetOverride = null, string uexpOverride = null)
        {
            string uassetWritePath = uassetOverride ?? table.SourceUassetPath;
            string uexpWritePath = uexpOverride ?? table.SourceUexpPath;
            var oldUexpBytes = File.ReadAllBytes(table.SourceUexpPath);

            File.WriteAllBytes(uassetWritePath, File.ReadAllBytes(table.SourceUassetPath));
            List<byte> bytesToWrite = new List<byte>();
            bytesToWrite.AddRange(table.HeaderBytes);

            if(table.Rows.Count > 0)
            {
                byte[] entryCountBytes = BitConverter.GetBytes(table.Rows.Count);
                bytesToWrite.AddRange(entryCountBytes);
            }

            Dictionary<int, string> uassetStrings = CommonUtilities.ParseUAssetFile(table.SourceUassetPath);

            foreach(var row in table.Rows)
            {
                if (table.IsAsset)
                {
                    bytesToWrite.AddRange(GetSimpleBytesFromValue(row.Key.DataType, row.Key.KeyData, uassetStrings, uassetWritePath));
                }
                else
                {
                    bytesToWrite.AddRange(CommonUtilities.GetBytesFromStringWithPossibleSuffix((string)row.Key.KeyData, ref uassetStrings, uassetWritePath, uassetWritePath));
                }
                
                byte[] objectBytes = GetBytesFromObject(row.Value, uassetStrings, uassetWritePath);
                bytesToWrite.AddRange(objectBytes);
            }

            bytesToWrite.AddRange(table.FooterBytes);
            File.WriteAllBytes(uexpWritePath, bytesToWrite.ToArray());
            
            if(table.Rows.Count > 0)
            {
                CommonUtilities.UpdateUAssetToMatchFileSizeOfUexp(oldUexpBytes, bytesToWrite.ToArray(), uassetWritePath);
            }
        }

        public static byte[] GetBytesFromObject(UEDataTableObject dataObject, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            List<byte> bytes = new List<byte>();
            switch (dataObject.ObjectType)
            {
                case UE4ObjectType.Object:
                    bytes.AddRange(GetBytesFromObjectCells(dataObject, uassetStrings, uassetPath));
                    bytes.AddRange(CommonUtilities.GetBytesFromStringWithPossibleSuffix("None", ref uassetStrings, uassetPath, uassetPath));
                    break;
                case UE4ObjectType.LinearColor:
                case UE4ObjectType.Vector4:
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[0].Value));
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[1].Value));
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[2].Value));
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[3].Value));
                    break;
                case UE4ObjectType.Vector:
                case UE4ObjectType.Rotator:
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[0].Value));
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[1].Value));
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[2].Value));
                    break;
                case UE4ObjectType.Vector2D:
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[0].Value));
                    bytes.AddRange(BitConverter.GetBytes((float)dataObject.Cells[1].Value));
                    break;
            }
            return bytes.ToArray();
        }

        public static byte[] GetBytesFromObjectCells(UEDataTableObject dataObject, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            List<byte> bytes = new List<byte>();
            foreach(var cell in dataObject.Cells)
            {
                bytes.AddRange(cell.HeaderBytes);
                bytes.AddRange(GetInternalBytesFromCell(cell, uassetStrings, uassetPath));
            }
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromArrayCell(UEDataTableCell cell, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            List<byte> bytes = new List<byte>();
            var itemList = (List<UEDataTableCell>)cell.Value;
            bytes.AddRange(BitConverter.GetBytes(itemList.Count));
            if(cell.StructArrayExtraBytes != null)
            {
                bytes.AddRange(cell.StructArrayExtraBytes);
            }
            if(itemList.Count > 0)
            {
                for (int i = 0; i < itemList.Count; i++)
                {
                    var currentArrayCell = itemList[i];
                    switch (currentArrayCell.Column.ColumnType)
                    {
                        case UE4PropertyType.StructProperty:
                            bytes.AddRange(GetBytesFromObject(currentArrayCell.Value, uassetStrings, uassetPath));
                            break;
                        case UE4PropertyType.BoolProperty:
                            bytes.AddRange(GetInternalBytesFromBoolCell(currentArrayCell, false));
                            break;
                        case UE4PropertyType.FloatProperty:
                            bytes.AddRange(GetInternalBytesFromFloatCell(currentArrayCell));
                            break;
                        case UE4PropertyType.IntProperty:
                        case UE4PropertyType.ObjectProperty:
                            bytes.AddRange(GetInternalBytesFromIntCell(currentArrayCell));
                            break;
                        case UE4PropertyType.UInt32Property:
                            bytes.AddRange(GetInternalBytesFromUInt32Cell(currentArrayCell));
                            break;
                        case UE4PropertyType.ByteProperty:
                        case UE4PropertyType.NameProperty:
                            bytes.AddRange(GetInternalBytesFromEnumNameCell(currentArrayCell, uassetStrings, uassetPath));
                            break;
                        case UE4PropertyType.SoftObjectProperty:
                            bytes.AddRange(GetInternalBytesFromSoftObjectCell(currentArrayCell, uassetStrings, uassetPath));
                            break;
                        case UE4PropertyType.StrProperty:
                            bytes.AddRange(GetInternalBytesFromStringCell(currentArrayCell));
                            break;
                    }
                }
            }
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromBoolCell(UEDataTableCell cell, bool addExtra = true)
        {
            List<byte> bytes = new List<byte>();
            byte[] boolBytes = BitConverter.GetBytes((bool)cell.Value);
            bytes.AddRange(boolBytes);
            if (addExtra)
            {
                bytes.Add(0);
            }
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromByteCell(UEDataTableCell cell)
        {
            List<byte> bytes = new List<byte>();
            byte cellByte = byte.Parse(((object)cell.Value).ToString());
            bytes.Add(cellByte);
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromEnumNameCell(UEDataTableCell cell, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            List<byte> bytes = new List<byte>();
            byte[] uassetBytes = CommonUtilities.GetBytesFromStringWithPossibleSuffix(cell.Value, ref uassetStrings, uassetPath, uassetPath);
            bytes.AddRange(uassetBytes);
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromFloatCell(UEDataTableCell cell)
        {
            List<byte> bytes = new List<byte>();
            byte[] floatBytes = BitConverter.GetBytes(float.Parse(((object)cell.Value).ToString()));
            bytes.AddRange(floatBytes);
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromIntCell(UEDataTableCell cell)
        {
            List<byte> bytes = new List<byte>();
            byte[] intBytes = BitConverter.GetBytes(int.Parse(((object)cell.Value).ToString()));
            bytes.AddRange(intBytes);
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromUInt32Cell(UEDataTableCell cell)
        {
            List<byte> bytes = new List<byte>();
            byte[] uintBytes = BitConverter.GetBytes(uint.Parse(((object)cell.Value).ToString()));
            bytes.AddRange(uintBytes);
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromSoftObjectCell(UEDataTableCell cell, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            List<byte> bytes = new List<byte>();
            byte[] softObjectBytes = CommonUtilities.GetBytesFromStringWithPossibleSuffix(cell.Value, ref uassetStrings, uassetPath, uassetPath);
            bytes.AddRange(softObjectBytes);
            bytes.AddRange(new byte[] { 0, 0, 0, 0 });
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromStringCell(UEDataTableCell cell)
        {
            List<byte> bytes = new List<byte>();
            string cellString = (string)cell.Value;
            int stringLength = cellString.Length;
            cellString = cellString.Trim('\0') + "\0";
            cell.TextIsUnicode = cellString.Any(c => c > MAX_ASCII_CODE);
            if (cell.TextIsUnicode)
            {
                stringLength *= -1;
            }
            bytes.AddRange(BitConverter.GetBytes(stringLength));
            if (stringLength != 0)
            {
                bytes.AddRange(cell.TextIsUnicode ? Encoding.Unicode.GetBytes(cellString) : Encoding.UTF8.GetBytes(cellString));
            }
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromTextCell(UEDataTableCell cell)
        {
            List<byte> bytes = new List<byte>();
            string cellText = (string)cell.Value;
            if (cellText != "")
            {
                cellText = cellText.Trim('\0') + "\0";
                int textLength = cellText.Length;
                cell.TextIsUnicode = cellText.Any(c => c > MAX_ASCII_CODE);
                if (cell.TextIsUnicode)
                {
                    textLength *= -1;
                }
                List<byte> textBytes = new List<byte>();
                textBytes.AddRange(BitConverter.GetBytes(textLength));
                if (textLength != 0)
                {
                    textBytes.AddRange(cell.TextIsUnicode ? Encoding.Unicode.GetBytes(cellText) : Encoding.UTF8.GetBytes(cellText));
                }
                bytes.AddRange(textBytes);
            }
            return bytes.ToArray();
        }

        public static byte[] GetInternalBytesFromCell(UEDataTableCell cell, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            byte[] bytes = null;
            switch (cell.Column.ColumnType)
            {
                case UE4PropertyType.ArrayProperty:
                    bytes = GetInternalBytesFromArrayCell(cell, uassetStrings, uassetPath);
                    break;
                case UE4PropertyType.BoolProperty:
                    bytes = GetInternalBytesFromBoolCell(cell);
                    break;
                case UE4PropertyType.ByteProperty:
                    bytes = GetInternalBytesFromByteCell(cell);
                    break;
                case UE4PropertyType.EnumProperty:
                case UE4PropertyType.NameProperty:
                    bytes = GetInternalBytesFromEnumNameCell(cell, uassetStrings, uassetPath);
                    break;
                case UE4PropertyType.FloatProperty:
                    bytes = GetInternalBytesFromFloatCell(cell);
                    break;
                case UE4PropertyType.IntProperty:
                case UE4PropertyType.ObjectProperty:
                    bytes = GetInternalBytesFromIntCell(cell);
                    break;
                case UE4PropertyType.UInt32Property:
                    bytes = GetInternalBytesFromUInt32Cell(cell);
                    break;
                case UE4PropertyType.SoftObjectProperty:
                    bytes = GetInternalBytesFromSoftObjectCell(cell, uassetStrings, uassetPath);
                    break;
                case UE4PropertyType.StrProperty:
                    bytes = GetInternalBytesFromStringCell(cell);
                    break;
                case UE4PropertyType.StructProperty:
                    bytes = GetBytesFromObject(cell.Value, uassetStrings, uassetPath);
                    break;
                case UE4PropertyType.TextProperty:
                    bytes = GetInternalBytesFromTextCell(cell);
                    break;
            }
            return bytes;
        }

        public static byte[] GetSimpleBytesFromValue(UE4PropertyType propertyType, dynamic value, Dictionary<int, string> uassetStrings, string uassetPath)
        {
            byte[] bytes = null;
            switch (propertyType)
            {
                case UE4PropertyType.EnumProperty:
                case UE4PropertyType.NameProperty:
                    bytes = CommonUtilities.GetBytesFromStringWithPossibleSuffix(value, ref uassetStrings, uassetPath, uassetPath);
                    break;
                case UE4PropertyType.IntProperty:
                case UE4PropertyType.ObjectProperty:
                    bytes = BitConverter.GetBytes(int.Parse(((object)value).ToString()));
                    break;
                case UE4PropertyType.UInt32Property:
                    bytes = BitConverter.GetBytes(uint.Parse(((object)value).ToString()));
                    break;
                case UE4PropertyType.StrProperty:
                    bytes = GetSimpleBytesFromString(value);
                    break;
            }
            return bytes;
        }

        public static byte[] GetSimpleBytesFromString(string value)
        {
            List<byte> bytes = new List<byte>();
            string cellString = value;
            int stringLength = cellString.Length;
            cellString = cellString.Trim('\0') + "\0";
            bool textIsUnicode = cellString.Any(c => c > MAX_ASCII_CODE);
            if (textIsUnicode)
            {
                stringLength *= -1;
            }
            if (stringLength != 0)
            {
                bytes.AddRange(textIsUnicode ? Encoding.Unicode.GetBytes(cellString) : Encoding.UTF8.GetBytes(cellString));
            }
            return bytes.ToArray();
        }
    }
}
