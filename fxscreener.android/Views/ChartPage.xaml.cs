using fxscreener.android.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Diagnostics;

namespace fxscreener.android.Views;

public partial class ChartPage : ContentPage
{
    private ChartViewModel _viewModel;
    private ChartRenderer _renderer;

    public ChartPage(ChartViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _renderer = new ChartRenderer();
    }

    private void OnCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
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
        // Обработка касаний для выбора бара
        if (e.ActionType == SKTouchAction.Pressed)
        {
            var location = e.Location;
            var index = _renderer.GetBarIndexAtPoint(location, _viewModel.ChartData, canvasView.CanvasSize.Width);
            if (index >= 0 && index < _viewModel.ChartData.Bars.Count)
            {
                _viewModel.ChartData.SelectedIndex = index;
                canvasView.InvalidateSurface(); // перерисовать
            }
            e.Handled = true;
        }
        // Для масштабирования и скролла добавим позже
    }
}