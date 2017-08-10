using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BezierBorders
{
    /// <summary>
    ///     Interaction logic for AdvancedBorder.xaml
    /// </summary>
    public class AdvancedBorder : Border
    {
        private static StreamGeometry BorderGeometryField = new StreamGeometry();
        private static StreamGeometry BackgroundGeometryField = new StreamGeometry();
        private static StreamGeometry[] BorderGeometries = new StreamGeometry[4];
        private static Pen penLeftPenField = new Pen();
        private static Pen penRightPenField = new Pen();
        private static Pen penTopPenField = new Pen();
        private static Pen penBottomPenField = new Pen();
        private bool useComplexRenderCodePath;


        private static bool AreUniformCorners(CornerRadius borderRadii)
        {
            var topLeft = borderRadii.TopLeft;
            return DoubleUtil.AreClose(topLeft, borderRadii.TopRight) &&
                   DoubleUtil.AreClose(topLeft, borderRadii.BottomLeft) &&
                   DoubleUtil.AreClose(topLeft, borderRadii.BottomRight);
        }

        /// Helper to deflate rectangle by thickness
        private static Rect HelperDeflateRect(Rect rt, Thickness thick)
        {
            return new Rect(rt.Left + thick.Left,
                rt.Top + thick.Top,
                Math.Max(0.0, rt.Width - thick.Left - thick.Right),
                Math.Max(0.0, rt.Height - thick.Top - thick.Bottom));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var borders = BorderThickness;
            var boundRect = new Rect(finalSize);
            var innerRect = HelperDeflateRect(boundRect, borders);

            //  arrange child
            var child = Child;
            if (child != null)
            {
                var childRect = HelperDeflateRect(innerRect, Padding);
                child.Arrange(childRect);
            }

            var radii = CornerRadius;
            var borderBrush = base.BorderBrush;
            var uniformCorners = AreUniformCorners(radii);

            //  decide which code path to execute. complex (geometry path based) rendering 
            //  is used if one of the following is true:

            //  1. there are non-uniform rounded corners
            useComplexRenderCodePath = !uniformCorners;

            if (!useComplexRenderCodePath
                && borderBrush != null)
            {
                var originIndependentBrush = borderBrush as SolidColorBrush;

                //bool uniformBorders = borders.IsUniform;

                useComplexRenderCodePath = true;
            }

            if (useComplexRenderCodePath)
            {
                var innerRadii = new Radii(radii, borders, false);

                StreamGeometry backgroundGeometry = null;

                //  calculate border / background rendering geometry
                if (!DoubleUtil.IsZero(innerRect.Width) && !DoubleUtil.IsZero(innerRect.Height))
                {
                    backgroundGeometry = new StreamGeometry();

                    using (var ctx = backgroundGeometry.Open())
                    {
                        GenerateGeometry(ctx, innerRect, innerRadii);
                    }

                    backgroundGeometry.Freeze();
                    BackgroundGeometryField = backgroundGeometry;
                }


                if (!DoubleUtil.IsZero(boundRect.Width) && !DoubleUtil.IsZero(boundRect.Height))
                {
                    var outerRadii = new Radii(radii, borders, true);
                    var borderGeometry = new StreamGeometry();

                    using (var ctx = borderGeometry.Open())
                    {
                        GenerateGeometry(ctx, boundRect, outerRadii);

                        if (backgroundGeometry != null)
                            GenerateGeometry(ctx, innerRect, innerRadii);
                    }
                    borderGeometry.Freeze();
                    BorderGeometryField = borderGeometry;
                }
            }

            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var useLayoutRounding = UseLayoutRounding;
            var dpi = GetDpi();

            if (useComplexRenderCodePath)
            {
                Brush brush = base.BorderBrush;
                var borderGeometry = BorderGeometryField;
                if (borderGeometry != null
                    && (brush = base.BorderBrush) != null)
                    dc.DrawGeometry(brush, null, borderGeometry);

                var backgroundGeometry = BackgroundGeometryField;
                if (backgroundGeometry != null
                    && (brush = Background) != null)
                    dc.DrawGeometry(brush, null, backgroundGeometry);
            }
            else
            {
                var border = BorderThickness;
                Brush borderBrush = base.BorderBrush;

                var cornerRadius = CornerRadius;
                var outerCornerRadius = cornerRadius.TopLeft;
                    // Already validated that all corners have the same radius
                var roundedCorners = !DoubleUtil.IsZero(outerCornerRadius);

                // If we have a brush with which to draw the border, do so.
                // NB: We double draw corners right now.  Corner handling is tricky (bevelling, &c...) and
                //     we need a firm spec before doing "the right thing."  (greglett, ffortes)
                if (!DoubleUtil.IsBorderZero(border)
                    && (borderBrush = base.BorderBrush) != null)
                {
                    // Initialize the first pen.  Note that each pen is created via new()
                    // and frozen if possible.  Doing this avoids the pen 
                    // being copied when used in the DrawLine methods.
                    var pen = penLeftPenField;
                    if (pen == null)
                    {
                        pen = new Pen();
                        pen.Brush = borderBrush;

                        if (useLayoutRounding)
                            pen.Thickness = DoubleUtil.RoundLayoutValue(border.Left, dpi.DpiScaleX);
                        else
                            pen.Thickness = border.Left;
                        if (borderBrush.IsFrozen)
                            pen.Freeze();

                        penLeftPenField = pen;
                    }

                    double halfThickness;
                    if (DoubleUtil.IsUniform(border))
                    {
                        halfThickness = pen.Thickness * 0.5;


                        // Create rect w/ border thickness, and round if applying layout rounding.
                        var rect = new Rect(new Point(halfThickness, halfThickness),
                            new Point(RenderSize.Width - halfThickness, RenderSize.Height - halfThickness));

                        if (roundedCorners)
                            dc.DrawRoundedRectangle(
                                null,
                                pen,
                                rect,
                                outerCornerRadius,
                                outerCornerRadius);
                        else
                            dc.DrawRectangle(
                                null,
                                pen,
                                rect);
                    }
                    else
                    {
                        // Nonuniform border; stroke each edge.
                        if (DoubleUtil.GreaterThan(border.Left, 0))
                        {
                            halfThickness = pen.Thickness * 0.5;
                            dc.DrawLine(
                                pen,
                                new Point(halfThickness, 0),
                                new Point(halfThickness, RenderSize.Height));
                        }

                        if (DoubleUtil.GreaterThan(border.Right, 0))
                        {
                            pen = penRightPenField;
                            if (pen == null)
                            {
                                pen = new Pen();
                                pen.Brush = borderBrush;

                                if (useLayoutRounding)
                                    pen.Thickness = DoubleUtil.RoundLayoutValue(border.Right, dpi.DpiScaleX);
                                else
                                    pen.Thickness = border.Right;

                                if (borderBrush.IsFrozen)
                                    pen.Freeze();

                                penRightPenField = pen;
                            }

                            halfThickness = pen.Thickness * 0.5;
                            dc.DrawLine(
                                pen,
                                new Point(RenderSize.Width - halfThickness, 0),
                                new Point(RenderSize.Width - halfThickness, RenderSize.Height));
                        }

                        if (DoubleUtil.GreaterThan(border.Top, 0))
                        {
                            pen = penTopPenField;
                            if (pen == null)
                            {
                                pen = new Pen();
                                pen.Brush = borderBrush;
                                if (useLayoutRounding)
                                    pen.Thickness = DoubleUtil.RoundLayoutValue(border.Top, dpi.DpiScaleY);
                                else
                                    pen.Thickness = border.Top;

                                if (borderBrush.IsFrozen)
                                    pen.Freeze();

                                penTopPenField = pen;
                            }

                            halfThickness = pen.Thickness * 0.5;
                            dc.DrawLine(
                                pen,
                                new Point(0, halfThickness),
                                new Point(RenderSize.Width, halfThickness));
                        }

                        if (DoubleUtil.GreaterThan(border.Bottom, 0))
                        {
                            pen = penBottomPenField;
                            if (pen == null)
                            {
                                pen = new Pen();
                                pen.Brush = borderBrush;
                                if (useLayoutRounding)
                                    pen.Thickness = DoubleUtil.RoundLayoutValue(border.Bottom, dpi.DpiScaleY);
                                else
                                    pen.Thickness = border.Bottom;
                                if (borderBrush.IsFrozen)
                                    pen.Freeze();

                                penBottomPenField = pen;
                            }

                            halfThickness = pen.Thickness * 0.5;
                            dc.DrawLine(
                                pen,
                                new Point(0, RenderSize.Height - halfThickness),
                                new Point(RenderSize.Width, RenderSize.Height - halfThickness));
                        }
                    }
                }

                // Draw background in rectangle inside border.
                var background = Background;
                if (background != null)
                {
                    // Intialize background 
                    Point ptTl, ptBr;

                    if (useLayoutRounding)
                    {
                        ptTl = new Point(DoubleUtil.RoundLayoutValue(border.Left, dpi.DpiScaleX),
                            DoubleUtil.RoundLayoutValue(border.Top, dpi.DpiScaleY));


                        ptBr = new Point(RenderSize.Width - DoubleUtil.RoundLayoutValue(border.Right, dpi.DpiScaleX),
                            RenderSize.Height - DoubleUtil.RoundLayoutValue(border.Bottom, dpi.DpiScaleY));
                    }
                    else
                    {
                        ptTl = new Point(border.Left, border.Top);
                        ptBr = new Point(RenderSize.Width - border.Right, RenderSize.Height - border.Bottom);
                    }

                    // Do not draw background if the borders are so large that they overlap.
                    if (ptBr.X > ptTl.X && ptBr.Y > ptTl.Y)
                        if (roundedCorners)
                        {
                            var innerRadii = new Radii(cornerRadius, border, false);
                                // Determine the inner edge radius
                            var innerCornerRadius = innerRadii.TopLeft;
                                // Already validated that all corners have the same radius
                            dc.DrawRoundedRectangle(background, null, new Rect(ptTl, ptBr), innerCornerRadius,
                                innerCornerRadius);
                        }
                        else
                        {
                            dc.DrawRectangle(background, null, new Rect(ptTl, ptBr));
                        }
                }
            }
        }

        private DpiScale GetDpi()
        {
            var source = PresentationSource.FromVisual(this);

            double dpiX, dpiY;
            if (source != null)
            {
                dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
            }
            else
            {
                throw new InvalidOperationException();
            }

            return new DpiScale(dpiX, dpiY);
        }

        /// <summary>
        ///     Generates a StreamGeometry.
        /// </summary>
        /// <param name="ctx">An already opened StreamGeometryContext.</param>
        /// <param name="rect">Rectangle for geomentry conversion.</param>
        /// <param name="radii">Corner radii.</param>
        /// <returns>Result geometry.</returns>
        private static void GenerateGeometry(StreamGeometryContext ctx, Rect rect, Radii radii)
        {
            //
            //  compute the coordinates of the key points
            //

            var topLeft = new Point(radii.LeftTop, 0);
            var topRight = new Point(rect.Width - radii.RightTop, 0);
            var rightTop = new Point(rect.Width, radii.TopRight);
            var rightBottom = new Point(rect.Width, rect.Height - radii.BottomRight);
            var bottomRight = new Point(rect.Width - radii.RightBottom, rect.Height);
            var bottomLeft = new Point(radii.LeftBottom, rect.Height);
            var leftBottom = new Point(0, rect.Height - radii.BottomLeft);
            var leftTop = new Point(0, radii.TopLeft);

            //
            //  check keypoints for overlap and resolve by partitioning radii according to
            //  the percentage of each one.  
            //

            //  top edge is handled here
            if (topLeft.X > topRight.X)
            {
                var v = radii.LeftTop / (radii.LeftTop + radii.RightTop) * rect.Width;
                topLeft.X = v;
                topRight.X = v;
            }

            //  right edge
            if (rightTop.Y > rightBottom.Y)
            {
                var v = radii.TopRight / (radii.TopRight + radii.BottomRight) * rect.Height;
                rightTop.Y = v;
                rightBottom.Y = v;
            }

            //  bottom edge
            if (bottomRight.X < bottomLeft.X)
            {
                var v = radii.LeftBottom / (radii.LeftBottom + radii.RightBottom) * rect.Width;
                bottomRight.X = v;
                bottomLeft.X = v;
            }

            // left edge
            if (leftBottom.Y < leftTop.Y)
            {
                var v = radii.TopLeft / (radii.TopLeft + radii.BottomLeft) * rect.Height;
                leftBottom.Y = v;
                leftTop.Y = v;
            }

            //
            //  add on offsets
            //

            var offset = new Vector(rect.TopLeft.X, rect.TopLeft.Y);
            topLeft += offset;
            topRight += offset;
            rightTop += offset;
            rightBottom += offset;
            bottomRight += offset;
            bottomLeft += offset;
            leftBottom += offset;
            leftTop += offset;

            //
            //  create the border geometry
            //
            ctx.BeginFigure(topLeft, true /* is filled */, true /* is closed */);

            // Top line
            ctx.LineTo(topRight, true /* is stroked */, false /* is smooth join */);

            // Upper-right corner
            var radiusX = rect.TopRight.X - topRight.X;
            var radiusY = rightTop.Y - rect.TopRight.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(rightTop, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);

            // Right line
            ctx.LineTo(rightBottom, true /* is stroked */, false /* is smooth join */);

            // Lower-right corner
            radiusX = rect.BottomRight.X - bottomRight.X;
            radiusY = rect.BottomRight.Y - rightBottom.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(bottomRight, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);

            // Bottom line
            ctx.LineTo(bottomLeft, true /* is stroked */, false /* is smooth join */);

            // Lower-left corner
            radiusX = bottomLeft.X - rect.BottomLeft.X;
            radiusY = rect.BottomLeft.Y - leftBottom.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(leftBottom, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);

            // Left line
            ctx.LineTo(leftTop, true /* is stroked */, false /* is smooth join */);

            // Upper-left corner
            radiusX = topLeft.X - rect.TopLeft.X;
            radiusY = leftTop.Y - rect.TopLeft.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(topLeft, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        private static void GenerateGeometries(StreamGeometryContext ctx, Rect rect, Radii radii, Dock pos)
        {
            //
            //  compute the coordinates of the key points
            //

            var topLeft = new Point(radii.LeftTop, 0);
            var topRight = new Point(rect.Width - radii.RightTop, 0);
            var rightTop = new Point(rect.Width, radii.TopRight);
            var rightBottom = new Point(rect.Width, rect.Height - radii.BottomRight);
            var bottomRight = new Point(rect.Width - radii.RightBottom, rect.Height);
            var bottomLeft = new Point(radii.LeftBottom, rect.Height);
            var leftBottom = new Point(0, rect.Height - radii.BottomLeft);
            var leftTop = new Point(0, radii.TopLeft);

            //
            //  check keypoints for overlap and resolve by partitioning radii according to
            //  the percentage of each one.  
            //

            //  top edge is handled here
            if (topLeft.X > topRight.X)
            {
                var v = radii.LeftTop / (radii.LeftTop + radii.RightTop) * rect.Width;
                topLeft.X = v;
                topRight.X = v;
            }

            //  right edge
            if (rightTop.Y > rightBottom.Y)
            {
                var v = radii.TopRight / (radii.TopRight + radii.BottomRight) * rect.Height;
                rightTop.Y = v;
                rightBottom.Y = v;
            }

            //  bottom edge
            if (bottomRight.X < bottomLeft.X)
            {
                var v = radii.LeftBottom / (radii.LeftBottom + radii.RightBottom) * rect.Width;
                bottomRight.X = v;
                bottomLeft.X = v;
            }

            // left edge
            if (leftBottom.Y < leftTop.Y)
            {
                var v = radii.TopLeft / (radii.TopLeft + radii.BottomLeft) * rect.Height;
                leftBottom.Y = v;
                leftTop.Y = v;
            }

            //
            //  add on offsets
            //

            var offset = new Vector(rect.TopLeft.X, rect.TopLeft.Y);
            topLeft += offset;
            topRight += offset;
            rightTop += offset;
            rightBottom += offset;
            bottomRight += offset;
            bottomLeft += offset;
            leftBottom += offset;
            leftTop += offset;

            //
            //  create the border geometry
            //
            ctx.BeginFigure(topLeft, true /* is filled */, true /* is closed */);

            // Top line
            ctx.LineTo(topRight, true /* is stroked */, false /* is smooth join */);

            // Upper-right corner
            var radiusX = rect.TopRight.X - topRight.X;
            var radiusY = rightTop.Y - rect.TopRight.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(rightTop, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);

            // Right line
            ctx.LineTo(rightBottom, true /* is stroked */, false /* is smooth join */);

            // Lower-right corner
            radiusX = rect.BottomRight.X - bottomRight.X;
            radiusY = rect.BottomRight.Y - rightBottom.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(bottomRight, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);

            // Bottom line
            ctx.LineTo(bottomLeft, true /* is stroked */, false /* is smooth join */);

            // Lower-left corner
            radiusX = bottomLeft.X - rect.BottomLeft.X;
            radiusY = rect.BottomLeft.Y - leftBottom.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(leftBottom, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);

            // Left line
            ctx.LineTo(leftTop, true /* is stroked */, false /* is smooth join */);

            // Upper-left corner
            radiusX = topLeft.X - rect.TopLeft.X;
            radiusY = leftTop.Y - rect.TopLeft.Y;
            if (!DoubleUtil.IsZero(radiusX)
                || !DoubleUtil.IsZero(radiusY))
                ctx.ArcTo(topLeft, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }
    }
}