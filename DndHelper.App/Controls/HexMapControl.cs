using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Tokens;

namespace DndHelper.App.Controls;

/// <summary>
/// Draws a hex map with tokens and overlays. It never edits anything itself: pointer actions are reported through commands
/// so the view model decides what they mean. Wheel zooms, right or middle drag pans.
/// </summary>
public sealed class HexMapControl : Control
{
    public static readonly StyledProperty<HexMap?> MapProperty =
        AvaloniaProperty.Register<HexMapControl, HexMap?>(nameof(Map));

    public static readonly StyledProperty<IReadOnlyList<MapToken>?> TokensProperty =
        AvaloniaProperty.Register<HexMapControl, IReadOnlyList<MapToken>?>(nameof(Tokens));

    public static readonly StyledProperty<string?> SelectedTokenIdProperty =
        AvaloniaProperty.Register<HexMapControl, string?>(nameof(SelectedTokenId));

    public static readonly StyledProperty<IReadOnlyCollection<Hex>?> HighlightProperty =
        AvaloniaProperty.Register<HexMapControl, IReadOnlyCollection<Hex>?>(nameof(Highlight));

    public static readonly StyledProperty<IReadOnlyList<Hex>?> PathPreviewProperty =
        AvaloniaProperty.Register<HexMapControl, IReadOnlyList<Hex>?>(nameof(PathPreview));

    /// <summary>Hexes covered by the spell or area effect being placed.</summary>
    public static readonly StyledProperty<IReadOnlyCollection<Hex>?> AreaHexesProperty =
        AvaloniaProperty.Register<HexMapControl, IReadOnlyCollection<Hex>?>(nameof(AreaHexes));

    /// <summary>Where the area effect starts; drawn as a marker.</summary>
    public static readonly StyledProperty<Hex?> AreaOriginProperty =
        AvaloniaProperty.Register<HexMapControl, Hex?>(nameof(AreaOrigin));

    /// <summary>Bump this when the map or tokens were changed in place, to force a redraw.</summary>
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<HexMapControl, int>(nameof(Revision));

    /// <summary>Bump this to zoom and pan so the whole map is visible.</summary>
    public static readonly StyledProperty<int> FitRequestProperty =
        AvaloniaProperty.Register<HexMapControl, int>(nameof(FitRequest));

    /// <summary>False on the players' screen, so they do not see enemy health bars.</summary>
    public static readonly StyledProperty<bool> ShowEnemyHealthProperty =
        AvaloniaProperty.Register<HexMapControl, bool>(nameof(ShowEnemyHealth), true);

    /// <summary>Hex orientation.</summary>
    public static readonly StyledProperty<HexOrientation> OrientationProperty =
        AvaloniaProperty.Register<HexMapControl, HexOrientation>(nameof(Orientation), HexOrientation.PointyTop);

    public static readonly StyledProperty<ICommand?> HexPressedCommandProperty =
        AvaloniaProperty.Register<HexMapControl, ICommand?>(nameof(HexPressedCommand));

    public static readonly StyledProperty<ICommand?> HexDraggedCommandProperty =
        AvaloniaProperty.Register<HexMapControl, ICommand?>(nameof(HexDraggedCommand));

    public static readonly StyledProperty<ICommand?> HexHoveredCommandProperty =
        AvaloniaProperty.Register<HexMapControl, ICommand?>(nameof(HexHoveredCommand));

