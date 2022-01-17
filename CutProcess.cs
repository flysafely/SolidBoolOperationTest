using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using CommonTools;

namespace SolidBoolOperationTest
{
    public class CutProcess
    {
        private Application _activeApp;
        
        private Document _activeDoc;

        public IList<Dictionary<string, object>> intersectSolids = new List<Dictionary<string, object>>();

        // 同类型构件冲突对
        private IList<Array> collideElements = new List<Array>();

        // 默认剪切策略(结构(柱>梁)>建筑)
        private Dictionary<BuiltInCategory, int> CutPolicy = new Dictionary<BuiltInCategory, int>
        {
            {BuiltInCategory.OST_Walls, (int) CutOrder.Level10},
            {BuiltInCategory.OST_Floors, (int) CutOrder.Level3},
            {BuiltInCategory.OST_Columns, (int) CutOrder.Level1},
            {BuiltInCategory.OST_StructuralColumns, (int) CutOrder.Level1},
            {BuiltInCategory.OST_StructuralFraming, (int) CutOrder.Level2}
        };
        
        public CutProcess(Application app, Document doc, Dictionary<BuiltInCategory, int> customPolicy)
        {
            _activeApp = app;
            _activeDoc = doc;
            // 初始化剪切策略
            InitCutPolicy(customPolicy);
        }

        private void InitCutPolicy(Dictionary<BuiltInCategory, int> customPolicy)
        {
            foreach (var dicItem in customPolicy)
            {
                ConfirmCutPolicy(dicItem.Key, dicItem.Value);
            }
        }

        private void ConfirmCutPolicy(BuiltInCategory category, int levelNum)
        {
            if (CutPolicy.ContainsKey(category))
            {
                if (CutPolicy[category] != levelNum)
                {
                    CutPolicy[category] = levelNum;
                }
            }
            else
            {
                CutPolicy.Add(category, levelNum);
            }
        }

        public void ImplementIntersectElementsCutPolicy(IList<PendingElement> pendingElements)
        {
            if (pendingElements.Count < 1)
            {
                return;
            }

            foreach (var pendingElement in pendingElements)
            {   
                foreach (var intersectPendingElement in pendingElement.IntersectEles)
                {

                    var pendingElementCutLevelNum = GetPendingElementCutOrderNumber(pendingElement);
                    var intersectpendingElementCutLevelNum = GetPendingElementCutOrderNumber(intersectPendingElement);
                    // 从原对象的标记属性中获取特殊优先级获取

                    if (pendingElementCutLevelNum == intersectpendingElementCutLevelNum)
                    {
                        // 同类构件相交处理
                        collideElements.Add(new object[]{pendingElement, intersectPendingElement});
                    }
                    else
                    {
                        if (pendingElementCutLevelNum > intersectpendingElementCutLevelNum)
                        {
                            ClassifyReadyToCreateSolid(intersectPendingElement, pendingElement);

                        }
                    }
                }
            }
        }

        private int GetPendingElementCutOrderNumber(PendingElement pendingElement)
        {
            var cutLevelNum = CutPolicy[(BuiltInCategory) pendingElement.element.Category.GetHashCode()];
            // 从原对象的标记属性中获取特殊优先级获取
            try
            {
                return int.Parse(pendingElement.element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)
                    .AsValueString());
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return cutLevelNum;
            }
        }
        
        private void ClassifyReadyToCreateSolid(PendingElement cuttingPendingElement, PendingElement cuttedPendingElement)
        {
            var intersectSolid = GetIntersectSolid(cuttingPendingElement, cuttedPendingElement);
            if (intersectSolid != null)
            {
               intersectSolids.Add(new Dictionary<string, object>()
               {
                   {"Document", cuttedPendingElement.element.Document},
                   {"HostCuttedPendingElement", cuttedPendingElement},
                   {"IntersectSolid", GetIntersectSolid(cuttingPendingElement, cuttedPendingElement)}
               }); 
            }
        }
        
