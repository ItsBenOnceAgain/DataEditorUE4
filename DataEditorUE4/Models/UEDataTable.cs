using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataEditorUE4.Models
{
    public class UEDataTable
    {
        public string TableName { get; set; }
        public UEDataTableColumn[] Columns { get; set; }
        public Dictionary<string, UEDataTableObject> Rows { get; set; }
        public string SourceUassetPath { get; set; }
        public string SourceUexpPath { get; set; }
        public byte[] HeaderBytes { get; set; }
        public byte[] FooterBytes { get; set; }

        public UEDataTable(Dictionary<string, UEDataTableObject> rows, string name, byte[] headerBytes, byte[] footerBytes)
        {
            Rows = rows;
            Columns = Rows.Count == 0 ? null : Rows.First().Value.Cells.Select(x => x.Column).ToArray();
            TableName = name;
            HeaderBytes = headerBytes;
            FooterBytes = footerBytes;
        }
    }
}
