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
    public class CompsiteElementsClassifier
    {
        private readonly Document _activeDoc;
      
        public Dictionary<string, IList<Element>> ElementsDic;

        public CompsiteElementsClassifier(Document doc, IList<Reference> refs)
        {
            _activeDoc = doc;
            // 初始化元素分组字典
            InitElementsDic();
            // 聚类操作
            ClassifyElement(refs);
        }

        private void InitElementsDic()
        {
            ElementsDic = new Dictionary<string, IList<Element>>
            {
                {BuiltInCategory.OST_Walls.ToString(), new List<Element>()},
                {BuiltInCategory.OST_Floors.ToString(), new List<Element>()},
                {BuiltInCategory.OST_Columns.ToString(), new List<Element>()},
                {BuiltInCategory.OST_StructuralFraming.ToString(), new List<Element>()}
            };
        }

        private void ClassifyElement(IList<Reference> refs)
        {   
            var existsNestingRevitLink = false;
            var outsideRevitLinkIds = new List<ElementId>();
            foreach (var reference in refs)
            {
                var ele = _activeDoc.GetElement(reference);
                switch ((BuiltInCategory) ele.Category.Id.IntegerValue)
                {
                    case BuiltInCategory.OST_Walls:
                        ElementsDic[BuiltInCategory.OST_Walls.ToString()].Add(ele);
                        break;
                    case BuiltInCategory.OST_Floors:
                        ElementsDic[BuiltInCategory.OST_Floors.ToString()].Add(ele);
                        break;
                    case BuiltInCategory.OST_Columns:
                        ElementsDic[BuiltInCategory.OST_Columns.ToString()].Add(ele);
                        break;
                    case BuiltInCategory.OST_StructuralFraming:
                        ElementsDic[BuiltInCategory.OST_StructuralFraming.ToString()].Add(ele);
                        break;
                    case BuiltInCategory.OST_RvtLinks:
                        // 存在revitlinkinstance情况下进行嵌套判断
                        existsNestingRevitLink = true;
                        // 将非嵌套的revitlinkinstance记录，用于后面排除
                        outsideRevitLinkIds.Add(ele.Id);
                        // 非嵌套revitlinkinstance处理
                        AnalysisRvtLinkElements(ele);
                        break;
                }
            }

            if (existsNestingRevitLink)
            {
                AnalysisNestRvtLink(outsideRevitLinkIds);
            }
            
        }

        //private delegate void AddElementsToDic(Enum builtInCategory);

        private void AnalysisRvtLinkElements(Element revitLinkEle)
        {
            var revitLink = revitLinkEle as RevitLinkInstance;
            var linkDoc = revitLink ? .GetLinkDocument();
            
            AddElementsToDic(linkDoc, BuiltInCategory.OST_Walls);
            AddElementsToDic(linkDoc, BuiltInCategory.OST_Floors);
            AddElementsToDic(linkDoc, BuiltInCategory.OST_Columns);
            AddElementsToDic(linkDoc, BuiltInCategory.OST_StructuralFraming);
        }

        private void AnalysisNestRvtLink(List<ElementId> elementIds)
        {
            var elementsCollector = new FilteredElementCollector(_activeDoc);
            var filter = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            foreach (var revitLink in elements)
            {
                if (!elementIds.Contains(revitLink.Id) && revitLink as ElementType == null)
                {
                    AnalysisRvtLinkElements(revitLink);
                }
            }
            
        }
        
        private void AddElementsToDic(Document linkDoc, BuiltInCategory builtInCategory)
        {
            var elementsCollector = new FilteredElementCollector(linkDoc);
            var filter = new ElementCategoryFilter(builtInCategory);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            ElementsDic[builtInCategory.ToString()] = ElementsDic[builtInCategory.ToString()].Concat(elements).Where(e => e as ElementType == null).ToList();
        }
    }
}