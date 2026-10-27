using System;
using System.IO;

namespace com.github.lhervier.ksp.fix.constructionphysicsless
{
    /// <summary>
    /// What the player can set in PluginData/settings.cfg, next to the DLL. Read once, when KSP starts.
    /// </summary>
    internal static class Settings
    {
        /// <summary>
        /// Whether the physicsless parts stay in place when a vessel comes off rails while a part is held in EVA
        /// construction mode (see <see cref="PhysicslessPartsStayStill"/>).
        /// </summary>
        public static bool PhysicslessPartsStayStill { get; private set; } = true;

        /// <summary>
        /// Whether the Rigidbodies the stock code leaves on the physicsless parts, when a held part is released with
        /// the construction panel already closed, are removed instead of only being kept kinematic. Only read when
        /// <see cref="PhysicslessPartsStayStill"/> is on.
        /// </summary>
        public static bool OrphanRigidbodiesRemoved { get; private set; } = true;

        /// <summary>
        /// Reads the settings file and applies its log level. A missing file, a missing value or an unknown
        /// value leaves the default: the fix on, orphan Rigidbodies removed, logging at Info.
        /// </summary>
        public static void Load()
        {
            string folder = Path.GetDirectoryName(typeof(Settings).Assembly.Location);
            string path = Path.Combine(Path.Combine(folder, "PluginData"), "settings.cfg");
            if (!File.Exists(path))
            {
                Log.Warning($"No settings file at {path}, using the defaults");
                return;
            }
            ConfigNode node = ConfigNode.Load(path);
            if (node == null)
            {
                Log.Warning($"Could not read {path}, using the defaults");
                return;
            }

            string level = node.GetValue("logLevel");
            LogLevel parsedLevel;
            if (Enum.TryParse(level, true, out parsedLevel) && Enum.IsDefined(typeof(LogLevel), parsedLevel))
            {
                Log.Level = parsedLevel;
            }
            else
            {
                Log.Warning($"Unknown logLevel '{level}' in {path}, logging at {Log.Level}");
            }

            PhysicslessPartsStayStill = ReadBool(node, "physicslessPartsStayStill", PhysicslessPartsStayStill, path);
            OrphanRigidbodiesRemoved = ReadBool(node, "orphanRigidbodiesRemoved", OrphanRigidbodiesRemoved, path);
        }

        /// <summary>
        /// Returns the boolean value <paramref name="name"/> of <paramref name="node"/>, or
        /// <paramref name="fallback"/> when it is missing or not a boolean (the latter with a warning).
        /// </summary>
        private static bool ReadBool(ConfigNode node, string name, bool fallback, string path)
        {
            string value = node.GetValue(name);
            if (value == null)
            {
                return fallback;
            }
            bool parsed;
            if (bool.TryParse(value, out parsed))
            {
                return parsed;
            }
            Log.Warning($"Unknown {name} '{value}' in {path}, using {fallback}");
            return fallback;
        }
    }
}
