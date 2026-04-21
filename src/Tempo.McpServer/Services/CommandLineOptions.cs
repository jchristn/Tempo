namespace Tempo.McpServer.Services
{
    using System;

    /// <summary>
    /// Command-line options for the Tempo MCP server.
    /// </summary>
    public class CommandLineOptions
    {
        /// <summary>Optional settings path.</summary>
        public string? SettingsPath { get; set; } = null;

        /// <summary>True to print settings and exit.</summary>
        public bool ShowConfiguration { get; set; } = false;

        /// <summary>True to install Claude configuration and exit.</summary>
        public bool Install { get; set; } = false;

        /// <summary>True to preview install changes.</summary>
        public bool DryRun { get; set; } = false;

        /// <summary>True to show help and exit.</summary>
        public bool Help { get; set; } = false;

        /// <summary>Parse command-line arguments.</summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Parsed options.</returns>
        public static CommandLineOptions Parse(string[] args)
        {
            CommandLineOptions options = new CommandLineOptions();
            if (args == null) return options;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                {
                    options.SettingsPath = arg.Substring("--config=".Length);
                }
                else if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    options.SettingsPath = args[i + 1];
                    i++;
                }
                else if (arg.Equals("--showconfig", StringComparison.OrdinalIgnoreCase))
                {
                    options.ShowConfiguration = true;
                }
                else if (arg.Equals("install", StringComparison.OrdinalIgnoreCase))
                {
                    options.Install = true;
                }
                else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
                {
                    options.DryRun = true;
                }
                else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    options.Help = true;
                }
            }

            return options;
        }
    }
}
