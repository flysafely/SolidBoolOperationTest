using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace SolidBoolOperationTest
{
    public class PendingElement
    {
        int elementId;
        // 默认剪切策略(结构(柱>梁)>建筑)
        Dictionary<string, int> defaultCutPolicy;
        // 存取本对象存在相交的元素ID在剪切策略模式下，该元素作为被减对象还是剪切对象的布尔值(cutting=true/false)
        Dictionary<int, bool> intersectEleInfos
        {
            get;
            set;
        }

        public PendingElement(int eleId, IList<Element> intersectEles, Dictionary<string, int> policy)
        {
            elementId = eleId;
            defaultCutPolicy = policy;
            // 
            RankElementsCutOrder(intersectEles);
        }

        private void RankElementsCutOrder(IList<Element> intersectEles)
        {

        }
    }
}
