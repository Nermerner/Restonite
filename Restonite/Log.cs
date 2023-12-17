using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Restonite
{
    internal static class Log
    {
        public static void Setup(WizardUi ui, Action<string> debug, Action<string> info, Action<string> warn, Action<string> error)
        {
            _ui = ui;
            _debug = debug;
            _info = info;
            _warn = warn;
            _error = error;
        }

        private static WizardUi _ui;
        private static Action<string> _debug = x => { };
        private static Action<string> _info = x => { };
        private static Action<string> _warn = x => { };
        private static Action<string> _error = x => { };

        public static void Debug(string logMessage)
        {
            _debug(logMessage);
            _ui.LogDebug(logMessage);
        }

        public static void Info(string logMessage)
        {
            _info(logMessage);
            _ui.LogInfo(logMessage);
        }

        public static void Warn(string logMessage)
        {
            _warn(logMessage);
            _ui.LogWarn(logMessage);
        }

        public static void Error(string logMessage)
        {
            _error(logMessage);
            _ui.LogError(logMessage);
        }

        public static void Success(string logMessage)
        {
            _info(logMessage);
            _ui.LogSuccess(logMessage);
        }

        public static void Clear()
        {
            _ui.ClearLog();
        }
    }
}
