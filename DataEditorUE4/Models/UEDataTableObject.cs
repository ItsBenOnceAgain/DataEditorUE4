using System;
using System.Collections.Generic;
using System.Text;

namespace DataEditorUE4.Models
{
    public class UEDataTableObject
    {
        public UEDataTableCell[] Cells { get; set; }

        public UE4ObjectType ObjectType { get; set; }

        public UEDataTableObject(UEDataTableCell[] cells, UE4ObjectType type)
        {
            Cells = cells;
            ObjectType = type;
        }
    }
}
