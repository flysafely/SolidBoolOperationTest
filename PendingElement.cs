using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommonTools;

namespace SmartComponentDeduction
{
    public class PendingElement
    {
        public Element element { get; }

        public CutOrder DocPriority { get; }

        public Transform TransformInWCS { get; }

        // 存取本对象存在相交的元素ID在剪切策略模式下，该元素作为被减对象还是剪切对象的布尔值(cutting=true/false)
        public IList<PendingElement> IntersectEles { get; }

        public PendingElement(Element ele, Transform transform, CutOrder docPriority)
        {
            // 元素本身
            element = ele;
            // 世界坐标系中的坐标转换
            TransformInWCS = transform;
            // 文档优先级
            DocPriority = docPriority;
            IntersectEles = new List<PendingElement>();
        }

        public Solid GetPendingElementSolid()
        {
            return Tools.GetArchMainSolid(element, null);
        }

        public void AddIntersectElement(PendingElement pendingElement)
        {
            IntersectEles.Add(pendingElement);
        }
    }
}