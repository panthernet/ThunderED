using System;
using System.IO;

namespace TED_ConfigEditor.Classes
{
    public class Logger
    {
        private FileStream _stream;
        private StreamWriter _writer;
        private readonly string _fileName;

        protected virtual string GetLogString(string source)
        {
            return $"[{DateTime.Now.ToLongTimeString()}] {source}";
        }

        public bool IsEnabled { get; set; }

        public Logger(string fileName, bool isEnabled = true)
        {
            _fileName = fileName;
            if (!Directory.Exists(Path.GetDirectoryName(_fileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(_fileName));
            IsEnabled = isEnabled;
        }

        public virtual void Log(Exception ex, string message = null)
        {
            try
            {
                if (!IsEnabled) return;
                if (_writer != null) LogFast(ex, message);

                if (ex != null)
                {
                    File.AppendAllText(_fileName, GetLogString(ex.ToString()));
                    if (ex.InnerException != null)
                        File.AppendAllText(_fileName, GetLogString(ex.InnerException.ToString()));
                }

                if (!string.IsNullOrEmpty(message))
                    File.AppendAllText(_fileName, GetLogString(message));
            }
            catch
            {
                // ignored
            }
        }


        public virtual void Log(string message)
        {
            try
            {
                if (!IsEnabled) return;
                if (_writer != null) LogFast(message);
                File.AppendAllText(_fileName, GetLogString(message));
            }
            catch
            {
                // ignored
            }
        }

        public virtual void LogFast(string message)
        {
            if(!IsEnabled) return;
            try
            {
                if (_writer == null)
                {
                    _stream = File.Open(_fileName, FileMode.OpenOrCreate);
                    _writer = new StreamWriter(_stream);
                }
                _writer.Write(GetLogString(message));
            }
            catch
            {
                _stream?.Dispose();
                _stream = null;
                _writer?.Dispose();
                _writer = null;
            }
        }

        public virtual void LogFast(Exception ex, string message)
        {
            if(!IsEnabled) return;
            try
            {
                if (_writer == null)
                {
                    _stream = File.Open(_fileName, FileMode.OpenOrCreate);
                    _writer = new StreamWriter(_stream);
                }

                if (ex != null)
                {
                    _writer.Write(GetLogString(ex.ToString()));
                    if(ex.InnerException != null)
                        _writer.Write(GetLogString(ex.InnerException.ToString()));
                }

                if(!string.IsNullOrEmpty(message))
                    _writer.Write(GetLogString(message));
            }
            catch
            {
                _stream?.Dispose();
                _stream = null;
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
