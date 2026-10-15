namespace StableHost;

static class BindingHunt
{
    public static void Run(int pid) => StableBindings.Dump(pid);
}
