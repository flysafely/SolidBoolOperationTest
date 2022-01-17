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
using Autodesk.Revit.ApplicationServices;
using CommonTools;

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
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming
        };
        Dictionary<BuiltInCategory, int> CutPolicy = new Dictionary<BuiltInCategory, int>
        {
            {BuiltInCategory.OST_Walls, (int) CutOrder.Level10},
            {BuiltInCategory.OST_Floors, (int) CutOrder.Level3},
            {BuiltInCategory.OST_Columns, (int) CutOrder.Level1},
            {BuiltInCategory.OST_StructuralColumns, (int) CutOrder.Level1},
            {BuiltInCategory.OST_StructuralFraming, (int) CutOrder.Level2}
        };
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument activeUiDoc = commandData.Application.ActiveUIDocument;
            Document activeDoc = activeUiDoc.Document;
            Application activeApp = commandData.Application.Application;

            
            var refs = activeUiDoc.Selection.PickObjects(ObjectType.Element, new ElementsSelectionFilter());
            var compositeElementsClassifier = new CompositeElementsClassifier(activeDoc, refs, targetCategories);
            var intersectResults = compositeElementsClassifier.GetExistIntersectElements();
            var cutProcess = new CutProcess(activeApp, activeDoc, CutPolicy);
            cutProcess.ImplementIntersectElementsCutPolicy(intersectResults);
            
            foreach (var intersectSolid in cutProcess.intersectSolids.Where(e => (e["Document"] as Document).Equals(activeDoc)).ToList())
            {
                if (intersectSolid != null)
                {   
                    FamilySymbol cutSolidFamilySymbol = Tools.CreateFamilySymbol(activeDoc, activeApp, intersectSolid["IntersectSolid"] as Solid);
                    using (Transaction tran = new Transaction(activeDoc, "createNewFamilyInstance"))
                    {
                        tran.Start();
                        FamilyInstance familyInstance = activeDoc.Create.NewFamilyInstance((intersectSolid["IntersectSolid"] as Solid).ComputeCentroid(), cutSolidFamilySymbol, (intersectSolid["HostCuttedPendingElement"] as PendingElement).element, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        InstanceVoidCutUtils.AddInstanceVoidCut(activeDoc, (intersectSolid["HostCuttedPendingElement"] as PendingElement).element, familyInstance);
                        tran.Commit();
                    }
                }
            }
            
            TaskDialog.Show("note", cutProcess.intersectSolids.Count.ToString());
            
            return Result.Succeeded;
        }
    }
}