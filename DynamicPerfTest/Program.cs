using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace DynamicPerfTest
{
    /// <summary>
    /// This program demonstrates that debugging DynamicMetaObject in VS022 is much slower than in VS2019
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Test dynamic performance in {Environment.Version} with {(!Debugger.IsAttached ? "no " : "")} debugger attached...");

            var start = DateTime.Now;
            Type genericType = typeof(DynaObj<string>).GetGenericTypeDefinition();

            const int loops = 5000;
            for (int i = 0; i < loops; i++)
            {
                Console.Title = $"Test running for {(DateTime.Now - start).TotalSeconds:F2}s: {i}/{loops}";

                // Define generic types, so BindGetMember gets called every time
                Type dynamicType = CompileResultType($"DummyType{i}");
                Type dynaType = genericType.MakeGenericType(dynamicType);

                dynamic obj = Activator.CreateInstance(dynaType);

                // Access dynamic object will call BindGetMember and TryGetMember
                var val = obj.test;
            }

            Console.WriteLine($"Test ended after {(DateTime.Now - start).TotalSeconds}s");
        }



        public static Type CompileResultType(string typeName)
        {
            TypeBuilder tb = GetTypeBuilder(typeName);
            tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            return tb.CreateType();
        }

        private static TypeBuilder GetTypeBuilder(string typeName)
        {
            var an = new AssemblyName(typeName);
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
            TypeBuilder tb = moduleBuilder.DefineType(typeName,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);
            return tb;
        }
    }
}