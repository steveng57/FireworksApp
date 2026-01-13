using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace FireworksApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle WPF dispatcher unhandled exceptions (UI thread)
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Handle non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Handle task scheduler unobserved exceptions
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Debug.WriteLine("[App] Global exception handlers registered");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("WPF Dispatcher", e.Exception);

        // Try to keep app running for non-critical errors
        if (!IsCriticalException(e.Exception))
        {
            e.Handled = true;
            Debug.WriteLine("[App] Exception handled, continuing execution");
        }
        else
        {
            MessageBox.Show(
                $"A critical error occurred:\n\n{e.Exception.Message}\n\nThe application will now close.",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("AppDomain", ex);

            if (e.IsTerminating)
            {
                Debug.WriteLine("[App] FATAL: Application is terminating");
            }
        }
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogException("Task", e.Exception);
        e.SetObserved(); // Prevent process termination
    }

    private static void LogException(string source, Exception ex)
    {
        Debug.WriteLine($"[App] UNHANDLED EXCEPTION ({source}):");
        Debug.WriteLine($"  Type: {ex.GetType().FullName}");
        Debug.WriteLine($"  Message: {ex.Message}");
        Debug.WriteLine($"  StackTrace:\n{ex.StackTrace}");

        if (ex.InnerException is not null)
        {
            Debug.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
        }
    }

    private static bool IsCriticalException(Exception ex)
    {
        // Determine if exception should terminate the app
        return ex is OutOfMemoryException
            or System.Runtime.InteropServices.SEHException
            or System.Threading.ThreadAbortException
            or StackOverflowException
            or AccessViolationException;
    }
}
