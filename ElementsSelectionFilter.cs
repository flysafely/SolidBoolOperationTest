using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;

namespace SmartComponentDeduction
{
    public class ElementsSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            return Filter(element);
        }

        public bool Filter(Element element)
        {
            IList<int> allowCategorys = new List<int>()
            {
                // 需要增加时候直接添加即可
                (int) BuiltInCategory.OST_Walls,
                (int) BuiltInCategory.OST_Floors,
                (int) BuiltInCategory.OST_Columns,
                (int) BuiltInCategory.OST_StructuralColumns,
                (int) BuiltInCategory.OST_StructuralFraming,
                (int) BuiltInCategory.OST_RvtLinks
            };
            if (allowCategorys.Contains(element.Category.Id.IntegerValue))
            {
                return true;
            }

            return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
}