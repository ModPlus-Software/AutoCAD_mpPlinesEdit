using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace mpPlinesEdit
{
    public class DevicePixels : Decorator
    {
        private Point _pixelOffset;

        public DevicePixels()
        {
            SnapsToDevicePixels = true;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            LayoutUpdated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            var pixelOffset = GetPixelOffset();
            if (AreClose(pixelOffset, _pixelOffset))
                return;
            _pixelOffset = pixelOffset;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var child = Child;
            if (child != null)
            {
                var finalRect = HelperDeflateRect(new Rect(arrangeSize), _pixelOffset);
                child.Arrange(finalRect);
            }
            return arrangeSize;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var child = Child;
            if (child == null)
                return new Size();
            var availableSize = new Size(Math.Max(0.0, constraint.Width - _pixelOffset.X), Math.Max(0.0, constraint.Height - _pixelOffset.Y));
            child.Measure(availableSize);
            var desiredSize = child.DesiredSize;
            return new Size(desiredSize.Width + _pixelOffset.X, desiredSize.Height + _pixelOffset.Y);
        }

        private static Rect HelperDeflateRect(Rect rt, Point offset)
        {
            return new Rect(rt.Left + offset.X, rt.Top + offset.Y, Math.Max(0.0, rt.Width - offset.X), Math.Max(0.0, rt.Height - offset.Y));
        }

        private static Matrix GetVisualTransform(Visual v)
        {
            if (v == null)
                return Matrix.Identity;
            var trans1 = Matrix.Identity;
            var transform = VisualTreeHelper.GetTransform(v);
            if (transform != null)
            {
                var trans2 = transform.Value;
                trans1 = Matrix.Multiply(trans1, trans2);
            }
            var offset = VisualTreeHelper.GetOffset(v);
            trans1.Translate(offset.X, offset.Y);
            return trans1;
        }

        private static Point TryApplyVisualTransform(Point point, Visual v, bool inverse, bool throwOnError, out bool success)
        {
            success = true;
            if (v != null)
            {
                var visualTransform = GetVisualTransform(v);
                if (inverse)
                {
                    if (!throwOnError && !visualTransform.HasInverse)
                    {
                        success = false;
                        return new Point(0.0, 0.0);
                    }
                    visualTransform.Invert();
                }
                point = visualTransform.Transform(point);
            }
            return point;
        }

        private static Point ApplyVisualTransform(Point point, Visual v, bool inverse)
        {
            bool success;
            return TryApplyVisualTransform(point, v, inverse, true, out success);
        }

        private Point GetPixelOffset()
        {
            var point1 = new Point();
            var presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource != null)
            {
                var rootVisual = presentationSource.RootVisual;
                var point2 = ApplyVisualTransform(TransformToAncestor(rootVisual).Transform(point1), rootVisual, false);
                point1 = presentationSource.CompositionTarget.TransformToDevice.Transform(point2);
                point1.X = Math.Ceiling(point1.X);
                point1.Y = Math.Ceiling(point1.Y);
                point1 = presentationSource.CompositionTarget.TransformFromDevice.Transform(point1);
                point1 = ApplyVisualTransform(point1, rootVisual, true);
                point1 = rootVisual.TransformToDescendant(this).Transform(point1);
            }
            return point1;
        }

        private static bool AreClose(Point point1, Point point2)
        {
            return AreClose(point1.X, point2.X) && AreClose(point1.Y, point2.Y);
        }

        private static bool AreClose(double value1, double value2)
        {
            if (value1 == value2)
                return true;
            var num = value1 - value2;
            return num < 1.53E-06 && num > -1.53E-06;
        }
    }
}