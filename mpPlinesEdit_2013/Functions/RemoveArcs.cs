namespace mpPlinesEdit.Functions
{
    using System;
    using System.Windows.Controls;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.Runtime;
    using Help;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <summary>
    /// Удаление из полилинии дуговых сегментов
    /// </summary>
    public class RemoveArcs
    {
        [CommandMethod("ModPlus", "mpPl-NoArc", CommandFlags.UsePickSet)]
        public static void StartFunction()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                var ed = doc.Editor;

                // settings
                var setWin = new NoArcSettings();
                if (setWin.ShowDialog() == false)
                {
                    return;
                }

                var workType = (setWin.CbWorkType.SelectedItem as ComboBoxItem)?.Name;
                var deletePLines = setWin.ChkDeletePlines.IsChecked != null && setWin.ChkDeletePlines.IsChecked.Value;
                var minRadius = double.TryParse(setWin.TbMinRadius.Text, out var d) ? d : double.NaN;

                // selection
                var pso = new PromptSelectionOptions
                {
                    AllowDuplicates = false,
                    MessageForAdding = "\n" + Language.GetItem(PlinesEditFunction.LangItem, "k2") + ":"
                };
                var filList = new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") };
                var sf = new SelectionFilter(filList);
                var psr = ed.GetSelection(pso, sf);
                if (psr.Status != PromptStatus.OK)
                {
                    return;
                }

                var ids = psr.Value.GetObjectIds();

                // work
                using (doc.LockDocument())
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (var objectId in ids)
                        {
                            var sourcePolyline = tr.GetObject(objectId, OpenMode.ForWrite) as Polyline;
                            Polyline newPolyline = null;
                            switch (workType)
                            {
                                #region segmentCount
                                case "SegmentCount":
                                    var pio = new PromptIntegerOptions("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k23") + ":")
                                    {
                                        DefaultValue = 5,
                                        LowerLimit = 1,
                                        AllowNone = true,
                                        AllowNegative = false
                                    };
                                    var pir = ed.GetInteger(pio);
                                    if (pir.Status != PromptStatus.OK)
                                    {
                                        return;
                                    }

                                    newPolyline = new Polyline();
                                    var k = 0;
                                    for (var i = 0; i < sourcePolyline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePolyline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPolyline.AddVertexAt(
                                                k,
                                                sourcePolyline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePolyline.GetStartWidthAt(i),
                                                sourcePolyline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePolyline.GetArcSegment2dAt(i);
                                            if (double.IsNaN(minRadius) || arc.Radius >= minRadius)
                                            {
                                                var lengthOfSegmentPart = arc.GetLength(
                                                                              arc.GetParameterOf(arc.StartPoint),
                                                                              arc.GetParameterOf(arc.EndPoint)) / pir.Value;

                                                for (var j = 0; j < pir.Value; j++)
                                                {
                                                    var pt = sourcePolyline.GetPointAtDist(
                                                        sourcePolyline.GetDistAtPoint(sourcePolyline.GetPoint3dAt(i)) +
                                                        (lengthOfSegmentPart * j));
                                                    newPolyline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePolyline.GetStartWidthAt(i),
                                                        sourcePolyline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                {
                                                    bulge = sourcePolyline.GetBulgeAt(i);
                                                }

                                                newPolyline.AddVertexAt(
                                                    k,
                                                    sourcePolyline.GetPoint2dAt(i),
                                                    bulge,
                                                    sourcePolyline.GetStartWidthAt(i),
                                                    sourcePolyline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }

                                    break;
                                #endregion
                                #region SegmentLength
                                case "SegmentLength":
                                    var pdo = new PromptDoubleOptions("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k24") + ":")
                                    {
                                        DefaultValue = 100,
                                        AllowArbitraryInput = true,
                                        AllowNone = true,
                                        AllowZero = false,
                                        AllowNegative = false
                                    };
                                    var pdr = ed.GetDouble(pdo);
                                    if (pdr.Status != PromptStatus.OK)
                                    {
                                        return;
                                    }

                                    newPolyline = new Polyline();
                                    k = 0;
                                    for (var i = 0; i < sourcePolyline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePolyline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPolyline.AddVertexAt(
                                                k,
                                                sourcePolyline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePolyline.GetStartWidthAt(i),
                                                sourcePolyline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePolyline.GetArcSegment2dAt(i);
                                            if ((double.IsNaN(minRadius) || arc.Radius >= minRadius) |
                                                Math.Abs((arc.StartPoint - arc.EndPoint).Length) > pdr.Value)
                                            {
                                                var arcLength = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint));
                                                var lengthOfSegmentPart = pdr.Value;

                                                for (var j = 0; j <= Math.Truncate(arcLength / pdr.Value); j++)
                                                {
                                                    var pt = sourcePolyline.GetPointAtDist(
                                                        sourcePolyline.GetDistAtPoint(sourcePolyline.GetPoint3dAt(i)) +
                                                        (lengthOfSegmentPart * j));
                                                    newPolyline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePolyline.GetStartWidthAt(i),
                                                        sourcePolyline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                {
                                                    bulge = sourcePolyline.GetBulgeAt(i);
                                                }

                                                newPolyline.AddVertexAt(
                                                    k,
                                                    sourcePolyline.GetPoint2dAt(i),
                                                    bulge,
                                                    sourcePolyline.GetStartWidthAt(i),
                                                    sourcePolyline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }

                                    break;
                                #endregion
                                #region ChordHeight
                                case "ChordHeight":
                                    pdo = new PromptDoubleOptions("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k25") + ":")
                                    {
                                        DefaultValue = 0.5,
                                        AllowArbitraryInput = true,
                                        AllowNone = true,
                                        AllowZero = false,
                                        AllowNegative = false
                                    };
                                    pdr = ed.GetDouble(pdo);
                                    if (pdr.Status != PromptStatus.OK)
                                    {
                                        return;
                                    }

                                    newPolyline = new Polyline();
                                    k = 0;
                                    for (var i = 0; i < sourcePolyline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePolyline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPolyline.AddVertexAt(
                                                k,
                                                sourcePolyline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePolyline.GetStartWidthAt(i),
                                                sourcePolyline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePolyline.GetArcSegment2dAt(i);
                                            if (double.IsNaN(minRadius) || arc.Radius >= minRadius)
                                            {
                                                var chordLength = GetChordByHeightAndRadius(pdr.Value, arc.Radius);
                                                var angle = AngleByChordAndRadius(chordLength, arc.Radius);
                                                var sLength = arc.Radius * angle;
                                                var arcLength = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint));

                                                for (var j = 0; j <= Math.Truncate(arcLength / sLength); j++)
                                                {
                                                    var pt = sourcePolyline.GetPointAtDist(
                                                        sourcePolyline.GetDistAtPoint(sourcePolyline.GetPoint3dAt(i)) +
                                                        (sLength * j));
                                                    newPolyline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePolyline.GetStartWidthAt(i),
                                                        sourcePolyline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                {
                                                    bulge = sourcePolyline.GetBulgeAt(i);
                                                }

                                                newPolyline.AddVertexAt(
                                                    k,
                                                    sourcePolyline.GetPoint2dAt(i),
                                                    bulge,
                                                    sourcePolyline.GetStartWidthAt(i),
                                                    sourcePolyline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }

                                    break;
                                #endregion
                                #region ChordLength
                                case "ChordLength":
                                    pdo = new PromptDoubleOptions("\n" + Language.GetItem(PlinesEditFunction.LangItem, "k26") + ":")
                                    {
                                        DefaultValue = 10,
                                        AllowArbitraryInput = true,
                                        AllowNone = true,
                                        AllowZero = false,
                                        AllowNegative = false
                                    };
                                    pdr = ed.GetDouble(pdo);
                                    if (pdr.Status != PromptStatus.OK)
                                    {
                                        return;
                                    }

                                    newPolyline = new Polyline();
                                    k = 0;
                                    for (var i = 0; i < sourcePolyline?.NumberOfVertices; i++)
                                    {
                                        if (sourcePolyline.GetSegmentType(i) != SegmentType.Arc)
                                        {
                                            newPolyline.AddVertexAt(
                                                k,
                                                sourcePolyline.GetPoint2dAt(i),
                                                0.0,
                                                sourcePolyline.GetStartWidthAt(i),
                                                sourcePolyline.GetEndWidthAt(i));
                                            k++;
                                        }
                                        else
                                        {
                                            var arc = sourcePolyline.GetArcSegment2dAt(i);
                                            if (double.IsNaN(minRadius) || arc.Radius >= minRadius)
                                            {
                                                var angle = AngleByChordAndRadius(pdr.Value, arc.Radius);
                                                var sLength = arc.Radius * angle;
                                                var arcLength = arc.GetLength(
                                                    arc.GetParameterOf(arc.StartPoint),
                                                    arc.GetParameterOf(arc.EndPoint));

                                                for (var j = 0; j <= Math.Truncate(arcLength / sLength); j++)
                                                {
                                                    var pt = sourcePolyline.GetPointAtDist(
                                                        sourcePolyline.GetDistAtPoint(sourcePolyline.GetPoint3dAt(i)) +
                                                        (sLength * j));
                                                    newPolyline.AddVertexAt(
                                                        k,
                                                        new Point2d(pt.X, pt.Y),
                                                        0.0,
                                                        sourcePolyline.GetStartWidthAt(i),
                                                        sourcePolyline.GetEndWidthAt(i));
                                                    k++;
                                                }
                                            }
                                            else
                                            {
                                                var bulge = 0.0;
                                                if (arc.Radius < minRadius)
                                                {
                                                    bulge = sourcePolyline.GetBulgeAt(i);
                                                }

                                                newPolyline.AddVertexAt(
                                                    k,
                                                    sourcePolyline.GetPoint2dAt(i),
                                                    bulge,
                                                    sourcePolyline.GetStartWidthAt(i),
                                                    sourcePolyline.GetEndWidthAt(i));
                                                k++;
                                            }
                                        }
                                    }

                                    break;
                                    #endregion
                            }

                            if (deletePLines)
                            {
                                sourcePolyline?.Erase(true);
                            }

                            // add
                            if (newPolyline != null)
                            {
                                CopyPolylineSettings(newPolyline, sourcePolyline);
                                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                                btr.AppendEntity(newPolyline);
                                tr.AddNewlyCreatedDBObject(newPolyline, true);
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
        /// <param name="height">Высота сегмента</param>
        /// <param name="radius">Радиус</param>
        /// <returns></returns>
        private static double GetChordByHeightAndRadius(double height, double radius)
        {
            return 2 * Math.Sqrt(height * ((2 * radius) - height));
        }

        private static void CopyPolylineSettings(Polyline polylineTo, Polyline polylineFrom)
        {
            polylineTo.LayerId = polylineFrom.LayerId;
            polylineTo.Linetype = polylineFrom.Linetype;
            polylineTo.LineWeight = polylineFrom.LineWeight;
            polylineTo.LinetypeScale = polylineFrom.LinetypeScale;
            polylineTo.Color = polylineFrom.Color;
            polylineTo.XData = polylineFrom.XData;
            polylineTo.Closed = polylineFrom.Closed;
        }
    }
}
