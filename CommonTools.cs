using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SolidBoolOperationTest;

namespace CommonTools
{
    public static class Tools
    {
        public static double ToFeet(this double value)
        {
            double foot = UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
            return foot;
        }

        public static double ToMillimeters(this double value)
        {
            double millimeter = UnitUtils.Convert(value, UnitTypeId.Feet, UnitTypeId.Millimeters);
            return millimeter;
        }

        public static int StrToInt(this string text)
        {
            int intValue = 0;
            var result = int.TryParse(text, out intValue);

            if (!result)
            {
                intValue = 100;
            }

            return intValue;
        }

        public static byte StrToByte(this string text)
        {
            byte byteValue = 0;
            var result = byte.TryParse(text, out byteValue);

            if (!result)
            {
                byteValue = 0;
            }

            return byteValue;
        }

        /// <summary>
        /// 获取直线交点(两端无限长)
        /// </summary>
        /// <param name="curveOne">第一条直线</param>
        /// <param name="curveTwo">第二条直线</param>
        /// <returns></returns>
        public static XYZ GetBothSideUnboundLinesIntersectPoint(Curve curveOne, Curve curveTwo)
        {
            Curve curveOneStartUnbound = Line.CreateUnbound(curveOne.GetEndPoint(0), (curveOne as Line).Direction);
            Curve curveOneEndUnbound = Line.CreateUnbound(curveOne.GetEndPoint(1), (curveOne as Line).Direction);
            Curve curveTwoStartUnbound = Line.CreateUnbound(curveTwo.GetEndPoint(0), (curveTwo as Line).Direction);
            Curve curveTwoEndUnbound = Line.CreateUnbound(curveTwo.GetEndPoint(1), (curveTwo as Line).Direction);

            if (GetUnboundLinesIntersectPoint(curveOneStartUnbound, curveTwoStartUnbound) != null)
            {
                return GetUnboundLinesIntersectPoint(curveOneStartUnbound, curveTwoStartUnbound);
            }

            if (GetUnboundLinesIntersectPoint(curveOneStartUnbound, curveTwoEndUnbound) != null)
            {
                return GetUnboundLinesIntersectPoint(curveOneStartUnbound, curveTwoEndUnbound);
            }

            if (GetUnboundLinesIntersectPoint(curveOneEndUnbound, curveTwoStartUnbound) != null)
            {
                return GetUnboundLinesIntersectPoint(curveOneEndUnbound, curveTwoStartUnbound);
            }

            if (GetUnboundLinesIntersectPoint(curveOneEndUnbound, curveTwoEndUnbound) != null)
            {
                return GetUnboundLinesIntersectPoint(curveOneEndUnbound, curveTwoEndUnbound);
            }

            return null;
        }

        public static XYZ GetUnboundLinesIntersectPoint(Curve curveOne, Curve curveTwo)
        {
            IntersectionResultArray resultArray = null;
            SetComparisonResult result = curveOne.Intersect(curveTwo, out resultArray);

            //获取相交点
            XYZ point = null;

            if (result.Equals(SetComparisonResult.Overlap))
            {
                foreach (IntersectionResult item in resultArray)
                {
                    point = item.XYZPoint;
                }
            }

            return point;
        }
        // private void GetAndTransformSolidInfo(Application application, Element element, Options geoOptions)
        // {
        //     // 获取所选元素的几何元素
        //     GeometryElement geoElement = element.get_Geometry(geoOptions);
        //
        //     // 获取 geometry object
        //     foreach (GeometryObject geoObject in geoElement)
        //     {
        //         // 获取包含几何信息的几何实例（筛选出非空的）
        //         GeometryInstance instance = geoObject as GeometryInstance;
        //         if (null != instance)
        //         {
        //             foreach (GeometryObject instObj in instance.SymbolGeometry)
        //             {
        //                 Solid solid = instObj as Solid;
        //                 //将空实体，没有面也没有边的实体剔除
        //                 if (null == solid || 0 == solid.Faces.Size || 0 == solid.Edges.Size)
        //                 {
        //                     continue;
        //                 }
        //
        //                 Transform instTransform = instance.Transform;
        //                 // 从实体获取面转换形成的点
        //                 foreach (Face face in solid.Faces)
        //                 {
        //                     Mesh mesh = face.Triangulate();
        //                     foreach (XYZ i in mesh.Vertices)
        //                     {
        //                         XYZ point = i;
        //                         XYZ transformedPoint1 = instTransform.OfPoint(point);
        //                         //此处可以插入自己需要的方法
        //                     }
        //                 }
        //                 // 从实体获取边转换形成的点
        //                 foreach (Edge edge in solid.Edges)
        //                 {
        //                     foreach (XYZ i in edge.Tessellate())
        //                     {
        //                         XYZ point = i;
        //                         XYZ transformedPoint2 = instTransform.OfPoint(point);
        //                         //此处可以插入自己需要的方法
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }

