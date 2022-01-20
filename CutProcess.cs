using System;
using System.Collections;
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
    public struct CutInstanceStruct
    {
        public Solid CuttingSolid;
        public FamilySymbol CutFamilySymbol;
        public Dictionary<Line, double> Rotation;
        public PendingElement HostElement;
    };

    public class CutProcess
    {
        private Application _activeApp;

        private Document _activeDoc;

        public List<CutInstanceStruct> CutInstances = new List<CutInstanceStruct>();

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
            foreach (var cutIns in CutInstances.Where(e => e.HostElement.element.Document.Equals(_activeDoc))
                         .ToList())
            {
                if (cutIns.CuttingSolid != null)
                {
                    CutInstanceStruct cutNewInstanceKeyInfo = GetCutFamilySymbolAndRotation(cutIns);
                    using (Transaction tran = new Transaction(_activeDoc, "CreateCutInstanceInActiveDoc"))
                    {
                        tran.Start();
                        CutHostElementWithTransformedCutFamilyInstance(cutNewInstanceKeyInfo);
                        tran.Commit();
                    }
                }
            }
        }

        private void CutHostElementWithTransformedCutFamilyInstance(CutInstanceStruct cutInsStruct)
        {
            FamilyInstance cutInstance = _activeDoc.Create.NewFamilyInstance(
                cutInsStruct.CuttingSolid.ComputeCentroid(),
                cutInsStruct.CutFamilySymbol,
                cutInsStruct.HostElement.element,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

            if (cutInsStruct.Rotation != null)
            {
                foreach (var info in cutInsStruct.Rotation)
                {
                    ElementTransformUtils.RotateElement(_activeDoc, cutInstance.Id, info.Key, info.Value);
                }
            }

            try
            {
                InstanceVoidCutUtils.AddInstanceVoidCut(_activeDoc, cutInsStruct.HostElement.element, cutInstance);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private CutInstanceStruct GetCutFamilySymbolAndRotation(CutInstanceStruct cutInsStruct)
        {
            cutInsStruct.Rotation = null;
            bool isSolidCube = ConfirmSolidIsFitShareFamilySymbol(cutInsStruct.CuttingSolid);
            if (!isSolidCube)
            {
                cutInsStruct.CutFamilySymbol =
                    Tools.CreateFamilySymbol(_activeDoc, _activeApp, cutInsStruct.CuttingSolid);
                return cutInsStruct;
            }
            else
            {
                // 确定立方体三边对应长宽高后，再确定该模式下的围绕各个面垂直轴的旋转角度
                var rotation = GetIntersectSolidRotationInWCS(cutInsStruct.CuttingSolid);
                var edgesEnumerator = cutInsStruct.CuttingSolid.Edges.GetEnumerator();
                using (Transaction tran = new Transaction(_activeDoc, "共用symbol参数化"))
                {
                    // 确定立方体三边对应长宽高后，再确定该模式下的围绕各个面垂直轴的旋转角度
                    tran.Start();
                    FamilySymbol newSizeFamilySymbol =
                        shareCubeFamilySymbol.Duplicate(string.Format("HostEleID-{0}&GUID-{1}",
                            cutInsStruct.HostElement.element.Id.IntegerValue.ToString(),
                            Guid.NewGuid().ToString("N").Substring(0, 6))) as FamilySymbol;
                    while (edgesEnumerator.MoveNext())
                    {
                        Line line = (edgesEnumerator.Current as Edge).AsCurve() as Line;
                        if (line == null)
                        {
                            continue;
                        }
                        if (line.Length < 1/304.8)
                        {
                            continue;
                        }
                        if (Math.Abs(line.Direction.Z) == 1)
                        {
                            newSizeFamilySymbol.LookupParameter("Height").Set(line.Length / 2);
                            continue;
                        }
                        else if (Math.Abs(line.Direction.X) == 1)
                        {
                            newSizeFamilySymbol.LookupParameter("Length").Set(line.Length / 2);
                            continue;
                        }
                        else if (Math.Abs(line.Direction.Y) == 1)
                        {
                            newSizeFamilySymbol.LookupParameter("Width").Set(line.Length / 2);
                            continue;
                        }
                    }
                    tran.Commit();
                    cutInsStruct.CutFamilySymbol = newSizeFamilySymbol;
                    cutInsStruct.Rotation = rotation;
                    return cutInsStruct;
                }
            }
        }

        private Dictionary<Line, double> GetIntersectSolidRotationInWCS(Solid solid)
        {
            // 获取成对的面，来获取实际立方体的尺寸
            IEnumerator facesEnumerator = solid.Faces.GetEnumerator();

            int[] planarFacesOne;
            int[] planarFacesTwo;
            int[] planarFacesThree;

            List<int> unusedIndexs = new List<int>(){0, 1, 2, 3, 4, 5};
            for (int i = 0; i < solid.Faces.Size; i++)
            {
                for (int j = i + 1; j < solid.Faces.Size; j++)
                {
                    if ((solid.Faces.get_Item(i) as PlanarFace).FaceNormal.IsAlmostEqualTo(-(solid.Faces.get_Item(j) as PlanarFace).FaceNormal))
                    {
                        unusedIndexs.Remove(i);
                        unusedIndexs.Remove(j);
                        planarFacesOne = new int[]{i, j};
                    }
                }
            }

            for (int i = 0; i < unusedIndexs.Count; i++)
            {
                for (int j = i + 1; i < unusedIndexs.Count; i++)
                {
                    if ((solid.Faces.get_Item(unusedIndexs[i]) as PlanarFace).FaceNormal.IsAlmostEqualTo(
                            -(solid.Faces.get_Item(unusedIndexs[j]) as PlanarFace).FaceNormal))
                    {
                        unusedIndexs.Remove(unusedIndexs[i]);
                        unusedIndexs.Remove(unusedIndexs[j]);
                        planarFacesTwo = new int[]{unusedIndexs[i], unusedIndexs[j]};
                    }
                }
            }

            planarFacesThree = new int[] {unusedIndexs[0], unusedIndexs[1]};
            
            // Plane planarFacesOne1 = solid.Faces.get_Item(planarFacesOne?planarFacesOne[0]:plan) as PlanarFace
            
            // while (facesEnumerator.MoveNext())
            // {
            //     PlanarFace planarface = facesEnumerator.Current as PlanarFace;
            //     if (planarface.FaceNormal)
            //     {
            //         return false;
            //     }
            // }
            
            XYZ rotatedPoint = solid.ComputeCentroid();
            Plane planeYZ = Plane.CreateByThreePoints(rotatedPoint, rotatedPoint - new XYZ(0, 500, 0), 
                rotatedPoint - new XYZ(0, 0, 500));
            Plane planeXZ = Plane.CreateByThreePoints(rotatedPoint, rotatedPoint - new XYZ(500, 0, 0),
                rotatedPoint - new XYZ(0, 0, 500));
            Plane planeXY = Plane.CreateByThreePoints(rotatedPoint, rotatedPoint - new XYZ(500, 0, 0),
                rotatedPoint - new XYZ(0, 500, 0));
            // 待完成
            return null;
        }

        private bool ConfirmSolidIsFitShareFamilySymbol(Solid solid)
        {
            
            // 判定每一个面是否为长方形
            IEnumerator facesEnumerator = solid.Faces.GetEnumerator();
            while (facesEnumerator.MoveNext())
            {
                PlanarFace face = facesEnumerator.Current as PlanarFace;
                if (!face.GetEdgesAsCurveLoops()[0].IsRectangular(face.GetSurface() as Plane))
                {
                    return false;
                }
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
            // 从原对象的标记属性中获取特殊优先级
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
                CutInstanceStruct cutInstanceStruct = new CutInstanceStruct();
                cutInstanceStruct.HostElement = cuttedPendingElement;
                cutInstanceStruct.CuttingSolid = GetIntersectSolid(cuttingPendingElement, cuttedPendingElement);
                CutInstances.Add(cutInstanceStruct);
            }
        }

        private Solid GetIntersectSolid(PendingElement cuttingPendingElement, PendingElement cuttedPendingElement)
        {
            Solid intersectSolid;
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