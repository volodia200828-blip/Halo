using System;
using System.Windows;
using System.Windows.Media;

namespace Halo.App.Controls;

public static class RadialGeometryHelper
{
    public static PathGeometry CreatePieSlice(
        Point center,
        double innerRadius,
        double outerRadius,
        double startAngleDeg,
        double endAngleDeg)
    {
        // Convert to radians (angles clockwise with 0 at Top: -90 deg offset)
        double startRad = (startAngleDeg - 90.0) * Math.PI / 180.0;
        double endRad = (endAngleDeg - 90.0) * Math.PI / 180.0;

        Point pInnerStart = new Point(
            center.X + innerRadius * Math.Cos(startRad),
            center.Y + innerRadius * Math.Sin(startRad));

        Point pOuterStart = new Point(
            center.X + outerRadius * Math.Cos(startRad),
            center.Y + outerRadius * Math.Sin(startRad));

        Point pOuterEnd = new Point(
            center.X + outerRadius * Math.Cos(endRad),
            center.Y + outerRadius * Math.Sin(endRad));

        Point pInnerEnd = new Point(
            center.X + innerRadius * Math.Cos(endRad),
            center.Y + innerRadius * Math.Sin(endRad));

        double angleDiff = endAngleDeg - startAngleDeg;
        bool isLargeArc = angleDiff > 180.0;

        var figure = new PathFigure
        {
            StartPoint = pInnerStart,
            IsClosed = true,
            IsFilled = true
        };

        // 1. Line to outer start
        figure.Segments.Add(new LineSegment(pOuterStart, true));

        // 2. Arc to outer end
        figure.Segments.Add(new ArcSegment(
            pOuterEnd,
            new Size(outerRadius, outerRadius),
            0,
            isLargeArc,
            SweepDirection.Clockwise,
            true));

        // 3. Line to inner end
        figure.Segments.Add(new LineSegment(pInnerEnd, true));

        // 4. Arc back to inner start
        figure.Segments.Add(new ArcSegment(
            pInnerStart,
            new Size(innerRadius, innerRadius),
            0,
            isLargeArc,
            SweepDirection.Counterclockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    public static Point GetRadialMidpoint(
        Point center,
        double innerRadius,
        double outerRadius,
        double startAngleDeg,
        double endAngleDeg)
    {
        double midAngleDeg = (startAngleDeg + endAngleDeg) / 2.0;
        double midRad = (midAngleDeg - 90.0) * Math.PI / 180.0;
        double midRadius = (innerRadius + outerRadius) / 2.0;

        return new Point(
            center.X + midRadius * Math.Cos(midRad),
            center.Y + midRadius * Math.Sin(midRad));
    }
}