    private const double MinSize = 6;
    private const double MaxSize = 120;

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#16161A"));
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.Parse("#664C9AFF"));
    private static readonly IBrush AreaBrush = new SolidColorBrush(Color.Parse("#88FF7A29"));
    private static readonly IBrush HealthBackBrush = new SolidColorBrush(Color.Parse("#CC000000"));
    private static readonly Pen AreaOriginPen = new(Brushes.White, 3);
    private static readonly Pen AreaHitPen = new(new SolidColorBrush(Color.Parse("#FFFF7A29")), 3);
    private static readonly Pen AreaBlockedPen = new(Brushes.Gray, 2, DashStyle.Dash);
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#55000000")), 1);
    private static readonly Pen HoverPen = new(Brushes.White, 2);
    private static readonly Pen PathPen = new(Brushes.Gold, 3);
    private static readonly Pen SelectionPen = new(Brushes.Gold, 3);
    private static readonly Pen TokenPen = new(Brushes.White, 2);

    private static readonly IReadOnlyDictionary<Terrain, IBrush> TerrainBrushes = new Dictionary<Terrain, IBrush>
    {
        [Terrain.Floor] = new SolidColorBrush(Color.Parse("#6B6B73")),
        [Terrain.Grass] = new SolidColorBrush(Color.Parse("#4F8A3C")),
        [Terrain.Forest] = new SolidColorBrush(Color.Parse("#2F5E2A")),
        [Terrain.Rubble] = new SolidColorBrush(Color.Parse("#8A7F72")),
        [Terrain.Mud] = new SolidColorBrush(Color.Parse("#6B4E32")),
        [Terrain.ShallowWater] = new SolidColorBrush(Color.Parse("#4C8FC9")),
        [Terrain.DeepWater] = new SolidColorBrush(Color.Parse("#214E8A")),
        [Terrain.Wall] = new SolidColorBrush(Color.Parse("#2A2A30")),
        [Terrain.Pit] = new SolidColorBrush(Color.Parse("#07070A"))
    };

    private static readonly IReadOnlyDictionary<TokenTeam, IBrush> TeamBrushes =
        Enum.GetValues<TokenTeam>().ToDictionary(t => t, t => (IBrush)new SolidColorBrush(Color.Parse(TokenColors.Hex(t))));

    private double _size = 28;
    private Point _origin = new(300, 200);
    private bool _needsFit = true;
    private Hex? _hover;
    private bool _panning;
    private bool _dragging;
    private Point _panStart;
    private Point _originAtPanStart;

    private StreamGeometry? _hexGeometry;
    private double _hexGeometrySize;
    private HexOrientation _hexGeometryOrientation;

    static HexMapControl()
    {
        AffectsRender<HexMapControl>(
            MapProperty, TokensProperty, SelectedTokenIdProperty, HighlightProperty,
            PathPreviewProperty, RevisionProperty, AreaHexesProperty, AreaOriginProperty, ShowEnemyHealthProperty);

        MapProperty.Changed.AddClassHandler<HexMapControl>((c, _) => c.RequestFit());
        FitRequestProperty.Changed.AddClassHandler<HexMapControl>((c, _) => c.RequestFit());
        OrientationProperty.Changed.AddClassHandler<HexMapControl>((c, _) => c.RequestFit());
    }

    public HexMapControl() => ClipToBounds = true;

    public HexMap? Map { get => GetValue(MapProperty); set => SetValue(MapProperty, value); }
    public IReadOnlyList<MapToken>? Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }
    public string? SelectedTokenId { get => GetValue(SelectedTokenIdProperty); set => SetValue(SelectedTokenIdProperty, value); }
    public IReadOnlyCollection<Hex>? Highlight { get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); }
    public IReadOnlyList<Hex>? PathPreview { get => GetValue(PathPreviewProperty); set => SetValue(PathPreviewProperty, value); }
    public IReadOnlyCollection<Hex>? AreaHexes { get => GetValue(AreaHexesProperty); set => SetValue(AreaHexesProperty, value); }
    public Hex? AreaOrigin { get => GetValue(AreaOriginProperty); set => SetValue(AreaOriginProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public bool ShowEnemyHealth { get => GetValue(ShowEnemyHealthProperty); set => SetValue(ShowEnemyHealthProperty, value); }
    public int FitRequest { get => GetValue(FitRequestProperty); set => SetValue(FitRequestProperty, value); }
    public HexOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public ICommand? HexPressedCommand { get => GetValue(HexPressedCommandProperty); set => SetValue(HexPressedCommandProperty, value); }
    public ICommand? HexDraggedCommand { get => GetValue(HexDraggedCommandProperty); set => SetValue(HexDraggedCommandProperty, value); }
    public ICommand? HexHoveredCommand { get => GetValue(HexHoveredCommandProperty); set => SetValue(HexHoveredCommandProperty, value); }

    private HexLayout Layout => new(Orientation, _size, new Point2(_origin.X, _origin.Y));

    private void RequestFit()
    {
        _needsFit = true;
        InvalidateVisual();
    }

    private void FitToMap()
    {
        _needsFit = false;

        if (Map?.Bounds() is null || Bounds.Width < 10 || Bounds.Height < 10) return;

        // Measure the map at size 1, then scale so it fills the control with a margin.
        var unit = new HexLayout(Orientation, 1);
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;

        foreach (var hex in Map.Hexes)
        {
            foreach (var corner in unit.Corners(hex))
            {
                minX = Math.Min(minX, corner.X); maxX = Math.Max(maxX, corner.X);
                minY = Math.Min(minY, corner.Y); maxY = Math.Max(maxY, corner.Y);
            }
        }

        const double margin = 24;
        var scaleX = (Bounds.Width - 2 * margin) / Math.Max(1e-6, maxX - minX);
        var scaleY = (Bounds.Height - 2 * margin) / Math.Max(1e-6, maxY - minY);
        _size = Math.Clamp(Math.Min(scaleX, scaleY), MinSize, MaxSize);

        var centreX = (minX + maxX) / 2 * _size;
        var centreY = (minY + maxY) / 2 * _size;
        _origin = new Point(Bounds.Width / 2 - centreX, Bounds.Height / 2 - centreY);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(BackgroundBrush, null, new Rect(Bounds.Size));

        if (Map is null) return;
        if (_needsFit) FitToMap();

        var layout = Layout;
        var geometry = GetHexGeometry(layout);
        var viewport = new Rect(Bounds.Size).Inflate(layout.Size * 2);

        foreach (var (hex, terrain) in Map.Cells)
        {
            var centre = Centre(layout, hex);
            if (!viewport.Contains(centre)) continue;

            using (context.PushTransform(Matrix.CreateTranslation(centre.X, centre.Y)))
                context.DrawGeometry(TerrainBrushes[terrain], GridPen, geometry);
        }

        if (Highlight is { } highlight)
        {
            foreach (var hex in highlight)
            {
                var centre = Centre(layout, hex);
                if (!viewport.Contains(centre)) continue;

                using (context.PushTransform(Matrix.CreateTranslation(centre.X, centre.Y)))
                    context.DrawGeometry(HighlightBrush, null, geometry);
            }
        }

        if (AreaHexes is { } area)
        {
            foreach (var hex in area)
            {
                var centre = Centre(layout, hex);
                if (!viewport.Contains(centre)) continue;

                using (context.PushTransform(Matrix.CreateTranslation(centre.X, centre.Y)))
                    context.DrawGeometry(AreaBrush, null, geometry);
            }
        }

        if (AreaOrigin is { } areaOrigin)
        {
            var centre = Centre(layout, areaOrigin);
            context.DrawEllipse(null, AreaOriginPen, centre, layout.Size * 0.3, layout.Size * 0.3);
        }

        if (PathPreview is { Count: > 1 } path)
        {
            for (var i = 1; i < path.Count; i++)
                context.DrawLine(PathPen, Centre(layout, path[i - 1]), Centre(layout, path[i]));
        }

        if (_hover is { } hovered)
        {
            using (context.PushTransform(Matrix.CreateTranslation(Centre(layout, hovered).X, Centre(layout, hovered).Y)))
                context.DrawGeometry(null, HoverPen, geometry);
        }

        DrawTokens(context, layout, viewport);
    }

    private void DrawTokens(DrawingContext context, HexLayout layout, Rect viewport)
    {
        if (Tokens is null) return;

        var radius = layout.Size * 0.62;

        foreach (var token in Tokens)
        {
            var centre = Centre(layout, token.Hex);
            if (!viewport.Contains(centre)) continue;

            using (context.PushOpacity(token.IsDown ? 0.4 : 1.0))
                context.DrawEllipse(TeamBrushes[token.Team], TokenPen, centre, radius, radius);

            if (token.Id == SelectedTokenId)
                context.DrawEllipse(null, SelectionPen, centre, radius + 4, radius + 4);

            if (token.AreaState == TokenAreaState.Hit)
                context.DrawEllipse(null, AreaHitPen, centre, radius + 8, radius + 8);
            else if (token.AreaState == TokenAreaState.Blocked)
                context.DrawEllipse(null, AreaBlockedPen, centre, radius + 8, radius + 8);

            var label = new FormattedText(
                token.Initials, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                Typeface.Default, Math.Max(8, layout.Size * 0.5), Brushes.White);

            context.DrawText(label, new Point(centre.X - label.Width / 2, centre.Y - label.Height / 2));

            if (token.HealthFraction is { } health && (ShowEnemyHealth || token.Team != TokenTeam.Enemy))
                DrawHealthBar(context, centre, radius, health);
        }
    }

    private static void DrawHealthBar(DrawingContext context, Point centre, double radius, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var height = Math.Max(3, radius * 0.22);
        var width = radius * 1.6;
        var left = centre.X - width / 2;
        var top = centre.Y + radius - height / 2;

        var colour = fraction > 0.5 ? "#3DA35D" : fraction > 0.25 ? "#E0B33A" : "#D64545";

        context.DrawRectangle(HealthBackBrush, null, new Rect(left, top, width, height));
        if (fraction > 0)
            context.DrawRectangle(new SolidColorBrush(Color.Parse(colour)), null, new Rect(left, top, width * fraction, height));
    }

    private StreamGeometry GetHexGeometry(HexLayout layout)
    {
        if (_hexGeometry is not null && _hexGeometrySize == layout.Size && _hexGeometryOrientation == layout.Orientation)
            return _hexGeometry;

        var corners = layout.With(origin: new Point2(0, 0)).Corners(Hex.Origin);
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(corners[0].X, corners[0].Y), true);
            for (var i = 1; i < corners.Count; i++)
                ctx.LineTo(new Point(corners[i].X, corners[i].Y));
            ctx.EndFigure(true);
        }

        _hexGeometry = geometry;
        _hexGeometrySize = layout.Size;
        _hexGeometryOrientation = layout.Orientation;
        return geometry;
    }

    private static Point Centre(HexLayout layout, Hex hex)
    {
        var p = layout.ToPixel(hex);
        return new Point(p.X, p.Y);
    }

    private Hex HexAt(Point point) => Layout.FromPixel(new Point2(point.X, point.Y));

    private static void Run(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _panning = true;
            _panStart = point.Position;
            _originAtPanStart = _origin;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (point.Properties.IsLeftButtonPressed && Map is not null)
        {
            _dragging = true;
            e.Pointer.Capture(this);
            Run(HexPressedCommand, (Hex?)HexAt(point.Position));
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var position = e.GetPosition(this);

        if (_panning)
        {
            _origin = new Point(
                _originAtPanStart.X + position.X - _panStart.X,
                _originAtPanStart.Y + position.Y - _panStart.Y);
            InvalidateVisual();
            return;
        }

        var hex = HexAt(position);
        if (_hover == hex) return;

        _hover = hex;
        InvalidateVisual();
        Run(HexHoveredCommand, (Hex?)hex);

        if (_dragging) Run(HexDraggedCommand, (Hex?)hex);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _panning = false;
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_panning || _dragging) return;

        _hover = null;
        InvalidateVisual();
        Run(HexHoveredCommand, (Hex?)null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var newSize = Math.Clamp(_size * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), MinSize, MaxSize);
        if (Math.Abs(newSize - _size) < 1e-9) return;

        // Keep the hex under the cursor where it is while zooming.
        var cursor = e.GetPosition(this);
        var ratio = newSize / _size;
        _origin = new Point(cursor.X - (cursor.X - _origin.X) * ratio, cursor.Y - (cursor.Y - _origin.Y) * ratio);
        _size = newSize;

        InvalidateVisual();
        e.Handled = true;
    }
}
