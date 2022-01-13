// using System;
// using System.Text;
// using System.Threading.Tasks;
// using Autodesk.Revit.UI;
// using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace SolidBoolOperationTest
{
    public class CompsiteElementsClassifier
    {
        private readonly Document _activeDoc;

        private Dictionary<string, List<PendingSolid>> _elementsDic;

        public CompsiteElementsClassifier(Document doc, IList<Reference> refs)
        {
            _activeDoc = doc;
            // 初始化元素分组字典
            Init_elementsDic();
            // 聚类操作
            ClassifyElement(refs);
        }

        private void Init_elementsDic()
        {
            _elementsDic = new Dictionary<string, List<PendingSolid>>
            {
                {
                    BuiltInCategory.OST_Walls.ToString(),
                    new List<PendingSolid>()
                },
                {
                    BuiltInCategory.OST_Floors.ToString(),
                    new List<PendingSolid>()
                },
                {
                    BuiltInCategory.OST_Columns.ToString(),
                    new List<PendingSolid>()
                },
                {
                    BuiltInCategory.OST_StructuralFraming.ToString(),
                    new List<PendingSolid>()
                }
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
                        _elementsDic[BuiltInCategory.OST_Walls.ToString()].Add(new PendingSolid(GetArchMainSolid(ele, null), false));
                        break;
                    case BuiltInCategory.OST_Floors:
                        _elementsDic[BuiltInCategory.OST_Floors.ToString()].Add(new PendingSolid(GetArchMainSolid(ele, null), false));
                        break;
                    case BuiltInCategory.OST_Columns:
                        _elementsDic[BuiltInCategory.OST_Columns.ToString()].Add(new PendingSolid(GetArchMainSolid(ele, null), false));
                        break;
                    case BuiltInCategory.OST_StructuralFraming:
                        _elementsDic[BuiltInCategory.OST_StructuralFraming.ToString()].Add(new PendingSolid(GetArchMainSolid(ele, null), false));
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

        private Solid GetArchMainSolid(Element element, Transform transform)
        {
            GeometryElement geometryElement = element.get_Geometry(new Options());
            IList<Solid> solids = new List<Solid>();
            IList<double> volumes = new List<double>();
            
            if (element is FamilyInstance)
            {
                foreach (GeometryObject geoObj in geometryElement)
                {

                    GeometryInstance geometryInstance = geoObj as GeometryInstance;
                    if (geometryInstance != null)
                    {
                        foreach (GeometryObject instObj in geometryInstance.SymbolGeometry)
                        {   
                            var solid = instObj as Solid;
                            if (solid != null &&　solid.Volume != 0 && solid.SurfaceArea != 0 )
                            {
                                solids.Add(solid);
                                volumes.Add(solid.Volume);
                            }
                        }
                    }
                    Solid solidDirect = geoObj as Solid;
                    if (solidDirect != null &&　solidDirect.Volume != 0 && solidDirect.SurfaceArea != 0 )
                    {
                        solids.Add(solidDirect);
                        volumes.Add(solidDirect.Volume);
                    }
                }
            }
            else
            {
                foreach (GeometryObject geoObject in geometryElement)
                {
                    var solid = geoObject as Solid;
                    if(solid != null &&　solid.Volume != 0 && solid.SurfaceArea != 0 )
                    {
                        solids.Add(solid);
                        volumes.Add(solid.Volume);
                    }
                }
            }
            // 赛选出体积最大的一个solid作为主Soild
            double maxVolume = volumes.Max();
            int index = volumes.IndexOf(maxVolume);
            Solid maxSolid = solids[index];
            if (transform == null)
            {
                return maxSolid;
            }
            else
            {
                return SolidUtils.CreateTransformed(maxSolid, transform);
            }
        }
        //private delegate void AddElementsToDic(Enum builtInCategory);

        private void AnalysisRvtLinkElements(Element revitLinkEle)
        {
            var revitLink = revitLinkEle as RevitLinkInstance;
            Transform linkTransform = revitLink.GetTransform();
            var linkDoc = revitLink?.GetLinkDocument();

            AddElementSolidsToDic(linkDoc, linkTransform, BuiltInCategory.OST_Walls);
            AddElementSolidsToDic(linkDoc, linkTransform, BuiltInCategory.OST_Floors);
            AddElementSolidsToDic(linkDoc, linkTransform, BuiltInCategory.OST_Columns);
            AddElementSolidsToDic(linkDoc, linkTransform, BuiltInCategory.OST_StructuralFraming);
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

        private void AddElementSolidsToDic(Document linkDoc, Transform transform, BuiltInCategory builtInCategory)
        {
            var elementsCollector = new FilteredElementCollector(linkDoc);
            var filter = new ElementCategoryFilter(builtInCategory);
            var elements = elementsCollector.WherePasses(filter).ToElements();
            IList<PendingSolid> pendingSolids = new List<PendingSolid>();
            foreach (var ele in elements)
            {
                if (ele as ElementType == null)
                {
                    pendingSolids.Add(new PendingSolid(GetArchMainSolid(ele, transform), false));
                }
            }
            _elementsDic[builtInCategory.ToString()] = _elementsDic[builtInCategory.ToString()].Concat(pendingSolids).ToList();
        }

        public Dictionary<string, List<PendingSolid>> GetElementsDictionary()
        {
            return _elementsDic;
        }
    }
}