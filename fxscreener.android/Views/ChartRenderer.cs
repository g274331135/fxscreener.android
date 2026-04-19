using fxscreener.android.Models;
using SkiaSharp;

namespace fxscreener.android.Views;

public class ChartRenderer
{
    // Отступы
    private const float LeftMargin = 10;           // Отступ слева
    private const float RightMargin = 70;          // Отступ справа для шкалы цены
    private const float TopMargin = 10;            // Отступ сверху
    private const float BottomMargin = 40;          // Отступ снизу для шкалы времени

    // Панели
    private const float PricePanelHeightRatio = 0.7f;   // 70% для свечей
    private const float IndicatorPanelHeightRatio = 0.3f; // 30% для WPR

    // Настройки отступов для баров
    private const int RightBarsOffset = 5;          // Отступ в 5 баров справа

    // Размеры шрифтов
    private const float PriceScaleFontSize = 28;    // Шрифт ценовой шкалы
    private const float TimeScaleFontSize = 24;     // Шрифт шкалы времени
    private const float TooltipFontSize = 24;       // Шрифт подсказки

    // Цвета
    private readonly SKColor _bullishColor = SKColor.Parse("#26A69A");
    private readonly SKColor _bearishColor = SKColor.Parse("#EF5350");
    private readonly SKColor _wpr5Color = SKColor.Parse("#FF9800");
    private readonly SKColor _wpr21Color = SKColor.Parse("#2196F3");
    private readonly SKColor _gridColor = SKColor.Parse("#E0E0E0");
    private readonly SKColor _textColor = SKColor.Parse("#2C3E50");
    private readonly SKColor _selectedBarColor = SKColor.Parse("#FFD54F");
    private readonly SKColor _axisColor = SKColor.Parse("#BDBDBD");

    public void Draw(SKCanvas canvas, int width, int height, ChartData data)
    {
        if (data.Bars == null || data.Bars.Count == 0) return;

        // Вычисляем доступную ширину для рисования
        var drawWidth = width - LeftMargin - RightMargin;

        // Высота панелей
        var pricePanelHeight = height * PricePanelHeightRatio;
        var indicatorPanelHeight = height * IndicatorPanelHeightRatio;

        // Определяем видимый диапазон с учётом отступа справа
        var totalBars = data.Bars.Count;
        var startIdx = data.VisibleStartIndex;
        // Добавляем отступ справа, но не выходим за пределы массива
        var endIdx = Math.Min(data.VisibleEndIndex + RightBarsOffset, totalBars - 1);

        var visibleBars = data.Bars.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();
        if (visibleBars.Count == 0) return;

        // Находим min/max цены для видимых баров
        var minPrice = visibleBars.Min(b => b.Low);
        var maxPrice = visibleBars.Max(b => b.High);
        var priceRange = maxPrice - minPrice;
        if (priceRange == 0) priceRange = 1;

        // Добавляем 5% отступа сверху и снизу для лучшего вида
        var pricePadding = priceRange * 0.05;
        minPrice -= pricePadding;
        maxPrice += pricePadding;

        // Находим min/max WPR
        var minWpr = -100.0;
        var maxWpr = 0.0;
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

        // Ширина одного бара
        var barWidth = (float)drawWidth / visibleBars.Count;
        var candleWidth = Math.Max(3, barWidth * 0.7f);
        var candleXOffset = (barWidth - candleWidth) / 2;

        // Рисуем ценовую шкалу (Y) справа
        DrawPriceScale(canvas, width, pricePanelHeight, minPrice, maxPrice);

        // Рисуем сетку для ценовой панели
        DrawGrid(canvas, width, pricePanelHeight, minPrice, maxPrice);

        // Рисуем бары (свечи)
        for (int i = 0; i < visibleBars.Count; i++)
        {
            var bar = visibleBars[i];
            var x = LeftMargin + i * barWidth + candleXOffset;
            var yOpen = MapY(bar.Open, minPrice, maxPrice, pricePanelHeight);
            var yClose = MapY(bar.Close, minPrice, maxPrice, pricePanelHeight);
            var yHigh = MapY(bar.High, minPrice, maxPrice, pricePanelHeight);
            var yLow = MapY(bar.Low, minPrice, maxPrice, pricePanelHeight);

            var isBullish = bar.Close > bar.Open;
            var color = isBullish ? _bullishColor : _bearishColor;

            // Тень (вертикальная линия)
            using var strokePaint = new SKPaint { Color = color, StrokeWidth = 3f, IsAntialias = true };
            canvas.DrawLine(x + candleWidth / 2, yHigh, x + candleWidth / 2, yLow, strokePaint);

            // Тело свечи (прямоугольник)
            //if (Math.Abs(yClose - yOpen) > 1)
            //{
            //    var bodyRect = SKRect.Create(x, Math.Min(yOpen, yClose), candleWidth, Math.Abs(yClose - yOpen));
            //    using var fillPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            //    canvas.DrawRect(bodyRect, fillPaint);
            //}

            // Горизонтальные засечки (open и close)
            canvas.DrawLine(x, yOpen, x + 5, yOpen, strokePaint);
            canvas.DrawLine(x + candleWidth - 5, yClose, x + candleWidth, yClose, strokePaint);
        }

        // Выделение выбранного бара
        if (data.SelectedIndex >= startIdx && data.SelectedIndex <= endIdx)
        {
            var localIdx = data.SelectedIndex - startIdx;
            var x = LeftMargin + localIdx * barWidth;
            using var highlightPaint = new SKPaint { Color = _selectedBarColor, Style = SKPaintStyle.Fill, IsAntialias = true };
            var highlightRect = SKRect.Create(x, 0, barWidth, pricePanelHeight);
            canvas.DrawRect(highlightRect, highlightPaint);
        }

        // Рисуем панель индикатора WPR
        DrawIndicatorPanel(canvas, width, pricePanelHeight, indicatorPanelHeight, data, startIdx, endIdx, minWpr, maxWpr);

        // Рисуем шкалу времени
        DrawTimeLabels(canvas, width, pricePanelHeight + indicatorPanelHeight, visibleBars, barWidth);

        // Подсказка для выбранного бара
        if (data.SelectedIndex >= 0 && data.SelectedIndex < data.Bars.Count)
        {
            DrawTooltip(canvas, data.Bars[data.SelectedIndex],
                data.Wpr5[data.SelectedIndex]?.Close ?? 0,
                data.Wpr21[data.SelectedIndex]?.Close ?? 0,
                data.SelectedIndex, width, height);
        }
    }

