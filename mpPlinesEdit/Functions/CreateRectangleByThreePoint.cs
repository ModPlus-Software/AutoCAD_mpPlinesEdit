namespace mpPlinesEdit.Functions
{
    using System;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.GraphicsInterface;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;
    using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

    /// <summary>
    /// Отрисовка прямоугольника по трем точкам
    /// </summary>
    public class CreateRectangleByThreePoint
    {
        [CommandMethod("ModPlus", "mpPl-Rect3Pt", CommandFlags.Redraw)]
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

                // first point
                var ppo = new PromptPointOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k20")}:")
                {
                    UseBasePoint = false,
                    AllowNone = true
                };

                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK)
                {
                    return;
                }

                // var fPt = ModPlus.MpCadHelpers.UcsToWcs(ppr.Value);
                var fPt = ppr.Value;

                // second point
                ppo = new PromptPointOptions($"\n{Language.GetItem(PlinesEditFunction.LangItem, "k21")}:")
                {
                    UseBasePoint = true,
                    BasePoint = fPt,
                    UseDashedLine = true,
                    AllowNone = true
                };

                ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK)
                {
                    return;
                }

                var sPt = ppr.Value;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // jig
                    var jig = new Rect3PtJig();
                    var jr = jig.StartJig(fPt, sPt);

                    if (jr.Status != PromptStatus.OK)
                    {
                        return;
                    }

                    // draw polyline
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

        internal class Rect3PtJig : DrawJig
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

                // return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
                _ed = Application.DocumentManager.MdiActiveDocument.Editor;
                PromptResult rs = _ed.Drag(this);
                if (rs.Status == PromptStatus.OK)
                {
                    CalculatePolyline();
                }

                return rs;
            }

            public Polyline Poly()
            {
                return _polyline;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var jigPromptPointOptions = new JigPromptPointOptions(
                    $"\n{Language.GetItem(PlinesEditFunction.LangItem, "k22")}:")
                {
                    UserInputControls = UserInputControls.Accept3dCoordinates
                                        | UserInputControls.NoNegativeResponseAccepted
                                        | UserInputControls.NullResponseAccepted,
                    BasePoint = _sPoint,
                    UseBasePoint = true,
                    Cursor = CursorType.RubberBand
                };

                var ppr = prompts.AcquirePoint(jigPromptPointOptions);

                if (ppr.Status != PromptStatus.OK)
                {
                    return SamplerStatus.Cancel;
                }

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

            private void CalculatePolyline()
            {
                // Строим временный отрезок из точки 1 в точку 2
                var tmpLine = new Line(_fPoint, _sPoint);

                // Строим три вспомогательных вектора
                var vecCurrentToFirst = _currPoint - _fPoint;
                var vecCurrentToSecond = _currPoint - _sPoint;
                var vecSecondToFirst = _sPoint - _fPoint;
                /* Определим катет в треугольнике, которой образуется текущей точкой и второй точкой
                через угол между векторами
                */
                var katet = Math.Sin(vecCurrentToSecond.GetAngleTo(vecSecondToFirst)) * vecCurrentToSecond.Length;

                // Найдем угол между вектором из текущей точки к первой точке и вспомогательной линией
                var angleOnToTmpLinePlane = vecCurrentToFirst.GetAngleTo(vecSecondToFirst, tmpLine.Normal);

                // Получим знак (направление) в зависимости от угла (изменим знак переменной "катет")
                if (angleOnToTmpLinePlane < Math.PI)
                {
                    katet = -katet;
                }

                // Получим 3 точку по направлению и катету и вектор
                var thPoint = _sPoint + (vecSecondToFirst.GetPerpendicularVector().GetNormal() * katet);
                var vecThirdToSecond = thPoint - _sPoint;

                // Получим 4 точку по тому-же принципу. Для откладывания длины использовать абс.значение!
                Point3d fourPoint;
                if (angleOnToTmpLinePlane < Math.PI)
                {
                    fourPoint = thPoint - (vecThirdToSecond.GetPerpendicularVector().GetNormal() * tmpLine.Length);
                }
                else
                {
                    fourPoint = thPoint + (vecThirdToSecond.GetPerpendicularVector().GetNormal() * tmpLine.Length);
                }

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
                CalculatePolyline();
                draw.Geometry.Draw(_polyline);
                return true;
            }

            private bool CursorHasMoved()
            {
                return _currPoint.DistanceTo(_prevPoint) > Tolerance.Global.EqualPoint;
            }
        }
    }
}
