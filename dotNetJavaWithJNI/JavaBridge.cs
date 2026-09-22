using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace dotNetJavaWithJNI;

public static class JavaBridge
{
    private const int JNI_OK = 0;
    private const int JNI_VERSION_24 = 0x00180000;

    private static readonly object SyncRoot = new();
    private static IntPtr _jvm;
    private static IntPtr _env;
    private static bool _initialized;

    public static int InvokeCustomAdd(int a, int b)
    {
        EnsureInitialized();

        var jni = GetJniInterface(_env);
        var className = Marshal.StringToHGlobalAnsi("com/example/operations/CustomOperationsImpl");

        try
        {
            var findClass = Marshal.GetDelegateForFunctionPointer<FindClassDelegate>(jni.FindClass);
            var classRef = findClass(_env, className);
            if (classRef == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not find Java class com.example.operations.CustomOperationsImpl.");
            }

            var constructorName = Marshal.StringToHGlobalAnsi("<init>");
            var constructorSig = Marshal.StringToHGlobalAnsi("()V");
            try
            {
                var getMethodId = Marshal.GetDelegateForFunctionPointer<GetMethodIDDelegate>(jni.GetMethodID);
                var constructorId = getMethodId(_env, classRef, constructorName, constructorSig);
                if (constructorId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Could not find Java constructor.");
                }

                var newObject = Marshal.GetDelegateForFunctionPointer<NewObjectDelegate>(jni.NewObject);
                var instance = newObject(_env, classRef, constructorId);
                if (instance == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create Java instance.");
                }

                var methodName = Marshal.StringToHGlobalAnsi("customAdd");
                var methodSig = Marshal.StringToHGlobalAnsi("(II)I");
                try
                {
                    var methodId = getMethodId(_env, classRef, methodName, methodSig);
                    if (methodId == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Could not find Java method customAdd(int,int). ");
                    }

                    var callIntMethod = Marshal.GetDelegateForFunctionPointer<CallIntMethodDelegate>(jni.CallIntMethod);
                    return callIntMethod(_env, instance, methodId, a, b);
                }
                finally
                {
                    Marshal.FreeHGlobal(methodName);
                    Marshal.FreeHGlobal(methodSig);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(constructorName);
                Marshal.FreeHGlobal(constructorSig);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(className);
        }
    }

    private static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            var javaHome = ResolveJavaHome();
            var jvmServerBin = Path.Combine(javaHome, "bin", "server");
            SetDllDirectory(jvmServerBin);
            NativeLibrary.Load(Path.Combine(jvmServerBin, "jvm.dll"));

            var classpath = GetJavaClasspath();
            var optionString = Marshal.StringToHGlobalAnsi("-Djava.class.path=" + classpath);
            var option = new JavaVMOption
            {
                optionString = optionString,
                extraInfo = IntPtr.Zero
            };

            var optionSize = Marshal.SizeOf<JavaVMOption>();
            var optionsPtr = Marshal.AllocHGlobal(optionSize);
            try
            {
                Marshal.StructureToPtr(option, optionsPtr, false);

                var initArgs = new JavaVMInitArgs
                {
                    version = JNI_VERSION_24,
                    nOptions = 1,
                    options = optionsPtr,
                    ignoreUnrecognized = 0
                };

                var jvmPtr = IntPtr.Zero;
                var envPtr = IntPtr.Zero;
                var rc = JNI_CreateJavaVM(out jvmPtr, out envPtr, ref initArgs);
                if (rc != JNI_OK)
                {
                    throw new InvalidOperationException($"JNI_CreateJavaVM failed with error code {rc}.");
                }

                _jvm = jvmPtr;
                _env = envPtr;
                _initialized = true;
            }
            finally
            {
                Marshal.FreeHGlobal(optionsPtr);
                Marshal.FreeHGlobal(optionString);
            }
        }
    }

    private static string ResolveJavaHome()
    {
        var configuredJavaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrWhiteSpace(configuredJavaHome))
        {
            throw new InvalidOperationException("JAVA_HOME is not defined. Set JAVA_HOME to a valid JDK installation directory before starting the JNI bridge.");
        }

        var javaBin = Path.Combine(configuredJavaHome, "bin");
        if (!Directory.Exists(javaBin))
        {
            throw new InvalidOperationException($"JAVA_HOME is set to '{configuredJavaHome}', but the JDK bin directory was not found.");
        }

        var jvmDll = Path.Combine(javaBin, "server", "jvm.dll");
        if (!File.Exists(jvmDll))
        {
            throw new InvalidOperationException($"JAVA_HOME is set to '{configuredJavaHome}', but a valid JDK server JVM was not found at '{jvmDll}'.");
        }

        return configuredJavaHome;
    }

    private static string GetJavaClasspath()
    {
        var entries = new List<string>();
        var existingClasspath = Environment.GetEnvironmentVariable("CLASSPATH");
        if (!string.IsNullOrWhiteSpace(existingClasspath))
        {
            entries.AddRange(existingClasspath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        var projectClasses = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "javaAPIs", "target", "classes"));
        if (Directory.Exists(projectClasses))
        {
            entries.Add(projectClasses);
        }

        var fallback = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "javaAPIs", "target", "classes"));
        if (Directory.Exists(fallback))
        {
            entries.Add(fallback);
        }

        var classpath = string.Join(Path.PathSeparator, entries.Distinct(StringComparer.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(classpath))
        {
            throw new InvalidOperationException("The Java classpath is empty. Build the shared Java project and ensure the classpath can be resolved.");
        }

        return classpath;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("jvm.dll", EntryPoint = "JNI_CreateJavaVM", CallingConvention = CallingConvention.Winapi)]
    private static extern int JNI_CreateJavaVM(out IntPtr jvm, out IntPtr env, ref JavaVMInitArgs args);

    private static JNINativeInterface_ GetJniInterface(IntPtr env)
    {
        var vtable = Marshal.ReadIntPtr(env);

        return new JNINativeInterface_
        {
            FindClass = Marshal.ReadIntPtr(vtable, 6 * IntPtr.Size),
            GetMethodID = Marshal.ReadIntPtr(vtable, 33 * IntPtr.Size),
            NewObject = Marshal.ReadIntPtr(vtable, 28 * IntPtr.Size),
            CallIntMethod = Marshal.ReadIntPtr(vtable, 49 * IntPtr.Size)
        };
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr FindClassDelegate(IntPtr env, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetMethodIDDelegate(IntPtr env, IntPtr clazz, IntPtr name, IntPtr sig);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr NewObjectDelegate(IntPtr env, IntPtr clazz, IntPtr methodID, params int[] args);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int CallIntMethodDelegate(IntPtr env, IntPtr obj, IntPtr methodID, int a, int b);

    [StructLayout(LayoutKind.Sequential)]
    private struct JavaVMOption
    {
        public IntPtr optionString;
        public IntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JavaVMInitArgs
    {
        public int version;
        public int nOptions;
        public IntPtr options;
        public int ignoreUnrecognized;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JNINativeInterface_
    {
        public IntPtr FindClass;
        public IntPtr GetMethodID;
        public IntPtr NewObject;
        public IntPtr CallIntMethod;
    }
}