    private float MapY(double value, double min, double max, float panelHeight)
    {
        return (float)(panelHeight - (value - min) / (max - min) * panelHeight);
    }

    private void DrawPriceScale(SKCanvas canvas, int width, float panelHeight, double minPrice, double maxPrice)
    {
        var steps = 6; // 6 ценовых уровней
        var step = (maxPrice - minPrice) / steps;

        using var paint = new SKPaint
        {
            Color = _textColor,
            TextSize = PriceScaleFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Right
        };

        for (int i = 0; i <= steps; i++)
        {
            var price = minPrice + i * step;
            var y = MapY(price, minPrice, maxPrice, panelHeight);

            // Форматируем цену (автоматическое определение количества знаков)
            string priceText;
            if (Math.Abs(price) < 10)
                priceText = price.ToString("F5");
            else if (Math.Abs(price) < 100)
                priceText = price.ToString("F4");
            else if (Math.Abs(price) < 1000)
                priceText = price.ToString("F3");
            else
                priceText = price.ToString("F2");

            canvas.DrawText(priceText, width - 8, y + 5, paint);
        }
    }

    private void DrawGrid(SKCanvas canvas, int width, float height, double min, double max, float leftMargin = LeftMargin)
    {
        var steps = 6;
        var step = (max - min) / steps;

        using var paint = new SKPaint { Color = _gridColor, StrokeWidth = 0.8f, Style = SKPaintStyle.Stroke };

        for (int i = 0; i <= steps; i++)
        {
            var y = MapY(min + i * step, min, max, height);
            canvas.DrawLine(leftMargin, y, width - RightMargin, y, paint);
        }
    }

