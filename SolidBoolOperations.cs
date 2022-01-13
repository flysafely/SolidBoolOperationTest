using System;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;

namespace SolidBoolOperationTest
{
    [Transaction(TransactionMode.Manual)]
    public class SolidBoolOperations : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument activeUIDoc = commandData.Application.ActiveUIDocument;
            Document activeDoc = activeUIDoc.Document;

            // FilteredElementCollector linkInstances = new FilteredElementCollector(activeDoc);
            // linkInstances = linkInstances.WherePasses(new ElementClassFilter(typeof(RevitLinkInstance)));
            // Document linkDoc = null;
            // if (linkInstances != null)
            // {
            //     foreach (RevitLinkInstance linkIns in linkInstances)
            //     {
            //         linkDoc = linkIns.GetLinkDocument();
            //         break;
            //     }
            // }
            IList<Reference> refs =
                activeUIDoc.Selection.PickObjects(ObjectType.Element, new ElementsSelectionFilter());
            CompsiteElementsClassifier compsiteElementsClassifier = new CompsiteElementsClassifier(activeDoc, refs);
            var results = compsiteElementsClassifier.GetElementsDictionary();
            TaskDialog.Show("Notes",
                string.Format("当前文档中-墙数量:{0}个;/n板数量:{1}个;/n柱数量:{2}个;/n梁数量:{3}",
                    results[BuiltInCategory.OST_Walls.ToString()].Count.ToString(),
                    results[BuiltInCategory.OST_Floors.ToString()].Count.ToString(),
                    results[BuiltInCategory.OST_Columns.ToString()].Count.ToString(),
                    results[BuiltInCategory.OST_StructuralFraming.ToString()].Count.ToString())
            );
            // 柱子实例获取
            // FamilyInstance column = activeDoc.GetElement(new ElementId(532721)) as FamilyInstance;
            // //Wall wall = activeDoc.GetElement(new ElementId(530413)) as Wall;
            //
            // GeometryElement columnGeoElement = column.get_Geometry(new Options());
            // Solid columnSolid = null; ;
            // foreach (GeometryObject geometryObject in columnGeoElement)
            // {
            //     if (geometryObject is Solid)
            //     {
            //         columnSolid = (Solid)geometryObject;
            //         break;
            //     }
            // }

            //GeometryElement wallGeoElement = wall.get_Geometry(new Options());
            //Solid wallSolid = null;
            //foreach (GeometryObject geometryObject in wallGeoElement)
            //{
            //    if (geometryObject is Solid)
            //    {
            //        wallSolid = (Solid)geometryObject;
            //        break;
            //    }
            //}
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            // // 相交过滤器
            // FilteredElementCollector collector = new FilteredElementCollector(activeDoc);
            // ElementIntersectsSolidFilter solidFilter = new ElementIntersectsSolidFilter(columnSolid);
            //
            // collector.WherePasses(solidFilter);
            // stopwatch.Stop();
            // TaskDialog.Show("note", stopwatch.ElapsedMilliseconds.ToString());
            // // Add these interseting element to the selection
            // foreach (Element elem in collector)
            // {
            //     TaskDialog.Show("note", (elem.GetType() == typeof(Wall)).ToString());
            // }


            //using (Transaction tran = new Transaction(activeDoc, "default"))
            //{
            //    tran.Start();
            //    DeductionOperation(activeDoc, column, wall);
            //    SolidByUnion(new List<Solid> { columnSolid, wallSolid});
            //    tran.Commit();
            //}

            return Result.Succeeded;
        }
    }
}