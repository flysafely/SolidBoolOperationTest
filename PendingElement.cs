using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SolidBoolOperationTest
{
    public class PendingSolid
    {
        private Solid mainSolid;

        private bool isLinkingElement;
        
        // 存取本对象存在相交的元素ID在剪切策略模式下，该元素作为被减对象还是剪切对象的布尔值(cutting=true/false)
        private IList<PendingSolid> intersectEles = new List<PendingSolid>();

        public PendingSolid(Solid solid, bool isLink)
        {
            // 对象ID
            mainSolid = solid;
            // 是否是链接文件中的对象
            isLinkingElement = isLink;
        }

        public void AddIntersectElement(PendingSolid pendingSolid)
        {
            intersectEles.Add(pendingSolid);
        }
    }
}
