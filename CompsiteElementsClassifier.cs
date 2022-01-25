// using System;
// using System.Text;
// using System.Threading.Tasks;
// using Autodesk.Revit.UI;
// using Autodesk.Revit.Attributes;

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using CommonTools;

namespace SmartComponentDeduction
{
    public class CompositeElementsClassifier
    {
        private IList<object> targetCategories;

        private readonly Document _activeDoc;

        private readonly List<Element> selectedElements = new List<Element>();

        // 使用object作为键值是为了方便后期筛选条件类型发生变化
        private IList<PendingElement> _resultElements = new List<PendingElement>();

        private IList<Document> allDocuments = new List<Document>();

        private IList<Transform> allLinkInstanceTransforms = new List<Transform>();

        public CompositeElementsClassifier(Document doc, IList<Reference> refs, IList<object> categoryEnums)
        {
            _activeDoc = doc;

            allDocuments.Add(doc);

            allLinkInstanceTransforms.Add(null);
            // 目标筛选的类型
            targetCategories = categoryEnums;
            // 聚类操作
            ClassifyElement(refs);
            // 关联相交元素
            AssociativeIntersectingElement();
        }

        private void ClassifyElement(IList<Reference> refs)
        {
            var outsideRevitLinkIds = new List<ElementId>();
            var outsideRevitLinkNames = new List<string>();
            var revitLinkInActiveDoc = new List<Element>();
            foreach (var reference in refs)
            {
                var ele = _activeDoc.GetElement(reference);
                if ((BuiltInCategory) ele.Category.Id.IntegerValue == BuiltInCategory.OST_RvtLinks)
                {
                    revitLinkInActiveDoc.Add(ele);
                }
                else
                {
                    selectedElements.Add(ele);
                    _resultElements.Add(new PendingElement(ele, null, CutOrder.Level100));
                }
            }

            foreach (var revitLinkElement in revitLinkInActiveDoc)
            {
                outsideRevitLinkIds.Add(revitLinkElement.Id);
                outsideRevitLinkNames.AddRange((revitLinkElement as RevitLinkInstance).Name
                    .Split(new char[] {':'}).Where(s => s.Contains("rvt")).ToList());

                // 非嵌套RevitLinkInstance处理
                AnalysisNonNestedElements(revitLinkElement, _resultElements);
            }

            if (revitLinkInActiveDoc.Count > 0)
            {
                // 由于嵌套和非嵌套链接文件在当前文件中都能直接获取到，所以需要在分析嵌套链接文件时候排除非嵌套的链接，减少重复操作
                AnalysisNestedElements(outsideRevitLinkIds, outsideRevitLinkNames, _resultElements);
            }
        }

        private void AnalysisNonNestedElements(Element revitLinkEle, IList<PendingElement> tempList)
        {
            var revitLink = revitLinkEle as RevitLinkInstance;
            var linkTransform = revitLink?.GetTransform();
            var linkDoc = revitLink?.GetLinkDocument();
            if (linkDoc != null)
            {
                allDocuments.Add(linkDoc);
                allLinkInstanceTransforms.Add(linkTransform);
                foreach (var targetCategory in targetCategories)
                {
                    AddLinkingElementToDic(linkDoc, linkTransform, targetCategory, tempList);
                }
            }
        }

        private void AnalysisNestedElements(List<ElementId> elementIds, List<string> revitLinkInstanceNames,
            IList<PendingElement> tempList)
        {
            var elementsCollector = new FilteredElementCollector(_activeDoc);
            var filter = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            foreach (var revitLink in elements)
            {
                if (revitLink as ElementType == null && !elementIds.Contains(revitLink.Id) &&
                    revitLinkInstanceNames.Where(n => revitLink.Name.Contains(n)).ToList().Count > 0)
                {
                    AnalysisNonNestedElements(revitLink, tempList);
                }
            }
        }

        private void AddLinkingElementToDic(Document linkDoc, Transform transform, object category,
            IList<PendingElement> tempList)
        {
            var elementsCollector = new FilteredElementCollector(linkDoc);
            var filter = new ElementCategoryFilter((BuiltInCategory) category);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            foreach (var ele in elements)
            {
                if (ele as ElementType == null)
                {
                    tempList.Add(new PendingElement(ele, transform, CutOrder.Level0));
                }
            }
        }

