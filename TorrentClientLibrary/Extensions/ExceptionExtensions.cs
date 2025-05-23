using System;

namespace TorrentFlow.TorrentClientLibrary.Extensions
{
    public static class ExceptionExtensions
    {
        public static string Format(this Exception exception)
        {
            string message = string.Empty;

            while (exception != null)
            {
                message += exception.GetType().ToString() + ": " + exception.Message.Trim();
                message += Environment.NewLine;
                message += exception.StackTrace;
                message += Environment.NewLine;

                exception = exception.InnerException;
            }

            return message;
        }
    }
}
