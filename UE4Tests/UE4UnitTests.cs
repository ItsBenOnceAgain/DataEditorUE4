using DataEditorUE4.Utilities;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UE4Tests
{
    public class UE4UnitTests
    {
        [TestCaseSource(nameof(GetParseFileNames))]
        public void TestParse(Tuple<string, string> fileNames)
        {
            DataTableParser.CreateDataTable(fileNames.Item1, (fileNames.Item2));
        }

        [TestCaseSource(nameof(GetWriteFileNames))]
        public void TestWrite(Tuple<Tuple<string, string>, Tuple<string, string>> testData)
        {
            var table = DataTableParser.CreateDataTable(testData.Item1.Item1, testData.Item1.Item2);
            DataTableFileWriter.WriteTableToFile(table, testData.Item2.Item1, testData.Item2.Item2);

            byte[] originalBytes = File.ReadAllBytes(testData.Item1.Item2);
            byte[] savedBytes = File.ReadAllBytes(testData.Item2.Item2);

            CollectionAssert.AreEqual(originalBytes, savedBytes);
        }

        private static IEnumerable GetParseFileNames()
        {
            var fileNames = GetFileNames(@"..\..\..\..\TestFiles\OctopathTableParseTest\");
            foreach (var filePair in fileNames)
            {
                string baseFileName = filePair.Item1.Split(@"\").Last().Replace(".uasset", "");
                var data = new TestCaseData(filePair).SetName($"TestParse{baseFileName}");
                yield return data;
            }
        }

        private static IEnumerable GetWriteFileNames()
        {
            var fileNamesParse = GetFileNames(@"..\..\..\..\TestFiles\OctopathTableParseTest\");
            foreach (var filePair in fileNamesParse)
            {
                string[] pathPieces = filePair.Item1.Split(@"\");
                string baseFileName = pathPieces.Last().Replace(".uasset", "");

                for(int i = 0; i < pathPieces.Length; i++)
                {
                    if(pathPieces[i] == "OctopathTableParseTest")
                    {
                        pathPieces[i] = "OctopathTableWriteTest";
                    }
                }
                string newUasset = string.Join(@"\", pathPieces);
                string newUexp = newUasset.Replace(".uasset", ".uexp");
                var testData = new Tuple<Tuple<string, string>, Tuple<string, string>>(filePair, new Tuple<string, string>(newUasset, newUexp));

                var data = new TestCaseData(testData).SetName($"TestWrite{baseFileName}");
                yield return data;
            }
        }

        private static List<Tuple<string, string>> GetFileNames(string path)
        {
            string[] allFiles = Directory.GetFiles(path);
            List<string> listUasset = allFiles.Where(x => x.EndsWith(".uasset")).ToList();
            List<string> listUexp = allFiles.Where(x => x.EndsWith(".uexp")).ToList();
            List<Tuple<string, string>> fileList = new List<Tuple<string, string>>();
            foreach(string uasset in listUasset)
            {
                string baseFileName = uasset.Split(@"\").Last().Replace(".uasset", "");
                string uexp = listUexp.Where(x => x == uasset.Split(".uasset").First() + ".uexp").Single();
                fileList.Add(new Tuple<string, string>(uasset, uexp));
            }
            return fileList;
        }
    }
}