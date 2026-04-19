using fxscreener.android.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;

namespace fxscreener.android.Views;

public partial class ChartPage : ContentPage
{
    private readonly ChartViewModel _viewModel;
    private ChartRenderer? _renderer;
    private SKCanvasView? _canvasView;

    public ChartPage(ChartViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Создаём CanvasView программно
        _canvasView = new SKCanvasView();
        _canvasView.PaintSurface += OnCanvasPaintSurface;
        _canvasView.EnableTouchEvents = true;
        _canvasView.Touch += OnCanvasTouch;

        // Добавляем в контейнер
        ChartContainer.Content = _canvasView;

        // Инициализируем рендерер
        _renderer = new ChartRenderer();
    }

    private void OnCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        if (_renderer == null || _canvasView == null) return;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.White);

        if (_viewModel.ChartData?.Bars == null || _viewModel.ChartData.Bars.Count == 0)
            return;

        var info = e.Info;
        var width = info.Width;
        var height = info.Height;

        _renderer.Draw(canvas, width, height, _viewModel.ChartData);
    }

    private void OnCanvasTouch(object sender, SKTouchEventArgs e)
    {
        if (_renderer == null || _canvasView == null || _viewModel.ChartData == null) return;

        // Обработка касаний для выбора бара
        if (e.ActionType == SKTouchAction.Pressed)
        {
            var location = e.Location;
            var index = _renderer.GetBarIndexAtPoint(location, _viewModel.ChartData, (float)_canvasView.CanvasSize.Width);
            if (index >= 0 && index < _viewModel.ChartData.Bars.Count)
            {
                _viewModel.ChartData.SelectedIndex = index;
                _canvasView.InvalidateSurface(); // перерисовать
            }
            e.Handled = true;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Устанавливаем заголовок страницы
        Title = _viewModel.Title;
    }
}