        private Solid GetIntersectSolid(PendingElement cuttingPendingElement, PendingElement cuttedPendingElement)
        {
            Solid intersectSolid = null;
            var cuttingHostDoc = cuttingPendingElement.element.Document;
            var cuttedHostDoc = cuttedPendingElement.element.Document;

            Transform cuttingSolidTransform = null;

            if (!cuttingHostDoc.Equals(cuttedHostDoc))
            {
                if (cuttingHostDoc.Equals(_activeDoc) && !cuttedHostDoc.Equals(_activeDoc))
                {
                    cuttingSolidTransform = cuttedPendingElement.TransformInWCS.Inverse;
                }
                else if (cuttedHostDoc.Equals(_activeDoc) && !cuttingHostDoc.Equals(_activeDoc))
                {
                    cuttingSolidTransform = cuttingPendingElement.TransformInWCS;
                }
                else
                {
                    cuttingSolidTransform = cuttingPendingElement.TransformInWCS.Multiply(cuttedPendingElement.TransformInWCS.Inverse);
                }
            }
            var cuttingSolid = Tools.GetArchMainSolid(cuttingPendingElement.element, cuttingSolidTransform);
            var cuttedSolid = Tools.GetArchMainSolid(cuttedPendingElement.element, null);
            try
            {
                intersectSolid = BooleanOperationsUtils.ExecuteBooleanOperation(cuttingSolid, cuttedSolid, BooleanOperationsType.Intersect);
            }
            catch (Exception e)
            {   
                Console.WriteLine(e.ToString());
                var scaledCuttingSolid = SolidUtils.CreateTransformed(cuttingSolid,
                    cuttingSolid.GetBoundingBox().Transform.ScaleBasis(0.99999));
                intersectSolid = BooleanOperationsUtils.ExecuteBooleanOperation(scaledCuttingSolid, cuttedSolid, BooleanOperationsType.Intersect);
            }

            if (intersectSolid.Volume > 0)
            {
                return intersectSolid;
            }
            else
            {
                return null;
            }
        }
        
        // public Solid IntersectAnalysis(Dictionary<object, List<PendingElement>> classifiedPendingElements)
        // {
        //     //
        //     foreach (var classPendingElements in classifiedPendingElements)
        //     {
        //         // 本类中的体存在相交需要标注(类似碰撞检查)
        //         foreach (var pending in classPendingElements.Value)
        //         {
        //             newDic.Add(pending.theElement.Id, pending);
        //         }
        //         // 本类与其他类的体相交判断记录
        //     }
        // }

        private Solid SolidByUnion(List<Solid> solids)
        {
            Solid result;
            if (solids.Count > 2)
            {
                Solid solid1 = solids[0];
                solids.RemoveAt(0);
                Solid solid2 = SolidByUnion(solids);
                var intersect =
                    BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                if (intersect.Volume > 0)
                {
                    var difference =
                        BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect,
                            BooleanOperationsType.Difference);
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2,
                        BooleanOperationsType.Union);
                }
                else
                {
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2,
                        BooleanOperationsType.Union);
                }

                return result;
            }
            else
            {
                Solid solid1 = solids[0];
                Solid solid2 = solids[1];
                var intersect =
                    BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                if (intersect.Volume > 0)
                {
                    var difference =
                        BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect,
                            BooleanOperationsType.Difference);
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2,
                        BooleanOperationsType.Union);
                }
                else
                {
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2,
                        BooleanOperationsType.Union);
                }

                return result;
            }
        }

        private Solid GetIntersectPart(Solid part1, Solid part2)
        {
            Solid result = null;
            return result;
        }

        private void DeductionOperation(Document activeDoc, Element a, Element b)
        {
            if (JoinGeometryUtils.AreElementsJoined(activeDoc, a, b))
            {
                JoinGeometryUtils.UnjoinGeometry(activeDoc, a, b);
            }

            try
            {
                JoinGeometryUtils.JoinGeometry(activeDoc, a, b);
                if (!JoinGeometryUtils.IsCuttingElementInJoin(activeDoc, a, b))
                {
                    JoinGeometryUtils.SwitchJoinOrder(activeDoc, a, b);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}