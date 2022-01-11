using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace SolidBoolOperationTest
{
    public class CompsiteElementsClassifier
    {
        Document activeDoc;
        Dictionary<Type, IList<Element>> elementsDic = new Dictionary<Type, IList<Element>>();
        public CompsiteElementsClassifier(Document doc, IList<Reference> refs)
        {
            activeDoc = doc;
            // 聚类操作
            ClassifyElement(refs);

        }

        private void ClassifyElement(IList<Reference> refs)
        {   
            IList<Element> wallEles = new List<Element>();
            IList<Element> floorEles = new List<Element>();
            IList<Element> columnEles = new List<Element>();
            IList<Element> beamEles = new List<Element>();
            IList<Element> revitLinkEles = new List<Element>();

            foreach (Reference reference in refs)
            {
                Element ele = activeDoc.GetElement(reference);
                if ((ele as Wall) != null)
                {
                    wallEles.Add(ele);
                }
                else if ((ele as Floor) != null)
                {
                    floorEles.Add(ele);
                }
                else if ((ele as FamilyInstance) != null)
                {
                    if (ele.Category.Equals(BuiltInCategory.OST_Columns))
                    {
                        columnEles.Add(ele);
                    }
                    else if (ele.Category.Equals(BuiltInCategory.OST_StructuralFraming))
                    {
                        beamEles.Add(ele);
                    }
                }
                else if ((ele as RevitLinkInstance) != null)
                {
                    revitLinkEles.Add(ele);
                }
            }
            foreach (RevitLinkInstance revitLink in revitLinkEles)
            {
                Document linkDoc = revitLink.GetLinkDocument();
                
            }
            // 处理链接文件中的各类构件
            linkDoc = linkIns.GetLinkDocument();
        }

        private Dictionary<string, > revitLinkDocClassfiy()
        {

        }

        private object FamilyTypeConvert(Reference reference)
        {
            var wall = activeDoc.GetElement(reference) as Wall;
            if (wall != null)
            {
                return wall;
            }
            var floor = activeDoc.GetElement(reference) as Floor;
            if (floor != null)
            {
                return floor;
            }
            var familyinstance = activeDoc.GetElement(reference) as FamilyInstance;
            if (familyinstance != null)
            {
                return familyinstance;
            }
            var rvtlinkinstance = activeDoc.GetElement(reference) as RevitLinkInstance;
            if ( rvtlinkinstance != null)
            {
                return rvtlinkinstance;
            }
            return null;
        }

        private IList<Document> GetRVTLinkDocsInActiveDoc(Document activeDoc)
        {
            IList<Document> results = new List<Document>();
            FilteredElementCollector linkInstances = new FilteredElementCollector(activeDoc);
            linkInstances = linkInstances.WherePasses(new ElementClassFilter(typeof(RevitLinkInstance)));
            if (linkInstances != null)
            {
                foreach (RevitLinkInstance linkIns in linkInstances)
                {
                    results.Add(linkIns.GetLinkDocument());
                }
            }

            if (results.Count > 0)
            {
                return results;
            }
            else
            {
                return null;
            }
        }

        private Document GetRVTLinkDoc(RevitLinkInstance revitLinkInstance)
        {
            if (revitLinkInstance != null)
            {
                return revitLinkInstance.GetLinkDocument();
            }
            return null;
        } 
    }
}
