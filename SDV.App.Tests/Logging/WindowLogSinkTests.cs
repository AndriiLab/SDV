using SDV.App.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace SDV.App.Tests.Logging;

public class WindowLogSinkTests
{
    private readonly WindowLogSink _sut = new();
    private readonly List<(LogEventLevel Level, string Message)> _emitted = [];

    public WindowLogSinkTests()
    {
        _sut.OnLogEmitted = (level, message) => _emitted.Add((level, message));
    }

    [Fact]
    public void Emit_MessageWithProperties_RendersTemplateWithTrailingNewLine()
    {
        _sut.Emit(LogEvent(LogEventLevel.Information, "Found {Number} projects", ("Number", 3)));

        Assert.Equal((LogEventLevel.Information, "Found 3 projects\n"), Assert.Single(_emitted));
    }

    [Fact]
    public void Emit_WithException_AppendsExceptionMessage()
    {
        _sut.Emit(LogEvent(LogEventLevel.Error, "Build failed", exception: new InvalidOperationException("boom")));

        Assert.Equal("Build failed | boom\n", Assert.Single(_emitted).Message);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Fatal)]
    public void Emit_AnyLevel_PassesLevelThrough(LogEventLevel level)
    {
        _sut.Emit(LogEvent(level, "message"));

        Assert.Equal(level, Assert.Single(_emitted).Level);
    }

    [Fact]
    public void Emit_NoCallback_DoesNotThrow()
    {
        var sink = new WindowLogSink();

        var ex = Record.Exception(() => sink.Emit(LogEvent(LogEventLevel.Information, "message")));

        Assert.Null(ex);
    }

    private static LogEvent LogEvent(LogEventLevel level, string template,
        (string Name, object Value)? property = null, Exception? exception = null)
    {
        var properties = property is { } p
            ? [new LogEventProperty(p.Name, new ScalarValue(p.Value))]
            : Array.Empty<LogEventProperty>();
        return new LogEvent(DateTimeOffset.Now, level, exception, new MessageTemplateParser().Parse(template), properties);
    }
}
