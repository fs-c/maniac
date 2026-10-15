using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace Maniac.Stable;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--selftest" || args[0] == "selftest"))
        {
            try
            {
                SelfTest.Run();
                Console.WriteLine("selftest ok");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
        return 0;
    }
}
