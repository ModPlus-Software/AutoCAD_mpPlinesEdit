namespace mpPlinesEdit.Functions
{
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <summary>
    /// Конвертировать 3d полилинию в 2d полилинию
    /// </summary>
    public class ConvertThreeDto2D
    {
        [CommandMethod("ModPlus", "mpPl-3Dto2D", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            try
            {
                var pso = new PromptSelectionOptions
                {
                    AllowDuplicates = false,
                    MessageForAdding = "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k1") + ":"
                };
                var filterList = new TypedValue[2];
                filterList[0] = new TypedValue((int)DxfCode.Start, "POLYLINE");
                filterList[1] = new TypedValue(70, "8");
                var sf = new SelectionFilter(filterList);
                var psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK)
                    return;
                var ids = psr.Value.GetObjectIds();

                using (doc.LockDocument())
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (var objectId in ids)
                        {
                            var dbObj = tr.GetObject(objectId, OpenMode.ForWrite);
                            if (dbObj is Polyline3d)
                            {
                                var polyline = new Polyline();

                                var curve = dbObj as Curve;
                                for (var i = 0; i <= curve.EndParam; i++)
                                {
                                    var pt = curve.GetPointAtParameter(i);
                                    polyline.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0.0, 0.0, 0.0);
                                }

                                polyline.Closed = curve.Closed;

                                // свойства
                                polyline.LayerId = curve.LayerId;
                                polyline.Linetype = curve.Linetype;
                                polyline.LineWeight = curve.LineWeight;
                                polyline.LinetypeScale = curve.LinetypeScale;
                                polyline.Color = curve.Color;
                                polyline.XData = curve.XData;

                                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                                btr.AppendEntity(polyline);
                                tr.AddNewlyCreatedDBObject(polyline, true);

                                // erase
                                curve.Erase(true);
                            }
                        }

                        tr.Commit();
                    }
                }
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }
    }
}
