using System;
using System.Data;
using System.Text;
using System.Collections;
using System.Threading;
using Microsoft.VisualBasic;
using Ion.Storage;
using System.Globalization;

namespace Holo.Managers
{
    /// <summary>
    /// Provides functions for management and manipulation of string objects.
    /// </summary>
    public static class stringManager
    {
        /// <summary>
        /// Contains the strings loaded from system_strings.
        /// </summary>
        private static Hashtable langStrings;
        /// <summary>
        /// Contains the array of swearwords to be filtered from chat etc, loaded from system_wordfilter.
        /// </summary>
        private static string[] swearWords;
        /// <summary>
        /// Swearwords in chat etc should be replaced by this censor.
        /// </summary>
        private static string filterCensor;
        /// <summary>
        /// The language extension to use for the emulator.
        /// </summary>
        internal static string langExt;
        /// <summary>
        /// Initializes the string manager with a certain language.
        /// </summary>
        /// <param name="langExtension">The language to use for the emulator, eg, 'en' for English.</param>
        public static void Init(string langExtension, bool Update)
        {
            try
            {
            langExt = langExtension;
            langStrings = new Hashtable();

            Out.WriteLine("Initializing strings from system_strings table for language '" + langExtension + "' ...");
            string[] langKeys;
            string[] langVars;
            DataTable dTable;
            using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
            {

                dTable = dbClient.getTable("SELECT stringid, var_" + langExt + " FROM system_strings ORDER BY id ASC");
            }
            langKeys = new string[dTable.Rows.Count];
            langVars = new string[dTable.Rows.Count];
            foreach (DataRow dRow in dTable.Rows)
            {
                langStrings.Add(Convert.ToString(dRow["stringid"]), Convert.ToString(dRow["var_" + langExt]));
            }
                
            
            Out.WriteLine("Loaded " + langStrings.Count + " strings from system_strings table.");

            if (Config.getTableEntry("welcomemessage_enable") == "1")
            {
                if (getString("welcomemessage_text") != "")
                {
                    Config.enableWelcomeMessage = true;
                    Out.WriteLine("Welcome message enabled.");
                }
                else
                    Out.WriteLine("Welcome message was preferred as enabled, but has been left blank. Ignored.");
            }
            else
            {
                Out.WriteLine("Welcome message disabled.");
            }

            if (Update)
                Thread.CurrentThread.Abort();
            }
            catch (Exception e) { Out.writeSeriousError(e.ToString()); Eucalypt.Shutdown(); }
        }
        /// <summary>
        /// Intializes/reloads the word filter, which filters swearwords etc from texts.
        /// </summary>
        public static void initFilter(bool Update)
        {
            if (Config.getTableEntry("wordfilter_enable") == "1")
            {
                Out.WriteLine("Initializing word filter...");
                DataColumn dCol;
                using (DatabaseClient dbClient = Eucalypt.criticalManager.GetClient())
                {
                    dCol = dbClient.getColumn("SELECT word FROM wordfilter");
                }
                swearWords = dataHandling.dColToArray(dCol);
                filterCensor = Config.getTableEntry("wordfilter_censor");

                if (swearWords.Length == 0 || filterCensor == "")
                    Out.WriteLine("Word filter was preferred as enabled but no words and/or replacement found, wordfilter disabled.");
                else
                {
                    Config.enableWordFilter = true;
                    Out.WriteLine("Word filter enabled, " + swearWords.Length + " word(s) found, replacement: " + filterCensor);
                }
            }
            else
            {
                Out.WriteLine("Word filter disabled.");
            }
            if (Update)
                Thread.CurrentThread.Abort();
        }
        /// <summary>
        /// Retrieves a system_strings entry for a certain key. The strings are loaded at the initialization of the string manager.
        /// </summary>
        /// <param name="stringID">The key of the string to retrieve.</param>
        public static string getString(string stringID)
        {
            try { return langStrings[stringID].ToString(); }
            catch { return stringID; }
        }
        /// <summary>
        /// Filters the swearwords in an input string and replaces them by the set censor.
        /// </summary>
        /// <param name="Text">The string to filter.</param>
        public static string filterSwearWords(string Text)
        {
            if (Config.enableWordFilter)
            {
                for (int i = 0; i < swearWords.Length; i++)
                    Text = Strings.Replace(Text, swearWords[i], filterCensor, 1, -1, Constants.vbTextCompare);
            }
            return Text;
        }
        /// <summary>
        /// Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length. If any error occurs, then "" is returned.
        /// </summary>
        /// <param name="Input">The input string.</param>
        /// <param name="startIndex">The zero-based starting character position of a substring in this instance.</param>
        /// <param name="Length">The number of characters in the substring.</param>
        public static string getStringPart(string Input, int startIndex, int Length)
        {
                if ((Input.Length - startIndex) < Length)
                    return "";
                return Input.Substring(startIndex, Length); 
        }
        /// <summary>
        /// Wraps a string array of parameters to one string, separated by spaces.
        /// </summary>
        /// <param name="s">The string arrays with parameters.</param>
        /// <param name="startIndex">The parameter ID in the array to start off with. Parameters with lower IDs won't be included.</param>
        public static string wrapParameters(string[] s, int startIndex)
        {
            StringBuilder sb = new StringBuilder();
            //try
            {
                for (int i = startIndex; i < s.Length; i++)
                    sb.Append(" " + s[i]);

                return sb.ToString().Substring(1);
            }
            //catch { return ""; }
        }
        /// <summary>
        /// Checks if a string has somekind of character in it
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool isGoodText(string text)
        {
            return !text.Contains("\n");
            
        }

        public static bool usernameIsValid(string Username)
        {
            if (Username.Length > 15)
                return false;
            const string Allowed = "1234567890qwertyuiopasdfghjklzxcvbnm-+=?!@:.,$";
            foreach (char chr in Username.ToLower())
            {
                if (!Allowed.Contains(chr.ToString()))
                    return false;
            }

            return true;
        }
        /// <summary>
        /// Formats a given floating point value to the format 0.00 and returns it as a string.
        /// </summary>
        /// <param name="f">The floating point value to format and return.</param>
        public static string formatFloatForClient(float f)
        {
            return f.ToString("0.00", CultureInfo.InvariantCulture);
        }

    }
}