        /*public static bool VerifyIntersectionByDistance(double threshold, SysSurface surface, Curve surfaceOneSideLine)
        {
            XYZ intersection = ConvertTools.GetBothSideUnboundLinesIntersectPoint(surface.pathLine, surfaceOneSideLine);
            //获取两条线是否有交点，用贴面绘制中线与其他贴面的外部线进行运算
            if (intersection == null)
            {
                return false;
            }
            //判断交点位置离端点的最近距离是否太远
            XYZ startPoint = surface.pathLine.GetEndPoint(0);
            XYZ endPoint = surface.pathLine.GetEndPoint(1);
            double toStartDistance = intersection.DistanceTo(startPoint);
            double toEndDistance = intersection.DistanceTo(endPoint);

            if (ConvertTools.ToFeet(threshold) < Math.Min(toEndDistance, toStartDistance))
            {
                return false;
            }

            if (toStartDistance > toEndDistance)
            {
                surface.pathLine = Line.CreateBound(startPoint, intersection);
                return true;
            }
            else
            {
                surface.pathLine = Line.CreateBound(intersection, endPoint);
                return true;
            }
        }*/

        public static Family CreateFreeFormElementFamily(Document doc, Application app, Solid solid, bool isSolid)
        {
            try
            {
                string familyTemplateFilePath = Path.Combine(app.FamilyTemplatePath, "公制常规模型.rft");
                //var basePath = $"{ProductPath.FamilyPath}\\{rvtVersion}";
                //string filePath = @"C:\ProgramData\Autodesk\RVT 2021\Family Templates\Chinese\公制常规模型.rft";
                Document familyDoc;
                if (File.Exists(familyTemplateFilePath))
                {
                    //创建族文档
                    familyDoc = doc.Application.NewFamilyDocument(familyTemplateFilePath);
                }
                else
                {
                    return null;
                }

                //使用族文档作为参数来开启事务
                // 事务传入的Document应该是familyDoc而不是传入的参数doc
                using (Transaction tran = new Transaction(familyDoc, "空心族创建"))
                {
                    var transform = Transform.CreateTranslation(-solid.ComputeCentroid());
                    var newSolid = SolidUtils.CreateTransformed(solid, transform);
                    tran.Start();
                    FreeFormElement freeForm = FreeFormElement.Create(familyDoc, newSolid);
                    if (!isSolid)
                    {
                        //空心剪切
                        familyDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS)?.Set(1);
                        freeForm.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING)?.Set(1);
                    }
                    tran.Commit();
                }

