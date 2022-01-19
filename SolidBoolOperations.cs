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

            Family cuttingFamily = Tools.LoadFamilyByFamilyName(activeApp, activeDoc, "CMCU自定义空心轮廓族");
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

            foreach (var intersectSolid in cutProcess.intersectSolids
                         .Where(e => (e["Document"] as Document).Equals(activeDoc)).ToList())
            {
                if (intersectSolid != null)
                {   
                    Solid intersect = intersectSolid["IntersectSolid"] as Solid;
                    FamilySymbol cutSolidFamilySymbol = familySymbol;
                    int faceCount = intersect.Faces.Size;
                    if (faceCount != 6)
                    {
                        cutSolidFamilySymbol = Tools.CreateFamilySymbol(activeDoc, activeApp, intersect);                        
                    }
                    else
                    {   
                        // cutSolidFamilySymbol = familySymbol.Duplicate(Guid.NewGuid().ToString("N").Substring(0, 6)) as FamilySymbol;
                        var edgesEnumerator = intersect.Edges.GetEnumerator();
                        using (Transaction tran = new Transaction(activeDoc, "createNewFamilyInstance2"))
                        {
                            tran.Start();
                            while (edgesEnumerator.MoveNext())
                            {
                                Line line = (edgesEnumerator.Current as Edge).AsCurve() as Line;
                                if (Math.Abs(line.Direction.Z) == 1)
                                {
                                    cutSolidFamilySymbol.LookupParameter("th").Set(line.Length / 2);
                                    cutSolidFamilySymbol.LookupParameter("bh").Set(line.Length / 2);
                                    continue;
                                }
                                else if (Math.Abs(line.Direction.X) == 1)
                                {
                                    cutSolidFamilySymbol.LookupParameter("ll").Set(line.Length / 2);
                                    cutSolidFamilySymbol.LookupParameter("rl").Set(line.Length / 2);
                                    continue;
                                }
                                else if (Math.Abs(line.Direction.Y) == 1)
                                {
                                    cutSolidFamilySymbol.LookupParameter("tw").Set(line.Length / 2);
                                    cutSolidFamilySymbol.LookupParameter("bw").Set(line.Length / 2);
                                    continue;
                                }
                            }
                            tran.Commit();
                        }
                    }
                    
                    using (Transaction tran = new Transaction(activeDoc, "createNewFamilyInstance"))
                    {
                        tran.Start();
                        FamilyInstance familyInstance = activeDoc.Create.NewFamilyInstance(
                            (intersectSolid["IntersectSolid"] as Solid).ComputeCentroid(), cutSolidFamilySymbol,
                            (intersectSolid["HostCuttedPendingElement"] as PendingElement).element,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        InstanceVoidCutUtils.AddInstanceVoidCut(activeDoc,
                            (intersectSolid["HostCuttedPendingElement"] as PendingElement).element, familyInstance);
                        tran.Commit();
                    }
                }
            }

            TaskDialog.Show("note", cutProcess.intersectSolids.Count.ToString());

            return Result.Succeeded;
        }
    }
}