//using System;

using System;
using System.Collections.Generic;
using System.Linq;
//using System.Diagnostics;
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
        IList<object> targetCategories = new List<object>()
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_StructuralFraming
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument activeUiDoc = commandData.Application.ActiveUIDocument;
            Document activeDoc = activeUiDoc.Document;

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
            
            var refs = activeUiDoc.Selection.PickObjects(ObjectType.Element, new ElementsSelectionFilter());
             var compositeElementsClassifier = new CompositeElementsClassifier(activeDoc, refs, targetCategories);
             var results = compositeElementsClassifier.GetExistIntersectElements();
             TaskDialog.Show("Notes", results.Count.ToString());
             // 柱子实例获取
            // FamilyInstance column = activeDoc.GetElement(new ElementId(532721)) as FamilyInstance;
            // //Wall wall = activeDoc.GetElement(new ElementId(530413)) as Wall;
            // var revitlink = activeDoc.GetElement(new ElementId(537047));
            // var linkinstance = revitlink as RevitLinkInstance;
            // var linkDoc = linkinstance?.GetLinkDocument();
            // GeometryElement columnGeoElement = column.get_Geometry(new Options());
            // Solid columnSolid = null;
            // Solid newSolid = null;
            // foreach (GeometryObject geometryObject in columnGeoElement)
            // {
            //     if (geometryObject is Solid)
            //     {
            //         columnSolid = (Solid)geometryObject;
            //         newSolid = SolidUtils.CreateTransformed(columnSolid, linkinstance.GetTransform().Inverse);
            //         break;
            //     }
            // }
            //
            //
            // var transform = newSolid.GetBoundingBox().Transform;
            //
            // var minSolid = newSolid.GetBoundingBox().Min;
            // var maxSolid = newSolid.GetBoundingBox().Max;
            //
            // var acturalMin = transform.OfPoint(minSolid);
            // var acturalMax = transform.OfPoint(maxSolid);
            //
            // var outline = new Outline(acturalMin, acturalMax);
            //
            // var boxFilter = new BoundingBoxIntersectsFilter(outline);
            //
            // var collector = new FilteredElementCollector(linkDoc);
            // var intersectElements = collector
            //         .WherePasses(boxFilter)
            //         .ToList();
            // foreach (var item in collector)
            // {
            //     TaskDialog.Show("note" , item.Id.ToString());
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