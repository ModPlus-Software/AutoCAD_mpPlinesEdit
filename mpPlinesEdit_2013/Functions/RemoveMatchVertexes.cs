namespace mpPlinesEdit.Functions
{
    using System.Collections.Generic;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <summary>
    /// Удаление совпадающих вершин полилинии
    /// </summary>
    public class RemoveMatchVertexes
    {
        [CommandMethod("ModPlus", "mpPl-VxMatchRemove", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                var pso = new PromptSelectionOptions
                {
                    AllowDuplicates = false,
                    MessageForAdding = "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k2") + ":"
                };
                var filList = new[] { new TypedValue((int)DxfCode.Start, "*POLYLINE") };
                var sf = new SelectionFilter(filList);
                var psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK)
                    return;
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
                            if (dbObj is Polyline polyline)
                            {
                                var listOfDuplicateVertex = new List<int>();
                                for (var i = 0; i < polyline.NumberOfVertices - 1; i++)
                                {
                                    if (polyline.GetPoint2dAt(i).Equals(polyline.GetPoint2dAt(i + 1)))
                                        listOfDuplicateVertex.Add(i + 1);
                                }

                                if (listOfDuplicateVertex.Count > 0)
                                {
                                    /*
                                    при каждом удалении вершины количество вершин меняется
                                    значит после каждого удаления нужно нужно значение i уменьшать
                                    на переменную, в которую будет записано кол-во проходов
                                    */
                                    polyline.UpgradeOpen();
                                    var j = 0;
                                    foreach (var i in listOfDuplicateVertex)
                                    {
                                        if (j == 0)
                                        {
                                            var bulge = polyline.GetBulgeAt(i);
                                            if (!bulge.Equals(0.0))
                                                polyline.SetBulgeAt(i - 1, bulge);
                                            polyline.RemoveVertexAt(i);
                                        }
                                        else
                                        {
                                            var bulge = polyline.GetBulgeAt(i - j);
                                            if (!bulge.Equals(0.0))
                                                polyline.SetBulgeAt(i - j - 1, bulge);
                                            polyline.RemoveVertexAt(i - j);
                                        }

                                        j++;
                                    }

                                    deleted = listOfDuplicateVertex.Count;
                                }
                            }
                            else if (dbObj is Polyline2d polyline2d)
                            {
                                // Будем запоминать предыдущую и сравнивать с текущей
                                var i = 0;
                                Vertex2d vrBefore = null;
                                foreach (ObjectId vId in polyline2d)
                                {
                                    var v2D = (Vertex2d)tr.GetObject(vId, OpenMode.ForRead);
                                    if (vrBefore == null)
                                    {
                                        vrBefore = v2D;
                                    }
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
                                    polyline2d.UpgradeOpen();
                                    polyline2d.RecordGraphicsModified(true);
                                    deleted = i;
                                }
                            }
                            else if (dbObj is Polyline3d polyline3d)
                            {
                                // Будем запоминать предыдущую и сравнивать с текущей
                                var i = 0;
                                PolylineVertex3d vrBefore = null;
                                foreach (ObjectId vId in polyline3d)
                                {
                                    var v3D = (PolylineVertex3d)tr.GetObject(vId, OpenMode.ForRead);
                                    if (vrBefore == null)
                                    {
                                        vrBefore = v3D;
                                    }
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
                                    polyline3d.UpgradeOpen();
                                    polyline3d.RecordGraphicsModified(true);
                                    deleted = i;
                                }
                            }

                            // Вывод результата
                            if (deleted > 0)
                            {
                                if (plCount == 1)
                                {
                                    MessageBox.Show(Language.GetItem(PlinesEditFunction.LangItem, "k3") + ":" + deleted);
                                }
                                else
                                {
                                    ed.WriteMessage(
                                        "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k4") + ":" + objectId + " " +
                                        Language.GetItem(PlinesEditFunction.LangItem, "k3").ToLower() + ":" + deleted);
                                }
                            }
                            else
                            {
                                if (plCount == 1)
                                {
                                    MessageBox.Show(
                                        Language.GetItem(PlinesEditFunction.LangItem, "k4") + " " + 
                                        Language.GetItem(PlinesEditFunction.LangItem, "k5"));
                                }
                                else
                                {
                                    ed.WriteMessage(
                                        "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k4") + " " + objectId + " " + 
                                        Language.GetItem(PlinesEditFunction.LangItem, "k5"));
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
    }
}
