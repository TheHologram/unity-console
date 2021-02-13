/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

/* ****************************************************************************
 * Altered to combine BasicConsole and SuperConsole WindowsConsoleDriver 
 *   into single Console with direct references to Console.removed for 
 *   mono compatibility on windows and unity
 * ***************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting.Shell;

namespace UC
{
    public class UnityConsole : IConsole, IDisposable
    {

        #region Nested types: History, SuperConsoleOptions, Cursor

        /// <summary>
        /// Class managing the command history.
        /// </summary>
        public class History
        {
            private readonly List<string> _list = new List<string>();
            private System.Collections.Hashtable _hashtable = new Hashtable();
            private int _current;
            private bool _increment; // increment on Next()
            private StreamWriter writer;

            public bool NoDuplicates { get; set; }

            public string Current => _current >= 0 && _current < _list.Count ? _list[_current] : String.Empty;

            public void Load(StreamReader reader)
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    Add(line, true);
                }
            }
            public void AttachWriter(string file, bool autoflush)
            {
                try
                {
                    this.writer = new StreamWriter(file, true) { AutoFlush = autoflush };
                }
                catch
                {
                    this.writer = null;
                }
            }

            public void Add(string line, bool setCurrentAsLast)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    int oldCount = _list.Count;
                    _list.Add(line);
                    if (writer != null)
                    {
                        try
                        {
                            var write = true;
                            if (NoDuplicates)
                            {
                                write = !_hashtable.ContainsKey(line);
                                if (write) _hashtable[line] = null;
                            }
                            if (write) writer.WriteLine(line);
                        }
                        catch
                        {
                            writer = null;
                        }
                    }

                    if (setCurrentAsLast || _current == oldCount)
                    {
                        _current = _list.Count;
                    }
                    else
                    {
                        _current++;
                    }
                    // Do not increment on the immediately following Next()
                    _increment = false;
                }
            }

            public string Previous()
            {
                if (_current > 0)
                {
                    _current--;
                    _increment = true;
                }
                return Current;
            }

            public string Next()
            {
                if (_current + 1 < _list.Count)
                {
                    if (_increment) _current++;
                    _increment = true;
                }
                return Current;
            }

            public void Clear()
            {
                _current = 0;
                _list.Clear();
            }
        }

        /// <summary>
        /// List of available options
        /// </summary>
        class SuperConsoleOptions
        {
            private readonly List<string> _list = new List<string>();
            private int _current;

            public int Count => _list.Count;

            private string Current => _current >= 0 && _current < _list.Count ? _list[_current] : String.Empty;

            public void Clear()
            {
                _list.Clear();
                _current = -1;
            }

            public void Add(string line)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    _list.Add(line);
                }
            }

            public string Previous()
            {
                if (_list.Count > 0)
                {
                    _current = ((_current - 1) + _list.Count)%_list.Count;
                }
                return Current;
            }

            public string Next()
            {
                if (_list.Count > 0)
                {
                    _current = (_current + 1)%_list.Count;
                }
                return Current;
            }

            public string Root { get; set; }
        }

        /// <summary>
        /// Cursor position management
        /// </summary>
        class Cursor
        {
            private WindowsConsoleDriver Driver;
            /// <summary>
            /// Beginning position of the cursor - top coordinate.
            /// </summary>
            private int _anchorTop;

            /// <summary>
            /// Beginning position of the cursor - left coordinate.
            /// </summary>
            private int _anchorLeft;

            public Cursor(WindowsConsoleDriver driver)
            {
                Driver = driver;
            }

            public void Anchor()
            {
                _anchorTop = Driver.CursorTop;
                _anchorLeft = Driver.CursorLeft;
            }

            public void Reset()
            {
                Driver.CursorTop = _anchorTop;
                Driver.CursorLeft = _anchorLeft;
            }

            public void Place(int index)
            {
                Driver.CursorLeft = (_anchorLeft + index)%Driver.BufferWidth;
                int cursorTop = _anchorTop + (_anchorLeft + index)/Driver.BufferWidth;
                if (cursorTop >= Driver.BufferHeight)
                {
                    _anchorTop -= cursorTop - Driver.BufferHeight + 1;
                    cursorTop = Driver.BufferHeight - 1;
                }
                Driver.CursorTop = cursorTop;
            }

            public static void Move(WindowsConsoleDriver driver, int delta)
            {
                int position = driver.CursorTop*driver.BufferWidth + driver.CursorLeft + delta;

                driver.CursorLeft = position%driver.BufferWidth;
                driver.CursorTop = position/driver.BufferWidth;
            }
        }

        #endregion

        internal WindowsConsoleDriver Driver { get; } = new WindowsConsoleDriver();
        public TextWriter Output { get; set; }
        public TextWriter ErrorOutput { get; set; }
        protected AutoResetEvent CtrlCEvent { get; set; }
        protected Thread CreatingThread { get; set; }
        public bool SuppressOutput { get; set; } = false;

        private ConsoleColor _infoColor;
        private ConsoleColor _promptColor;
        private ConsoleColor _outColor;
        private ConsoleColor _errorColor;
        private ConsoleColor _warningColor;

        /// <summary>
        /// The console input buffer.
        /// </summary>
        private readonly StringBuilder _input = new StringBuilder();

        /// <summary>
        /// Current position - index into the input buffer
        /// </summary>
        private int _current;

        /// <summary>
        /// The number of white-spaces displayed for the auto-indenation of the current line
        /// </summary>
        private int _autoIndentSize;

        /// <summary>
        /// Length of the output currently rendered on screen.
        /// </summary>
        private int _rendered;

        /// <summary>
        /// Command history
        /// </summary>
        private readonly History _history;

        /// <summary>
        /// Tab options available in current context
        /// </summary>
        private readonly SuperConsoleOptions _options = new SuperConsoleOptions();

        /// <summary>
        /// Cursort anchor - position of cursor when the routine was called
        /// </summary>
        private Cursor _cursor;

        /// <summary>
        /// The command line that this console is attached to.
        /// </summary>
        private readonly CommandLine _commandLine;

        public UnityConsole(CommandLine commandLine, History history = null)
        {
            this._history = history ?? new History();
            this.Output = System.Console.Out;
            this.ErrorOutput = System.Console.Error;
            //Output = new StreamWriter(outStream) {AutoFlush = true};
            //ErrorOutput = new StreamWriter(errStream) { AutoFlush = true };
            SetupColors(true);

            CreatingThread = Thread.CurrentThread;

            //TODO: how to handle this ???
            //Console.CancelKeyPress += new ConsoleCancelEventHandler(delegate (object sender, ConsoleCancelEventArgs e)
            //{
            //    if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            //    {
            //        e.Cancel = true;
            //        CtrlCEvent.Set();
            //        CreatingThread.Abort(new KeyboardInterruptException(""));
            //    }
            //});

            CtrlCEvent = new AutoResetEvent(false);
            _commandLine = commandLine;
            _cursor = new Cursor(Driver);
        }

        public void Clear()
        {
            Driver.Clear();
        }
        public void ClearHistory()
        {
            Driver.Clear();
            _history.Clear();
        }

        public void ClearInput()
        {
            SetInput("");
        }

        private bool GetOptions()
        {
            _options.Clear();

            //IEnumerable<string> options = null;
            //if (_commandLine.TryGetOptions(_input, out options))
            //{
            //    foreach (var arg in options)
            //        _options.Add(arg);
            //    _options.Root = _input.ToString();
            //    return true;
            //}

            int len;
            for (len = _input.Length; len > 0; len--)
            {
                var c = _input[len - 1];
                if (!char.IsLetterOrDigit(c) && !(c == '.' || c == '_'))
                {
                    break;
                }
            }

            var name = _input.ToString(len, _input.Length - len);
            if (name.Trim().Length > 0)
            {
                var lastDot = name.LastIndexOf('.');
                string attr, pref, root;
                if (lastDot < 0)
                {
                    attr = string.Empty;
                    pref = name;
                    root = _input.ToString(0, len);
                }
                else
                {
                    attr = name.Substring(0, lastDot);
                    pref = name.Substring(lastDot + 1);
                    root = _input.ToString(0, len + lastDot + 1);
                }

                try
                {
                    IList<string> result;
                    if (string.IsNullOrEmpty(attr))
                    {
                        result = _commandLine.GetGlobals(name);
                    }
                    else
                    {
                        result = new List<string>();
                        foreach (var nm in _commandLine.GetMemberNames(attr))
                            if (!nm.StartsWith("_") && !nm.StartsWith("<"))
                                result.Add(nm);
                    }

                    _options.Root = root;
                    foreach (var option in result)
                    {
                        if (option.StartsWith(pref, StringComparison.CurrentCultureIgnoreCase))
                        {
                            _options.Add(option);
                        }
                    }
                }
                catch
                {
                    _options.Clear();
                }
                return true;
            }
            return false;
        }

        private void SetInput(string line)
        {
            _input.Length = 0;
            _input.Append(line);

            _current = _input.Length;

            Render();
        }

        private void Initialize()
        {
            _cursor.Anchor();
            _input.Length = 0;
            _current = 0;
            _rendered = 0;
        }

        // Check if the user is backspacing the auto-indentation. In that case, we go back all the way to
        // the previous indentation level.
        // Return true if we did backspace the auto-indenation.
        private bool BackspaceAutoIndentation()
        {
            if (_input.Length == 0 || _input.Length > _autoIndentSize) return false;

            // Is the auto-indenation all white space, or has the user since edited the auto-indentation?
            for (int i = 0; i < _input.Length; i++)
            {
                if (_input[i] != ' ') return false;
            }

            // Calculate the previous indentation level
            //!!! int newLength = ((input.Length - 1) / ConsoleOptions.AutoIndentSize) * ConsoleOptions.AutoIndentSize;            
            int newLength = _input.Length - 4;

            int backspaceSize = _input.Length - newLength;
            _input.Remove(newLength, backspaceSize);
            _current -= backspaceSize;
            Render();
            return true;
        }

        private void OnBackspace()
        {
            if (BackspaceAutoIndentation()) return;

            if (_input.Length > 0 && _current > 0)
            {
                _input.Remove(_current - 1, 1);
                _current--;
                Render();
            }
        }

        private void OnDelete()
        {
            if (_input.Length > 0 && _current < _input.Length)
            {
                _input.Remove(_current, 1);
                Render();
            }
        }

        private void Insert(ConsoleKeyInfo key)
        {
            char c;
            if (key.Key == ConsoleKey.F6)
            {
                Debug.Assert(FinalLineText.Length == 1);

                c = FinalLineText[0];
            }
            else
            {
                c = key.KeyChar;
            }
            Insert(c);
        }

        private void Insert(char c)
        {
            if (c == 0)
                return;
            if (_current == _input.Length)
            {
                if (Char.IsControl(c))
                {
                    string s = MapCharacter(c);
                    _current++;
                    _input.Append(c);
                    Output.Write(s);
                    _rendered += s.Length;
                }
                else
                {
                    _current++;
                    _input.Append(c);
                    Output.Write(c);
                    _rendered++;
                }
            }
            else
            {
                _input.Insert(_current, c);
                _current++;
                Render();
            }
        }

        private static string MapCharacter(char c)
        {
            if (c == 0) return "";
            if (c == 13) return "\r\n";
            if (c <= 26) return "^" + ((char) (c + 'A' - 1)).ToString();

            return "^?";
        }

        private static int GetCharacterSize(char c)
        {
            if (Char.IsControl(c))
            {
                return MapCharacter(c).Length;
            }
            else
            {
                return 1;
            }
        }

        public int Width { get { return Driver.WindowWidth; } }

        private void Render()
        {
            if (SuppressOutput) return;

            _cursor.Reset();
            StringBuilder output = new StringBuilder();
            int position = -1;
            for (int i = 0; i < _input.Length; i++)
            {
                if (i == _current)
                {
                    position = output.Length;
                }
                char c = _input[i];
                if (Char.IsControl(c))
                {
                    output.Append(MapCharacter(c));
                }
                else
                {
                    output.Append(c);
                }
            }

            if (_current == _input.Length)
            {
                position = output.Length;
            }

            string text = output.ToString();
            Output.Write(text);

            if (text.Length < _rendered)
            {
                Output.Write(new String(' ', _rendered - text.Length));
            }
            _rendered = text.Length;
            _cursor.Place(position);
        }

        private void MoveLeft(ConsoleModifiers keyModifiers)
        {
            if ((keyModifiers & ConsoleModifiers.Control) != 0)
            {
                // move back to the start of the previous word
                if (_input.Length > 0 && _current != 0)
                {
                    bool nonLetter = IsSeperator(_input[_current - 1]);
                    while (_current > 0 && (_current - 1 < _input.Length))
                    {
                        MoveLeft();

                        if (IsSeperator(_input[_current]) != nonLetter)
                        {
                            if (!nonLetter)
                            {
                                MoveRight();
                                break;
                            }

                            nonLetter = false;
                        }
                    }
                }
            }
            else
            {
                MoveLeft();
            }
        }

        private static bool IsSeperator(char ch)
        {
            return !Char.IsLetter(ch);
        }

        private void MoveRight(ConsoleModifiers keyModifiers)
        {
            if ((keyModifiers & ConsoleModifiers.Control) != 0)
            {
                // move to the next word
                if (_input.Length != 0 && _current < _input.Length)
                {
                    bool nonLetter = IsSeperator(_input[_current]);
                    while (_current < _input.Length)
                    {
                        MoveRight();

                        if (_current == _input.Length) break;
                        if (IsSeperator(_input[_current]) != nonLetter)
                        {
                            if (nonLetter)
                                break;

                            nonLetter = true;
                        }
                    }
                }
            }
            else
            {
                MoveRight();
            }
        }

        private void MoveRight()
        {
            if (_current < _input.Length)
            {
                char c = _input[_current];
                _current++;
                Cursor.Move(Driver, GetCharacterSize(c));
            }
        }

        private void MoveLeft()
        {
            if (_current > 0 && (_current - 1 < _input.Length))
            {
                _current--;
                char c = _input[_current];
                Cursor.Move(Driver, -GetCharacterSize(c));
            }
        }

        private const int TabSize = 4;

        private void InsertTab()
        {
            for (int i = TabSize - (_current%TabSize); i > 0; i--)
            {
                Insert(' ');
            }
        }

        private void MoveHome()
        {
            _current = 0;
            _cursor.Reset();
        }

        private void MoveEnd()
        {
            _current = _input.Length;
            _cursor.Place(_rendered);
        }
        
        public virtual string ReadLine(int autoIndentSize)
        {
            Initialize();

            _autoIndentSize = autoIndentSize;
            for (int i = 0; i < _autoIndentSize; i++)
                Insert(' ');

            bool inputChanged = false;
            bool optionsObsolete = true;

            for (;;)
            {
                ConsoleKeyInfo key = Driver.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Backspace:
                        OnBackspace();
                        inputChanged = optionsObsolete = true;
                        break;
                    case ConsoleKey.Delete:
                        OnDelete();
                        inputChanged = optionsObsolete = true;
                        break;
                    case ConsoleKey.Enter:
                        return OnEnter(inputChanged);
                    case ConsoleKey.Tab:
                    {
                        bool prefix = false;
                        if (optionsObsolete)
                        {
                            prefix = GetOptions();
                            optionsObsolete = false;
                        }

                        // Displays the next option in the option list,
                        // or beeps if no options available for current input prefix.
                        // If no input prefix, simply print tab.
                        DisplayNextOption(key, prefix);
                        inputChanged = true;
                        if (prefix && _options.Count == 1) // only one option
                            optionsObsolete = true;
                        break;
                    }
                    case ConsoleKey.UpArrow:
                        SetInput(_history.Previous());
                        optionsObsolete = true;
                        inputChanged = false;
                        break;
                    case ConsoleKey.DownArrow:
                        SetInput(_history.Next());
                        optionsObsolete = true;
                        inputChanged = false;
                        break;
                    case ConsoleKey.RightArrow:
                        MoveRight(key.Modifiers);
                        optionsObsolete = true;
                        break;
                    case ConsoleKey.LeftArrow:
                        MoveLeft(key.Modifiers);
                        optionsObsolete = true;
                        break;
                    case ConsoleKey.Escape:
                        SetInput(String.Empty);
                        inputChanged = optionsObsolete = true;
                        break;
                    case ConsoleKey.Home:
                        MoveHome();
                        optionsObsolete = true;
                        break;
                    case ConsoleKey.End:
                        MoveEnd();
                        optionsObsolete = true;
                        break;
                    case ConsoleKey.LeftWindows:
                    case ConsoleKey.RightWindows:
                        // ignore these
                        continue;

                    default:
                        if (key.KeyChar == '\x0D') goto case ConsoleKey.Enter; // Ctrl-M
                        if (key.KeyChar == '\x08') goto case ConsoleKey.Backspace; // Ctrl-H
                        Insert(key);
                        inputChanged = optionsObsolete = true;
                        break;
                }
            }
        }

        public void Abort()
        {
            this._commandLine.Terminate(0);
            Driver.Abort();
            ClearInput();
        }

        /// <summary>
        /// Displays the next option in the option list,
        /// or beeps if no options available for current input prefix.
        /// If no input prefix, simply print tab.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="prefix"></param>
        private void DisplayNextOption(ConsoleKeyInfo key, bool prefix)
        {
            if (_options.Count > 0)
            {
                string part = (key.Modifiers & ConsoleModifiers.Shift) != 0 ? _options.Previous() : _options.Next();
                SetInput(_options.Root + part);
            }
            else
            {
                if (prefix)
                {
                    Driver.Beep();
                }
                else
                {
                    InsertTab();
                }
            }
        }

        /// <summary>
        /// Handle the enter key. Adds the current input (if not empty) to the history.
        /// </summary>
        /// <param name="inputChanged"></param>
        /// <returns>The input string.</returns>
        private string OnEnter(bool inputChanged)
        {
            Output.Write("\n");
            string line = _input.ToString();
            if (line == FinalLineText) return null;
            if (line.Length > 0)
            {
                _history.Add(line, inputChanged);
            }
            return line;
        }

        string FinalLineText => Environment.OSVersion.Platform != PlatformID.Unix ? "\x1A" : "\x04";

        private void SetupColors(bool colorful)
        {

            if (colorful)
            {
                _infoColor = PickColor(ConsoleColor.White, ConsoleColor.White);
                _promptColor = PickColor(ConsoleColor.Gray, ConsoleColor.White);
                _outColor = PickColor(ConsoleColor.Green, ConsoleColor.White);
                _errorColor = PickColor(ConsoleColor.Red, ConsoleColor.White);
                _warningColor = PickColor(ConsoleColor.Yellow, ConsoleColor.White);
            }
            else
            {
#if !SILVERLIGHT
                _promptColor = _outColor = _errorColor = _warningColor = Driver.ForegroundColor;
#endif
            }
        }

        private ConsoleColor PickColor(ConsoleColor best, ConsoleColor other)
        {
#if SILVERLIGHT
            return best;
#else
            if (Driver.BackgroundColor != best)
            {
                return best;
            }

            return other;
#endif
        }

        protected void WriteColor(TextWriter output, string str, ConsoleColor c)
        {
            if (SuppressOutput) return;

#if !SILVERLIGHT // Console.ForegroundColor
            ConsoleColor origColor = Driver.ForegroundColor;
            Driver.ForegroundColor = c;
#endif
            output.Write(str);
            output.Flush();

#if !SILVERLIGHT // Console.ForegroundColor
            Driver.ForegroundColor = origColor;
#endif
        }

        #region IConsole Members

        
        public virtual void Write(string text, Style style)
        {
            switch (style)
            {
                //case Style.Info:
                //    WriteColor(Output, text, _infoColor);
                //    break;
                case Style.Prompt:
                    WriteColor(Output, text, _promptColor);
                    break;
                case Style.Out:
                    WriteColor(Output, text, _outColor);
                    break;
                case Style.Error:
                    WriteColor(ErrorOutput, text, _errorColor);
                    break;
                case Style.Warning:
                    WriteColor(ErrorOutput, text, _warningColor);
                    break;
            }
        }

        public void WriteLine(string text, Style style)
        {
            Write(text + Environment.NewLine, style);
        }

        public void WriteLine()
        {
            Write(Environment.NewLine, Style.Out);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            CtrlCEvent?.Close();

            GC.SuppressFinalize(this);
        }

        #endregion
    }

    class ShutdownConsoleException : Exception { }
}
