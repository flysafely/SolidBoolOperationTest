﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    
    // public struct CutInstanceStruct
    // {
    //     public double Length;
    //     public double Width;
    //     public double Height;
    //     public Solid OriginCutSolid;
    //     public AssociationTypes Relation;
    //     public FamilySymbol CutFamilySymbol;
    //     public Dictionary<object, double> Rotations;
    //     public PendingElement CuttingElement;
    //     public PendingElement HostElement;
    //
    //     public void initRotations()
    //     {
    //         Rotations = new Dictionary<object, double>();
    //     }
    // };
    
    public class CutProcess
    {
        private Application _activeApp;

        private Document _activeDoc;

        private string _cutRfaFileName;

        public List<CutInstance> cutInstances = new List<CutInstance>();
        
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

        public CutProcess(Application app, Document doc, Dictionary<BuiltInCategory, int> customPolicy, string rfaFileName)
        {
            _activeApp = app;
            _activeDoc = doc;
            _cutRfaFileName = rfaFileName;
            // 初始化剪切策略
            InitCutPolicy(customPolicy);
        }

        public void DoCuttingProcess(IList<PendingElement> pendingElements)
        {
            // 元素剪切优先级判断
            if (!ImplementIntersectElementsCutPolicy(pendingElements))
                return;
            DealWithCollideElements();
            CreateCutInstancesInActiveDoc();
            CreateCutInstancesInLinkDoc();
        }

        private void CreateCutInstancesInActiveDoc()
        {
            // 找出真实需要剪切的相交Solid
            foreach (var cutIns in cutInstances.Where(e => e.BeCutElement.element.Document.Equals(_activeDoc) && e.Relation != AssociationTypes.Collide)
                         .ToList())
            {
                if (cutIns.OriginCutSolid != null)
                {
                    GetCutNewInstanceKeyInfo(cutIns);
                    using (Transaction tran = new Transaction(_activeDoc, "CreateCutInstanceInActiveDoc"))
                    {
                        tran.Start();
                        CutHostElementWithTransformedCutFamilyInstance(cutIns);
                        tran.Commit();
                    }
                }
            }
        }

        private void CreateCutInstancesInLinkDoc()
        {
            // 在其他链接文件中创建空心剪切体的逻辑
        }

        private void DealWithCollideElements()
        {
            // 处理发生冲突的元素的逻辑
        }
        
        private void CutHostElementWithTransformedCutFamilyInstance(CutInstance cutIns)
        {
            if (cutIns.CutFamilySymbol == null)
            {
                return;
            }
            
            FamilyInstance cutInstance = _activeDoc.Create.NewFamilyInstance(
                cutIns.OriginCutSolid.ComputeCentroid(),
                cutIns.CutFamilySymbol,
                cutIns.BeCutElement.element,
                Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            cutIns.eleId = cutInstance.Id;
                                
            if (cutIns.Rotations != null)
            {
                foreach (var rotationDic in cutIns.Rotations)
                {
                    if (rotationDic.Key != null && rotationDic.Value != 0)
                    {
                        ElementTransformUtils.RotateElement(_activeDoc, cutIns.eleId, rotationDic.Key,
                                rotationDic.Value);
                        // TaskDialog.Show("note",
                        //     string.Format("X:{0},Y:{1},Z:{2}", rotationDic.Key.Direction.X, rotationDic.Key.Direction.Y,
                        //         rotationDic.Key.Direction.Z));
                    }
                }
            }
            try
            {
                InstanceVoidCutUtils.AddInstanceVoidCut(_activeDoc, cutIns.BeCutElement.element, cutInstance);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void GetCutNewInstanceKeyInfo(CutInstance cutIns)
        {
            bool isSolidCube = ConfirmSolidIsFitShareFamilySymbol(cutIns);
            if (!isSolidCube)
            {
                cutIns.CutFamilySymbol =
                    Tools.CreateFamilySymbol(_activeDoc, _activeApp, cutIns.OriginCutSolid);
            }
            else
            {   
                // 只加载一次shareCubeFamilySymbol，不成功则抛出异常
                if (shareCubeFamilySymbol == null)
                {
                    shareCubeFamilySymbol = Tools.GetCuttingFamilySymbol(_activeApp, _activeDoc, _cutRfaFileName);
                    if (shareCubeFamilySymbol == null)
                    {
                        throw new DirectoryNotFoundException("空心剪切族加载失败！");
                    }
                }
                // 确定立方体三边对应长宽高后，再确定该模式下的围绕各个面垂直轴的旋转角度
                //SetCutCubicSolidSizeAndRotations(cutIns);

                using (Transaction tran = new Transaction(_activeDoc, "共用symbol参数化"))
                {
                    // 确定立方体三边对应长宽高后，再确定该模式下的围绕各个面垂直轴的旋转角度
                    tran.Start();
                    FamilySymbol newSizeFamilySymbol =
                        shareCubeFamilySymbol.Duplicate(string.Format("HostEleID-{0}&GUID-{1}",
                            cutIns.BeCutElement.element.Id.IntegerValue.ToString(),
                            Guid.NewGuid().ToString("N").Substring(0, 6))) as FamilySymbol;
                    // 设置空心族参数值
                    newSizeFamilySymbol.LookupParameter("Length").Set(cutIns.Length / 2);
                    newSizeFamilySymbol.LookupParameter("Width").Set(cutIns.Width / 2);
                    newSizeFamilySymbol.LookupParameter("Height").Set(cutIns.Height / 2);
                    // ...
                    tran.Commit();
                    cutIns.CutFamilySymbol = newSizeFamilySymbol;
                }
            }
        }

        private void SetCutCubicSolidSizeAndRotations(CutInstance cutIns)
        {
            XYZ rotatedPoint = cutIns.OriginCutSolid.ComputeCentroid();
            
            List<PlanarFace[]> parallelFacesTwain = Tools.GetSolidParallelFaces(cutIns.OriginCutSolid);
            
            // Solid尺寸获取
            List<Line> CrossCentroidVToFacesLines = new List<Line>();
            foreach (var planarFaces in parallelFacesTwain)
            {   
                XYZ projectPointOne = planarFaces[0].Project(rotatedPoint).XYZPoint;
                XYZ projectPointTwo = planarFaces[1].Project(rotatedPoint).XYZPoint;
                Line LineCrossCentroid;
                try
                {
                    LineCrossCentroid = Line.CreateBound(projectPointOne, projectPointTwo);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    cutIns.OriginCutSolid = null;
                    return;
                }
                if (LineCrossCentroid.Length < Tools.ToFeet(1))
                {
                    cutIns.OriginCutSolid = null;
                    return;
                }
                CrossCentroidVToFacesLines.Add(LineCrossCentroid);
            }
            
            // 设置空心剪切的长宽高
            cutIns.Length = CrossCentroidVToFacesLines[0].Length;
            cutIns.Width = CrossCentroidVToFacesLines[1].Length;
            cutIns.Height = CrossCentroidVToFacesLines[2].Length;
            
            // 存在平行于XY平面则记录旋转轴和角度
            
            // 过滤掉不存在旋转的相交Solid
            // 构建质心重合且无偏转的Solid的两条轴心线段（朝向顶面的和朝向右面的）
            Line xForwardLine = Line.CreateBound(rotatedPoint, rotatedPoint + new XYZ(cutIns.Length / 2, 0, 0));
            Line zForwardLine = Line.CreateBound(rotatedPoint, rotatedPoint + new XYZ(0, 0, cutIns.Height / 2));

            double XForwardFaceArea = cutIns.Width * cutIns.Height;
            double ZForwardFaceArea = cutIns.Length * cutIns.Width;
            
            // 找到实际相交Solid中任意一个面积等于XForwardFaceArea和ZForwardFaceArea的面作为对应无旋转Solid右面和顶面的面
            PlanarFace originTopFace = null;
            PlanarFace originRightFace = null;
            foreach (var planarFaces in parallelFacesTwain)
            {
                if (Math.Abs(planarFaces[0].Area - ZForwardFaceArea) < 0.000001)
                {
                    originTopFace = planarFaces[0];
                    continue;
                }

                if (Math.Abs(planarFaces[0].Area - XForwardFaceArea) < 0.000001)
                {
                    originRightFace = planarFaces[0];
                }
            }

            bool isTopFaceDirectionOverLap = originTopFace.FaceNormal == zForwardLine.Direction ||
                                             originTopFace.FaceNormal == -zForwardLine.Direction;
            bool isRightFaceDirectionOverLap = originRightFace.FaceNormal == xForwardLine.Direction ||
                                               originRightFace.FaceNormal == -xForwardLine.Direction;
            
            // 如果相交体和虚拟无旋转solid有两个面的方向都一致了，说明相交体本身就是没有旋转的
            if (isTopFaceDirectionOverLap && isRightFaceDirectionOverLap)
            {
                return;
            }
            
            // 相交体存在旋转
            // 构造一个虚拟无旋转的Solid
            // 底面轮廓线Curveloop
            XYZ buttomFaceCenterPoint = rotatedPoint - new XYZ(0, 0, cutIns.Height / 2);
            XYZ buttomLeftPoint = buttomFaceCenterPoint - new XYZ(cutIns.Length / 2, cutIns.Width / 2, 0);
            XYZ topLeftPoint = buttomLeftPoint + new XYZ(0, cutIns.Width, 0);
            XYZ topRightPoint = topLeftPoint + new XYZ(cutIns.Length, 0, 0);
            XYZ buttomRightPoint = buttomLeftPoint + new XYZ(cutIns.Length, 0, 0);

            IList<Curve> buttomCurves = new List<Curve>();
            buttomCurves.Add(Line.CreateBound(buttomLeftPoint, topLeftPoint));
            buttomCurves.Add(Line.CreateBound(topLeftPoint, topRightPoint));
            buttomCurves.Add(Line.CreateBound(topRightPoint, buttomRightPoint));
            buttomCurves.Add(Line.CreateBound(buttomRightPoint, buttomLeftPoint));

            Solid rotationFreeSolid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>(){CurveLoop.Create(buttomCurves)}, new XYZ(0, 0, 1),
                cutIns.Height);
            
            // 获取质心与质心投影到面上点的线段
            Line originXForwardLine = Line.CreateBound(rotatedPoint, rotatedPoint + originRightFace.FaceNormal) ?? throw new ArgumentNullException("Line.CreateBound(rotatedPoint, originRightFace.Project(rotatedPoint).XYZPoint)");
            Line originZForwardLine = Line.CreateBound(rotatedPoint, rotatedPoint + originTopFace.FaceNormal) ?? throw new ArgumentNullException("Line.CreateBound(rotatedPoint, originTopFace.Project(rotatedPoint).XYZPoint)");
            
            if (originXForwardLine.Direction.CrossProduct(xForwardLine.Direction).IsZeroLength())
            {
                cutIns.Rotations.Add(null, 0);
            }
            else
            {
                cutIns.Rotations.Add(Line.CreateBound(rotatedPoint, rotatedPoint + xForwardLine.Direction.CrossProduct(originXForwardLine.Direction)), xForwardLine.Direction.AngleTo(originXForwardLine.Direction));
            }
            Transform firstTransform = Transform.CreateRotationAtPoint(xForwardLine.Direction.CrossProduct(originXForwardLine.Direction),
                xForwardLine.Direction.AngleTo(originXForwardLine.Direction), rotatedPoint);
            Solid tempSolid = SolidUtils.CreateTransformed(rotationFreeSolid, firstTransform);
            List<PlanarFace[]> tempParallelFacesTwain = Tools.GetSolidParallelFaces(tempSolid);
            PlanarFace tempOriginTopFace = null;
            foreach (var planarFaces in tempParallelFacesTwain)
            {
                if (Math.Abs(planarFaces[0].Area - ZForwardFaceArea) < 0.000001)
                {
                    tempOriginTopFace = planarFaces[0];
                    break;
                }
            }

            if (originZForwardLine.Direction.CrossProduct(tempOriginTopFace.FaceNormal).IsZeroLength())
            {
                cutIns.Rotations.Add(null, 0);
            }
            else
            {
                cutIns.Rotations.Add(originXForwardLine, tempOriginTopFace.FaceNormal.AngleTo(originZForwardLine.Direction));
            }
        }

        private bool ConfirmSolidIsFitShareFamilySymbol(CutInstance cutIns)
        {
            // 判定每一个面是否为长方形
            IEnumerator facesEnumerator = cutIns.OriginCutSolid.Faces.GetEnumerator();
            while (facesEnumerator.MoveNext())
            {
                PlanarFace face = facesEnumerator.Current as PlanarFace;
                if (!face.GetEdgesAsCurveLoops()[0].IsRectangular(face.GetSurface() as Plane))
                {
                    return false;
                }
            }
            List<PlanarFace[]> parallelFacesTwain = Tools.GetSolidParallelFaces(cutIns.OriginCutSolid);
            
            if (parallelFacesTwain.Count != 3)
                return false;

            XYZ rotatedPoint = cutIns.OriginCutSolid.ComputeCentroid();
            
            // Solid尺寸获取
            PlanarFace[] topAndButtomFaces = null;
            for (int i = 0; i < parallelFacesTwain.Count; i++)
            {
                XYZ projectPointOne = parallelFacesTwain[i][0].Project(rotatedPoint).XYZPoint;
                XYZ projectPointTwo = parallelFacesTwain[i][1].Project(rotatedPoint).XYZPoint;
                Line LineCrossCentroid;
                try
                {
                    LineCrossCentroid = Line.CreateBound(projectPointOne, projectPointTwo);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    cutIns.OriginCutSolid = null;
                    return false;
                }
                if (LineCrossCentroid.Length < Tools.ToFeet(1))
                {
                    cutIns.OriginCutSolid = null;
                    return false;
                }
                // 设置空心剪切的长宽高
                if (LineCrossCentroid.Direction.IsAlmostEqualTo(new XYZ(0, 0, 1)) || LineCrossCentroid.Direction.IsAlmostEqualTo(new XYZ(0, 0, -1)))
                {
                    topAndButtomFaces = parallelFacesTwain[i];
                    cutIns.Height = LineCrossCentroid.Length;
                }
                else
                {
                    if (cutIns.Length != 0)
                    {
                        if (cutIns.Length < LineCrossCentroid.Length)
                        {
                            cutIns.Width = cutIns.Length;
                            cutIns.Length = LineCrossCentroid.Length;
                        }
                        else
                        {
                            cutIns.Width = LineCrossCentroid.Length;
                        }
                    }
                    else
                    {
                        cutIns.Length = LineCrossCentroid.Length;
                    }
                }
            }
            
            if (topAndButtomFaces == null)
            {
                return false;
            }
            
            // 过滤掉不存在旋转的相交Solid
            // 构建质心重合且无偏转的Solid的两条轴心线段（朝向顶面的和朝向右面的）
            Line xForwardLine = Line.CreateBound(rotatedPoint, rotatedPoint + new XYZ(cutIns.Length / 2, 0, 0));

            double XForwardFaceArea = cutIns.Width * cutIns.Height;

            // 找到实际相交Solid中任意一个面积等于XForwardFaceArea和ZForwardFaceArea的面作为对应无旋转Solid右面和顶面的面
            PlanarFace originRightFace = null;
            foreach (var planarFaces in parallelFacesTwain)
            {
                if (Math.Abs(planarFaces[0].Area - XForwardFaceArea) < 0.000001 && !planarFaces[0].FaceNormal.IsAlmostEqualTo(new XYZ(0, 0, 1)))
                {
                    originRightFace = planarFaces[0];
                    break;
                }
            }

            bool isRightFaceDirectionOverLap = originRightFace.FaceNormal.IsAlmostEqualTo(xForwardLine.Direction) ||
                                               originRightFace.FaceNormal.IsAlmostEqualTo(-xForwardLine.Direction);
            
            // 如果相交体和虚拟无旋转solid有两个面的方向都一致了，说明相交体本身就是围绕Z轴没有旋转的
            if (isRightFaceDirectionOverLap)
            {
                return true;
            }
            
            Line originXForwardLine = Line.CreateBound(rotatedPoint, rotatedPoint + originRightFace.FaceNormal) ?? throw new ArgumentNullException("Line.CreateBound(rotatedPoint, originRightFace.Project(rotatedPoint).XYZPoint)");
            
            if (!originXForwardLine.Direction.CrossProduct(xForwardLine.Direction).IsZeroLength())
            {
                cutIns.Rotations.Add(Line.CreateBound(rotatedPoint, rotatedPoint + new XYZ(0, 0, 10)), xForwardLine.Direction.AngleTo(originXForwardLine.Direction));
            }
            
            return true;
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
                        if (pendingEleHostDocCutLevelNum == interPendingEleHostDocCutLevelNum)
                            ClassifyReadyToCreateSolid(intersectPendingElement, pendingElement, AssociationTypes.Collide);
                        else if (pendingEleHostDocCutLevelNum > interPendingEleHostDocCutLevelNum)
                            ClassifyReadyToCreateSolid(intersectPendingElement, pendingElement, AssociationTypes.Cut);
                    }
                    else
                    {
                        // 如果没有joined
                        if (pendingEleCutLevelNum > interPendingEleCutLevelNum) ClassifyReadyToCreateSolid(intersectPendingElement, pendingElement, AssociationTypes.Cut);
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
            PendingElement cuttedPendingElement, AssociationTypes specifiedRelation)
        {
            var intersectSolid = GetIntersectSolid(cuttingPendingElement, cuttedPendingElement);
            if (intersectSolid != null)
            {
                CutInstance cutIns = new CutInstance(intersectSolid);
                cutIns.Relation = intersectSolid.Volume != 0 ? specifiedRelation != AssociationTypes.Collide ? specifiedRelation : AssociationTypes.Collide : AssociationTypes.Join;
                cutIns.IntactElement = cuttingPendingElement;
                cutIns.BeCutElement = cuttedPendingElement;
                // cutIns.OriginCutSolid = intersectSolid;
                cutInstances.Add(cutIns);
            }
            // else
            // {
            //     if (cuttedPendingElement.element.Document.Equals(cuttingPendingElement.element.Document) && JoinGeometryUtils.AreElementsJoined(cuttedPendingElement.element.Document, cuttedPendingElement.element, cuttingPendingElement.element))
            //     {
            //         // 处理join状态下的其他相交pendingElements与当前相交pendingElement的扣减部分
            //         foreach (var intersectEle in cuttedPendingElement.IntersectEles)
            //         {
            //             if (intersectEle.element.Id != cuttingPendingElement.element.Id)
            //             {
            //                 if (JoinGeometryUtils.IsCuttingElementInJoin(cuttedPendingElement.element.Document, cuttedPendingElement.element, cuttingPendingElement.element))
            //                 {
            //                     if (Tools.GetArchMainSolid(cuttingPendingElement.element, cuttingPendingElement.TransformInWCS) == null || Tools.GetArchMainSolid(intersectEle.element, intersectEle.TransformInWCS) == null)
            //                     {
            //                         continue;
            //                     }
            //                     Solid joinedElementsIntersectSolid = null;
            //                     using (Transaction tran = new Transaction(_activeDoc, "CreateCutInstanceInActiveDoc333"))
            //                     {
            //                         tran.Start();
            //                         // 解除join状态，获得实际相交部分solid
            //                         JoinGeometryUtils.UnjoinGeometry(cuttedPendingElement.element.Document,
            //                             cuttedPendingElement.element, cuttingPendingElement.element);
            //                         _activeDoc.Regenerate();
            //                         
            //                         joinedElementsIntersectSolid = BooleanOperationsUtils.ExecuteBooleanOperation(
            //                                 Tools.GetArchMainSolid(cuttingPendingElement.element,
            //                                     cuttingPendingElement.TransformInWCS),
            //                                 Tools.GetArchMainSolid(intersectEle.element, intersectEle.TransformInWCS),
            //                                 BooleanOperationsType.Intersect);
            //
            //                         JoinGeometryUtils.JoinGeometry(cuttedPendingElement.element.Document,
            //                             cuttedPendingElement.element, cuttingPendingElement.element);
            //                         tran.Commit();
            //                     }
            //
            //                     if (joinedElementsIntersectSolid != null && joinedElementsIntersectSolid.Volume != 0)
            //                     {
            //                         CutInstance cutjoinIns = new CutInstance(joinedElementsIntersectSolid);
            //                         cutjoinIns.Relation = AssociationTypes.Join;
            //                         cutjoinIns.IntactElement = null;
            //                         cutjoinIns.BeCutElement = cuttingPendingElement;
            //                         cutInstances.Add(cutjoinIns);
            //                     }
            //                 }
            //             }
            //         }
            //     }
            // }
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
            if (cuttingSolid == null || cuttingSolid.Volume == 0 || cuttedSolid == null || cuttedSolid.Volume == 0)
            {
                return null;
            }
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