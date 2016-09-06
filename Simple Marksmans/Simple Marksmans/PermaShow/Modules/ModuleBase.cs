namespace Simple_Marksmans.PermaShow.Modules
{
    using Interfaces;

    internal abstract class ModuleBase : IModule
    {
        public abstract void Load();
    }
}