// using System;
// using System.Text;
// using System.Threading.Tasks;
// using Autodesk.Revit.UI;
// using Autodesk.Revit.Attributes;

//using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CommonTools;

namespace SolidBoolOperationTest
{
    public class CompositeElementsClassifier
    {
        private IList<object> targetCategories;
        
        private readonly Document _activeDoc;
        // 使用object作为键值是为了方便后期筛选条件类型发生变化
        private List<PendingElement> _resultElements;
        
        public IList<Document> allDocuments;

        public CompositeElementsClassifier(Document doc, IList<Reference> refs, IList<object> categoryEnums)
        {
            _activeDoc = doc;
            // 第一doc永远是当前打开文档
            allDocuments = new List<Document>();
            allDocuments.Add(doc);
            // 目标筛选的类型
            targetCategories = categoryEnums;
            // 聚类操作
            ClassifyElement(refs);
        }
        
        private void ClassifyElement(IList<Reference> refs)
        {
            var outsideRevitLinkIds = new List<ElementId>();
            var tempList = new List<PendingElement>();
            foreach (var reference in refs)
            {
                var ele = _activeDoc.GetElement(reference);
                if ((BuiltInCategory)ele.Category.Id.IntegerValue == BuiltInCategory.OST_RvtLinks)
                {
                    // 将非嵌套的RevitLinkInstance记录，用于后面排除
                    outsideRevitLinkIds.Add(ele.Id);
                    // 非嵌套RevitLinkInstance处理
                    AnalysisNonNestedElements(ele, tempList);
                    // 由于嵌套和非嵌套链接文件在当前文件中都能直接获取到，所以需要在分析嵌套链接文件时候排除非嵌套的链接，减少重复操作
                    AnalysisNestedElements(outsideRevitLinkIds, tempList);
                }
                else
                {
                    tempList.Add(new PendingElement(_activeDoc, ele, null));
                }
            }

            _resultElements = tempList;
        }
        
        private void AnalysisNonNestedElements(Element revitLinkEle, IList<PendingElement> tempList)
        {
            var revitLink = revitLinkEle as RevitLinkInstance;
            var linkTransform = revitLink?.GetTransform();
            var linkDoc = revitLink?.GetLinkDocument();
            // 记录下doc用于其他文档中的元素进行过滤操作
            allDocuments.Add(linkDoc);
            foreach (var targetCategory in targetCategories)
            {
                AddLinkingElementToDic(linkDoc, linkTransform, targetCategory, tempList);
            }
        }

        private void AnalysisNestedElements(List<ElementId> elementIds, IList<PendingElement> tempList)
        {
            var elementsCollector = new FilteredElementCollector(_activeDoc);
            var filter = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            foreach (var revitLink in elements)
            {
                if (!elementIds.Contains(revitLink.Id) && revitLink as ElementType == null)
                {
                    AnalysisNonNestedElements(revitLink, tempList);
                }
            }
        }

        private void AddLinkingElementToDic(Document linkDoc, Transform transform, object category, IList<PendingElement> tempList)
        {
            var elementsCollector = new FilteredElementCollector(linkDoc);
            var filter = new ElementCategoryFilter((BuiltInCategory)category);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            foreach (var ele in elements)
            {
                if (ele as ElementType == null)
                {
                    tempList.Add(new PendingElement(linkDoc, ele, transform));
                }
            }
        }

        public List<PendingElement> GetExistIntersectElements()
        {
            return _resultElements;
        }
    }
}