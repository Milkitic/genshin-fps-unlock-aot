namespace UnlockFps.Utils;

public static class TaskUtils
{
    public static bool TaskSleep(double milliseconds, CancellationTokenSource cts)
    {
        return TaskSleep(TimeSpan.FromMilliseconds(milliseconds), cts);
    }

    public static bool TaskSleep(double milliseconds, CancellationToken token)
    {
        return TaskSleep(TimeSpan.FromMilliseconds(milliseconds), token);
    }

    public static bool TaskSleep(TimeSpan delay, CancellationTokenSource cts)
    {
        return TaskSleep(delay, cts.Token);
    }

    public static bool TaskSleep(TimeSpan delay, in CancellationToken token)
    {
        try
        {
            Task.Delay(delay).Wait(token);
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return true;
    }
}