using System.Windows;

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

        var app = new App();
        app.Run();
        return 0;
    }
}
