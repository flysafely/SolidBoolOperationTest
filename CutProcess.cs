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
    public class CutProcess
    {   
        // 默认剪切策略(结构(柱>梁)>建筑)
        private Dictionary<string, int> CutPolicy;
        
        public CutProcess(Dictionary<string, int> customPolicy)
        {   
            // 初始化剪切策略
            InitCutPolicy(customPolicy);
        }

        private void InitCutPolicy(Dictionary<string, int> customPolicy)
        {
            CutPolicy = new Dictionary<string, int>
            {
                {BuiltInCategory.OST_Walls.ToString(), (int) CutOrder.Level10},
                {BuiltInCategory.OST_Floors.ToString(), (int) CutOrder.Level3},
                {BuiltInCategory.OST_Columns.ToString(), (int) CutOrder.Level1},
                {BuiltInCategory.OST_StructuralFraming.ToString(), (int) CutOrder.Level2}
            };
            foreach (var dicItem in customPolicy)
            {
                ConfirmCutPolicy(dicItem.Key, dicItem.Value);
            }
        }

        private void ConfirmCutPolicy(string categoryStr, int levelNum)
        {
            if (CutPolicy.ContainsKey(categoryStr))
            {
                if (CutPolicy[categoryStr] != levelNum)
                {
                    CutPolicy[categoryStr] = levelNum;
                }
            }
            else
            {
                CutPolicy.Add(categoryStr, levelNum);
            }
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

        private Solid SolidByUnion(List<Solid> solids)
        {
            Solid result;
            if (solids.Count > 2)
            {
                Solid solid1 = solids[0];
                solids.RemoveAt(0);
                Solid solid2 = SolidByUnion(solids);
                var intersect =
                    BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                if (intersect.Volume > 0)
                {
                    var difference =
                        BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect,
                            BooleanOperationsType.Difference);
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2,
                        BooleanOperationsType.Union);
                }
                else
                {
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2,
                        BooleanOperationsType.Union);
                }

                return result;
            }
            else
            {
                Solid solid1 = solids[0];
                Solid solid2 = solids[1];
                var intersect =
                    BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);
                if (intersect.Volume > 0)
                {
                    var difference =
                        BooleanOperationsUtils.ExecuteBooleanOperation(solid1, intersect,
                            BooleanOperationsType.Difference);
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(difference, solid2,
                        BooleanOperationsType.Union);
                }
                else
                {
                    result = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2,
                        BooleanOperationsType.Union);
                }

                return result;
            }
        }

        private Solid GetIntersectPart(Solid part1, Solid part2)
        {
            Solid result = null;
            return result;
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
                throw ex;
            }
        }
    }
}