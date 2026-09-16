using System;
using System.Runtime.InteropServices;

namespace AMEAccess.Speech
{
    /// <summary>
    /// Direct bindings to NVDA's controller client, used as a fallback and as a diagnostic.
    ///
    /// UniversalSpeech loads nvdaControllerClient32.dll itself, relative to its own folder. If
    /// that file is missing, UniversalSpeech silently reports no NVDA and falls through to its
    /// other engines. Talking to the controller client directly lets us say plainly whether
    /// NVDA is running, which is far more useful than "nothing happened".
    ///
    /// These four exports are the documented, long-stable NVDA controller client API.
    /// </summary>
    internal static class Nvda
    {
        // NVDA ships this as nvdaControllerClient32.dll, but some bundles drop the "32".
        // Bind both names and use whichever loads.
        private const string Dll32 = "nvdaControllerClient32.dll";
        private const string DllPlain = "nvdaControllerClient.dll";
        private const CallingConvention CC = CallingConvention.Cdecl;

        [DllImport(Dll32, CallingConvention = CC, EntryPoint = "nvdaController_testIfRunning")]
        private static extern int testRunning32();
        [DllImport(Dll32, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "nvdaController_speakText")]
        private static extern int speak32([MarshalAs(UnmanagedType.LPWStr)] string text);
        [DllImport(Dll32, CallingConvention = CC, EntryPoint = "nvdaController_cancelSpeech")]
        private static extern int cancel32();
        [DllImport(Dll32, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "nvdaController_brailleMessage")]
        private static extern int braille32([MarshalAs(UnmanagedType.LPWStr)] string text);

        [DllImport(DllPlain, CallingConvention = CC, EntryPoint = "nvdaController_testIfRunning")]
        private static extern int testRunningP();
        [DllImport(DllPlain, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "nvdaController_speakText")]
        private static extern int speakP([MarshalAs(UnmanagedType.LPWStr)] string text);
        [DllImport(DllPlain, CallingConvention = CC, EntryPoint = "nvdaController_cancelSpeech")]
        private static extern int cancelP();
        [DllImport(DllPlain, CharSet = CharSet.Unicode, CallingConvention = CC, EntryPoint = "nvdaController_brailleMessage")]
        private static extern int brailleP([MarshalAs(UnmanagedType.LPWStr)] string text);

        private static bool _plain;
        internal static string LoadedName => !DllPresent ? null : (_plain ? DllPlain : Dll32);

        private static int testRunning() => _plain ? testRunningP() : testRunning32();
        private static int speakText(string t) => _plain ? speakP(t) : speak32(t);
        private static int cancelSpeech() => _plain ? cancelP() : cancel32();
        private static int brailleMessage(string t) => _plain ? brailleP(t) : braille32(t);

        internal static bool DllPresent { get; private set; }
        internal static string LoadError { get; private set; }

        /// <summary>Is the DLL loadable at all? Separate question from whether NVDA is running.</summary>
        internal static bool Probe()
        {
            try
            {
                _plain = false;
                testRunning32();
                DllPresent = true; LoadError = null;
                return true;
            }
            catch (Exception e) { LoadError = e.GetType().Name + ": " + e.Message; }

            try
            {
                _plain = true;
                testRunningP();
                DllPresent = true; LoadError = null;
                return true;
            }
            catch (Exception e)
            {
                DllPresent = false;
                LoadError = e.GetType().Name + ": " + e.Message;
            }
            return DllPresent;
        }

        /// <summary>NVDA returns 0 from testIfRunning when it is running.</summary>
        internal static bool IsRunning()
        {
            if (!DllPresent) return false;
            try { return testRunning() == 0; }
            catch { return false; }
        }

        internal static bool Speak(string text, bool interrupt)
        {
            if (!DllPresent) return false;
            try
            {
                if (interrupt) cancelSpeech();
                return speakText(text) == 0;
            }
            catch { return false; }
        }

        internal static void Stop()
        {
            if (!DllPresent) return;
            try { cancelSpeech(); } catch { }
        }

        internal static void Braille(string text)
        {
            if (!DllPresent) return;
            try { brailleMessage(text); } catch { }
        }
    }
}