                Family family = familyDoc.LoadFamily(doc, new FamilyLoadOptions());
                familyDoc.Close(false);
                return family;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
            }
        }

        public static Family LoadFamily(Document doc, string familyName)
        {
            var family = new FilteredElementCollector(doc).OfClass(typeof(Family)).Select(p => p as Family)
                .FirstOrDefault(p => p.Name == familyName);
            string familyPath =
                string.Format("C:\\ProgramData\\Autodesk\\RVT 2021\\Libraries\\Chinese\\结构\\钢筋形状\\{0}.rfa", familyName);
            if (family == null)
            {
                // 重新载入
                /*                var paths = Directory.GetFiles(familyPath, familyName + ".rfa", SearchOption.AllDirectories);
                                if (paths.Count() == 0)
                                    return null;
                                var path = paths.First();*/
                bool loadResult = doc.LoadFamily(familyPath, new FamilyLoadOptions(), out family);
                if (!loadResult)
                    throw new Exception("族加载失败!");
                foreach (FamilySymbol fs in family.GetFamilySymbolIds().Select(p => doc.GetElement(p)))
                {
                    fs.Activate();
                }
            }

            return family;
        }

        public static Family LoadFamilyByFamilyName(Application app, Document loadingDoc, string familyName)
        {   
            // 获取rfa文件名称
            string familyFilePath = Path.Combine(app.FamilyTemplatePath, string.Format("{0}.rfa", familyName));
            // 判断当前文档中是否已经加载过该族
            var family = new FilteredElementCollector(loadingDoc).OfClass(typeof(Family)).Select(p => p as Family)
                .FirstOrDefault(p => p.Name == familyName);
            if (family == null)
            {   
                bool loadResult = false;
                using (Transaction tran = new Transaction(loadingDoc, "空心族创建"))
                {   
                    tran.Start();
                    loadResult = loadingDoc.LoadFamily(familyFilePath, new FamilyLoadOptions(), out family);
                    tran.Commit();
                }

                if (!loadResult)
                {
                    throw new Exception("族加载失败!");
                }
                //
                // foreach (FamilySymbol fs in family.GetFamilySymbolIds().Select(p => loadingDoc.GetElement(p)))
                // {
                //     fs.Activate();
                // }
            }
            return family;
        }
        /// <summary>
        /// 创建自定义预制厚度(全厚度)叠合板
        /// </summary>
        /// <param name="activeDoc">打开的文档</param>
        /// <param name="points">顶面顶点列表</param>
        /// <param name="level">标高</param>
        /// <param name="precastThickness">预制厚度</param>
        /// <param name="thickness">叠合板全厚度</param>
        public static ElementId CreateCustomFloorslab(Document activeDoc, IList<XYZ> points, Level level,
            double precastThickness, double thickness)
        {
            // 构建楼板顶面轮廓线数组
            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(points[0], points[1]));
            curveArray.Append(Line.CreateBound(points[1], points[2]));
            curveArray.Append(Line.CreateBound(points[2], points[3]));
            curveArray.Append(Line.CreateBound(points[3], points[0]));

            // 创建楼板实体
            Floor floorslab =
                activeDoc.Create.NewSlab(curveArray, level, Line.CreateBound(points[0], points[1]), 0, false);
            /*FamilyInstance familyInstance = activeDoc.Create.NewFamilyInstance();
            InstanceVoidCutUtils.AddInstanceVoidCut(activeDoc, floorslab, floorslab);*/
            // 获取文档中已经存在的楼板类型
            FilteredElementCollector elements = new FilteredElementCollector(activeDoc);
            List<FloorType> floorTypes = elements.OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
            // 获取样板楼板类型实例(默认取第一个即可)
            FloorType defaultFloorType = floorTypes[0];
            // 楼板类型名称
            string typeName = string.Format("预制叠合板 - {0}({1})mm", precastThickness.ToString(), thickness.ToString());
            // 遍历判断是否存在特定厚度的楼板类型
            foreach (FloorType floorType in floorTypes)
            {
                if (typeName.Equals(floorType.Name))
                {
                    floorslab.FloorType = floorType;
                    return floorslab.Id;
                }
            }

            // 如果不存在特定厚度的楼板类型则创建新的楼板类型
            FloorType newFloorType = defaultFloorType.Duplicate(typeName) as FloorType;
            CompoundStructure cs = newFloorType.GetCompoundStructure();
            IList<CompoundStructureLayer> csls = cs.GetLayers();
            foreach (CompoundStructureLayer layer in csls)
            {
                if (layer.Function == MaterialFunctionAssignment.Structure)
                {
                    layer.Width = Tools.ToFeet(precastThickness);
                    break;
                }
            }

            cs.SetLayers(csls);
            newFloorType.SetCompoundStructure(cs);
            floorslab.FloorType = newFloorType;
            return floorslab.Id;
        }

        /// <summary>
        /// 设置叠合板的类型和厚度信息
        /// </summary>
        /// <param name="doc">活动文档</param>
        /// <param name="compositeSlab">叠合板实例</param>
        // public static void SetFloorSlabTypeInfo(Document doc, CompositeSlab compositeSlab)
        // {
        //     // 获取文档中已经存在的楼板类型
        //     FilteredElementCollector elements = new FilteredElementCollector(doc);
        //     List<FloorType> floorTypes = elements.OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
        //     // 获取样板楼板类型实例(默认取第一个即可)
        //     FloorType defaultFloorType = floorTypes[0];
        //     // 楼板类型名称
        //     string typeName = string.Format("预制叠合板 - {0}({1})mm", compositeSlab.precastThickness.ToString(), compositeSlab.wholeThickness.ToString());
        //     // 遍历判断是否存在特定厚度的楼板类型
        //     foreach (FloorType floorType in floorTypes)
        //     {
        //         if (typeName.Equals(floorType.Name))
        //         {
        //             compositeSlab.mainFloorSlab.FloorType = floorType;
        //             return;
        //         }
        //     }
        //     // 如果不存在特定厚度的楼板类型则创建新的楼板类型
        //     FloorType newFloorType = defaultFloorType.Duplicate(typeName) as FloorType;
        //     CompoundStructure cs = newFloorType.GetCompoundStructure();
        //     IList<CompoundStructureLayer> csls = cs.GetLayers();
        //     foreach (CompoundStructureLayer layer in csls)
        //     {
        //         if (layer.Function == MaterialFunctionAssignment.Structure)
        //         {
        //             layer.Width = Tools.ToFeet(compositeSlab.precastThickness);
        //             break;
        //         }
        //     }
        //     cs.SetLayers(csls);
        //     newFloorType.SetCompoundStructure(cs);
        //     compositeSlab.mainFloorSlab.FloorType = newFloorType;
        // }
        public static Level GetLevel(Document activeDoc, double levelValue)
        {
            FilteredElementCollector collector = new FilteredElementCollector(activeDoc);
            ICollection<Element> collections = collector.OfClass(typeof(Level)).ToElements();
            foreach (Element ele in collections)
            {
                Level level = (Level) ele;
                if (level.Elevation == Tools.ToFeet(levelValue))
                {
                    return level;
                }
            }

            using (Transaction tran = new Transaction(activeDoc, "createLevel"))
            {
                tran.Start();
                Level newLevel = Level.Create(activeDoc, Tools.ToFeet(levelValue));
                tran.Commit();
                return newLevel;
            }
        }

        /// <summary>
        /// 将散开的线段，聚类为以收尾相连的为一组的列表，通过迭代分析
        /// </summary>
        /// <param name="curves">线段列表</param>
        /// <returns></returns>
        public static IList<CurveLoop> GetCurveLoopsByCurves(IList<Curve> curves)
        {
            IList<Curve> unused_curves = new List<Curve>();
            if (curves.Count() == 0)
            {
                return null;
            }

            IList<CurveLoop> resultCurveLoops = new List<CurveLoop>();
            IList<Curve> curveList = new List<Curve>();

            foreach (Curve curve in curves)
            {
                if (curveList.Count() == 0)
                {
                    curveList.Add(curve);
                    continue;
                }

                XYZ curveStartPoint = curve.GetEndPoint(0);
                XYZ curveEndPoint = curve.GetEndPoint(1);

                if (curveStartPoint.IsAlmostEqualTo(curveList.Last().GetEndPoint(1)))
                {
                    curveList.Add(curve);
                    continue;
                }

                if (curveEndPoint.IsAlmostEqualTo(curveList[0].GetEndPoint(0)))
                {
                    curveList.Insert(0, curve);
                    continue;
                }

                unused_curves.Add(curve);
            }

            CurveLoop curveLoop = new CurveLoop();
            foreach (Curve curve in curveList)
            {
                curveLoop.Append(curve);
            }

            resultCurveLoops.Add(curveLoop);

            if (unused_curves.Count() > 0)
            {
                IList<CurveLoop> curvesFromUnused = GetCurveLoopsByCurves(unused_curves);
                resultCurveLoops = resultCurveLoops.Concat(curvesFromUnused).ToList();
            }

            return resultCurveLoops;
        }

        public static Solid GetArchMainSolid(Element element, Transform transform)
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
                            if (solid != null && solid.Volume != 0 && solid.SurfaceArea != 0)
                            {
                                solids.Add(SolidUtils.CreateTransformed(solid, geometryInstance.Transform));
                                volumes.Add(solid.Volume);
                            }
                        }
                    }

                    Solid solidDirect = geoObj as Solid;
                    if (solidDirect != null && solidDirect.Volume != 0 && solidDirect.SurfaceArea != 0)
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
                    if (solid != null && solid.Volume != 0 && solid.SurfaceArea != 0)
                    {
                        solids.Add(solid);
                        volumes.Add(solid.Volume);
                    }
                }
            }

            // 赛选出体积最大的一个solid作为主Soild
            if (solids.Count < 1)
            {
                return null;
            }

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

        public static FamilySymbol CreateFamilySymbol(Document doc, Application app, Solid cutSolid)
        {
            if (cutSolid == null || cutSolid.Volume == 0)
            {
                return null;
            }
            var hollowStretch = CreateFreeFormElementFamily(doc, app, cutSolid, false);

            var ids = hollowStretch.GetFamilySymbolIds();
            var elementIdEnumerator = ids.GetEnumerator();

            while (elementIdEnumerator.MoveNext())
            {
                var familySymbol = doc.GetElement(elementIdEnumerator.Current) as FamilySymbol;
                if (familySymbol != null)
                {
                    using (Transaction tran = new Transaction(doc, "createNewFamilyInstance"))
                    {
                        tran.Start();
                        if (!familySymbol.IsActive)
                        {
                            familySymbol.Activate();
                        }

                        tran.Commit();
                    }
                }

                return familySymbol;
            }

            return null;
        }

        public static bool ContainProperty(this object instance, string propertyName)
        {
            if (instance != null && !string.IsNullOrEmpty(propertyName))
            {
                PropertyInfo _findedPropertyInfo = instance.GetType().GetProperty(propertyName);
                return _findedPropertyInfo != null;
            }

            return false;
        }

        public static ElementQuickFilter GetBoxFilterBySolid(Solid targetSolid, double toleranceMM)
        {
            BoundingBoxXYZ elementBoundingBox = targetSolid.GetBoundingBox();
            Transform boundingBoxTransform = elementBoundingBox.Transform;
            XYZ minVertex = elementBoundingBox.Min;
            XYZ maxVertex = elementBoundingBox.Max;
            XYZ minVertexInWcs = boundingBoxTransform.OfPoint(minVertex);
            XYZ maxVertexInWcs = boundingBoxTransform.OfPoint(maxVertex);
            Outline outlineInWcs = new Outline(minVertexInWcs, maxVertexInWcs);
            ElementQuickFilter elementBoxFilter = new BoundingBoxIntersectsFilter(outlineInWcs, ToFeet(toleranceMM));
            return elementBoxFilter;
        }

        public static FamilySymbol GetCuttingFamilySymbol(Application activeApp, Document activeDoc, string rfaName)
        {
            Family cuttingFamily = LoadFamilyByFamilyName(activeApp, activeDoc, rfaName);
            ISet<ElementId> ids = cuttingFamily.GetFamilySymbolIds();
            var idsEnumerator = ids.GetEnumerator();

            FamilySymbol familySymbol = null;
            while (idsEnumerator.MoveNext())
            {
                familySymbol = activeDoc.GetElement(idsEnumerator.Current) as FamilySymbol;
                if (familySymbol != null)
                {
                    using (Transaction tran = new Transaction(activeDoc, "createNewFamilyInstance"))
                    {
                        tran.Start();
                        if (!familySymbol.IsActive)
                        {
                            familySymbol.Activate();
                        }
                        tran.Commit();
                    }
                }
            }
            return familySymbol;
        }

        public static List<PlanarFace[]> GetSolidParallelFaces(Solid solid)
        {
            List<PlanarFace[]> parallelFacesTwain = new List<PlanarFace[]>();
            List<int> usedIndexs = new List<int>();
            for (int i = 0; i < solid.Faces.Size; i++)
            {   
                if (usedIndexs.Contains(i))
                    continue;
                for (int j = i + 1; j < solid.Faces.Size; j++)
                {
                    if (usedIndexs.Contains(j))
                        continue;
                    if ((solid.Faces.get_Item(i) as PlanarFace).FaceNormal.IsAlmostEqualTo(-(solid.Faces.get_Item(j) as PlanarFace).FaceNormal))
                    {
                        usedIndexs.Add(i);
                        usedIndexs.Add(j);
                        parallelFacesTwain.Add(new []{solid.Faces.get_Item(i) as PlanarFace, solid.Faces.get_Item(j) as PlanarFace});
                    }
                }
            }
            return parallelFacesTwain;
        }
        public static XYZ GetProjectPoint(Plane plane, XYZ xyz)
        {
            Transform tf = Transform.Identity;
            tf.BasisX = plane.XVec;
            tf.BasisY = plane.YVec;
            tf.BasisZ = plane.Normal;
            tf.Origin = plane.Origin;
            XYZ p = tf.Inverse.OfPoint(xyz);
            p = new XYZ(p.X, p.Y, 0);
            return tf.OfPoint(p);
        }
    }
}