using System.Collections.Generic;
using Autodesk.Revit.DB;
namespace SmartComponentDeduction
{
    public class CutInstance
    {
        public ElementId eleId { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Solid OriginCutSolid { get; set; }
        public AssociationTypes Relation { get; set; }
        public FamilySymbol CutFamilySymbol { get; set; }
        public Dictionary<Line, double> Rotations { get; set; }
        public PendingElement IntactElement { get; set; }
        public PendingElement BeCutElement { get; set; }

        public CutInstance(Solid solid)
        {
            Length = 0d;
            Width = 0d;
            Height = 0d;
            OriginCutSolid = solid;
            Rotations = new Dictionary<Line, double>();
        }
    }
}