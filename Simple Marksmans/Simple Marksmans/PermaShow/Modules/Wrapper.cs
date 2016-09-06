namespace Simple_Marksmans.PermaShow.Modules
{
    using System;

    using System.Collections.Generic;

    using System.Linq;

    using System.Reflection;

    internal class Wrapper : ModuleBase
    {
        public Wrapper this[string name] { get { return ModuleObjects.First(x=>x.GetType().Name == name); } }
        
        public List<Wrapper> ModuleObjects { get; } = new List<Wrapper>(); 

        public override void Load()
        {
            try
            {
                LoadAll();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred : {0}", ex);
                throw;
            }
        }

        public void InvokeLoadMethodForAll()
        {
            ModuleObjects.ForEach(x => x.Load());
        }

        public IEnumerable<Type> GetAll()
        {
            try
            {
                return Assembly.GetAssembly(typeof(Wrapper)).GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(Wrapper)) && x.Name != "Wrapper").ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred : {0}", ex);
                throw;
            }
        }

        private void LoadAll()
        {
            try
            {
                foreach (var type in GetAll())
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type, null);
                        try
                        {
                            ModuleObjects.Add(instance as Wrapper);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception occurred : {0}", ex);
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception occurred : {0}", ex);
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred : {0}", ex);
                throw;
            }
        }

        public T Bind<T>(ModuleBase baseType) where T : Wrapper
        {
            try
            {
                return (T)Convert.ChangeType(baseType, typeof(T));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred : {0}", ex);
                throw;
            }
        }

        public T Bind<T>() where T : Wrapper
        {
            try
            {
                return (T)Convert.ChangeType(ModuleObjects.First(x => x.GetType() == typeof(T)), typeof(T));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred : {0}", ex);
                throw;
            }
        }
    }
}
