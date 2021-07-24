using System;
using System.Collections.Generic;
using System.Text;

namespace DataEditorUE4.Models
{
    public class UEDataTableCell
    {
        public UEDataTableColumn Column { get; set; }
        public dynamic Value { get; set; }
        public byte[] HeaderBytes { get; set; }
        public bool TextIsUnicode { get; set; }
        public UEDataTableCell(UEDataTableColumn column, dynamic value, byte[] headerBytes = null, bool textIsUnicode = false)
        {
            Column = column;
            Value = value;
            HeaderBytes = headerBytes;
            TextIsUnicode = textIsUnicode;
        }
    }
}
