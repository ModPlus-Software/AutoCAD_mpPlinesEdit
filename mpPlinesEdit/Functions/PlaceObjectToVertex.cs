namespace mpPlinesEdit.Functions
{
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.Runtime;
    using Help;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <summary>
    /// Расположение объекта на полилинии
    /// </summary>
    public class PlaceObjectToVertex
    {
        [CommandMethod("ModPlus", "mpPl-ObjectToVx", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
#if !DEBUG
            Statistic.SendCommandStarting(ModPlusConnector.Instance);
#endif
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                var peo = new PromptEntityOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k13")}:")
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

                var id = per.ObjectId;

                peo = new PromptEntityOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k14")}:")
                {
                    AllowNone = false,
                    AllowObjectOnLockedLayer = false
                };
                peo.SetRejectMessage($"\n{Language.GetItem(PlinesEditFunction.LangItem, "wrong")}");
                per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    return;
                }

                var objectId = per.ObjectId;
                var collection = new ObjectIdCollection { objectId };

                var win = new ObjectToVxSettings();
                var wr = win.ShowDialog();
                if (wr != null && !wr.Value)
                {
                    return;
                }

                var excludeFirstAndLastPt = win.ChkExcludeFirstAndLastPt.IsChecked != null &&
                                            win.ChkExcludeFirstAndLastPt.IsChecked.Value; // Исключать крайние точки
                var rotateObject = (ObjectToVxRotateObjectBy)win.CbRotateBy.SelectedIndex;
                var copyBlockBy = (ObjectToVxCopyBlockBy)win.CbCopyBlockBy.SelectedIndex;

                using (doc.LockDocument())
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var dbObj = tr.GetObject(id, OpenMode.ForRead);

                        if (dbObj is Polyline polyline)
                        {
                            for (var i = 0; i < polyline.NumberOfVertices; i++)
                            {
                                if (excludeFirstAndLastPt)
                                {
                                    if (i == 0 | i == polyline.NumberOfVertices - 1)
                                    {
                                        continue;
                                    }
                                }

                                var pt = polyline.GetPoint3dAt(i);

                                var objectToCopy = tr.GetObject(objectId, OpenMode.ForRead);

                                // copy
                                var mapping = new IdMapping();
                                db.DeepCloneObjects(collection, db.CurrentSpaceId, mapping, false);
                                var pair = mapping[objectId];

                                if (copyBlockBy == ObjectToVxCopyBlockBy.ByPosition & objectToCopy is BlockReference)
                                {
                                    // move    
                                    var copiedBlk = tr.GetObject(pair.Value, OpenMode.ForWrite) as BlockReference;
                                    if (copiedBlk != null)
                                    {
                                        var matrix = Matrix3d.Displacement(pt - copiedBlk.Position);
                                        copiedBlk.TransformBy(matrix);
                                    }
                                }
                                else
                                {
                                    // move
                                    var ent = tr.GetObject(pair.Value, OpenMode.ForWrite) as Entity;
                                    if (ent != null)
                                    {
                                        var entPt = GetGeometricCenter(ent.GeometricExtents);
                                        var matrix = Matrix3d.Displacement(pt - entPt);
                                        ent.TransformBy(matrix);
                                    }
                                }

                                if (rotateObject != ObjectToVxRotateObjectBy.None)
                                {
                                    var ent = tr.GetObject(pair.Value, OpenMode.ForWrite) as Entity;
                                    double? angle = null;
                                    var ptCurrent = polyline.GetPoint3dAt(i);

                                    if (i != 0 & i != polyline.NumberOfVertices - 1)
                                    {
                                        var ptBefore = polyline.GetPoint3dAt(i - 1);
                                        var ptAfter = polyline.GetPoint3dAt(i + 1);

                                        if (rotateObject == ObjectToVxRotateObjectBy.ByFirst)
                                        {
                                            if (polyline.GetBulgeAt(i - 1).Equals(0.0))
                                            {
                                                angle = (ptCurrent - ptBefore).AngleOnPlane(polyline.GetPlane());
                                            }
                                            else
                                            {
                                                var fd =
                                                    polyline.GetFirstDerivative(
                                                        polyline.GetParameterAtPoint(polyline.GetPoint3dAt(i)) - 0.000001);
                                                angle = fd.AngleOnPlane(polyline.GetPlane());
                                            }
                                        }

                                        if (rotateObject == ObjectToVxRotateObjectBy.BySecond)
                                        {
                                            if (polyline.GetBulgeAt(i).Equals(0.0))
                                            {
                                                angle = (ptAfter - ptCurrent).AngleOnPlane(polyline.GetPlane());
                                            }
                                            else
                                            {
                                                var fd =
                                                    polyline.GetFirstDerivative(
                                                        polyline.GetParameterAtPoint(polyline.GetPoint3dAt(i)) + 0.000001);
                                                angle = fd.AngleOnPlane(polyline.GetPlane());
                                            }
                                        }
                                    }
                                    else if (i == 0)
                                    {
                                        if (polyline.GetBulgeAt(i).Equals(0.0))
                                        {
                                            var ptAfter = polyline.GetPoint3dAt(i + 1);
                                            angle = (ptAfter - ptCurrent).AngleOnPlane(polyline.GetPlane());
                                        }
                                        else
                                        {
                                            var fd =
                                                polyline.GetFirstDerivative(
                                                    polyline.GetParameterAtPoint(polyline.GetPoint3dAt(i)) + 0.000001);
                                            angle = fd.AngleOnPlane(polyline.GetPlane());
                                        }
                                    }
                                    else if (i == polyline.NumberOfVertices - 1)
                                    {
                                        if (polyline.GetBulgeAt(i - 1).Equals(0.0))
                                        {
                                            var ptBefore = polyline.GetPoint3dAt(i - 1);
                                            angle = (ptCurrent - ptBefore).AngleOnPlane(polyline.GetPlane());
                                        }
                                        else
                                        {
                                            var fd =
                                                polyline.GetFirstDerivative(
                                                    polyline.GetParameterAtPoint(polyline.GetPoint3dAt(i)) - 0.000001);
                                            angle = fd.AngleOnPlane(polyline.GetPlane());
                                        }
                                    }

                                    // Rotate
                                    if (angle != null)
                                    {
                                        var matrix = Matrix3d.Rotation(
                                            angle.Value,
                                            ed.CurrentUserCoordinateSystem.CoordinateSystem3d.Zaxis, ptCurrent);
                                        ent?.TransformBy(matrix);
                                    }
                                }
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

        private static Point3d GetGeometricCenter(Extents3d ext)
        {
            return new Point3d(
                (ext.MaxPoint.X + ext.MinPoint.X) / 2,
                (ext.MaxPoint.Y + ext.MinPoint.Y) / 2,
                (ext.MaxPoint.Z + ext.MinPoint.Z) / 2);
        }

        private enum ObjectToVxRotateObjectBy
        {
            None = 0,
            ByFirst = 1,
            BySecond = 2
        }

        private enum ObjectToVxCopyBlockBy
        {
            ByPosition = 0,
            ByGeometricCenter = 1
        }
    }
}
