using System;
using System.Net;
using System.ComponentModel;

public class Downloader
{
    private volatile bool _completed;

    public void Download(string address, string location)
    {
        WebClient client = new WebClient();
        Uri Uri = new Uri(address);
        _completed = false;

        client.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);

        client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgress);
        client.DownloadFileAsync(Uri, location);
    }

    public bool DownloadCompleted { get { return _completed; } }

    private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
    {
        Console.WriteLine($"Downloaded {Math.Round(e.BytesReceived / 1.049e+6, 2)}MiB of {Math.Round(e.TotalBytesToReceive / 1.049e+6, 2)}MiB, {e.ProgressPercentage}% complete");
    }

    private void Completed(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Cancelled == true)
        {
            Console.WriteLine("Download has been cancelled.");
        }
        else
        {
            Console.WriteLine("Download completed.");
        }

        _completed = true;
    }
}