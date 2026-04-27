namespace fxscreener.android.Models;

public class WsSignal
{
    public SignalType Signal { get; set; } = SignalType.None;

    public string Text => Signal switch
    {
        SignalType.Bullish => "▲",
        SignalType.Bearish => "▼",
        _ => string.Empty
    };

    public bool IsBullish => Signal == SignalType.Bullish;
    public bool IsBearish => Signal == SignalType.Bearish;
    public bool HasSignal => Signal != SignalType.None;
}