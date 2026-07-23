using System.Runtime.InteropServices;

namespace CodexUsage.macOS.MenuBar;

internal static class ObjectiveC
{
    private const string Library = "/usr/lib/libobjc.A.dylib";

    static ObjectiveC()
    {
        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
    }

    internal static nint Class(string name)
    {
        var value = objc_getClass(name);
        return value != 0 ? value : throw new InvalidOperationException($"Objective-C class {name} is unavailable.");
    }

    internal static nint RegisteredClass(string name) => objc_getClass(name);

    internal static nint Selector(string name) => sel_registerName(name);

    internal static nint String(string value)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return Send(Class("NSString"), Selector("stringWithUTF8String:"), pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    internal static nint AllocateClass(string name) =>
        objc_allocateClassPair(Class("NSObject"), name, 0);

    internal static void RegisterClass(nint targetClass) => objc_registerClassPair(targetClass);

    internal static bool AddMethod(nint targetClass, string selector, Delegate callback) => class_addMethod(
        targetClass,
        Selector(selector),
        Marshal.GetFunctionPointerForDelegate(callback),
        "v@:@");

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Rect(double X, double Y, double Width, double Height);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Size(double Width, double Height);

    [DllImport(Library)]
    private static extern nint objc_getClass(string name);

    [DllImport(Library)]
    private static extern nint objc_allocateClassPair(nint superclass, string name, nuint extraBytes);

    [DllImport(Library)]
    private static extern void objc_registerClassPair(nint targetClass);

    [DllImport(Library)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_addMethod(nint targetClass, nint selector, nint implementation, string types);

    [DllImport(Library)]
    private static extern nint sel_registerName(string name);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint first, nint second);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, nint first, nint second, nint third);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, double argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern nint Send(nint receiver, nint selector, Rect argument);

    internal static Rect SendRect(nint receiver, nint selector)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return SendRectDirect(receiver, selector);
        }

        SendRectStret(out var result, receiver, selector);
        return result;
    }

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    private static extern Rect SendRectDirect(nint receiver, nint selector);

    [DllImport(Library, EntryPoint = "objc_msgSend_stret")]
    private static extern void SendRectStret(out Rect result, nint receiver, nint selector);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SendBoolResult(nint receiver, nint selector);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SendBoolResult(nint receiver, nint selector, long argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(nint receiver, nint selector);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(nint receiver, nint selector, nint argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(nint receiver, nint selector, long argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(nint receiver, nint selector, Rect argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(nint receiver, nint selector, Size argument);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendVoid(
        nint receiver,
        nint selector,
        Rect first,
        nint second,
        long third);

    [DllImport(Library, EntryPoint = "objc_msgSend")]
    internal static extern void SendBool(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool argument);
}
