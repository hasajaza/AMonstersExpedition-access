using System;
using System.Runtime.InteropServices;

namespace AMEAccess.Speech
{
    /// <summary>
    /// Bindings to UniversalSpeech.dll.
    ///
    /// The exports below were read directly from the 32-bit UniversalSpeech.dll in use
    /// (163 exports). Names that also appear with an "@N" stdcall alias are exported in
    /// cdecl form under their plain name, which is what we bind to.
    ///
    /// The engine-specific entry points matter for diagnosis. speechSay returns 1 as soon as
    /// ANY engine accepts the text - including a screen reader that is present but silent,
    /// such as JAWS in sleep mode. So "speechSay returned 1" does not prove you heard
    /// anything, and we need to know which engine answered.
    /// </summary>
    internal static class UniversalSpeech
    {
        private const string Dll = "UniversalSpeech.dll";
        private const CallingConvention CC = CallingConvention.Cdecl;

        // --- generic ----------------------------------------------------------
        [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "speechSay")]
        internal static extern int Say([MarshalAs(UnmanagedType.LPWStr)] string text, int interrupt);

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "speechStop")]
        internal static extern int Stop();

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "speechGetValue")]
        internal static extern int GetValue(int what);

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "speechSetValue")]
        internal static extern int SetValue(int what, int value);

        [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "brailleDisplayW")]
        internal static extern int Braille([MarshalAs(UnmanagedType.LPWStr)] string text);

        // --- which engine is actually answering -------------------------------
        [DllImport(Dll, CallingConvention = CC, EntryPoint = "getCurrentScreenReaderNameW")]
        private static extern IntPtr _currentScreenReaderNameW();

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "getCurrentScreenReader")]
        internal static extern int CurrentScreenReaderId();

        internal static string CurrentEngineName()
        {
            try
            {
                IntPtr p = _currentScreenReaderNameW();
                if (p == IntPtr.Zero) return "(none)";
                return Marshal.PtrToStringUni(p) ?? "(none)";
            }
            catch (Exception e) { return "(query failed: " + e.GetType().Name + ")"; }
        }

        // --- per-engine availability ------------------------------------------
        [DllImport(Dll, CallingConvention = CC, EntryPoint = "jfwIsAvailable")]
        internal static extern int JawsAvailable();

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "nvdaIsAvailable")]
        internal static extern int NvdaAvailable();

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "sapiIsAvailable")]
        internal static extern int SapiAvailable();

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "sapiIsEnabled")]
        internal static extern int SapiEnabled();

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "sapiEnable")]
        internal static extern int SapiEnable(int enable);

        // --- per-engine direct speech ------------------------------------------
        [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "sapiSayW")]
        internal static extern int SapiSay([MarshalAs(UnmanagedType.LPWStr)] string text, int interrupt);

        [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "nvdaSayW")]
        internal static extern int NvdaSay([MarshalAs(UnmanagedType.LPWStr)] string text, int interrupt);

        [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "jfwSayW")]
        internal static extern int JawsSay([MarshalAs(UnmanagedType.LPWStr)] string text, int interrupt);

        [DllImport(Dll, CallingConvention = CC, EntryPoint = "sapiStopSpeech")]
        internal static extern int SapiStop();

        /// <summary>Probe whether the native DLL is present and callable.</summary>
        internal static bool Probe(out string error)
        {
            try
            {
                GetValue(0);
                error = null;
                return true;
            }
            catch (DllNotFoundException)
            {
                error = Dll + " was not found. Put the 32-bit UniversalSpeech.dll next to the game executable.";
                return false;
            }
            catch (EntryPointNotFoundException e)
            {
                error = Dll + " loaded but an expected export is missing: " + e.Message;
                return false;
            }
            catch (BadImageFormatException)
            {
                error = Dll + " is the wrong architecture. The game is 32-bit, so the x86 build is required.";
                return false;
            }
            catch (Exception e)
            {
                error = Dll + " failed to initialise: " + e.Message;
                return false;
            }
        }

        internal static int SafeCall(Func<int> f)
        {
            try { return f(); } catch { return -1; }
        }
    }
}
