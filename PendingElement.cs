using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommonTools;

namespace SolidBoolOperationTest
{
    public class PendingElement
    {
        public Document HostDoc { get; }
        
        public Element theElement { get; }

        public Transform transformInWCS { get; }

        private bool IsLinkingElement { get; }
        
        // 存取本对象存在相交的元素ID在剪切策略模式下，该元素作为被减对象还是剪切对象的布尔值(cutting=true/false)
        private IList<Dictionary<string, object>> intersectEles = new List<Dictionary<string, object>>();

        public PendingElement(Document doc, Element ele, Transform trans)
        {   
            // 归属文档
            HostDoc = doc;
            // 元素本身
            theElement = ele;
            // 坐标系转换
            transformInWCS = trans;
            
        }

        public Solid GetPendingElementSolid()
        {
            return Tools.GetArchMainSolid(theElement, transformInWCS);
        }
        
        public void AddIntersectElement(PendingElement pendingElement, AssociationTypes cutType)
        {
            var info = new Dictionary<string, object>()
            {
                {"Element", pendingElement},
                {"Type", cutType}
            };
            intersectEles.Add(info);
        }
    }
}
