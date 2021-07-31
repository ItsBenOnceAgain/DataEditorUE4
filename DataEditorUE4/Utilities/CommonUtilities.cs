﻿using DataEditorUE4.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataEditorUE4.Utilities
{
    public static class CommonUtilities
    {
        public static Dictionary<int, string> ParseUAssetFile(string path)
        {
            byte[] allBytes = File.ReadAllBytes(path);
            long numberOfEntries = BitConverter.ToInt64(allBytes, Constants.UassetEntryCountOffset);
            int currentOffset = Constants.UassetListStartOffset;
            Dictionary<int, string> entries = new Dictionary<int, string>();
            for (int i = 0; i < numberOfEntries; i++)
            {
                int lengthOfString = BitConverter.ToInt32(allBytes, currentOffset);
                currentOffset += 4;
                int trueLength = lengthOfString;
                bool useUnicode = false;
                if(trueLength < 0)
                {
                    trueLength *= -2;
                    useUnicode = true;
                }

                byte[] entryArray = GetSubArray(allBytes, currentOffset, trueLength);
                string entry = useUnicode ? Encoding.Unicode.GetString(entryArray) : Encoding.ASCII.GetString(entryArray);
                entries.Add(i, entry.TrimEnd('\0'));

                currentOffset += trueLength + 4;
            }

            return entries;
        }

        public static byte[] GetSubArray(byte[] mainArray, int startIndex, int length)
        {
            byte[] subArray = new byte[length];
            Array.Copy(mainArray, startIndex, subArray, 0, length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(subArray);
            }
            return subArray;
        }

        public static string ParseUAssetStringWithPossibleSuffix(byte[] mainArray, int currentOffset, Dictionary<int, string> uassetStrings)
        {
            string stringValue = uassetStrings[BitConverter.ToInt32(mainArray, currentOffset)];
            currentOffset += 4;
            if (BitConverter.ToInt32(mainArray, currentOffset) != 0)
            {
                if(uassetStrings.ContainsValue($"{stringValue}_{(BitConverter.ToInt32(mainArray, currentOffset) - 1):D3}"))
                {
                    stringValue += $"_{(BitConverter.ToInt32(mainArray, currentOffset) - 1):D1}";
                }
                else
                {
                    stringValue += $"_{(BitConverter.ToInt32(mainArray, currentOffset) - 1):D3}";
                }
            }
            return stringValue;
        }

        public static void AddStringToUasset(string uassetPathRead, string uassetPathWrite, string stringToAdd)
        {
            byte[] allBytes = File.ReadAllBytes(uassetPathRead);
            int numberOfEntries = BitConverter.ToInt32(allBytes, Constants.UassetEntryCountOffset);
            int currentOffset = 0xc1;
            for(int i = 0; i < numberOfEntries; i++)
            {
                int currentStringLength = BitConverter.ToInt32(allBytes, currentOffset);
                if(currentStringLength < 0)
                {
                    currentStringLength *= -2;
                }

                currentOffset += currentStringLength + 8;
            }

            byte[] stringPrefix = BitConverter.GetBytes(stringToAdd.Length + 1);
            byte[] stringBytes = new byte[stringToAdd.Length + 1];

            UpdateBytesAtOffset(Encoding.ASCII.GetBytes(stringToAdd), stringBytes, 0);

            byte[] hashBytes = GetByteHash(stringToAdd);

            List<byte> finalAddBytesList = new List<byte>();
            finalAddBytesList.AddRange(stringPrefix);
            finalAddBytesList.AddRange(stringBytes);
            finalAddBytesList.AddRange(hashBytes);
            byte[] finalAddBytes = finalAddBytesList.ToArray();

            List<byte> finalBytesList = allBytes.Take(currentOffset).ToList();
            finalBytesList.AddRange(finalAddBytes);
            finalBytesList.AddRange(allBytes.Skip(currentOffset).Take(allBytes.Length - currentOffset));
            byte[] finalBytes = finalBytesList.ToArray();

            //Patching metadata
            byte[] originalBytes = finalBytes.Skip(0x18).Take(4).ToArray();
            byte[] editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0x18);

            originalBytes = finalBytes.Skip(0x29).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + 1);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0x29);

            originalBytes = finalBytes.Skip(0x3d).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0x3d);

            originalBytes = finalBytes.Skip(0x45).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0x45);

            originalBytes = finalBytes.Skip(0x49).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0x49);

            originalBytes = finalBytes.Skip(0x75).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + 1);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0x75);

            originalBytes = finalBytes.Skip(0xa5).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0xa5);

            originalBytes = finalBytes.Skip(0xa9).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0xa9);

            originalBytes = finalBytes.Skip(0xbd).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, 0xbd);

            originalBytes = finalBytes.Skip(finalBytes.Length - 0x58).Take(4).ToArray();
            editedBytes = BitConverter.GetBytes(BitConverter.ToInt32(originalBytes, 0) + finalAddBytes.Length);
            UpdateBytesAtOffset(editedBytes, finalBytes, finalBytes.Length - 0x58);

            File.WriteAllBytes(uassetPathWrite, finalBytes);
        }

        private static void UpdateBytesAtOffset(byte[] updateBytes, byte[] allBytes, int currentOffset)
        {
            for (int i = 0; i < updateBytes.Length; i++)
            {
                allBytes[currentOffset + i] = updateBytes[i];
            }
        }

        private static byte[] GetByteHash(string name)
        {
            uint[] tableCCITT = {
                0x00000000, 0x04C11DB7, 0x09823B6E, 0x0D4326D9, 0x130476DC, 0x17C56B6B, 0x1A864DB2, 0x1E475005, 0x2608EDB8, 0x22C9F00F, 0x2F8AD6D6, 0x2B4BCB61, 0x350C9B64, 0x31CD86D3, 0x3C8EA00A, 0x384FBDBD,
                0x4C11DB70, 0x48D0C6C7, 0x4593E01E, 0x4152FDA9, 0x5F15ADAC, 0x5BD4B01B, 0x569796C2, 0x52568B75, 0x6A1936C8, 0x6ED82B7F, 0x639B0DA6, 0x675A1011, 0x791D4014, 0x7DDC5DA3, 0x709F7B7A, 0x745E66CD,
                0x9823B6E0, 0x9CE2AB57, 0x91A18D8E, 0x95609039, 0x8B27C03C, 0x8FE6DD8B, 0x82A5FB52, 0x8664E6E5, 0xBE2B5B58, 0xBAEA46EF, 0xB7A96036, 0xB3687D81, 0xAD2F2D84, 0xA9EE3033, 0xA4AD16EA, 0xA06C0B5D,
                0xD4326D90, 0xD0F37027, 0xDDB056FE, 0xD9714B49, 0xC7361B4C, 0xC3F706FB, 0xCEB42022, 0xCA753D95, 0xF23A8028, 0xF6FB9D9F, 0xFBB8BB46, 0xFF79A6F1, 0xE13EF6F4, 0xE5FFEB43, 0xE8BCCD9A, 0xEC7DD02D,
                0x34867077, 0x30476DC0, 0x3D044B19, 0x39C556AE, 0x278206AB, 0x23431B1C, 0x2E003DC5, 0x2AC12072, 0x128E9DCF, 0x164F8078, 0x1B0CA6A1, 0x1FCDBB16, 0x018AEB13, 0x054BF6A4, 0x0808D07D, 0x0CC9CDCA,
                0x7897AB07, 0x7C56B6B0, 0x71159069, 0x75D48DDE, 0x6B93DDDB, 0x6F52C06C, 0x6211E6B5, 0x66D0FB02, 0x5E9F46BF, 0x5A5E5B08, 0x571D7DD1, 0x53DC6066, 0x4D9B3063, 0x495A2DD4, 0x44190B0D, 0x40D816BA,
                0xACA5C697, 0xA864DB20, 0xA527FDF9, 0xA1E6E04E, 0xBFA1B04B, 0xBB60ADFC, 0xB6238B25, 0xB2E29692, 0x8AAD2B2F, 0x8E6C3698, 0x832F1041, 0x87EE0DF6, 0x99A95DF3, 0x9D684044, 0x902B669D, 0x94EA7B2A,
                0xE0B41DE7, 0xE4750050, 0xE9362689, 0xEDF73B3E, 0xF3B06B3B, 0xF771768C, 0xFA325055, 0xFEF34DE2, 0xC6BCF05F, 0xC27DEDE8, 0xCF3ECB31, 0xCBFFD686, 0xD5B88683, 0xD1799B34, 0xDC3ABDED, 0xD8FBA05A,
                0x690CE0EE, 0x6DCDFD59, 0x608EDB80, 0x644FC637, 0x7A089632, 0x7EC98B85, 0x738AAD5C, 0x774BB0EB, 0x4F040D56, 0x4BC510E1, 0x46863638, 0x42472B8F, 0x5C007B8A, 0x58C1663D, 0x558240E4, 0x51435D53,
                0x251D3B9E, 0x21DC2629, 0x2C9F00F0, 0x285E1D47, 0x36194D42, 0x32D850F5, 0x3F9B762C, 0x3B5A6B9B, 0x0315D626, 0x07D4CB91, 0x0A97ED48, 0x0E56F0FF, 0x1011A0FA, 0x14D0BD4D, 0x19939B94, 0x1D528623,
                0xF12F560E, 0xF5EE4BB9, 0xF8AD6D60, 0xFC6C70D7, 0xE22B20D2, 0xE6EA3D65, 0xEBA91BBC, 0xEF68060B, 0xD727BBB6, 0xD3E6A601, 0xDEA580D8, 0xDA649D6F, 0xC423CD6A, 0xC0E2D0DD, 0xCDA1F604, 0xC960EBB3,
                0xBD3E8D7E, 0xB9FF90C9, 0xB4BCB610, 0xB07DABA7, 0xAE3AFBA2, 0xAAFBE615, 0xA7B8C0CC, 0xA379DD7B, 0x9B3660C6, 0x9FF77D71, 0x92B45BA8, 0x9675461F, 0x8832161A, 0x8CF30BAD, 0x81B02D74, 0x857130C3,
                0x5D8A9099, 0x594B8D2E, 0x5408ABF7, 0x50C9B640, 0x4E8EE645, 0x4A4FFBF2, 0x470CDD2B, 0x43CDC09C, 0x7B827D21, 0x7F436096, 0x7200464F, 0x76C15BF8, 0x68860BFD, 0x6C47164A, 0x61043093, 0x65C52D24,
                0x119B4BE9, 0x155A565E, 0x18197087, 0x1CD86D30, 0x029F3D35, 0x065E2082, 0x0B1D065B, 0x0FDC1BEC, 0x3793A651, 0x3352BBE6, 0x3E119D3F, 0x3AD08088, 0x2497D08D, 0x2056CD3A, 0x2D15EBE3, 0x29D4F654,
                0xC5A92679, 0xC1683BCE, 0xCC2B1D17, 0xC8EA00A0, 0xD6AD50A5, 0xD26C4D12, 0xDF2F6BCB, 0xDBEE767C, 0xE3A1CBC1, 0xE760D676, 0xEA23F0AF, 0xEEE2ED18, 0xF0A5BD1D, 0xF464A0AA, 0xF9278673, 0xFDE69BC4,
                0x89B8FD09, 0x8D79E0BE, 0x803AC667, 0x84FBDBD0, 0x9ABC8BD5, 0x9E7D9662, 0x933EB0BB, 0x97FFAD0C, 0xAFB010B1, 0xAB710D06, 0xA6322BDF, 0xA2F33668, 0xBCB4666D, 0xB8757BDA, 0xB5365D03, 0xB1F740B4
            };

            uint[] tableIEEE802 = {
                0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3, 0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
                0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7, 0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
                0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B, 0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
                0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F, 0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
                0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433, 0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
                0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457, 0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
                0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB, 0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
                0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F, 0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
                0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683, 0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
                0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7, 0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
                0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B, 0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
                0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F, 0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
                0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713, 0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
                0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777, 0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
                0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB, 0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
                0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF, 0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
            };

            long hash = 0;

            foreach(char c in name)
            {
                int value = (int)c.ToString().ToUpper()[0];

                hash = hash >> 8 & 0x00FFFFFF ^ tableCCITT[(hash ^ value) & 0x000000FF];
            }

            var value1 = hash & 0xFFFF;
            hash = 0xFFFFFFFF;

            char[] characterArray = Encoding.UTF8.GetChars(Encoding.ASCII.GetBytes(name));
            foreach(char c in characterArray)
            {
                hash = hash >> 8 ^ tableIEEE802[c ^ hash & 0xFF];
                for(int i = 0; i < 3; i++)
                {
                    hash = hash >> 8 ^ tableIEEE802[hash & 0xFF];
                }
            }

            var value2 = ~hash & 0xFFFF;
            byte[] value1Bytes = BitConverter.GetBytes(value1);
            byte[] value2Bytes = BitConverter.GetBytes(value2);
            byte[] finalArray = new byte[4];
            for(int i = 0; i < 2; i++)
            {
                finalArray[i] = value1Bytes[i];
            }
            for(int i = 2; i < 4; i++)
            {
                finalArray[i] = value2Bytes[i - 2];
            }
            return finalArray;
        }

        public static byte[] GetBytesFromStringWithPossibleSuffix(string stringWithPossibleSuffix, ref Dictionary<int, string> uassetStrings, string uassetPath, string modUassetPath)
        {
            string[] data = stringWithPossibleSuffix.Split('_');
            List<string> prefixDataList = data.ToList();
            prefixDataList.RemoveAt(data.Length - 1);
            string[] prefixData = prefixDataList.ToArray();
            string prefix = string.Join("_", prefixData);

            byte[] byteData = new byte[8];
            string suffix = data[data.Length - 1];
            bool suffixIsNumeric = int.TryParse(suffix, out int res);

            bool needToAddString = (!uassetStrings.ContainsValue(stringWithPossibleSuffix) && !uassetStrings.ContainsValue(prefix)) || 
                (!uassetStrings.ContainsValue(stringWithPossibleSuffix) && uassetStrings.ContainsValue(prefix) && !suffixIsNumeric);

            if (needToAddString)
            {
                AddStringToUasset(uassetPath, modUassetPath, stringWithPossibleSuffix);
                uassetStrings = ParseUAssetFile(modUassetPath);
            }

            if (uassetStrings.ContainsValue(stringWithPossibleSuffix))
            {
                UpdateBytesAtOffset(BitConverter.GetBytes(uassetStrings.Single(x => x.Value == stringWithPossibleSuffix).Key), byteData, 0);
            }
            else
            {
                byte[] numericValue = BitConverter.GetBytes(int.Parse(data[data.Length - 1]) + 1);
                UpdateBytesAtOffset(BitConverter.GetBytes(uassetStrings.Single(x => x.Value == prefix).Key), byteData, 0);
                UpdateBytesAtOffset(numericValue, byteData, 4);
            }
            return byteData;
        }

        public static void UpdateUAssetToMatchFileSizeOfUexp(byte[] oldUexpBytes, byte[] newUexpBytes, string uassetPath)
        {
            byte[] countOfOldUexpBytes = BitConverter.GetBytes((long)(oldUexpBytes.Length - 4));
            byte[] numOfNewUexpBytes = BitConverter.GetBytes((long)(newUexpBytes.Length - 4));

            var data = File.ReadAllBytes(uassetPath);
            var replacementIndex = FindSubArrayInLargerArray(data, countOfOldUexpBytes);
            UpdateBytesAtOffset(numOfNewUexpBytes, data, replacementIndex);
            File.WriteAllBytes(uassetPath, data);
        }

        public static int FindSubArrayInLargerArray(byte[] haystack, byte[] needle)
        {
            // iterate backwards, stop if the rest of the array is shorter than needle (i >= needle.Length)
            for (var i = haystack.Length - 1; i >= needle.Length - 1; i--)
            {
                var found = true;
                // also iterate backwards through needle, stop if elements do not match (!found)
                for (var j = needle.Length - 1; j >= 0 && found; j--)
                {
                    // compare needle's element with corresponding element of haystack
                    found = haystack[i - (needle.Length - 1 - j)] == needle[j];
                }
                if (found)
                    // result was found, i is now the index of the last found element, so subtract needle's length - 1
                    return i - (needle.Length - 1);
            }
            // not found, return -1
            return -1;
        }
    }
}
