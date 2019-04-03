/*
Полилинии бывают трех видов:
- Optimized (or "lightweight") 2D polylines
- Old-format (or "heavyweight") 2D polylines
- 3D polylines
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Autodesk.AutoCAD.Runtime;
using mpPlinesEdit.Help;
using ModPlusAPI;
using ModPlusAPI.Windows;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace mpPlinesEdit
{
    public class MpPlines
    {
        private const string LangItem = "mpPlinesEdit";

        public static int HelpGeometryColor = 150;

        [CommandMethod("ModPlus", "mpPl-3Dto2D", CommandFlags.UsePickSet)]
        public static void mpPl_3Dto2D()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            try
            {
                var pso = new PromptSelectionOptions
                {
                    AllowDuplicates = false,
                    MessageForAdding = "\n" + Language.GetItem(LangItem, "k1") + ":"
                };
                var filterlist = new TypedValue[2];
                filterlist[0] = new TypedValue((int)DxfCode.Start, "POLYLINE");
                filterlist[1] = new TypedValue(70, "8");
                var sf = new SelectionFilter(filterlist);
                var psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK) return;
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
                                var pline = new Polyline();

                                var pline3D = dbObj as Curve;
                                for (var i = 0; i <= pline3D.EndParam; i++)
                                {
                                    var pt = pline3D.GetPointAtParameter(i);
                                    pline.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0.0, 0.0, 0.0);
                                }
                                pline.Closed = pline3D.Closed;
                                // свойства
                                pline.LayerId = pline3D.LayerId;
                                pline.Linetype = pline3D.Linetype;
                                pline.LineWeight = pline3D.LineWeight;
                                pline.LinetypeScale = pline3D.LinetypeScale;
                                pline.Color = pline3D.Color;
                                pline.XData = pline3D.XData;

                                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                                btr.AppendEntity(pline);
                                tr.AddNewlyCreatedDBObject(pline, true);
                                //erase
                                pline3D.Erase(true);
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

        /// <summary>
        /// Удаление совпадающих вершин полилинии
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-VxMatchRemove", CommandFlags.UsePickSet)]
        public static void mpPl_VxMatchRemove()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                var pso = new PromptSelectionOptions
                {
                    AllowDuplicates = false,
                    MessageForAdding = "\n" + Language.GetItem(LangItem, "k2") + ":"
                };
                var filList = new[] { new TypedValue((int)DxfCode.Start, "*POLYLINE") };
                var sf = new SelectionFilter(filList);
                var psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK) return;
                var ids = psr.Value.GetObjectIds();
                var plCount = psr.Value.Count;

                using (doc.LockDocument())
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (var objectId in ids)
                        {
                            var dbObj = tr.GetObject(objectId, OpenMode.ForRead);
                            var deleted = 0;
                            // Если простая полилиния
                            if (dbObj is Polyline)
                            {
                                var listOfDuplicateVertex = new List<int>();
                                var pline = dbObj as Polyline;
                                for (var i = 0; i < pline.NumberOfVertices - 1; i++)
                                {
                                    if (pline.GetPoint2dAt(i).Equals(pline.GetPoint2dAt(i + 1)))
                                        listOfDuplicateVertex.Add(i + 1);
                                }
                                if (listOfDuplicateVertex.Count > 0)
                                {
                                    /*
                                    при каждом удалении вершины количество вершин меняется
                                    значит после каждого удаления нужно нужно значение i уменьшать
                                    на переменную, в которую будет записано кол-во проходов
                                    */
                                    pline.UpgradeOpen();
                                    var j = 0;
                                    foreach (var i in listOfDuplicateVertex)
                                    {
                                        if (j == 0)
                                        {
                                            var bulge = pline.GetBulgeAt(i);
                                            if (!bulge.Equals(0.0))
                                                pline.SetBulgeAt(i - 1, bulge);
                                            pline.RemoveVertexAt(i);

                                        }
                                        else
                                        {
                                            var bulge = pline.GetBulgeAt(i - j);
                                            if (!bulge.Equals(0.0))
                                                pline.SetBulgeAt(i - j - 1, bulge);
                                            pline.RemoveVertexAt(i - j);
                                        }
                                        j++;
                                    }
                                    deleted = listOfDuplicateVertex.Count;
                                }
                            }
                            else if (dbObj is Polyline2d)
                            {
                                // Будем запоминать предыдущую и сравнивать с текущей
                                var i = 0;
                                var pline = dbObj as Polyline2d;
                                Vertex2d vrBefore = null;
                                foreach (ObjectId vId in pline)
                                {
                                    var v2D = (Vertex2d)tr.GetObject(vId, OpenMode.ForRead);
                                    if (vrBefore == null)
                                        vrBefore = v2D;
                                    else
                                    {
                                        if (vrBefore.Position.Equals(v2D.Position))
                                        {
                                            v2D.UpgradeOpen();
                                            if (!v2D.Bulge.Equals(0.0))
                                                vrBefore.Bulge = v2D.Bulge;
                                            v2D.Erase(true);
                                            i++;
                                        }
                                    }
                                    vrBefore = v2D;
                                }
                                if (i > 0)
                                {
                                    pline.UpgradeOpen();
                                    pline.RecordGraphicsModified(true);
                                    deleted = i;
                                }
                            }
                            else if (dbObj is Polyline3d)
                            {
                                // Будем запоминать предыдущую и сравнивать с текущей
                                var i = 0;
                                var pline = dbObj as Polyline3d;
                                PolylineVertex3d vrBefore = null;
                                foreach (ObjectId vId in pline)
                                {
                                    var v3D = (PolylineVertex3d)tr.GetObject(vId, OpenMode.ForRead);
                                    if (vrBefore == null)
                                        vrBefore = v3D;
                                    else
                                    {
                                        if (vrBefore.Position.Equals(v3D.Position))
                                        {
                                            v3D.UpgradeOpen();
                                            v3D.Erase(true);
                                            i++;
                                        }
                                    }
                                    vrBefore = v3D;
                                }
                                if (i > 0)
                                {
                                    pline.UpgradeOpen();
                                    pline.RecordGraphicsModified(true);
                                    deleted = i;
                                }
                            }
                            // Вывод результата
                            if (deleted > 0)
                            {
                                if (plCount == 1)
                                    MessageBox.Show(Language.GetItem(LangItem, "k3") + ":" + deleted);
                                else
                                    ed.WriteMessage("\n" + Language.GetItem(LangItem, "k4") + ":" + objectId + " " +
                                        Language.GetItem(LangItem, "k3").ToLower() + ":" + deleted);
                            }
                            else
                            {
                                if (plCount == 1)
                                    MessageBox.Show(Language.GetItem(LangItem, "k4") + " " + Language.GetItem(LangItem, "k5"));
                                else ed.WriteMessage("\n" + Language.GetItem(LangItem, "k4") + " " + objectId + " " + Language.GetItem(LangItem, "k5"));
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

        /// <summary>
        /// Удаление вершин, лежащих на одной прямой
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-VxCollin", CommandFlags.UsePickSet)]
        public static void mpPl_VxCollin()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var maxA = 0.0; // Предельное отклонение угла
            var maxH = 0.0; // Предельное отклонение высоты

            var pso = new PromptSelectionOptions
            {
                AllowDuplicates = false
            };
            pso.Keywords.Add("Dop", Language.GetItem(LangItem, "k7"));
            // Set our prompts to include our keywords
            var kws = pso.Keywords.GetDisplayString(true);
            pso.MessageForAdding = "\n" + Language.GetItem(LangItem, "k6") + ":" + kws;
            pso.KeywordInput +=
                 delegate (object sender, SelectionTextInputEventArgs e)
                 {
                     if (e.Input.Equals("Dop"))
                     {
                         var win = new VxCollinHelp
                         {
                             TbMaxA =
                             {
                                 Value = double.TryParse(ModPlus.Helpers.XDataHelpers.GetStringXData("mpPl_VxCollin_maxA"),
                                     out var d) ? d : 0.0
                             },
                             TbMaxH =
                             {
                                 Value = double.TryParse(ModPlus.Helpers.XDataHelpers.GetStringXData("mpPl_VxCollin_maxH"),
                                     out d) ? d : 0.0
                             }
                         };
                         if (win.ShowDialog() == true)
                         {
                             maxH = win.TbMaxH.Value ?? 0.0;
                             ModPlus.Helpers.XDataHelpers.SetStringXData("mpPl_VxCollin_maxH", maxH.ToString(CultureInfo.InvariantCulture));
                             ed.WriteMessage("\n" + Language.GetItem(LangItem, "k8") + ":" + maxH);
                             maxA = win.TbMaxA.Value ?? 0.0;
                             ModPlus.Helpers.XDataHelpers.SetStringXData("mpPl_VxCollin_maxA", maxA.ToString(CultureInfo.InvariantCulture));
                             ed.WriteMessage("\n" + Language.GetItem(LangItem, "k9") + ":" + maxA);
                         }
                     }
                 };

            try
            {
                var filList = new[] { new TypedValue((int)DxfCode.Start, "*POLYLINE") };
                var sf = new SelectionFilter(filList);

                var psr = ed.GetSelection(pso, sf);
                if (psr.Status == PromptStatus.OK)
                {
                    var ids = psr.Value.GetObjectIds();
                    var plCount = psr.Value.Count;
                    /*Сначала нужно пройти по полилинии
                    и получить коллекцию вершин для удаления
                    а потом уже удалить
                    В принципе - по аналогии удаления совпадающих вершин
                    только без проверки на скругление перед удалением
                    */
                    using (doc.LockDocument())
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            foreach (var objectId in ids)
                            {
                                var dbObj = tr.GetObject(objectId, OpenMode.ForRead);
                                var deleted = 0;
                                // Если простая полилиния
                                if (dbObj is Polyline)
                                {
                                    var pline = dbObj as Polyline;
                                    var listOfDuplicateVertex = new List<int>();
                                    if (pline.NumberOfVertices <= 2)
                                    {
                                        if (plCount == 1)
                                            MessageBox.Show(
                                                Language.GetItem(LangItem, "k4") + " " +
                                                Language.GetItem(LangItem, "k10") + " " + pline.NumberOfVertices + " " +
                                                Language.GetItem(LangItem, "k11") + "!");
                                        else
                                            ed.WriteMessage("\n" +
                                                Language.GetItem(LangItem, "k4") + ":" + objectId + " " +
                                                Language.GetItem(LangItem, "k10") + " " + pline.NumberOfVertices + " " +
                                                Language.GetItem(LangItem, "k11") + "!");
                                        continue;
                                    }
                                    for (var i = 0; i < pline.NumberOfVertices - 2; i++)
                                    {
                                        // Если в текущей вершине или следующей
                                        // есть скругление, то пропускаем
                                        if (!pline.GetBulgeAt(i).Equals(0.0)) continue;
                                        if (!pline.GetBulgeAt(i + 1).Equals(0.0)) continue;
                                        // Берем два вектора и сравниваем параллельность
                                        var pt1 = pline.GetPoint2dAt(i); // первая
                                        var pt2 = pline.GetPoint2dAt(i + 1); // вторая (средняя)
                                        var pt3 = pline.GetPoint2dAt(i + 2); // третья

                                        if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                                            listOfDuplicateVertex.Add(i + 1);
                                        else
                                        {
                                            // Если не параллельны, то сравниваем по допускам
                                            var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                                            if (ang <= maxA)
                                                listOfDuplicateVertex.Add(i + 1);
                                            var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                                            if (h <= maxH)
                                                listOfDuplicateVertex.Add(i + 1);
                                        }

                                    }
                                    if (listOfDuplicateVertex.Count > 0)
                                    {
                                        /*
                                    при каждом удалении вершины количество вершин меняется
                                    значит после каждого удаления нужно нужно значение i уменьшать
                                    на переменную, в которую будет записано кол-во проходов
                                    */
                                        pline.UpgradeOpen();
                                        var j = 0;
                                        foreach (var i in listOfDuplicateVertex)
                                        {
                                            if (j == 0)
                                            {
                                                pline.RemoveVertexAt(i);
                                            }
                                            else
                                            {
                                                pline.RemoveVertexAt(i - j);
                                            }
                                            j++;
                                        }
                                        deleted = listOfDuplicateVertex.Count;
                                    }
                                }
                                else if (dbObj is Polyline2d)
                                {
                                    var pline = dbObj as Polyline2d;
                                    var vertexes =
                                        (from ObjectId vId in pline
                                         select (Vertex2d)tr.GetObject(vId, OpenMode.ForRead))
                                            .ToList();
                                    if (vertexes.Count <= 2)
                                    {
                                        if (plCount == 1)
                                            MessageBox.Show(
                                                Language.GetItem(LangItem, "k4") + " " +
                                                Language.GetItem(LangItem, "k10") + " " + vertexes.Count + " " +
                                                Language.GetItem(LangItem, "k11") + "!");
                                        else
                                            ed.WriteMessage("\n" +
                                                            Language.GetItem(LangItem, "k4") + ":" + objectId + " " +
                                                            Language.GetItem(LangItem, "k10") + " " + vertexes.Count + " " +
                                                            Language.GetItem(LangItem, "k11") + "!");
                                        continue;
                                    }
                                    for (var i = 0; i < vertexes.Count - 2; i++)
                                    {
                                        // Если в текущей вершине или следующей
                                        // есть скругление, то пропускаем
                                        if (!vertexes[i].Bulge.Equals(0.0)) continue;
                                        if (!vertexes[i + 1].Bulge.Equals(0.0)) continue;
                                        // Берем два вектора и сравниваем параллельность
                                        var pt1 = vertexes[i].Position; // первая
                                        var pt2 = vertexes[i + 1].Position; // вторая (средняя)
                                        var pt3 = vertexes[i + 2].Position; // третья

                                        if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                                        {
                                            vertexes[i + 1].UpgradeOpen();
                                            vertexes[i + 1].Erase(true);
                                            deleted++;
                                        }
                                        else
                                        {
                                            // Если не параллельны, то сравниваем по допускам
                                            var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                                            if (ang <= maxA)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }
                                            var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                                            if (h <= maxH)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }
                                        }
                                    }

                                    if (deleted > 0)
                                    {
                                        pline.UpgradeOpen();
                                        pline.RecordGraphicsModified(true);
                                    }
                                }
                                else if (dbObj is Polyline3d)
                                {
                                    var pline = dbObj as Polyline3d;
                                    var vertexes =
                                        (from ObjectId vId in pline
                                         select (PolylineVertex3d)tr.GetObject(vId, OpenMode.ForRead)).ToList();
                                    if (vertexes.Count <= 2)
                                    {
                                        if (plCount == 1)
                                            MessageBox.Show(
                                                Language.GetItem(LangItem, "k4") + " " +
                                                Language.GetItem(LangItem, "k10") + " " + vertexes.Count + " " +
                                                Language.GetItem(LangItem, "k11") + "!");
                                        else
                                            ed.WriteMessage("\n" +
                                                            Language.GetItem(LangItem, "k4") + ":" + objectId + " " +
                                                            Language.GetItem(LangItem, "k10") + " " + vertexes.Count + " " +
                                                            Language.GetItem(LangItem, "k11") + "!");
                                        continue;
                                    }
                                    for (var i = 0; i < vertexes.Count - 2; i++)
                                    {
                                        // 3d полилиния не содержит дуг!
                                        // Берем два вектора и сравниваем параллельность
                                        var pt1 = vertexes[i].Position; // первая
                                        var pt2 = vertexes[i + 1].Position; // вторая (средняя)
                                        var pt3 = vertexes[i + 2].Position; // третья

                                        if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                                        {
                                            vertexes[i + 1].UpgradeOpen();
                                            vertexes[i + 1].Erase(true);
                                            deleted++;
                                        }
                                        else
                                        {
                                            // Если не параллельны, то сравниваем по допускам
                                            var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                                            if (ang <= maxA)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }
                                            var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                                            if (h <= maxH)
                                            {
                                                vertexes[i + 1].UpgradeOpen();
                                                vertexes[i + 1].Erase(true);
                                                deleted++;
                                            }
                                        }
                                    }

                                    if (deleted > 0)
                                    {
                                        pline.UpgradeOpen();
                                        pline.RecordGraphicsModified(true);
                                    }
                                }
                                // Вывод результата
                                if (deleted > 0)
                                {
                                    if (plCount == 1)
                                        MessageBox.Show(Language.GetItem(LangItem, "k3") + ":" + deleted);
                                    else
                                        ed.WriteMessage("\n" + Language.GetItem(LangItem, "k4") + ":" + objectId + " " +
                                            Language.GetItem(LangItem, "k3").ToLower() + ":" + deleted);
                                }
                                else
                                {
                                    if (plCount == 1)
                                        MessageBox.Show(
                                            Language.GetItem(LangItem, "k4") + " " +
                                            Language.GetItem(LangItem, "k12"));
                                    else
                                        ed.WriteMessage("\n" +
                                            Language.GetItem(LangItem, "k4") + ":" + objectId + " " +
                                            Language.GetItem(LangItem, "k12"));
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
        }


        /// <summary>
        /// Расположение объекта на полилинии
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-ObjectToVx", CommandFlags.UsePickSet)]
        public static void mpPl_ObjectToVx()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                var peo = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "k13") + ":")
                {
                    AllowNone = false,
                    AllowObjectOnLockedLayer = true
                };
                peo.SetRejectMessage("\n" + Language.GetItem(LangItem, "wrong"));
                peo.AddAllowedClass(typeof(Polyline), true);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;
                var plineId = per.ObjectId;

                peo = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "k14") + ":")
                {
                    AllowNone = false,
                    AllowObjectOnLockedLayer = false
                };
                peo.SetRejectMessage("\n" + Language.GetItem(LangItem, "wrong"));
                per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;
                var objectId = per.ObjectId;
                var collection = new ObjectIdCollection { objectId };

                var win = new ObjectToVxSettings();
                var wr = win.ShowDialog();
                if (wr != null && !wr.Value) return;

                var excludeFirstAndLastPt = win.ChkExcludeFirstAndLastPt.IsChecked != null &&
                                            win.ChkExcludeFirstAndLastPt.IsChecked.Value; // Исключать крайние точки
                var rotateObject = (ObjectToVxRotateObjectBy)win.CbRotateBy.SelectedIndex;
                var copyBlockBy = (ObjectToVxCopyBlockBy)win.CbCopyBlockBy.SelectedIndex;

                using (doc.LockDocument())
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var dbObj = tr.GetObject(plineId, OpenMode.ForRead);

                        if (dbObj is Polyline)
                        {
                            var pline = dbObj as Polyline;

                            for (var i = 0; i < pline.NumberOfVertices; i++)
                            {
                                if (excludeFirstAndLastPt)
                                    if (i == 0 | i == pline.NumberOfVertices - 1) continue;

                                var pt = pline.GetPoint3dAt(i);

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
                                    var ptCurrent = pline.GetPoint3dAt(i);

                                    if (i != 0 & i != pline.NumberOfVertices - 1)
                                    {
                                        var ptBefore = pline.GetPoint3dAt(i - 1);
                                        var ptAfter = pline.GetPoint3dAt(i + 1);

                                        if (rotateObject == ObjectToVxRotateObjectBy.ByFirst)
                                        {
                                            if (pline.GetBulgeAt(i - 1).Equals(0.0))
                                                angle = (ptCurrent - ptBefore).AngleOnPlane(pline.GetPlane());
                                            else
                                            {
                                                var fd =
                                                    pline.GetFirstDerivative(
                                                        pline.GetParameterAtPoint(pline.GetPoint3dAt(i)) - 0.000001);
                                                angle = fd.AngleOnPlane(pline.GetPlane());
                                            }
                                        }
                                        if (rotateObject == ObjectToVxRotateObjectBy.BySecond)
                                        {
                                            if (pline.GetBulgeAt(i).Equals(0.0))
                                                angle = (ptAfter - ptCurrent).AngleOnPlane(pline.GetPlane());
                                            else
                                            {
                                                var fd =
                                                    pline.GetFirstDerivative(
                                                        pline.GetParameterAtPoint(pline.GetPoint3dAt(i)) + 0.000001);
                                                angle = fd.AngleOnPlane(pline.GetPlane());
                                            }
                                        }
                                    }

                                    else if (i == 0)
                                    {
                                        if (pline.GetBulgeAt(i).Equals(0.0))
                                        {
                                            var ptAfter = pline.GetPoint3dAt(i + 1);
                                            angle = (ptAfter - ptCurrent).AngleOnPlane(pline.GetPlane());
                                        }
                                        else
                                        {
                                            var fd =
                                                pline.GetFirstDerivative(
                                                    pline.GetParameterAtPoint(pline.GetPoint3dAt(i)) + 0.000001);
                                            angle = fd.AngleOnPlane(pline.GetPlane());
                                        }
                                    }
                                    else if (i == pline.NumberOfVertices - 1)
                                    {
                                        if (pline.GetBulgeAt(i - 1).Equals(0.0))
                                        {
                                            var ptBefore = pline.GetPoint3dAt(i - 1);
                                            angle = (ptCurrent - ptBefore).AngleOnPlane(pline.GetPlane());
                                        }
                                        else
                                        {
                                            var fd =
                                                pline.GetFirstDerivative(
                                                    pline.GetParameterAtPoint(pline.GetPoint3dAt(i)) - 0.000001);
                                            angle = fd.AngleOnPlane(pline.GetPlane());
                                        }
                                    }
                                    // Rotate
                                    if (angle != null)
                                    {
                                        var matrix = Matrix3d.Rotation(angle.Value,
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

        /// <summary>
        /// Заменить дуговой сегмент линейным
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-Arc2Line", CommandFlags.UsePickSet)]
        public static void mpPl_Arc2Line()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            //Событие наведения курсора на объект
            ed.TurnForcedPickOn();
            ed.PointMonitor += Arc2Line_EdOnPointMonitor;
            try
            {
                while (true)
                {
                    var peo = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "k15") + ":")
                    {
                        AllowNone = false,
                        AllowObjectOnLockedLayer = true
                    };
                    peo.SetRejectMessage("\n" + Language.GetItem(LangItem, "wrong"));
                    peo.AddAllowedClass(typeof(Polyline), true);

                    var per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) return;

                    var plineId = per.ObjectId;
                    var pickedPt = ModPlus.Helpers.AutocadHelpers.UcsToWcs(per.PickedPoint);

                    using (doc.LockDocument())
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var dbObj = tr.GetObject(plineId, OpenMode.ForRead);

                            if (dbObj is Polyline)
                            {
                                var pline = dbObj as Polyline;

                                var p = pline.GetClosestPointTo(pickedPt, false);
                                var param = pline.GetParameterAtPoint(p);
                                var vx = Convert.ToInt32(Math.Truncate(param));
                                if (pline.GetSegmentType(vx) == SegmentType.Arc)
                                {
                                    pline.UpgradeOpen();
                                    pline.SetBulgeAt(vx, 0.0);
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
                                if (obj is Polyline)
                                {
                                    var pline = obj as Polyline;
                                    var p = pline.GetClosestPointTo(pointMonitorEventArgs.Context.ComputedPoint, false);
                                    var param = pline.GetParameterAtPoint(p);
                                    var vx = Convert.ToInt32(Math.Truncate(param));
                                    if (pline.GetSegmentType(vx) == SegmentType.Arc)
                                    {
                                        var nextVx = vx + 1;
                                        if (vx == pline.NumberOfVertices - 1)
                                            if (pline.Closed)
                                                nextVx = 0;
                                        var line = new Line(
                                            pline.GetPoint3dAt(vx),
                                            pline.GetPoint3dAt(nextVx)
                                            )
                                        {
                                            ColorIndex = HelpGeometryColor
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
                //ignored
            }
        }

        /// <summary>
        /// Заменить линейный (или любой) сегмент дуговым
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-Line2Arc", CommandFlags.UsePickSet)]
        public static void mpPl_Line2Arc()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;
                var workType = "Tangent";
                while (true)
                {
                    var peo = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "k16") + ":")
                    {
                        AllowNone = false,
                        AllowObjectOnLockedLayer = true
                    };
                    peo.SetRejectMessage("\n" + Language.GetItem(LangItem, "wrong"));
                    peo.AddAllowedClass(typeof(Polyline), true);

                    var per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) return;

                    var plineId = per.ObjectId;
                    var pickedPt = ModPlus.Helpers.AutocadHelpers.UcsToWcs(per.PickedPoint);

                    using (doc.LockDocument())
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var dbObj = tr.GetObject(plineId, OpenMode.ForWrite);

                            if (dbObj is Polyline)
                            {
                                var pline = dbObj as Polyline;
                                var p = pline.GetClosestPointTo(pickedPt, false);
                                var param = pline.GetParameterAtPoint(p);
                                var vx = Convert.ToInt32(Math.Truncate(param));
                                var jig = new LineToArcSegment();
                                var jres = jig.StartJig(pline, pline.GetPoint3dAt(vx), vx, workType);
                                if (jres.Status != PromptStatus.OK) return;
                                workType = jig.WorkType();
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
        }

        private class LineToArcSegment : DrawJig
        {
            private Point3d _prevPoint;
            private Point3d _currPoint;
            private Point3d _startPoint;
            private Polyline _pline;
            private int _vertex;
            private string _workType;
            private double _startBulge;

            public PromptResult StartJig(Polyline pline, Point3d fPt, int vx, string workType)
            {
                _prevPoint = fPt;
                _pline = pline;
                _startPoint = fPt;
                _vertex = vx;
                _startBulge = _pline.GetBulgeAt(_vertex);
                _workType = workType;
                return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
            }

            public string WorkType()
            {
                return _workType;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var ppo = new JigPromptPointOptions("");
                while (true)
                {
                    if (_workType.Equals("Tangent"))
                        ppo.SetMessageAndKeywords("\n" + Language.GetItem(LangItem, "k17"), "Tangent Point");
                    if (_workType.Equals("Point"))
                        ppo.SetMessageAndKeywords("\n" + Language.GetItem(LangItem, "k18"), "Tangent Point");
                    ppo.BasePoint = _startPoint;
                    ppo.UseBasePoint = true;
                    ppo.UserInputControls = UserInputControls.Accept3dCoordinates
                                            | UserInputControls.NullResponseAccepted
                                            | UserInputControls.AcceptOtherInputString
                                            | UserInputControls.NoNegativeResponseAccepted;

                    var ppr = prompts.AcquirePoint(ppo);

                    if (ppr.Status == PromptStatus.Keyword)
                    {
                        _workType = ppr.StringResult;
                    }
                    else if (ppr.Status == PromptStatus.OK)
                    {
                        _currPoint = ppr.Value;

                        if (CursorHasMoved())
                        {
                            _prevPoint = _currPoint;
                            return SamplerStatus.OK;
                        }
                        return SamplerStatus.NoChange;
                    }
                    else if (ppr.Status != PromptStatus.OK | ppr.Status != PromptStatus.Keyword)
                        return SamplerStatus.Cancel;
                }

            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                var line = new Line(_startPoint, _currPoint)
                {
                    ColorIndex = HelpGeometryColor
                };
                draw.Geometry.Draw(line);
                _pline.SetBulgeAt(_vertex, _startBulge);
                // pline edit
                var tangent = _currPoint - _startPoint;
                int? nextVertex;
                if (_vertex != _pline.NumberOfVertices - 1)
                    nextVertex = _vertex + 1;
                else if (_pline.Closed)
                    nextVertex = 0;
                else return true;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (nextVertex != null)
                {
                    var chordVector = _pline.GetPoint3dAt(nextVertex.Value) - _startPoint;
                    // По касательной
                    if (_workType.Equals("Tangent"))
                    {
                        var bulge = Math.Tan(tangent.GetAngleTo(chordVector) / 2);
                        if ((tangent.GetAngleTo(chordVector, _pline.Normal) > Math.PI))
                            bulge = -bulge;
                        _pline.SetBulgeAt(_vertex, bulge);
                        draw.Geometry.Draw(_pline);
                    }
                    // По точке прохождения
                    else if (_workType.Equals("Point"))
                    {
                        // Строим вспомогательную геометрию в виде дуги для получения полного угла
                        var cArc = new CircularArc3d(_startPoint, _currPoint, _pline.GetPoint3dAt(nextVertex.Value));
                        var angle = cArc.ReferenceVector.AngleOnPlane(new Plane(cArc.Center, cArc.Normal));
                        var arc = new Arc(cArc.Center, cArc.Normal, cArc.Radius,
                            cArc.StartAngle + angle, cArc.EndAngle + angle);

                        var bulge = Math.Tan(arc.TotalAngle / 4);
                        if ((tangent.GetAngleTo(chordVector, _pline.Normal) > Math.PI))
                            bulge = -bulge;

                        _pline.SetBulgeAt(_vertex, bulge);
                        draw.Geometry.Draw(_pline);
                    }
                }
                return true;
            }

            private bool CursorHasMoved()
            {
                return _currPoint.DistanceTo(_prevPoint) > Tolerance.Global.EqualPoint;
            }
        }

        /// <summary>
        /// Динамическое добавление вершины
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-AddVertex", CommandFlags.UsePickSet)]
        public static void mpPl_AddVertex()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                var peo = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "k13") + ":")
                {
                    AllowNone = false,
                    AllowObjectOnLockedLayer = true
                };
                peo.SetRejectMessage("\n" + Language.GetItem(LangItem, "wrong"));
                peo.AddAllowedClass(typeof(Polyline), true);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                var plineId = per.ObjectId;
                var loop = true;

                using (doc.LockDocument())
                {
                    while (loop)
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var dbObj = tr.GetObject(plineId, OpenMode.ForWrite);

                            if (dbObj is Polyline)
                            {

                                var pline = dbObj as Polyline;
                                var jig = new AddVertexJig();
                                var jres = jig.StartJig(pline);
                                if (jres.Status != PromptStatus.OK) loop = false;
                                else
                                {
                                    if (!pline.IsWriteEnabled) pline.UpgradeOpen();
                                    pline.AddVertexAt(jig.Vertex() + 1, jig.PickedPoint(), 0.0, 0.0, 0.0);
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
        }

        private class AddVertexJig : DrawJig
        {
            private Point3d _prevPoint;
            private Point3d _currPoint;
            private Point3d _startPoint;
            private Polyline _pline;
            private int _vertex;

            public PromptResult StartJig(Polyline pline)
            {
                _pline = pline;
                _prevPoint = _pline.GetPoint3dAt(0);
                _startPoint = _pline.GetPoint3dAt(0);

                return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
            }

            public int Vertex()
            {
                return _vertex;
            }

            public Point2d PickedPoint()
            {
                return new Point2d(_currPoint.X, _currPoint.Y);
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var ppo = new JigPromptPointOptions("\n" + Language.GetItem(LangItem, "k19") + ":")
                {
                    BasePoint = _startPoint,
                    UseBasePoint = true,
                    UserInputControls = UserInputControls.Accept3dCoordinates
                                        | UserInputControls.NullResponseAccepted
                                        | UserInputControls.AcceptOtherInputString
                                        | UserInputControls.NoNegativeResponseAccepted
                };


                var ppr = prompts.AcquirePoint(ppo);

                if (ppr.Status != PromptStatus.OK)
                    return SamplerStatus.Cancel;

                if (ppr.Status == PromptStatus.OK)
                {
                    _currPoint = ppr.Value;

                    if (CursorHasMoved())
                    {
                        _prevPoint = _currPoint;
                        return SamplerStatus.OK;
                    }
                    return SamplerStatus.NoChange;
                }

                return SamplerStatus.NoChange;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                var mods = System.Windows.Forms.Control.ModifierKeys;
                var control = (mods & System.Windows.Forms.Keys.Control) > 0;
                var pt = _pline.GetClosestPointTo(_currPoint, false);
                var param = _pline.GetParameterAtPoint(pt);
                _vertex = Convert.ToInt32(Math.Truncate(param));
                var maxVx = _pline.NumberOfVertices - 1;
                if (control)
                {
                    if (_vertex < maxVx)
                        _vertex++;
                }

                if (_vertex != maxVx)
                {
                    // Если вершина не последня
                    var line1 = new Line(_pline.GetPoint3dAt(_vertex), _currPoint)
                    {
                        ColorIndex = HelpGeometryColor
                    };
                    draw.Geometry.Draw(line1);
                    var line2 = new Line(_pline.GetPoint3dAt(_vertex + 1), _currPoint)
                    {
                        ColorIndex = HelpGeometryColor
                    };
                    draw.Geometry.Draw(line2);
                }
                else
                {
                    var line1 = new Line(_pline.GetPoint3dAt(_vertex), _currPoint)
                    {
                        ColorIndex = HelpGeometryColor
                    };
                    draw.Geometry.Draw(line1);
                    if (_pline.Closed)
                    {
                        // Если полилиния замкнута, то рисуем отрезок к первой вершине
                        var line2 = new Line(_pline.GetPoint3dAt(0), _currPoint)
                        {
                            ColorIndex = HelpGeometryColor
                        };
                        draw.Geometry.Draw(line2);
                    }
                }
                return true;
            }

            private bool CursorHasMoved()
            {
                return _currPoint.DistanceTo(_prevPoint) > Tolerance.Global.EqualPoint;
            }
        }

        /// <summary>
        /// Отрисовка прямоугольника по трем точкам
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-Rect3Pt", CommandFlags.Redraw)]
        public static void mpPl_Rect3Pt()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;
                // first point
                var ppo = new PromptPointOptions("\n" + Language.GetItem(LangItem, "k20") + ":")
                {
                    UseBasePoint = false,
                    AllowNone = true
                };

                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) return;
                //var fPt = ModPlus.MpCadHelpers.UcsToWcs(ppr.Value);
                var fPt = ppr.Value;
                // second point
                ppo = new PromptPointOptions("\n" + Language.GetItem(LangItem, "k21") + ":")
                {
                    UseBasePoint = true,
                    BasePoint = fPt,
                    UseDashedLine = true,
                    AllowNone = true
                };

                ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) return;
                var sPt = ppr.Value;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // jig
                    var jig = new Rect3PtJig();
                    var jr = jig.StartJig(fPt, sPt);

                    if (jr.Status != PromptStatus.OK) return;
                    // draw pline
                    var pline = jig.Poly();

                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                    btr.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                    tr.Commit();
                }
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }

        class Rect3PtJig : DrawJig
        {
            private Point3d _prevPoint;
            private Point3d _currPoint;
            private Point3d _fPoint;
            private Point3d _sPoint;
            private Polyline _polyline;
            private Editor _ed;

            public PromptResult StartJig(Point3d fPt, Point3d sPt)
            {
                _fPoint = ModPlus.Helpers.AutocadHelpers.UcsToWcs(fPt);
                _sPoint = ModPlus.Helpers.AutocadHelpers.UcsToWcs(sPt);
                _prevPoint = sPt;
                _polyline = new Polyline();
                //return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
                _ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
                PromptResult rs = _ed.Drag(this);
                if (rs.Status == PromptStatus.OK)
                {
                    CalcPline();
                }
                return rs;
            }

            public Polyline Poly()
            {
                return _polyline;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var jppo = new JigPromptPointOptions("\n" + Language.GetItem(LangItem, "k22") + ":")
                {
                    UserInputControls = UserInputControls.Accept3dCoordinates
                                        | UserInputControls.NoNegativeResponseAccepted
                                        | UserInputControls.NullResponseAccepted,
                    BasePoint = _sPoint,
                    UseBasePoint = true,
                    Cursor = CursorType.RubberBand
                };

                var ppr = prompts.AcquirePoint(jppo);

                if (ppr.Status != PromptStatus.OK)
                    return SamplerStatus.Cancel;

                if (ppr.Status == PromptStatus.OK)
                {
                    _currPoint = ppr.Value;

                    if (CursorHasMoved())
                    {
                        _prevPoint = _currPoint;
                        return SamplerStatus.OK;
                    }
                    return SamplerStatus.NoChange;
                }

                return SamplerStatus.NoChange;
            }
            void CalcPline()
            {
                // Строим временный отрезок из точки 1 в точку 2
                var tmpLine = new Line(_fPoint, _sPoint);
                // Строим три вспомогательных вектора
                var vecCurrentToFirst = _currPoint - _fPoint;
                var vecCurrentToSecond = _currPoint - _sPoint;
                var vecSecondToFirst = _sPoint - _fPoint;
                /* Определим катет в треугольнике, которй образуется текущей точкой и второй точкой
                через угол между векторами
                */
                var katet = Math.Sin(vecCurrentToSecond.GetAngleTo(vecSecondToFirst)) * vecCurrentToSecond.Length;
                // Найдем угол между вектором из текущей точки к первой точке и вспомогательной линией
                var angleOnToTmpLinePlane = vecCurrentToFirst.GetAngleTo(vecSecondToFirst, tmpLine.Normal);
                // Получим знак (направление) в зависимости от угла (изменим знак переменной "катет")
                if (angleOnToTmpLinePlane < Math.PI)
                    katet = -katet;
                // Получим 3 точку по направлению и катету и вектор
                var thPoint = _sPoint + vecSecondToFirst.GetPerpendicularVector().GetNormal() * katet;
                var vecThirdToSecond = thPoint - _sPoint;
                // Получим 4 точку по тому-же принципу. Для откладывания длины использовать абс.значение!
                Point3d fourPoint;
                if (angleOnToTmpLinePlane < Math.PI)
                    fourPoint = thPoint - vecThirdToSecond.GetPerpendicularVector().GetNormal() * tmpLine.Length;
                else fourPoint = thPoint + vecThirdToSecond.GetPerpendicularVector().GetNormal() * tmpLine.Length;
                _polyline.Reset(true, 4);
                _polyline.AddVertexAt(0, new Point2d(_fPoint.X, _fPoint.Y), 0.0, 0.0, 0.0);
                _polyline.AddVertexAt(1, new Point2d(_sPoint.X, _sPoint.Y), 0.0, 0.0, 0.0);
                _polyline.AddVertexAt(2, new Point2d(thPoint.X, thPoint.Y), 0.0, 0.0, 0.0);
                _polyline.AddVertexAt(3, new Point2d(fourPoint.X, fourPoint.Y), 0.0, 0.0, 0.0);
                _polyline.SetDatabaseDefaults();
                _polyline.Closed = true;
            }
            protected override bool WorldDraw(WorldDraw draw)
            {
                CalcPline();
                draw.Geometry.Draw(_polyline);
                return true;
            }
            private bool CursorHasMoved()
            {
                return _currPoint.DistanceTo(_prevPoint) > Tolerance.Global.EqualPoint;
            }
        }

        /// <summary>
        /// Удаление из полилинии дуговых сегментов
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-NoArc", CommandFlags.UsePickSet)]
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        public static void mpPl_NoArc()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;
                // settings
                var setWin = new NoArcSettings();
                if (setWin.ShowDialog() == false) return;
                var workType = (setWin.CbWorkType.SelectedItem as ComboBoxItem).Name;
                var deletePLines = setWin.ChkDeletePlines.IsChecked.Value;
                double d;
                var minRadius = double.TryParse(setWin.TbMinRadius.Text, out d) ? d : double.NaN;

                // selection
                var pso = new PromptSelectionOptions
                {
                    AllowDuplicates = false,
                    MessageForAdding = "\n" + Language.GetItem(LangItem, "k2") + ":"
                };
                var filList = new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") };
                var sf = new SelectionFilter(filList);
                var psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK) return;
                var ids = psr.Value.GetObjectIds();
                // work
                using (doc.LockDocument())
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (var objectId in ids)
                        {
                            var sourcePline = tr.GetObject(objectId, OpenMode.ForWrite) as Polyline;
                            Polyline newPline = null;
                            switch (workType)
                            {
                                #region segmentCount
                                case "SegmentCount":
                                    var pio = new PromptIntegerOptions("\n" + Language.GetItem(LangItem, "k23") + ":")
                                    {
                                        DefaultValue = 5,
                                        LowerLimit = 1,
                                        AllowNone = true,
                                        AllowNegative = false
                                    };
                                    var pir = ed.GetInteger(pio);
                                    if (pir.Status != PromptStatus.OK) return;
                                    newPline = new Polyline();
                                    var k = 0;
                                    for (var i = 0; i < sourcePline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePline.GetArcSegment2dAt(i);
                                            if (double.IsNaN(minRadius) || arc.Radius >= minRadius)
                                            {
                                                var lengthOfSegmentPart = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint)) / pir.Value;

                                                for (var j = 0; j < pir.Value; j++)
                                                {
                                                    var pt = sourcePline.GetPointAtDist(
                                                        sourcePline.GetDistAtPoint(sourcePline.GetPoint3dAt(i)) +
                                                        lengthOfSegmentPart * j);
                                                    newPline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePline.GetStartWidthAt(i),
                                                        sourcePline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                    bulge = sourcePline.GetBulgeAt(i);
                                                newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                bulge,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }
                                    break;
                                #endregion
                                #region SegmentLength
                                case "SegmentLength":
                                    var pdo = new PromptDoubleOptions("\n" + Language.GetItem(LangItem, "k24") + ":")
                                    {
                                        DefaultValue = 100,
                                        AllowArbitraryInput = true,
                                        AllowNone = true,
                                        AllowZero = false,
                                        AllowNegative = false
                                    };
                                    var pdr = ed.GetDouble(pdo);
                                    if (pdr.Status != PromptStatus.OK) return;
                                    newPline = new Polyline();
                                    k = 0;
                                    for (var i = 0; i < sourcePline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePline.GetArcSegment2dAt(i);
                                            if ((double.IsNaN(minRadius) || arc.Radius >= minRadius) |
                                                Math.Abs((arc.StartPoint - arc.EndPoint).Length) > pdr.Value)
                                            {
                                                var arcLength = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint));
                                                var lengthOfSegmentPart = pdr.Value;

                                                for (var j = 0; j <= Math.Truncate(arcLength / pdr.Value); j++)
                                                {
                                                    var pt = sourcePline.GetPointAtDist(
                                                        sourcePline.GetDistAtPoint(sourcePline.GetPoint3dAt(i)) +
                                                        lengthOfSegmentPart * j);
                                                    newPline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePline.GetStartWidthAt(i),
                                                        sourcePline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                    bulge = sourcePline.GetBulgeAt(i);
                                                newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                bulge,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }
                                    break;
                                #endregion
                                #region ChordHeight
                                case "ChordHeight":
                                    pdo = new PromptDoubleOptions("\n" + Language.GetItem(LangItem, "k25") + ":")
                                    {
                                        DefaultValue = 0.5,
                                        AllowArbitraryInput = true,
                                        AllowNone = true,
                                        AllowZero = false,
                                        AllowNegative = false
                                    };
                                    pdr = ed.GetDouble(pdo);
                                    if (pdr.Status != PromptStatus.OK) return;
                                    newPline = new Polyline();
                                    k = 0;
                                    for (var i = 0; i < sourcePline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePline.GetArcSegment2dAt(i);
                                            if (double.IsNaN(minRadius) || arc.Radius >= minRadius)
                                            {
                                                var chordLength = GetChordByHeightAndRadius(pdr.Value, arc.Radius);
                                                var angle = AngleByChordAndRadius(chordLength, arc.Radius);
                                                var sLength = (arc.Radius * angle);
                                                var arcLength = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint));


                                                for (var j = 0; j <= Math.Truncate(arcLength / sLength); j++)
                                                {
                                                    var pt = sourcePline.GetPointAtDist(
                                                        sourcePline.GetDistAtPoint(sourcePline.GetPoint3dAt(i)) +
                                                        sLength * j);
                                                    newPline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePline.GetStartWidthAt(i),
                                                        sourcePline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                    bulge = sourcePline.GetBulgeAt(i);
                                                newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                bulge,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }
                                    break;
                                #endregion
                                #region ChordLength
                                case "ChordLength":
                                    pdo = new PromptDoubleOptions("\n" + Language.GetItem(LangItem, "k26") + ":")
                                    {
                                        DefaultValue = 10,
                                        AllowArbitraryInput = true,
                                        AllowNone = true,
                                        AllowZero = false,
                                        AllowNegative = false
                                    };
                                    pdr = ed.GetDouble(pdo);
                                    if (pdr.Status != PromptStatus.OK) return;
                                    newPline = new Polyline();
                                    k = 0;
                                    for (var i = 0; i < sourcePline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePline.GetArcSegment2dAt(i);
                                            if (double.IsNaN(minRadius) || arc.Radius >= minRadius)
                                            //| Math.Abs((arc.StartPoint - arc.EndPoint).Length) > pdr.Value)
                                            {
                                                var angle = AngleByChordAndRadius(pdr.Value, arc.Radius);
                                                var sLength = (arc.Radius * angle);
                                                var arcLength = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint));

                                                for (var j = 0; j <= Math.Truncate(arcLength / sLength); j++)
                                                {
                                                    var pt = sourcePline.GetPointAtDist(
                                                        sourcePline.GetDistAtPoint(sourcePline.GetPoint3dAt(i)) +
                                                        sLength * j);
                                                    newPline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePline.GetStartWidthAt(i),
                                                        sourcePline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                    bulge = sourcePline.GetBulgeAt(i);
                                                newPline.AddVertexAt(
                                                k,
                                                sourcePline.GetPoint2dAt(i),
                                                bulge,
                                                sourcePline.GetStartWidthAt(i),
                                                sourcePline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }
                                    break;
                                    #endregion
                            }
                            if (deletePLines)
                                sourcePline?.Erase(true);
                            // add
                            if (newPline != null)
                            {
                                CopyPlineSettings(newPline, sourcePline);
                                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                                btr.AppendEntity(newPline);
                                tr.AddNewlyCreatedDBObject(newPline, true);
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
        /// <summary>
        /// Получение угла сегмента дуги по радиусу и длине хорды
        /// </summary>
        /// <param name="chord">Длина хорды</param>
        /// <param name="radius">Радиус</param>
        /// <returns></returns>
        private static double AngleByChordAndRadius(double chord, double radius)
        {
            return 2 * Math.Asin(chord / (2 * radius));
        }
        /// <summary>
        /// Получение длины хорды по радиусу и высоте сегмента
        /// </summary>
        /// <param name="heigth">Высота сегмента</param>
        /// <param name="radius">Радиус</param>
        /// <returns></returns>
        private static double GetChordByHeightAndRadius(double heigth, double radius)
        {
            return 2 * Math.Sqrt(heigth * (2 * radius - heigth));
        }

        private static void CopyPlineSettings(Polyline plineTo, Polyline plineFrom)
        {
            plineTo.LayerId = plineFrom.LayerId;
            plineTo.Linetype = plineFrom.Linetype;
            plineTo.LineWeight = plineFrom.LineWeight;
            plineTo.LinetypeScale = plineFrom.LinetypeScale;
            plineTo.Color = plineFrom.Color;
            plineTo.XData = plineFrom.XData;
            plineTo.Closed = plineFrom.Closed;
        }

        /// <summary>
        /// Построение средней линии между двумя указанными кривыми
        /// </summary>
        [CommandMethod("ModPlus", "mpPl-MiddleLine", CommandFlags.Session)]
        public static void mpPl_MiddleLine()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var points = new Point2dCollection();
            var pline = new Polyline();
            try
            {
                // Блокируем документ
                using (doc.LockDocument())
                {
                    // Стартуем транзакцию (т.к. создаем объект)
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        // Выбор первого примитива (кривой)
                        var peo = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "k27") + ":")
                        {
                            AllowNone = false,
                            AllowObjectOnLockedLayer = true
                        };
                        peo.SetRejectMessage("\n" + Language.GetItem(LangItem, "wrong"));
                        peo.AddAllowedClass(typeof(Polyline2d), true);
                        peo.AddAllowedClass(typeof(Polyline3d), true);
                        peo.AddAllowedClass(typeof(Polyline), true);
                        peo.AddAllowedClass(typeof(Line), true);
                        peo.AddAllowedClass(typeof(Spline), true);
                        peo.AddAllowedClass(typeof(Polyline2d), true);

                        var per = ed.GetEntity(peo);
                        if (per.Status != PromptStatus.OK) return;

                        var firstCurve = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Curve;
                        if (firstCurve == null) return;
                        // Выбор второго примитива (кривой)
                        peo.Message = "\n" + Language.GetItem(LangItem, "k28") + ":";
                        per = ed.GetEntity(peo);
                        if (per.Status != PromptStatus.OK) return;
                        var secondCurve = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Curve;
                        if (secondCurve == null) return;
                        // Количество опорных точек
                        int i;
                        var pio = new PromptIntegerOptions("\n"+ Language.GetItem(LangItem, "k29") +":")
                        {
                            DefaultValue =
                                int.TryParse(UserConfigFile.GetValue(UserConfigFile.ConfigFileZone.Settings, "mpPl_MiddleLine", "PointCount"), out i) ? i : 100,
                            LowerLimit = 2,
                            UpperLimit = 1000
                        };

                        var pir = ed.GetInteger(pio);
                        if (pir.Status != PromptStatus.OK) return;
                        var oporPtCount = pir.Value;
                        UserConfigFile.SetValue(UserConfigFile.ConfigFileZone.Settings, "mpPl_MiddleLine", "PointCount", oporPtCount.ToString(CultureInfo.InvariantCulture), true);

                        // Получаем коллекцию точек для построения новой полилинии

                        for (var j = 0; j < oporPtCount; j++)
                        {
                            var firstDist = (firstCurve.GetDistanceAtParameter(firstCurve.EndParam) -
                                             firstCurve.GetDistanceAtParameter(firstCurve.StartParam)) / oporPtCount;
                            var secondDist = (secondCurve.GetDistanceAtParameter(secondCurve.EndParam) -
                                             secondCurve.GetDistanceAtParameter(secondCurve.StartParam)) / oporPtCount;
                            var firstPoint =
                                firstCurve.GetPointAtParameter(firstCurve.GetParameterAtDistance(firstDist * j));
                            //var secondPoint =
                            //    secondCurve.GetPointAtParameter(secondCurve.GetParameterAtDistance(secondDist * j));

                            var secondPoint = secondCurve.GetClosestPointTo(firstPoint, false);

                            points.Add(new Point2d(
                                (firstPoint.X + secondPoint.X) / 2,
                                (firstPoint.Y + secondPoint.Y) / 2
                                ));
                        }
                        // Строим полилинию
                        if (points.Count > 0)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);

                            for (var k = 0; k < points.Count; k++)
                                pline.AddVertexAt(k, points[k], 0.0, 0.0, 0.0);
                            btr.AppendEntity(pline);
                            tr.AddNewlyCreatedDBObject(pline, true);
                        }
                        tr.Commit();
                    }
                    // Запрос на упрощение полилинии
                    if (points.Count > 2)
                        if (MessageBox.ShowYesNo(Language.GetItem(LangItem, "k30"), MessageBoxIcon.Question))
                        {
                            RemovePointsFromPline(pline.ObjectId);
                        }
                }
            }
            catch (System.Exception exception)
            {
                ExceptionBox.Show(exception);
            }
        }

        private static void RemovePointsFromPline(ObjectId objId)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                var pline = tr.GetObject(objId, OpenMode.ForWrite) as Polyline;

                var maxA = 0.0; // Предельное отклонение угла
                var maxH = 0.0; // Предельное отклонение высоты

                var listOfDuplicateVertex = new List<int>();
                for (var i = 0; i < pline?.NumberOfVertices - 2; i++)
                {
                    // Если в текущей вершине или следующей
                    // есть скругление, то пропускаем
                    if (!pline.GetBulgeAt(i).Equals(0.0)) continue;
                    if (!pline.GetBulgeAt(i + 1).Equals(0.0)) continue;
                    // Берем два вектора и сравниваем параллельность
                    var pt1 = pline.GetPoint2dAt(i); // первая
                    var pt2 = pline.GetPoint2dAt(i + 1); // вторая (средняя)
                    var pt3 = pline.GetPoint2dAt(i + 2); // третья

                    if ((pt2 - pt1).IsParallelTo(pt3 - pt2))
                        listOfDuplicateVertex.Add(i + 1);
                    else
                    {
                        // Если не параллельны, то сравниваем по допускам
                        var ang = (pt3 - pt2).GetAngleTo(pt2 - pt1);
                        if (ang <= maxA)
                            listOfDuplicateVertex.Add(i + 1);
                        var h = Math.Abs((pt2 - pt1).Length * Math.Cos(ang));
                        if (h <= maxH)
                            listOfDuplicateVertex.Add(i + 1);
                    }

                }
                if (listOfDuplicateVertex.Count > 0)
                {
                    /*
            при каждом удалении вершины количество вершин меняется
            значит после каждого удаления нужно значение i уменьшать
            на переменную, в которую будет записано кол-во проходов
            */
                    pline?.UpgradeOpen();
                    var j = 0;
                    foreach (var i in listOfDuplicateVertex)
                    {
                        if (j == 0)
                        {
                            pline?.RemoveVertexAt(i);
                        }
                        else
                        {
                            pline?.RemoveVertexAt(i - j);
                        }
                        j++;
                    }
                }
                tr.Commit();
            }
        }
    }
}
