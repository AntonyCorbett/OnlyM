namespace OnlyM.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Parses the command-line. Supports switches of the form "-[switch]"
    /// and parameters of the form "/[param]="
    /// </summary>
    public class CommandLineParser
    {
        private const string IdKey = "/id";

        /// <summary>
        /// singleton instance of CommandLineParser
        /// </summary>
        private static CommandLineParser ParserInstance;

        private readonly List<string> _rawItems;
        private readonly List<string> _switches;
        private readonly Dictionary<string, string> _parameters;

        public static CommandLineParser Instance => ParserInstance ?? (ParserInstance = new CommandLineParser());

        private readonly string[] _switchValues =
        {
            "-nogpu"
        };

        private readonly string[] _paramKeys =
        {
            IdKey
        };

        private bool _parsed;

        /// <summary>
        /// Gets the "/id=" argument
        /// </summary>
        /// <returns>Id value.</returns>
        public string GetId()
        {
            return GetParamValue(IdKey);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParser"/> class. 
        /// Initialise with optional args.
        /// </summary>
        /// <param name="args">
        /// command-line args (useful for units tests, otherwise omit).
        /// </param>
        public CommandLineParser(IEnumerable<string> args = null)
        {
            _rawItems = args?.ToList() ?? Environment.GetCommandLineArgs().ToList();
            _switches = new List<string>();
            _parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get collection of switches, i.e. command-line args of the form "-[switch]"
        /// </summary>
        public IEnumerable<string> Switches
        {
            get
            {
                Parse();
                return _switches;
            }
        }

        /// <summary>
        /// Get collection of params, i.e. command-line args of the form "/[param]="
        /// </summary>
        public IReadOnlyDictionary<string, string> Parameters
        {
            get
            {
                Parse();
                return _parameters;
            }
        }

        /// <summary>
        /// Determines if the specified switch is present on the command-line
        /// </summary>
        /// <param name="key">The switch value, e.g. "-myswitch"</param>
        /// <returns>True if present</returns>
        // ReSharper disable once UnusedMember.Global
        public bool IsSwitchSet(string key)
        {
            return Switches.Contains(key);
        }

        /// <summary>
        /// Gets the specified parameter from the command-line
        /// </summary>
        /// <param name="key">The parameter key, e.g. "/myparam"</param>
        /// <param name="defValue">The value to use if the key is not present</param>
        /// <returns>The value</returns>
        public string GetParamValue(string key, string defValue = null)
        {
            string result = defValue;

            if (Parameters.ContainsKey(key))
            {
                result = Parameters[key];
            }

            return result;
        }

        private void Parse()
        {
            if (!_parsed)
            {
                StringBuilder currentParamValue = new StringBuilder();
                string currentKey = null;

                // skip the app path (item 0)...
                int startIndex = 1;
                for (int n = startIndex; n < _rawItems.Count; ++n)
                {
                    var item = _rawItems[n];

                    if (!string.IsNullOrWhiteSpace(item) && !item.Equals("="))
                    {
                        if (_switchValues.Contains(item, StringComparer.OrdinalIgnoreCase))
                        {
                            _switches.Add(item.ToLower());
                            SaveParam(currentKey, currentParamValue);
                        }
                        else
                        {
                            var key = _paramKeys.FirstOrDefault(x => item.StartsWith(x, StringComparison.OrdinalIgnoreCase));
                            if (key != null)
                            {
                                SaveParam(currentKey, currentParamValue);

                                currentKey = key;
                                currentParamValue.Clear();

                                if (item.Length > key.Length)
                                {
                                    if (currentParamValue.Length > 0)
                                    {
                                        currentParamValue.Append(" ");
                                    }

                                    currentParamValue.Append(item.Substring(key.Length).Replace("=", string.Empty).Trim());
                                }
                            }
                            else
                            {
                                // add to the param value...
                                if (currentParamValue.Length > 0)
                                {
                                    currentParamValue.Append(" ");
                                }

                                currentParamValue.Append(item);
                            }
                        }
                    }
                }

                SaveParam(currentKey, currentParamValue);
                _parsed = true;
            }
        }

        private void SaveParam(string currentKey, StringBuilder currentParamValue)
        {
            if (!string.IsNullOrEmpty(currentKey) && currentParamValue.Length > 0)
            {
                if (!_parameters.ContainsKey(currentKey))
                {
                    _parameters.Add(currentKey, currentParamValue.ToString());
                }
            }
        }
    }
}
