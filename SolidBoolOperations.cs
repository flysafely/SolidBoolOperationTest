using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace SolidBoolOperationTest
{
    [Transaction(TransactionMode.Manual)]
    public class SolidBoolOperations : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument activeUIDoc = commandData.Application.ActiveUIDocument;
            Document activeDoc = activeUIDoc.Document;
            
            // 柱子实例获取
            FamilyInstance column = activeDoc.GetElement(new ElementId(532721)) as FamilyInstance;
            Wall wall = activeDoc.GetElement(new ElementId(530413)) as Wall;

            GeometryElement columnGeoElement = column.get_Geometry(new Options());
            Solid columnSolid = null; ;
            foreach (GeometryObject geometryObject in columnGeoElement)
            {
                if (geometryObject is Solid)
                {
                    columnSolid = (Solid)geometryObject;
                    break;
                }
            }

            GeometryElement wallGeoElement = wall.get_Geometry(new Options());
            Solid wallSolid = null;
            foreach (GeometryObject geometryObject in wallGeoElement)
            {
                if (geometryObject is Solid)
                {
                    wallSolid = (Solid)geometryObject;
                    break;
                }
            }

            using (Transaction tran = new Transaction(activeDoc, "default"))
            {
                tran.Start();
                DeductionOperation(activeDoc, column, wall);
                SolidByUnion(new List<Solid> { columnSolid, wallSolid});
                tran.Commit();
            }

            return Result.Succeeded;
        }
        private Solid SolidByUnion(List<Solid> solids)
        {
            Solid result;
            if (solids.Count > 2)
            {
                Solid solid1 = solids[0];
                solids.RemoveAt(0);
                Solid solid2 = SolidByUnion(solids);
                var intersect = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                if (intersect.Volume > 0)
                {
                    var difference = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect, BooleanOperationsType.Difference);
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2, BooleanOperationsType.Union);
                }
                else
                {
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Union);
                }
                return result;

            }
            else
            {
                Solid solid1 = solids[0];
                Solid solid2 = solids[1];
                var intersect = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                if (intersect.Volume > 0)
                {
                    var difference = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect, BooleanOperationsType.Difference);
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2, BooleanOperationsType.Union);
                }
                else
                {
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Union);
                }
                return result;
            }
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

            }
        }
    }
}