    private void DrawIndicatorPanel(SKCanvas canvas, int width, float yOffset, float height, ChartData data, int start, int end, double minWpr, double maxWpr)
    {
        var visibleCount = end - start + 1;
        var barWidth = (float)(width - LeftMargin - RightMargin) / visibleCount;

        // Рисуем сетку
        DrawGrid(canvas, width, height, minWpr, maxWpr, LeftMargin);

        // Линия WPR5
        var points5 = new List<SKPoint>();
        for (int i = 0; i < visibleCount; i++)
        {
            var idx = start + i;
            if (idx < data.Wpr5.Count)
            {
                var x = LeftMargin + i * barWidth;
                var y = MapY(data.Wpr5[idx].Close, minWpr, maxWpr, height);
                points5.Add(new SKPoint(x, y + yOffset));
            }
        }

        using (var path5 = new SKPath())
        {
            if (points5.Count > 0)
            {
                path5.MoveTo(points5[0]);
                for (int i = 1; i < points5.Count; i++)
                    path5.LineTo(points5[i]);
                using var paint = new SKPaint { Color = _wpr5Color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
                canvas.DrawPath(path5, paint);
            }
        }

        // Линия WPR21
        var points21 = new List<SKPoint>();
        for (int i = 0; i < visibleCount; i++)
        {
            var idx = start + i;
            if (idx < data.Wpr21.Count)
            {
                var x = LeftMargin + i * barWidth;
                var y = MapY(data.Wpr21[idx].Close, minWpr, maxWpr, height);
                points21.Add(new SKPoint(x, y + yOffset));
            }
        }

        using (var path21 = new SKPath())
        {
            if (points21.Count > 0)
            {
                path21.MoveTo(points21[0]);
                for (int i = 1; i < points21.Count; i++)
                    path21.LineTo(points21[i]);
                using var paint = new SKPaint { Color = _wpr21Color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
                canvas.DrawPath(path21, paint);
            }
        }

        // Горизонтальные линии для уровней WPR (-20, -80)
        using var levelPaint = new SKPaint { Color = SKColor.Parse("#AAAAAA"), StrokeWidth = 2f, Style = SKPaintStyle.Stroke };
        var yMinus20 = MapY(-20, minWpr, maxWpr, height);
        var yMinus80 = MapY(-80, minWpr, maxWpr, height);
        canvas.DrawLine(LeftMargin, yMinus20 + yOffset, width - RightMargin, yMinus20 + yOffset, levelPaint);
        canvas.DrawLine(LeftMargin, yMinus80 + yOffset, width - RightMargin, yMinus80 + yOffset, levelPaint);

        // Подписи уровней
        using var textPaint = new SKPaint { Color = SKColor.Parse("#666666"), TextSize = 20, IsAntialias = true };
        canvas.DrawText("-20", width - RightMargin + 2, yMinus20 + yOffset + 4, textPaint);
        canvas.DrawText("-80", width - RightMargin + 2, yMinus80 + yOffset + 4, textPaint);
    }

    private void DrawTimeLabels(SKCanvas canvas, int width, float yPos, List<Bar> visibleBars, float barWidth)
    {
        if (visibleBars.Count == 0) return;

        using var paint = new SKPaint
        {
            Color = _textColor,
            TextSize = TimeScaleFontSize,
            IsAntialias = true
        };

        // Показываем подписи для первого, среднего и последнего бара
        var firstTime = visibleBars.First().Time;
        var lastTime = visibleBars.Last().Time;
        var midIndex = visibleBars.Count / 2;
        var midTime = visibleBars[midIndex].Time;

        // Форматируем дату
        string FormatDateTime(DateTime dt)
        {
            // Если в пределах одной недели, показываем день и час
            if ((lastTime - firstTime).TotalDays < 7)
                return dt.ToString("dd.MM HH:mm");
            // Если больше недели, показываем только дату
            return dt.ToString("dd.MM");
        }

        canvas.DrawText(FormatDateTime(firstTime), LeftMargin, yPos + 20, paint);

        var midX = LeftMargin + midIndex * barWidth;
        canvas.DrawText(FormatDateTime(midTime), midX - 30, yPos + 20, paint);

        var lastX = width - RightMargin - 60;
        canvas.DrawText(FormatDateTime(lastTime), lastX, yPos + 20, paint);
    }

    private void DrawTooltip(SKCanvas canvas, Bar bar, double wpr5, double wpr21, int index, int width, int height)
    {
        using var paint = new SKPaint
        {
            Color = SKColor.Parse("#1E1E1E"),
            TextSize = TooltipFontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Left
        };

        using var bgPaint = new SKPaint
        {
            Color = SKColor.Parse("#FFFFFFCC"),
            Style = SKPaintStyle.Fill
        };

        var tooltipText = $"{bar.Time:dd.MM HH:mm}  O:{bar.Open:F5}  H:{bar.High:F5}  L:{bar.Low:F5}  C:{bar.Close:F5}";
        var tooltipText2 = $"WPR5:{wpr5:F1}  WPR21:{wpr21:F1}";

        var textBounds = new SKRect();
        paint.MeasureText(tooltipText, ref textBounds);

        var x = LeftMargin + 10;
        var y = height - 50;

        // Фон подсказки
        canvas.DrawRect(x - 5, y - 22, textBounds.Width + 10, 48, bgPaint);

        canvas.DrawText(tooltipText, x, y, paint);
        canvas.DrawText(tooltipText2, x, y + 20, paint);
    }

    public int GetBarIndexAtPoint(SKPoint point, ChartData data, float canvasWidth)
    {
        var visibleCount = data.VisibleEndIndex - data.VisibleStartIndex + 1;
        var barWidth = (canvasWidth - LeftMargin - RightMargin) / visibleCount;

        if (point.X < LeftMargin || point.X > canvasWidth - RightMargin)
            return -1;

        var idx = (int)((point.X - LeftMargin) / barWidth);
        var result = data.VisibleStartIndex + idx;
        return (result >= 0 && result < data.Bars.Count) ? result : -1;
    }
}