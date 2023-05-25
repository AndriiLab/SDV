using System;
using Serilog.Core;
using Serilog.Events;

namespace SDV.App.Logging;

public class WindowLogSink : ILogEventSink
{
    public Action<LogEventLevel, string>? OnLogEmitted { get; set; }
    
    public void Emit(LogEvent logEvent)
    {
        var err = logEvent.Exception is not null ? $" | {logEvent.Exception?.Message}" : string.Empty;
        var message = $"{logEvent.MessageTemplate.Render(logEvent.Properties)}{err}\n";
        OnLogEmitted?.Invoke(logEvent.Level, message);
    }
}