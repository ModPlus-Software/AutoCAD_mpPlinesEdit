namespace mpPlinesEdit.Functions
{
    using System;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Runtime;
    using ModPlus.Helpers;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <summary>
    /// Заменить дуговой сегмент линейным
    /// </summary>
    public class ConvertSegmentArcToLine
    {
        [CommandMethod("ModPlus", "mpPl-Arc2Line", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
#if !DEBUG
            Statistic.SendCommandStarting(ModPlusConnector.Instance);
#endif
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // Событие наведения курсора на объект
            ed.TurnForcedPickOn();
            ed.PointMonitor += Arc2Line_EdOnPointMonitor;
            try
            {
                while (true)
                {
                    var peo = new PromptEntityOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k15")}:")
                    {
                        AllowNone = false,
                        AllowObjectOnLockedLayer = true
                    };
                    peo.SetRejectMessage($"\n{Language.GetItem(PlinesEditFunction.LangItem, "wrong")}");
                    peo.AddAllowedClass(typeof(Polyline), true);

                    var per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK)
                    {
                        return;
                    }

                    var polylineId = per.ObjectId;
                    var pickedPt = AutocadHelpers.UcsToWcs(per.PickedPoint);

                    using (doc.LockDocument())
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var dbObj = tr.GetObject(polylineId, OpenMode.ForRead);

                            if (dbObj is Polyline polyline)
                            {
                                var p = polyline.GetClosestPointTo(pickedPt, false);
                                var param = polyline.GetParameterAtPoint(p);
                                var vx = Convert.ToInt32(Math.Truncate(param));
                                if (polyline.GetSegmentType(vx) == SegmentType.Arc)
                                {
                                    polyline.UpgradeOpen();
                                    polyline.SetBulgeAt(vx, 0.0);
                                }
                            }

                            tr.Commit();
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
            finally
            {
                ed.TurnForcedPickOn();
                ed.PointMonitor -= Arc2Line_EdOnPointMonitor;
            }
        }

        private static void Arc2Line_EdOnPointMonitor(object sender, PointMonitorEventArgs pointMonitorEventArgs)
        {
            var ed = (Editor)sender;
            var doc = ed.Document;
            try
            {
                var paths = pointMonitorEventArgs.Context.GetPickedEntities();

                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    using (doc.LockDocument())
                    {
                        foreach (var path in paths)
                        {
                            var ids = path.GetObjectIds();

                            if (ids.Length > 0)
                            {
                                var id = ids[ids.GetUpperBound(0)];
                                var obj = tr.GetObject(id, OpenMode.ForRead);
                                if (obj is Polyline polyline)
                                {
                                    var p = polyline.GetClosestPointTo(pointMonitorEventArgs.Context.ComputedPoint, false);
                                    var param = polyline.GetParameterAtPoint(p);
                                    var vx = Convert.ToInt32(Math.Truncate(param));
                                    if (polyline.GetSegmentType(vx) == SegmentType.Arc)
                                    {
                                        var nextVx = vx + 1;
                                        if (vx == polyline.NumberOfVertices - 1)
                                        {
                                            if (polyline.Closed)
                                            {
                                                nextVx = 0;
                                            }
                                        }

                                        var line = new Line(
                                            polyline.GetPoint3dAt(vx),
                                            polyline.GetPoint3dAt(nextVx))
                                        {
                                            ColorIndex = PlinesEditFunction.HelpGeometryColor
                                        };
                                        pointMonitorEventArgs.Context.DrawContext.Geometry.Draw(line);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
