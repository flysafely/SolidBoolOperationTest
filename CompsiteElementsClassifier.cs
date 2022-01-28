using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CommonTools;

namespace SmartComponentDeduction
{
    public class CompositeElementsClassifier
    {
        private readonly IList<object> _targetCategories;

        private readonly Document _activeDoc;

        private readonly List<Element> _selectedElements = new List<Element>();

        // 使用object作为键值是为了方便后期筛选条件类型发生变化
        private readonly IList<PendingElement> _resultElements = new List<PendingElement>();

        private readonly IList<Document> _allDocuments = new List<Document>();

        private readonly IList<Transform> _allLinkInstanceTransforms = new List<Transform>();
        
        public CompositeElementsClassifier(Document doc, IList<Reference> refs, IList<object> categoryEnums)
        {
            _activeDoc = doc;

            _allDocuments.Add(doc);

            _allLinkInstanceTransforms.Add(null);
            // 目标筛选的类型
            _targetCategories = categoryEnums;
            // 聚类操作
            ClassifyElement(refs);
            // 关联相交元素
            AssociativeIntersectElements();
        }

        private void ClassifyElement(IEnumerable<Reference> refs)
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
                    _selectedElements.Add(ele);
                    _resultElements.Add(new PendingElement(ele, null, CutOrder.Level100));
                }
            }

            foreach (var revitLinkElement in revitLinkInActiveDoc)
            {
                outsideRevitLinkIds.Add(revitLinkElement.Id);
                outsideRevitLinkNames.AddRange(
                    (revitLinkElement as RevitLinkInstance)?.Name.Split(':').Where(s => s.Contains("rvt")).ToList() ??
                    throw new InvalidOperationException());

                // 非嵌套RevitLinkInstance处理
                AnalysisNonNestedElements(revitLinkElement);
            }

            if (revitLinkInActiveDoc.Count > 0)
            {
                // 由于嵌套和非嵌套链接文件在当前文件中都能直接获取到，所以需要在分析嵌套链接文件时候排除非嵌套的链接，减少重复操作
                AnalysisNestedElements(outsideRevitLinkIds, outsideRevitLinkNames);
            }
        }

        private void AnalysisNonNestedElements(Element revitLinkEle)
        {
            var revitLink = revitLinkEle as RevitLinkInstance;
            var linkTransform = revitLink?.GetTransform();
            var linkDoc = revitLink?.GetLinkDocument();
            if (linkDoc == null) return;

            _allDocuments.Add(linkDoc);
            _allLinkInstanceTransforms.Add(linkTransform);
            foreach (var targetCategory in _targetCategories)
            {
                AddLinkingElementToDic(linkDoc, linkTransform, targetCategory);
            }
        }

        private void AnalysisNestedElements(ICollection<ElementId> elementIds,
            IReadOnlyCollection<string> revitLinkInstanceNames)
        {
            var elementsCollector = new FilteredElementCollector(_activeDoc);
            var filter = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            var elements = elementsCollector.WherePasses(filter).WhereElementIsNotElementType()
                .Where(e => !elementIds.Contains(e.Id))
                .Where(e => revitLinkInstanceNames.Where(n => e.Name.Contains(n)).ToList().Count > 0).ToList();

            foreach (var revitLink in elements)
            {
                AnalysisNonNestedElements(revitLink);
            }
        }

        private void AddLinkingElementToDic(Document linkDoc, Transform transform, object category)
        {
            var elementsCollector = new FilteredElementCollector(linkDoc);
            var filter = new ElementCategoryFilter((BuiltInCategory) category);
            var elements = elementsCollector.WherePasses(filter).WhereElementIsNotElementType().ToElements();
            foreach (var ele in elements)
            {
                _resultElements.Add(new PendingElement(ele, transform, CutOrder.Level0));
            }
        }

        private void AssociativeIntersectElements()
        {
            List<Task> tasks = new List<Task>();
            List<List<PendingElement>> elementGroups = new List<List<PendingElement>>();
            int threadCount = 10;
            int groupCount = _resultElements.Count / threadCount;
            int lastGroupCount = _resultElements.Count % threadCount;
            if (groupCount != 0)
            {
                for (int i = 0; i < threadCount; i++)
                {
                    var elements = _resultElements.ToList().GetRange(groupCount * i, groupCount);
                    elementGroups.Add(elements);
                }
            }
            if (lastGroupCount > 0)
            {
                elementGroups.Add(_resultElements.ToList().GetRange(_resultElements.Count - lastGroupCount, lastGroupCount));
            }
            
            foreach (var pendingElements in elementGroups)
            {
                tasks.Add(new Task(() =>
                {
                    List<Task> inTasks = new List<Task>();
                    
                    foreach (var doc in _allDocuments)
                    {
                        inTasks.Add(new Task(() =>
                        {
                            foreach (var pendingElement in pendingElements)
                            {
                                AddIntersectPendingElements(doc, pendingElement);
                            }
                        }));
                    }
                    foreach (var inTask in inTasks)
                    {
                        inTask.Start();
                    }
                    Task.WaitAll(inTasks.ToArray());
                }));
            }
            
            foreach (var task in tasks)
            {   
                task.Start();
            }

            Task.WaitAll(tasks.ToArray());
        }
        
        private ElementAndDocRelations IntersectFilterPreJudge(Document targetDoc, PendingElement originElement)
        {
            // 目标搜索文档为当前打开文档
            if (targetDoc.Equals(_activeDoc))
            {
                return originElement.element.Document.Equals(targetDoc)
                    ? ElementAndDocRelations.ActiveElementInActiveDoc
                    : ElementAndDocRelations.LinkedElementInActiveDoc;
            }
            // 目标搜索文档为链接文档

            // 链接元素在本文档中过滤相交元素
            if (originElement.element.Document.Equals(_activeDoc))
            {
                return ElementAndDocRelations.ActiveElementInLinkedDoc;
            }

            return originElement.element.Document.Equals(targetDoc)
                ? ElementAndDocRelations.LinkedElementInSelfLinKedDoc
                : ElementAndDocRelations.LinkedElementInOtherLinkedDoc;
        }

        private void AddIntersectPendingElements(Document targetDoc, PendingElement originElement)
        {
            var targetDocTransform = _allLinkInstanceTransforms[_allDocuments.IndexOf(targetDoc)];
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
            object temp = _allDocuments;
            Monitor.Enter(temp);
            try
            {
                return targetDoc.Equals(_activeDoc)
                    ? _selectedElements.FindAll(s => elementBoxFilter.PassesFilter(s))
                        .Where(e => e.Id != pendingElement.element.Id && e.Category != null &&
                                    _targetCategories.Contains((BuiltInCategory) e.Category.GetHashCode()))
                        .ToList()
                    : new FilteredElementCollector(targetDoc)
                        .WherePasses(elementBoxFilter)
                        .Where(e => e.Id != pendingElement.element.Id && e.Category != null &&
                                    _targetCategories.Contains((BuiltInCategory) e.Category.GetHashCode()))
                        .ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                Monitor.Exit(temp);
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
            
            switch (relation)
            {
                case ElementAndDocRelations.ActiveElementInLinkedDoc:
                    elementSolid = SolidUtils.CreateTransformed(elementSolid, targetDocTransform?.Inverse);
                    break;
                case ElementAndDocRelations.LinkedElementInActiveDoc:
                    elementSolid = SolidUtils.CreateTransformed(elementSolid, pendingElement.TransformInWCS);
                    break;
                case ElementAndDocRelations.LinkedElementInOtherLinkedDoc:
                    elementSolid = SolidUtils.CreateTransformed(
                        SolidUtils.CreateTransformed(elementSolid, pendingElement.TransformInWCS),
                        targetDocTransform.Inverse);
                    break;
                case ElementAndDocRelations.ActiveElementInActiveDoc:
                    break;
                case ElementAndDocRelations.LinkedElementInSelfLinKedDoc:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

                return elementSolid;
        }

        public IList<PendingElement> GetExistIntersectElements()
        {
            return _resultElements.Where(e => e.IntersectEles.Count > 0).ToList();
        }
    }
}