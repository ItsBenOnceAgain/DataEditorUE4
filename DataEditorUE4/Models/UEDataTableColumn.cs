using System;
using System.Collections.Generic;
using System.Text;

namespace DataEditorUE4.Models
{
    public class UEDataTableColumn
    {
        public string ColumnName { get; set; }
        public UE4PropertyType ColumnType { get; set; }

        public UEDataTableColumn(string column, UE4PropertyType type)
        {
            ColumnName = column;
            ColumnType = type;
        }
    }
}
