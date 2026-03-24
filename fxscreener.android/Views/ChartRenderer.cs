using fxscreener.android.Models;
using SkiaSharp;

namespace fxscreener.android.Views;

public class ChartRenderer
{
    private const float TopMargin = 40;
    private const float BottomMargin = 40;
    private const float PricePanelHeightRatio = 0.7f; // 70% высоты для свечей
    private const float IndicatorPanelHeightRatio = 0.3f; // 30% для WPR

    // Цвета
    private readonly SKColor _bullishColor = SKColor.Parse("#26A69A");
    private readonly SKColor _bearishColor = SKColor.Parse("#EF5350");
    private readonly SKColor _wpr5Color = SKColor.Parse("#FF9800");
    private readonly SKColor _wpr21Color = SKColor.Parse("#2196F3");
    private readonly SKColor _gridColor = SKColor.Parse("#E0E0E0");
    private readonly SKColor _textColor = SKColor.Parse("#424242");
    private readonly SKColor _selectedBarColor = SKColor.Parse("#FFD54F");

    public void Draw(SKCanvas canvas, int width, int height, ChartData data)
    {
        if (data.Bars == null || data.Bars.Count == 0) return;

        var pricePanelHeight = height * PricePanelHeightRatio;
        var indicatorPanelHeight = height * IndicatorPanelHeightRatio;

        // Определяем видимый диапазон
        var startIdx = data.VisibleStartIndex;
        var endIdx = data.VisibleEndIndex;
        var visibleBars = data.Bars.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
        if (visibleBars.Count == 0) return;

        // Находим min/max цены и WPR для масштабирования
        var minPrice = visibleBars.Min(b => b.Low);
        var maxPrice = visibleBars.Max(b => b.High);
        var priceRange = maxPrice - minPrice;
        if (priceRange == 0) priceRange = 1;

        var minWpr = -100.0;
        var maxWpr = 0.0;
        // для WPR можно вычислить по видимым барам из data.Wpr5/Wpr21
        var wpr5Visible = data.Wpr5.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
        var wpr21Visible = data.Wpr21.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
        if (wpr5Visible.Any())
        {
            minWpr = Math.Min(minWpr, wpr5Visible.Min(b => b.Low));
            minWpr = Math.Min(minWpr, wpr21Visible.Min(b => b.Low));
            maxWpr = Math.Max(maxWpr, wpr5Visible.Max(b => b.High));
            maxWpr = Math.Max(maxWpr, wpr21Visible.Max(b => b.High));
        }
        var wprRange = maxWpr - minWpr;
        if (wprRange == 0) wprRange = 1;

        var barWidth = (float)width / visibleBars.Count;
        var candleWidth = Math.Max(2, barWidth * 0.8f);
        var candleXOffset = (barWidth - candleWidth) / 2;

        // Рисуем сетку для ценовой панели
        DrawGrid(canvas, width, pricePanelHeight, minPrice, maxPrice, true);

        // Рисуем бары (OHLC)
        for (int i = 0; i < visibleBars.Count; i++)
        {
            var bar = visibleBars[i];
            var x = i * barWidth + candleXOffset;
            var yOpen = MapY(bar.Open, minPrice, maxPrice, pricePanelHeight);
            var yClose = MapY(bar.Close, minPrice, maxPrice, pricePanelHeight);
            var yHigh = MapY(bar.High, minPrice, maxPrice, pricePanelHeight);
            var yLow = MapY(bar.Low, minPrice, maxPrice, pricePanelHeight);

            var isBullish = bar.Close > bar.Open;
            var color = isBullish ? _bullishColor : _bearishColor;

            // Тень (вертикальная линия)
            using var strokePaint = new SKPaint { Color = color, StrokeWidth = 1, IsAntialias = true };
            canvas.DrawLine(x + candleWidth / 2, yHigh, x + candleWidth / 2, yLow, strokePaint);

            // Тело (вертикальная линия с засечками) — реализуем как прямоугольник
            var bodyRect = SKRect.Create(x, Math.Min(yOpen, yClose), candleWidth, Math.Abs(yClose - yOpen));
            using var fillPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            canvas.DrawRect(bodyRect, fillPaint);

            // Засечки open и close (горизонтальные)
            var openTickX = x;
            var closeTickX = x + candleWidth;
            canvas.DrawLine(openTickX, yOpen, openTickX + 5, yOpen, strokePaint);
            canvas.DrawLine(closeTickX - 5, yClose, closeTickX, yClose, strokePaint);
        }

        // Выделение выбранного бара
        if (data.SelectedIndex >= startIdx && data.SelectedIndex <= endIdx)
        {
            var localIdx = data.SelectedIndex - startIdx;
            var x = localIdx * barWidth;
            using var highlightPaint = new SKPaint { Color = _selectedBarColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            var highlightRect = SKRect.Create(x, 0, barWidth, pricePanelHeight);
            canvas.DrawRect(highlightRect, highlightPaint);
        }

        // Рисуем WPR на нижней панели
        DrawIndicatorPanel(canvas, width, pricePanelHeight, indicatorPanelHeight, data, startIdx, endIdx, minWpr, maxWpr);

        // Рисуем подписи цены справа
        DrawPriceLabels(canvas, width, pricePanelHeight, minPrice, maxPrice);

        // Рисуем шкалу времени
        DrawTimeLabels(canvas, width, pricePanelHeight + indicatorPanelHeight, visibleBars, barWidth);

        // Если выбран бар, показываем значения
        if (data.SelectedIndex >= 0 && data.SelectedIndex < data.Bars.Count)
        {
            DrawTooltip(canvas, data.Bars[data.SelectedIndex],
                data.Wpr5[data.SelectedIndex]?.Close ?? 0,
                data.Wpr21[data.SelectedIndex]?.Close ?? 0,
                data.SelectedIndex, width, height);
        }
    }

    // Вспомогательные методы:
    private float MapY(double value, double min, double max, float panelHeight) =>
        (float)(panelHeight - (value - min) / (max - min) * panelHeight);

    private void DrawGrid(SKCanvas canvas, int width, float height, double min, double max, bool isPrice)
    {
        // Рисуем горизонтальные линии
        var step = (max - min) / 5;
        for (int i = 0; i <= 5; i++)
        {
            var y = MapY(min + i * step, min, max, height);
            using var paint = new SKPaint { Color = _gridColor, StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(0, y, width, y, paint);
        }
    }

    private void DrawIndicatorPanel(SKCanvas canvas, int width, float yOffset, float height, ChartData data, int start, int end, double minWpr, double maxWpr)
    {
        var visibleCount = end - start + 1;
        var barWidth = (float)width / visibleCount;

        // Рисуем сетку для индикатора
        DrawGrid(canvas, width, height, minWpr, maxWpr, false);

        // Линия WPR5
        var points5 = new List<SKPoint>();
        for (int i = 0; i < visibleCount; i++)
        {
            var idx = start + i;
            if (idx < data.Wpr5.Count)
            {
                var x = i * barWidth;
                var y = MapY(data.Wpr5[idx].Close, minWpr, maxWpr, height);
                points5.Add(new SKPoint(x, y + yOffset));
            }
        }
        using var path5 = new SKPath();
        if (points5.Count > 0)
        {
            path5.MoveTo(points5[0]);
            for (int i = 1; i < points5.Count; i++)
                path5.LineTo(points5[i]);
            using var paint = new SKPaint { Color = _wpr5Color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawPath(path5, paint);
        }

        // Линия WPR21
        var points21 = new List<SKPoint>();
        for (int i = 0; i < visibleCount; i++)
        {
            var idx = start + i;
            if (idx < data.Wpr21.Count)
            {
                var x = i * barWidth;
                var y = MapY(data.Wpr21[idx].Close, minWpr, maxWpr, height);
                points21.Add(new SKPoint(x, y + yOffset));
            }
        }
        using var path21 = new SKPath();
        if (points21.Count > 0)
        {
            path21.MoveTo(points21[0]);
            for (int i = 1; i < points21.Count; i++)
                path21.LineTo(points21[i]);
            using var paint = new SKPaint { Color = _wpr21Color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawPath(path21, paint);
        }
    }

    // Метод для получения индекса бара по координате
    public int GetBarIndexAtPoint(SKPoint point, ChartData data)
    {
        // упрощённо: считаем ширину бара на основе видимой области
        var visibleCount = data.VisibleEndIndex - data.VisibleStartIndex + 1;
        var barWidth = (float)canvasView.Width / visibleCount;
        var idx = (int)(point.X / barWidth);
        return data.VisibleStartIndex + idx;
    }

    // Остальные вспомогательные методы (DrawPriceLabels, DrawTimeLabels, DrawTooltip) опущены для краткости
}