using DataEditorUE4.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace DataEditorUE4.Utilities
{
    public class DataTableParser
    {
        public static UEDataTable CreateDataTable(string uassetPath, string uexpPath)
        {
            Dictionary<int, string> uassetStrings = CommonUtilities.ParseUAssetFile(uassetPath);
            byte[] allBytes = File.ReadAllBytes(uexpPath);
            Dictionary<string, UEDataTableObject> rows = new Dictionary<string, UEDataTableObject>();
            byte[] tableHeaderBytes;
            byte[] tableFooterBytes;
            if (CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, 0, uassetStrings) != "None")
            {
                tableHeaderBytes = CommonUtilities.GetSubArray(allBytes, 0, Constants.UexpEntryCountOffset);
                int numOfEntries = BitConverter.ToInt32(allBytes, Constants.UexpEntryCountOffset);
                int currentOffset = Constants.UexpListStartOffset;
                for (int i = 0; i < numOfEntries; i++)
                {
                    string rowKey = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
                    currentOffset += 0x8;
                    rows.Add(rowKey, ParseSingleDataObject(uassetStrings, allBytes, ref currentOffset));
                }
                tableFooterBytes = CommonUtilities.GetSubArray(allBytes, currentOffset, 4);
            }
            else
            {
                tableHeaderBytes = CommonUtilities.GetBytesFromStringWithPossibleSuffix("None", ref uassetStrings, uassetPath, uassetPath);
                tableFooterBytes = CommonUtilities.GetSubArray(allBytes, 8, allBytes.Length - 8);
            }

            string baseTableName = uassetPath.Split(@"\").Last().Replace(".uasset", "");
            var table = new UEDataTable(rows, baseTableName, tableHeaderBytes, tableFooterBytes);
            table.SourceUassetPath = uassetPath;
            table.SourceUexpPath = uexpPath;

            return table;
        }

        public static UEDataTableObject ParseSingleDataObject(Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset)
        {
            bool hasReachedNone = false;
            var cells = new List<UEDataTableCell>();
            while (!hasReachedNone)
            {
                cells.Add(ParseSingleDataCell(uassetStrings, allBytes, ref currentOffset));
                hasReachedNone = CheckForNone(uassetStrings, allBytes, currentOffset);
                if (hasReachedNone)
                {
                    currentOffset += 0x08;
                }
            }

            return new UEDataTableObject(cells.ToArray(), UE4ObjectType.Object);
        }

        public static UEDataTableCell ParseSingleDataCell(Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset)
        {
            int cellStartOffset = currentOffset;
            string columnName = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x8;
            string propertyType = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x8;
            var cell = GetCellFromPropertyType(columnName, propertyType, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
            return cell;
        }

        public static bool CheckForNone(Dictionary<int, string> uassetStrings, byte[] allBytes, int currentOffset, string noneIndicator = "None")
        {
            string value = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            return value == noneIndicator;
        }

        public static UEDataTableCell GetCellFromPropertyType(string columnName, string propertyType, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableCell cell;
            switch (propertyType)
            {
                case Constants.ArrayPropertyString:
                    cell = CreateArrayCell(columnName, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.BoolPropertyString:
                    cell = CreateBoolCell(columnName, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.BytePropertyString:
                case Constants.EnumPropertyString:
                    cell = CreateByteEnumCell(columnName, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.FloatPropertyString:
                    cell = CreateFloatCell(columnName, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.IntPropertyString:
                    cell = CreateIntCell(columnName, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.NamePropertyString:
                    cell = CreateNameCell(columnName, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.SoftObjectPropertyString:
                    cell = CreateSoftObjectCell(columnName, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.StructPropertyString:
                    cell = CreateStructCell(columnName, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.TextPropertyString:
                    cell = CreateTextCell(columnName, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.StrPropertyString:
                    cell = CreateStrCell(columnName, uassetStrings, allBytes, ref currentOffset, cellStartOffset);
                    break;
                case Constants.ObjectPropertyString:
                    cell = CreateObjectCell(columnName, allBytes, ref currentOffset, cellStartOffset);
                    break;
                default:
                    throw new ArgumentException($"The property type {propertyType} is not currently supported.");
            }
            return cell;
        }

        public static UEDataTableCell CreateArrayCell(string columnName, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.ArrayProperty);
            currentOffset += 0x8;
            string arrayType = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x9;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            int numOfArrayEntries = BitConverter.ToInt32(allBytes, currentOffset);
            currentOffset += 0x4;
            UEDataTableCell cell;
            if(arrayType == Constants.StructPropertyString)
            {
                cell = CreateStructArrayCell(column, uassetStrings, allBytes, ref currentOffset, numOfArrayEntries, headerBytes);
            }
            else
            {
                var list = new List<UEDataTableCell>();
                for (int i = 0; i < numOfArrayEntries; i++)
                {
                    switch (arrayType)
                    {
                        case Constants.FloatPropertyString:
                            var floatValue = BitConverter.ToSingle(allBytes, currentOffset);
                            var floatCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.FloatProperty), floatValue);
                            list.Add(floatCell);
                            currentOffset += 0x04;
                            break;
                        case Constants.IntPropertyString:
                            int intValue = BitConverter.ToInt32(allBytes, currentOffset);
                            var intCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.IntProperty), intValue);
                            list.Add(intCell);
                            currentOffset += 0x04;
                            break;
                        case Constants.ObjectPropertyString:
                            int objectValue = BitConverter.ToInt32(allBytes, currentOffset);
                            var objectCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.ObjectProperty), objectValue);
                            list.Add(objectCell);
                            currentOffset += 0x04;
                            break;
                        case Constants.BoolPropertyString:
                            bool boolValue = BitConverter.ToBoolean(allBytes, currentOffset);
                            var boolCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.BoolProperty), boolValue);
                            list.Add(boolCell);
                            currentOffset += 0x01;
                            break;
                        case Constants.BytePropertyString:
                            string byteValue = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
                            var byteCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.ByteProperty), byteValue);
                            list.Add(byteCell);
                            currentOffset += 0x08;
                            break;
                        case Constants.NamePropertyString:
                            string nameValue = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
                            var nameCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.NameProperty), nameValue);
                            list.Add(nameCell);
                            currentOffset += 0x08;
                            break;
                        case Constants.SoftObjectPropertyString:
                            string softObjectValue = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
                            currentOffset += 0x08;
                            if(BitConverter.ToInt32(allBytes, currentOffset) == 0)
                            {
                                currentOffset += 0x04;
                            }
                            var softObjectCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.SoftObjectProperty), softObjectValue);
                            list.Add(softObjectCell);
                            break;
                        case Constants.StrPropertyString:
                            int stringLength = BitConverter.ToInt32(allBytes, currentOffset);
                            string strValue = "";
                            bool useUnicode = false;
                            currentOffset += 0x04;
                            if (stringLength < 0)
                            {
                                useUnicode = true;
                                int trueValueSize = stringLength * 2 * -1;
                                strValue = Encoding.Unicode.GetString(allBytes, currentOffset, trueValueSize);
                                currentOffset += trueValueSize;
                            }
                            else if(stringLength != 0)
                            {
                                strValue = Encoding.UTF8.GetString(allBytes, currentOffset, stringLength);
                                currentOffset += stringLength;
                            }
                            var strCell = new UEDataTableCell(new UEDataTableColumn(columnName, UE4PropertyType.StrProperty), strValue, null, useUnicode);
                            list.Add(strCell);
                            break;
                        default:
                            throw new ArgumentException($"Processing arrays of type {arrayType} not supported.");
                    }
                }
                cell = new UEDataTableCell(column, list, headerBytes);
            }
            return cell;
        }

        public static UEDataTableCell CreateStructArrayCell(UEDataTableColumn column, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int numOfEntries, byte[] mainArrayHeaderBytes)
        {
            int cellStartOffset = currentOffset;
            currentOffset += 0x18;
            string name = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x19;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);
            List<UEDataTableCell> arrayList = new List<UEDataTableCell>();
            for(int i = 0; i < numOfEntries; i++)
            {
                var structObject = GetStructObjectFromName(name, uassetStrings, allBytes, ref currentOffset);
                var internalObjectCell = new UEDataTableCell(new UEDataTableColumn(name, UE4PropertyType.StructProperty), structObject, headerBytes);
                arrayList.Add(internalObjectCell);
            }
            var cell = new UEDataTableCell(column, arrayList, mainArrayHeaderBytes);
            return cell;
        }

        public static UEDataTableCell CreateStructCell(string columnName, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.StructProperty);
            currentOffset += 0x8;
            string name = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x19;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            UEDataTableObject value = GetStructObjectFromName(name, uassetStrings, allBytes, ref currentOffset);
            var cell = new UEDataTableCell(column, value, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateObjectCell(string columnName, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.ObjectProperty);
            currentOffset += 0x9;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            int importValue = BitConverter.ToInt32(allBytes, currentOffset);
            currentOffset += 0x04;

            var cell = new UEDataTableCell(column, importValue, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateByteEnumCell(string columnName, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column;
            currentOffset += 0x8;
            string enumName = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x09;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            UEDataTableCell cell;
            if(enumName == "None")
            {
                column = new UEDataTableColumn(columnName, UE4PropertyType.ByteProperty);
                byte value = allBytes[currentOffset];
                currentOffset += 0x01;
                cell = new UEDataTableCell(column, value, headerBytes);
            }
            else
            {
                column = new UEDataTableColumn(columnName, UE4PropertyType.EnumProperty);
                string value = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
                currentOffset += 0x8;
                cell = new UEDataTableCell(column, value, headerBytes);
            }
            return cell;
        }

        public static UEDataTableCell CreateBoolCell(string columnName, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.BoolProperty);
            currentOffset += 0x8;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            bool value = BitConverter.ToBoolean(allBytes, currentOffset);
            currentOffset += 0x2;
            var cell = new UEDataTableCell(column, value, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateIntCell(string columnName, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.IntProperty);
            currentOffset += 0x9;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            int value = BitConverter.ToInt32(allBytes, currentOffset);
            currentOffset += 0x4;
            var cell = new UEDataTableCell(column, value, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateFloatCell(string columnName, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.FloatProperty);
            currentOffset += 0x9;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            float value = BitConverter.ToSingle(allBytes, currentOffset);
            currentOffset += 0x4;
            var cell = new UEDataTableCell(column, value, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateNameCell(string columnName, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.NameProperty);
            currentOffset += 0x9;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            string value = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x8;
            var cell = new UEDataTableCell(column, value, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateTextCell(string columnName, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            bool textIsUnicode = false;
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.TextProperty);
            int textPropertyByteLength = BitConverter.ToInt32(allBytes, currentOffset);
            string key = "";
            string value = "";
            byte[] headerBytes;
            if (textPropertyByteLength > 5)
            {
                currentOffset += 0x12;
                int keySize = BitConverter.ToInt32(allBytes, currentOffset);
                currentOffset += 0x04;
                key = Encoding.UTF8.GetString(allBytes, currentOffset, keySize);
                currentOffset += keySize;

                headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

                int valueSize = BitConverter.ToInt32(allBytes, currentOffset);
                currentOffset += 0x04;
                if (valueSize < 0)
                {
                    textIsUnicode = true;
                    int trueValueSize = valueSize * 2 * -1;
                    value = Encoding.Unicode.GetString(allBytes, currentOffset, trueValueSize);
                    currentOffset += trueValueSize;
                }
                else
                {
                    value = Encoding.UTF8.GetString(allBytes, currentOffset, valueSize);
                    currentOffset += valueSize;
                }
            }
            else
            {
                currentOffset += 0x09 + textPropertyByteLength;
                headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);
            }
            //var cell = new UEDataTableCell(column, new { Key = key, Value = value});
            var cell = new UEDataTableCell(column, value, headerBytes, textIsUnicode);
            return cell;
        }

        public static UEDataTableCell CreateSoftObjectCell(string columnName, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.SoftObjectProperty);
            currentOffset += 0x9;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            string value = CommonUtilities.ParseUAssetStringWithPossibleSuffix(allBytes, currentOffset, uassetStrings);
            currentOffset += 0x8;
            if(BitConverter.ToInt32(allBytes, currentOffset) != 0)
            {
                throw new ArgumentException("Ran into unexpected bytes!");
            }
            else
            {
                currentOffset += 0x4;
            }
            var cell = new UEDataTableCell(column, value, headerBytes);
            return cell;
        }

        public static UEDataTableCell CreateStrCell(string columnName, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset, int cellStartOffset)
        {
            bool textIsUnicode = false;
            UEDataTableColumn column = new UEDataTableColumn(columnName, UE4PropertyType.StrProperty);
            currentOffset += 0x09;

            byte[] headerBytes = CommonUtilities.GetSubArray(allBytes, cellStartOffset, currentOffset - cellStartOffset);

            int stringLength = BitConverter.ToInt32(allBytes, currentOffset);
            currentOffset += 0x04;
            string value;
            if (stringLength < 0)
            {
                textIsUnicode = true;
                int trueValueSize = stringLength * 2 * -1;
                value = Encoding.Unicode.GetString(allBytes, currentOffset, trueValueSize);
                currentOffset += trueValueSize;
            }
            else if(stringLength == 0)
            {
                value = "";
            }
            else
            {
                value = Encoding.UTF8.GetString(allBytes, currentOffset, stringLength);
                currentOffset += stringLength;
            }
            var cell = new UEDataTableCell(column, value, headerBytes, textIsUnicode);
            return cell;
        }

        public static UEDataTableObject GetStructObjectFromName(string name, Dictionary<int, string> uassetStrings, byte[] allBytes, ref int currentOffset)
        {
            UEDataTableObject value;
            switch (name)
            {
                case "Vector":
                case "Rotator":
                    float x = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float y = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float z = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    value = name == "Vector" ? CreateVectorObject(x, y, z) : CreateRotatorObject(x, y, z);
                    break;
                case "Vector2D":
                    float x2D = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float y2D = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    value = CreateVector2DObject(x2D, y2D);
                    break;
                case "Vector4":
                    float x4 = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float y4 = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float z4 = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float w4 = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    value = CreateVector4Object(x4, y4, z4, w4);
                    break;
                case "LinearColor":
                    float a = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float r = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float g = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    float b = BitConverter.ToSingle(allBytes, currentOffset);
                    currentOffset += 0x04;
                    value = CreateLinearColorObject(a, r, g, b);
                    break;
                default:
                    value = ParseSingleDataObject(uassetStrings, allBytes, ref currentOffset);
                    break;
            }
            return value;
        }

        public static UEDataTableObject CreateLinearColorObject(float a, float r, float g, float b)
        {
            var columnA = new UEDataTableColumn("a", UE4PropertyType.FloatProperty);
            var columnR = new UEDataTableColumn("r", UE4PropertyType.FloatProperty);
            var columnG = new UEDataTableColumn("g", UE4PropertyType.FloatProperty);
            var columnB = new UEDataTableColumn("b", UE4PropertyType.FloatProperty);

            var cellA = new UEDataTableCell(columnA, a);
            var cellR = new UEDataTableCell(columnR, r);
            var cellG = new UEDataTableCell(columnG, g);
            var cellB = new UEDataTableCell(columnB, b);

            return new UEDataTableObject(new UEDataTableCell[] { cellA, cellR, cellG, cellB }, UE4ObjectType.LinearColor);
        }

        public static UEDataTableObject CreateVector4Object(float x, float y, float z, float w)
        {
            var columnX = new UEDataTableColumn("x", UE4PropertyType.FloatProperty);
            var columnY = new UEDataTableColumn("y", UE4PropertyType.FloatProperty);
            var columnZ = new UEDataTableColumn("z", UE4PropertyType.FloatProperty);
            var columnW = new UEDataTableColumn("w", UE4PropertyType.FloatProperty);

            var cellX = new UEDataTableCell(columnX, x);
            var cellY = new UEDataTableCell(columnY, y);
            var cellZ = new UEDataTableCell(columnZ, z);
            var cellW = new UEDataTableCell(columnW, w);

            return new UEDataTableObject(new UEDataTableCell[] { cellX, cellY, cellZ, cellW }, UE4ObjectType.Vector4);
        }

        public static UEDataTableObject CreateVectorObject(float x, float y, float z)
        {
            var columnX = new UEDataTableColumn("x", UE4PropertyType.FloatProperty);
            var columnY = new UEDataTableColumn("y", UE4PropertyType.FloatProperty);
            var columnZ = new UEDataTableColumn("z", UE4PropertyType.FloatProperty);

            var cellX = new UEDataTableCell(columnX, x);
            var cellY = new UEDataTableCell(columnY, y);
            var cellZ = new UEDataTableCell(columnZ, z);

            return new UEDataTableObject(new UEDataTableCell[] { cellX, cellY, cellZ}, UE4ObjectType.Vector);
        }

        public static UEDataTableObject CreateRotatorObject(float x, float y, float z)
        {
            var columnX = new UEDataTableColumn("x", UE4PropertyType.FloatProperty);
            var columnY = new UEDataTableColumn("y", UE4PropertyType.FloatProperty);
            var columnZ = new UEDataTableColumn("z", UE4PropertyType.FloatProperty);

            var cellX = new UEDataTableCell(columnX, x);
            var cellY = new UEDataTableCell(columnY, y);
            var cellZ = new UEDataTableCell(columnZ, z);

            return new UEDataTableObject(new UEDataTableCell[] { cellX, cellY, cellZ }, UE4ObjectType.Rotator);
        }

        public static UEDataTableObject CreateVector2DObject(float x, float y)
        {
            var columnX = new UEDataTableColumn("x", UE4PropertyType.FloatProperty);
            var columnY = new UEDataTableColumn("y", UE4PropertyType.FloatProperty);

            var cellX = new UEDataTableCell(columnX, x);
            var cellY = new UEDataTableCell(columnY, y);

            return new UEDataTableObject(new UEDataTableCell[] { cellX, cellY }, UE4ObjectType.Vector2D);
        }
    }
}
