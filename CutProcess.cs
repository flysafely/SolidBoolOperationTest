using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    struct CutNewInstanceKeyInfo
    {
        public Solid CuttingSolid;
        public FamilySymbol CutFamilySymbol;
        public Dictionary<Line, double> Rotation;
        public Element HostElement;
    };  
    
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
            {BuiltInCategory.OST_Columns, (int) CutOrder.Level2},
            {BuiltInCategory.OST_StructuralColumns, (int) CutOrder.Level1},
            {BuiltInCategory.OST_StructuralFraming, (int) CutOrder.Level2}
        };
        
        // 默认空心剪切族类familysymbol
        private FamilySymbol shareCubeFamilySymbol;

        public CutProcess(Application app, Document doc, Dictionary<BuiltInCategory, int> customPolicy)
        {
            _activeApp = app;
            _activeDoc = doc;
            // 初始化剪切策略
            InitCutPolicy(customPolicy);
        }

        public void DoCuttingProcess(IList<PendingElement> pendingElements, string cutRfaFileName)
        {
            // 元素剪切优先级判断
            if (!ImplementIntersectElementsCutPolicy(pendingElements))
                return;
            
            shareCubeFamilySymbol = Tools.GetCuttingFamilySymbol(_activeApp, _activeDoc, cutRfaFileName);
            if (shareCubeFamilySymbol == null)
            {
                TaskDialog.Show("note!", "空心剪切族加载失败！");
            }
            else
            {
                CreateCutInstancesInActiveDoc();
                CreateCutInstancesInLinkDoc();
            }
        }

        private void CreateCutInstancesInActiveDoc()
        {
            foreach (var intersectDic in intersectSolids.Where(e => (e["Document"] as Document).Equals(_activeDoc))
                         .ToList())
            {
                if (intersectDic != null)
                {
                    CutNewInstanceKeyInfo cutNewInstanceKeyInfo = GetCutFamilySymbolAndRotation(intersectDic);
                    using (Transaction tran = new Transaction(_activeDoc, "CreateCutInstanceInActiveDoc"))
                    {
                        tran.Start();
                        CutHostElementWithTransformedCutFamilyInstance(cutNewInstanceKeyInfo);
                        tran.Commit();
                    }
                }
            }
        }

        private void CutHostElementWithTransformedCutFamilyInstance(CutNewInstanceKeyInfo cutNewInstanceKeyInfo)
        {
            FamilyInstance cutInstance = _activeDoc.Create.NewFamilyInstance(cutNewInstanceKeyInfo.CuttingSolid.ComputeCentroid(), cutNewInstanceKeyInfo.CutFamilySymbol, cutNewInstanceKeyInfo.HostElement, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            if (cutNewInstanceKeyInfo.Rotation != null)
            {
                foreach (var info in cutNewInstanceKeyInfo.Rotation)
                {
                    ElementTransformUtils.RotateElement(_activeDoc, cutInstance.Id, info.Key, info.Value);
                }
            }
            InstanceVoidCutUtils.AddInstanceVoidCut(_activeDoc, cutNewInstanceKeyInfo.HostElement, cutInstance);
        }
        
        private CutNewInstanceKeyInfo GetCutFamilySymbolAndRotation(Dictionary<string, object> intersectDic)
        {
            CutNewInstanceKeyInfo cutNewInstanceKeyInfo;
            cutNewInstanceKeyInfo.CuttingSolid = intersectDic["intersectSolid"] as Solid;
            cutNewInstanceKeyInfo.HostElement = (intersectDic["HostCuttedPendingElement"] as PendingElement).element;
            cutNewInstanceKeyInfo.Rotation = null;
            bool isSolidCube = ConfirmSolidIsFitShareFamilySymbol(cutNewInstanceKeyInfo.CuttingSolid);
            if (!isSolidCube)
            {
                cutNewInstanceKeyInfo.CutFamilySymbol = Tools.CreateFamilySymbol(_activeDoc, _activeApp, cutNewInstanceKeyInfo.CuttingSolid);
                return cutNewInstanceKeyInfo;
            }
            else
            {
                // 确定立方体三边对应长宽高后，再确定该模式下的围绕各个面垂直轴的旋转角度
                var rotation = GetIntersectSolidRotationInWCS(cutNewInstanceKeyInfo.CuttingSolid);
                var edgesEnumerator = cutNewInstanceKeyInfo.CuttingSolid.Edges.GetEnumerator();
                using (Transaction tran = new Transaction(_activeDoc, "共用symbol参数化"))
                {   
                    // 确定立方体三边对应长宽高后，再确定该模式下的围绕各个面垂直轴的旋转角度
                    tran.Start();
                    while (edgesEnumerator.MoveNext())
                    {
                        Line line = (edgesEnumerator.Current as Edge).AsCurve() as Line;
                        if (Math.Abs(line.Direction.Z) == 1)
                        {
                            shareCubeFamilySymbol.LookupParameter("th").Set(line.Length / 2);
                            shareCubeFamilySymbol.LookupParameter("bh").Set(line.Length / 2);
                            continue;
                        }
                        else if (Math.Abs(line.Direction.X) == 1)
                        {
                            shareCubeFamilySymbol.LookupParameter("ll").Set(line.Length / 2);
                            shareCubeFamilySymbol.LookupParameter("rl").Set(line.Length / 2);
                            continue;
                        }
                        else if (Math.Abs(line.Direction.Y) == 1)
                        {
                            shareCubeFamilySymbol.LookupParameter("tw").Set(line.Length / 2);
                            shareCubeFamilySymbol.LookupParameter("bw").Set(line.Length / 2);
                            continue;
                        }
                    }
                    tran.Commit();
                    cutNewInstanceKeyInfo.CutFamilySymbol = shareCubeFamilySymbol;
                    cutNewInstanceKeyInfo.Rotation = rotation;
                    return cutNewInstanceKeyInfo;
                }
            }
        }

        private Dictionary<Line, double> GetIntersectSolidRotationInWCS(Solid solid)
        {   
            // 待完成
            return null;
        }

        private bool ConfirmSolidIsFitShareFamilySymbol(Solid solid)
        {
            // 面数量判断
            int faceCount = solid.Faces.Size;
            if (faceCount != 6)
            {
                return false;
            }
            // 对面平行情况判断
            return true;
        }            
                    
        private void CreateCutInstancesInLinkDoc()
        {
            
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

        public bool ImplementIntersectElementsCutPolicy(IList<PendingElement> pendingElements)
        {
            if (pendingElements.Count < 1)
            {
                return false;
            }

            foreach (var pendingElement in pendingElements)
            {
                foreach (var intersectPendingElement in pendingElement.IntersectEles)
                {
                    var pendingEleCutLevelNum = GetPendingElementCutOrderNumber(pendingElement);
                    var interPendingEleCutLevelNum = GetPendingElementCutOrderNumber(intersectPendingElement);
                    // 从原对象的标记属性中获取特殊优先级获取

                    if (pendingEleCutLevelNum == interPendingEleCutLevelNum)
                    {
                        // 同类构件判断其归属文档优先级
                        var pendingEleHostDocCutLevelNum = (int) pendingElement.DocPriority;
                        var interPendingEleHostDocCutLevelNum = (int) intersectPendingElement.DocPriority;
                        if (pendingEleHostDocCutLevelNum > interPendingEleHostDocCutLevelNum)
                        {
                            ClassifyReadyToCreateSolid(intersectPendingElement, pendingElement);
                        }
                        else if (pendingEleHostDocCutLevelNum == interPendingEleHostDocCutLevelNum)
                        {
                            collideElements.Add(new object[] {pendingElement, intersectPendingElement});
                        }
                    }
                    else
                    {
                        if (pendingEleCutLevelNum > interPendingEleCutLevelNum)
                        {
                            ClassifyReadyToCreateSolid(intersectPendingElement, pendingElement);
                        }
                    }
                }
            }

            return true;
        }

        private int GetPendingElementCutOrderNumber(PendingElement pendingElement)
        {
            var cutLevelNum = CutPolicy[(BuiltInCategory) pendingElement.element.Category.GetHashCode()];
            // 从原对象的标记属性中获取特殊优先级获取
            try
            {
                return int.Parse(pendingElement.element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return cutLevelNum;
            }
        }

        private void ClassifyReadyToCreateSolid(PendingElement cuttingPendingElement,
            PendingElement cuttedPendingElement)
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
                    cuttingSolidTransform =
                        cuttingPendingElement.TransformInWCS.Multiply(cuttedPendingElement.TransformInWCS.Inverse);
                }
            }

            var cuttingSolid = Tools.GetArchMainSolid(cuttingPendingElement.element, cuttingSolidTransform);
            var cuttedSolid = Tools.GetArchMainSolid(cuttedPendingElement.element, null);
            try
            {
                intersectSolid =
                    BooleanOperationsUtils.ExecuteBooleanOperation(cuttingSolid, cuttedSolid,
                        BooleanOperationsType.Intersect);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                var scaledCuttingSolid = SolidUtils.CreateTransformed(cuttingSolid,
                    cuttingSolid.GetBoundingBox().Transform.ScaleBasis(0.99999));
                intersectSolid = BooleanOperationsUtils.ExecuteBooleanOperation(scaledCuttingSolid, cuttedSolid,
                    BooleanOperationsType.Intersect);
            }

            return intersectSolid.Volume > 0 ? intersectSolid : null;
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

        // private Solid SolidByUnion(List<Solid> solids)
        // {
        //     Solid result;
        //     if (solids.Count > 2)
        //     {
        //         Solid solid1 = solids[0];
        //         solids.RemoveAt(0);
        //         Solid solid2 = SolidByUnion(solids);
        //         var intersect =
        //             BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
        //         if (intersect.Volume > 0)
        //         {
        //             var difference =
        //                 BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect,
        //                     BooleanOperationsType.Difference);
        //             result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2,
        //                 BooleanOperationsType.Union);
        //         }
        //         else
        //         {
        //             result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2,
        //                 BooleanOperationsType.Union);
        //         }
        //
        //         return result;
        //     }
        //     else
        //     {
        //         Solid solid1 = solids[0];
        //         Solid solid2 = solids[1];
        //         var intersect =
        //             BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
        //         if (intersect.Volume > 0)
        //         {
        //             var difference =
        //                 BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect,
        //                     BooleanOperationsType.Difference);
        //             result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2,
        //                 BooleanOperationsType.Union);
        //         }
        //         else
        //         {
        //             result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2,
        //                 BooleanOperationsType.Union);
        //         }
        //
        //         return result;
        //     }
        // }
        //
        // private void DeductionOperation(Document activeDoc, Element a, Element b)
        // {
        //     if (JoinGeometryUtils.AreElementsJoined(activeDoc, a, b))
        //     {
        //         JoinGeometryUtils.UnjoinGeometry(activeDoc, a, b);
        //     }
        //
        //     try
        //     {
        //         JoinGeometryUtils.JoinGeometry(activeDoc, a, b);
        //         if (!JoinGeometryUtils.IsCuttingElementInJoin(activeDoc, a, b))
        //         {
        //             JoinGeometryUtils.SwitchJoinOrder(activeDoc, a, b);
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         throw ex;
        //     }
        // }
    }
}