        private void AssociativeIntersectingElement()
        {
            foreach (var document in allDocuments)
            {
                foreach (var pendingElement in _resultElements)
                {
                    AddIntersectPendingElements(document, pendingElement);
                }
            }
        }

        private ElementAndDocRelations IntersectFilterPreJudge(Document targetDoc, PendingElement originElement)
        {
            // 目标搜索文档为当前打开文档
            if (targetDoc.Equals(_activeDoc))
            {
                if (originElement.element.Document.Equals(targetDoc))
                {
                    return ElementAndDocRelations.ActiveElementInActiveDoc;
                }
                else
                {
                    return ElementAndDocRelations.LinkedElementInActiveDoc;
                }
            }
            // 目标搜索文档为链接文档
            else
            {
                // 链接元素在本文档中过滤相交元素
                if (originElement.element.Document.Equals(_activeDoc))
                {
                    return ElementAndDocRelations.ActiveElementInLinkedDoc;
                }
                // 链接元素在自己的链接文档中过滤相交元素
                else if (originElement.element.Document.Equals(targetDoc))
                {
                    return ElementAndDocRelations.LinkedElementInSelfLinKedDoc;
                }
                // 链接元素在其他的链接文档中过滤相交元素
                else
                {
                    return ElementAndDocRelations.LinkedElementInOtherLinkedDoc;
                }
            }
        }

        private void AddIntersectPendingElements(Document targetDoc, PendingElement originElement)
        {
            var targetDocTransform = allLinkInstanceTransforms[allDocuments.IndexOf(targetDoc)];

            var intersectElements = SolidQuickFilterIntersectElements(targetDoc, originElement, targetDocTransform);

            foreach (var intersectElement in intersectElements)
            {
                originElement.AddIntersectElement(new PendingElement(intersectElement, targetDocTransform,
                    intersectElement.Document.Equals(_activeDoc) ? CutOrder.Level100 : CutOrder.Level0));
            }
        }

        private IList<Element> SolidQuickFilterIntersectElements(Document targetDoc, PendingElement pendingElement,
            Transform targetDocTransform)
        {
            var elementSolid = GetParticularTransformedSolid(targetDoc, pendingElement, targetDocTransform);
            if (elementSolid == null)
            {
                return new List<Element>();
            }
            ElementQuickFilter elementBoxFilter = Tools.GetBoxFilterBySolid(elementSolid, 0); 
            try
            {
                if (targetDoc.Equals(_activeDoc))
                {
                    return selectedElements.FindAll(s => elementBoxFilter.PassesFilter(s))
                        .Where(e => e.Id != pendingElement.element.Id && e.Category != null &&
                                    targetCategories.Contains((BuiltInCategory) e.Category.GetHashCode()))
                        .ToList();
                }
                else
                {
                    return new FilteredElementCollector(targetDoc)
                        .WherePasses(elementBoxFilter)
                        .Where(e => e.Id != pendingElement.element.Id && e.Category != null &&
                                    targetCategories.Contains((BuiltInCategory) e.Category.GetHashCode()))
                        .ToList();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
            }
        }

        private Solid GetParticularTransformedSolid(Document targetDoc, PendingElement pendingElement,
            Transform targetDocTransform)
        {
            var relation = IntersectFilterPreJudge(targetDoc, pendingElement);

            var elementSolid = pendingElement.GetPendingElementSolid();

            if (elementSolid == null)
            {
                return null;
            }

            if (relation == ElementAndDocRelations.ActiveElementInLinkedDoc)
            {
                elementSolid = SolidUtils.CreateTransformed(elementSolid, targetDocTransform?.Inverse);
            }
            else if (relation == ElementAndDocRelations.LinkedElementInActiveDoc)
            {
                elementSolid = SolidUtils.CreateTransformed(elementSolid, pendingElement.TransformInWCS);
            }
            else if (relation == ElementAndDocRelations.LinkedElementInOtherLinkedDoc)
            {
                elementSolid = SolidUtils.CreateTransformed(
                    SolidUtils.CreateTransformed(elementSolid, pendingElement.TransformInWCS),
                    targetDocTransform.Inverse);
            }

            return elementSolid;
        }
        
        public IList<PendingElement> GetExistIntersectElements()
        {
            return _resultElements.Where(e => e.IntersectEles.Count > 0).ToList();
        }
    }
}