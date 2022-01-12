using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;

namespace SolidBoolOperationTest
{
    public class ElementsSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Walls)
            {
                return true;
            }
            else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Floors)
            {
                return true;
            }
            else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
            {
                return true;
            }
            else if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_RvtLinks)
            {
                return true;
            }
            else
            {
                return false;
            }

        }
        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
}
