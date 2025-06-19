namespace TestFusion.Internals.ConsoleOutput;

internal class DelayHelper
{
    internal static async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (TaskCanceledException e)
        {
        }
    }
}
