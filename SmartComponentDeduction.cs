using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI.Events;
using CommonTools;
using System.Windows;
using Application = Autodesk.Revit.ApplicationServices.Application;

namespace SmartComponentDeduction
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
            try
            {
                var refs = activeUiDoc.Selection.PickObjects(ObjectType.Element, new ElementsSelectionFilter());
                var compositeElementsClassifier = new CompositeElementsClassifier(activeDoc, refs, targetCategories);
                var intersectResults = compositeElementsClassifier.GetExistIntersectElements();
                var cutProcess = new CutProcess(activeApp, activeDoc, CutPolicy, "CMCU自定义空心轮廓族");
                cutProcess.DoCuttingProcess(intersectResults);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }
    }
}