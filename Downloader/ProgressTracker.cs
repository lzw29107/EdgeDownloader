namespace EdgeDownloader.Downloader;

public class ProgressTracker
{
    private readonly long TotalBytes;
    private long DownloadedBytes = 0;
    private long Progress = 0;
    private readonly Lock ProgressLock = new();

    public ProgressTracker(long totalBytes)
    {
        if (totalBytes <= 0)
            throw new InvalidOperationException("totalBytes must be greater than zero.");
        TotalBytes = totalBytes;
    }

    public void UpdateProgress(long downloadedBytes)
    {
        lock (ProgressLock)
        {
            DownloadedBytes += downloadedBytes;
            long progress = (DownloadedBytes * 10000) / TotalBytes;
            if (Progress < progress)
            {
                Progress = progress;
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"Progress: {DownloadedBytes / 1048576.0:F2}/{TotalBytes / 1048576.0:F2} MiB  {Progress / 100.0:F2}%");
                if (Progress == 10000)
                {
                    Console.WriteLine();
                    Console.WriteLine("Download completed.");
                }
            }
        }
    }
}
