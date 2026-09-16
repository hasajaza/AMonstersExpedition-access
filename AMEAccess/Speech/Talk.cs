using System;
using BepInEx.Logging;

namespace AMEAccess.Speech
{
    /// <summary>
    /// Speech front-end.
    ///
    /// The hard lesson behind this file: speechSay returning 1 does NOT mean you heard
    /// anything. It returns 1 as soon as any engine accepts the string, and a screen reader
    /// that is running but silenced - JAWS in sleep mode being the classic case - accepts it
    /// happily and says nothing. So the mod reports which engine answered, and offers a way to
    /// bypass the screen reader entirely by forcing SAPI.
    /// </summary>
    internal static class Talk
    {
        private static ManualLogSource _log;
        private static bool _usAvailable;
        private static int _spokenCount;
        private static string _last = "";
        private static float _lastExplicitAt;

        internal static bool Available => _usAvailable || Nvda.DllPresent;
        internal static string Last => _last;

        private static string Engine => (Cfg.SpeechEngine.Value ?? "auto").Trim().ToLowerInvariant();

        internal static void Init(ManualLogSource log)
        {
            _log = log;

            string err;
            _usAvailable = UniversalSpeech.Probe(out err);
            if (_usAvailable) _log.LogInfo("UniversalSpeech DLL loaded.");
            else _log.LogError("UniversalSpeech unavailable: " + err);

            Nvda.Probe();

            // Make SAPI available so there is always something that can make a noise, even if
            // no screen reader is running or the running one is asleep.
            if (_usAvailable && Cfg.EnableSapiFallback.Value)
            {
                int rc = UniversalSpeech.SafeCall(() => UniversalSpeech.SapiEnable(1));
                _log.LogInfo("sapiEnable(1) returned " + rc);
            }

            SelfTest(false);   // log only at startup: nobody wants four test lines every launch
        }

        /// <summary>
        /// Report what every speech route can do.
        ///
        /// At startup this only writes to the log - launching the game should not greet you with
        /// four test phrases. Pressing the speech-test key runs it with speak=true, which is
        /// when you actually want to hear which engine is talking.
        /// </summary>
        internal static void SelfTest(bool speak)
        {
            _log.LogInfo("--- speech self test ---");
            _log.LogInfo("configured engine      : " + Engine);
            _log.LogInfo("UniversalSpeech loaded : " + _usAvailable);

            if (_usAvailable)
            {
                _log.LogInfo("current engine name    : " + UniversalSpeech.CurrentEngineName());
                _log.LogInfo("current engine id      : " + UniversalSpeech.SafeCall(UniversalSpeech.CurrentScreenReaderId));
                _log.LogInfo("JAWS available         : " + UniversalSpeech.SafeCall(UniversalSpeech.JawsAvailable));
                _log.LogInfo("NVDA available         : " + UniversalSpeech.SafeCall(UniversalSpeech.NvdaAvailable));
                _log.LogInfo("SAPI available         : " + UniversalSpeech.SafeCall(UniversalSpeech.SapiAvailable));
                _log.LogInfo("SAPI enabled           : " + UniversalSpeech.SafeCall(UniversalSpeech.SapiEnabled));

                if (speak)
                {
                    _log.LogInfo("speechSay  -> " + UniversalSpeech.SafeCall(() => UniversalSpeech.Say("Universal speech test.", 1)));
                    _log.LogInfo("sapiSayW   -> " + UniversalSpeech.SafeCall(() => UniversalSpeech.SapiSay("S A P I test.", 1)));
                    _log.LogInfo("nvdaSayW   -> " + UniversalSpeech.SafeCall(() => UniversalSpeech.NvdaSay("N V D A test.", 1)));
                    _log.LogInfo("jfwSayW    -> " + UniversalSpeech.SafeCall(() => UniversalSpeech.JawsSay("Jaws test.", 1)));
                }
            }

            _log.LogInfo("NVDA client dll        : " + (Nvda.DllPresent ? Nvda.LoadedName : "not loadable - " + Nvda.LoadError));
            _log.LogInfo("NVDA running           : " + Nvda.IsRunning());
            if (speak && Nvda.DllPresent)
                _log.LogInfo("NVDA direct speak      : " + Nvda.Speak("N V D A direct test.", false));

            if (speak)
                _log.LogInfo("If you heard exactly one of these, set SpeechEngine in the config to that engine.");
            _log.LogInfo("--- end speech self test ---");
        }

        internal static void Explicit(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _lastExplicitAt = UnityEngine.Time.unscaledTime;
            Emit(text, true);
        }

        internal static void Incidental(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (UnityEngine.Time.unscaledTime - _lastExplicitAt < Cfg.IncidentalHoldOff.Value) return;
            Emit(text, Cfg.IncidentalInterrupts.Value);
        }

        internal static void RepeatLast()
        {
            if (string.IsNullOrEmpty(_last)) { Explicit("Nothing to repeat."); return; }
            Emit(_last, true);
        }

        internal static void Silence()
        {
            if (_usAvailable)
            {
                UniversalSpeech.SafeCall(UniversalSpeech.Stop);
                UniversalSpeech.SafeCall(UniversalSpeech.SapiStop);
            }
            Nvda.Stop();
        }

        private static string _lastEmitted;
        private static float _lastEmittedAt;

        private static void Emit(string text, bool interrupt)
        {
            // Suppress an identical line repeated in quick succession. Without this, any event
            // that fires on every step turns into a stutter.
            if (text == _lastEmitted && UnityEngine.Time.unscaledTime - _lastEmittedAt < Cfg.DuplicateWindow.Value)
                return;
            _lastEmitted = text;
            _lastEmittedAt = UnityEngine.Time.unscaledTime;

            _last = text;
            int flag = interrupt ? 1 : 0;
            bool spoke = false;
            string via = "none";

            switch (Engine)
            {
                case "sapi":
                    spoke = _usAvailable && UniversalSpeech.SafeCall(() => UniversalSpeech.SapiSay(text, flag)) > 0;
                    via = "sapi";
                    break;

                case "nvda":
                    if (Nvda.DllPresent && Nvda.Speak(text, interrupt)) { spoke = true; via = "nvda-direct"; }
                    else if (_usAvailable) { spoke = UniversalSpeech.SafeCall(() => UniversalSpeech.NvdaSay(text, flag)) > 0; via = "nvda"; }
                    break;

                case "jaws":
                    spoke = _usAvailable && UniversalSpeech.SafeCall(() => UniversalSpeech.JawsSay(text, flag)) > 0;
                    via = "jaws";
                    break;

                default: // auto
                    if (_usAvailable && UniversalSpeech.SafeCall(() => UniversalSpeech.Say(text, flag)) == 1)
                    { spoke = true; via = "universal"; }
                    else if (Nvda.DllPresent && Nvda.IsRunning() && Nvda.Speak(text, interrupt))
                    { spoke = true; via = "nvda-direct"; }
                    else if (_usAvailable && UniversalSpeech.SafeCall(() => UniversalSpeech.SapiSay(text, flag)) > 0)
                    { spoke = true; via = "sapi-fallback"; }
                    break;
            }

            if (spoke && Cfg.Braille.Value)
            {
                if (_usAvailable) UniversalSpeech.SafeCall(() => UniversalSpeech.Braille(text));
                Nvda.Braille(text);
            }

            if (Cfg.LogSpeech.Value || _spokenCount < 15)
            {
                _spokenCount++;
                _log.LogInfo("[speech " + (spoke ? "ok via " + via : "FAILED") + "] " + text);
            }
        }
    }
}
