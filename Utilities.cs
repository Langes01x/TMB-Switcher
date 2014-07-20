using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Net;

namespace TMB_Switcher
{
    public static class Utilities
    {
        #region Formatting Utilities

        /// <summary>
        /// Denotes the units that are used for a hashing speed
        /// </summary>
        public enum hashUnits
        {
            h,
            Kh,
            Mh,
            Gh
        }

        /// <summary>
        /// Formats a hashing speed into a proper display unit
        /// </summary>
        /// <param name="hashRate">Input hash rate</param>
        /// <param name="units">Units for the input hash rate</param>
        /// <returns>The hashing speed in a readable format</returns>
        public static string formatHashSpeed(double hashRate, hashUnits units)
        {
            if (units == hashUnits.h && hashRate > 1000)
            {
                hashRate /= 1000;
                units = hashUnits.Kh;
            }
            if (units == hashUnits.Kh && hashRate > 1000)
            {
                hashRate /= 1000;
                units = hashUnits.Mh;
            }
            if (units == hashUnits.Mh && hashRate > 1000)
            {
                hashRate /= 1000;
                units = hashUnits.Gh;
            }
            return string.Format("{0} {1}/s", Math.Round(hashRate, 6), units.ToString());
        }

        /// <summary>
        /// Formats the profit values into a proper length
        /// </summary>
        /// <param name="profit">Input profit</param>
        /// <returns>The profit value rounded to a proper length</returns>
        public static string formatProfit(double profit)
        {
            return profit.ToString("F8");
        }

        #endregion

        #region File Writing

        /// <summary>
        /// Writes to a log file (generally for exception logging)
        /// </summary>
        /// <param name="message"></param>
        public static void WriteLog(string message)
        {
            string logFile = Assembly.GetEntryAssembly().Location.Replace(".exe", ".log");
            using (StreamWriter file = new StreamWriter(logFile, true, Encoding.Unicode))
            {
                file.WriteLine(DateTime.Now + ":");
                file.WriteLine(message);
            }
        }

        /// <summary>
        /// Write to a log file the current profit values
        /// </summary>
        /// <param name="profit">A dictionary containing the current profit values</param>
        /// <param name="fileName">The file to write to</param>
        public static void WriteProfitLog(Dictionary<string, double> profit, string fileName)
        {
            if (Directory.Exists(Path.GetDirectoryName(fileName)))
            {
                bool fileExists = File.Exists(fileName);
                using (StreamWriter file = new StreamWriter(fileName, true, Encoding.Unicode))
                {
                    if (!fileExists)
                    {
                        file.Write("time");
                        foreach (string key in profit.Keys)
                            file.Write("," + key);
                        file.WriteLine();
                        file.Flush();
                    }
                    file.Write(DateTime.Now.ToString());
                    foreach (string key in profit.Keys)
                        file.Write("," + profit[key]);
                    file.WriteLine();
                    file.Flush();
                }
            }
            else
                WriteLog("Could not write to the profit log due to non-existent directory.");
        }

        /// <summary>
        /// Saves configuration to a file
        /// </summary>
        /// <param name="config">Configuration</param>
        /// <param name="fileName">The file to write to</param>
        public static void SaveConfig(Dictionary<string, object> config, string fileName)
        {
            using (StreamWriter file = new StreamWriter(fileName, false, Encoding.Unicode))
            {
                file.WriteLine("# TMB Switcher configuration file");
                file.WriteLine("# Generated on {0}", DateTime.Now);
                file.WriteLine("#");
                foreach (string key in config.Keys)
                {
                    object value = config[key];
                    string stringValue = null;
                    if (value is bool)
                    {
                        bool boolValue = (bool)value;
                        if (boolValue)
                            stringValue = "true";
                        else
                            stringValue = "false";
                    }
                    else if (value is string)
                        stringValue = "\"" + value.ToString() + "\"";
                    else
                        stringValue = value.ToString();
                    file.WriteLine("{0} = {1}", key, stringValue);
                }
                file.Flush();
            }
        }

        #endregion

        #region Table Utility Methods

        /// <summary>
        /// Adds dictionary data to a table (assumes columns already exist)
        /// </summary>
        /// <param name="data">Data to add to the table</param>
        /// <param name="table">The table to add data to</param>
        public static void AddToTable(Dictionary<string, double> data, DataTable table)
        {
            if (data == null)
                return;
            DataRow row = table.NewRow();
            row["time"] = DateTime.Now;
            foreach (string key in data.Keys)
            {
                // Prevent issues when we are missing columns (i.e. new algorithms added)
                if (table.Columns.Contains(key))
                    row[key] = data[key];
            }
            table.Rows.Add(row);
        }

        /// <summary>
        /// Deletes any data that is older than 24 hours in a table
        /// </summary>
        /// <param name="table">The table to delete from</param>
        public static void DeleteOlderThan24H(DataTable table)
        {
            DateTime deleteTime = DateTime.Now.AddDays(-1);
            while (table.Rows.Count > 0)
            {
                DateTime rowTime = Convert.ToDateTime(table.Rows[0]["time"]);
                if (rowTime < deleteTime)
                    table.Rows.RemoveAt(0);
                else
                    break;
            }
        }

        #endregion

        #region JSON Parsing / Escaping

        /// <summary>
        /// Splits some JSON data into pieces
        /// In general every two pieces represents a key-value pair
        /// </summary>
        /// <param name="jsonData">JSON data to split</param>
        /// <returns>A list of pieces</returns>
        public static string[] SplitJSON(string jsonData)
        {
            return jsonData.Split(new char[] { ':', '"', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Performs initial splitting for JSON data
        /// Generally only necessary when there are multiple result sets
        /// </summary>
        /// <param name="jsonOutput">JSON data to split</param>
        /// <returns>A list of result sets</returns>
        public static string[] SplitJSONOutput(string jsonOutput)
        {
            return jsonOutput.Replace(", ", ",").Replace(": ", ":").Replace("\"\"", "\" \"").Split(new string[] { "},{" }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Escapes some JSON data (used when adding a pool to the miner)
        /// </summary>
        /// <param name="data">Data to escape</param>
        /// <returns>Escaped data</returns>
        public static string EscapeJSON(string data)
        {
            return data.Replace("\\", "\\\\").Replace(",", "\\,");
        }

        #endregion

        #region Configuration Utilities

        public static Dictionary<string, object> ParseConfigFile(string[] configFile)
        {
            Dictionary<string, object> output = new Dictionary<string, object>();
            for (int i = 0; i < configFile.Length; i++)
            {
                string line = configFile[i].Trim();
                // Blank lines are ignored
                // # at the start of the line indicates a comment
                if (line.Length == 0 || line[0] == '#')
                    continue;
                string[] parts = line.Split('#')[0].Split('=');
                if (parts.Length == 1)
                {
                    MessageBox.Show(string.Format("Problem parsing config file on line {0}: \"{1}\". Using defaults.", i, line));
                    return null;
                }

                string nameValue = parts[0].Trim();
                if (parts.Length == 2)
                {
                    double doubleValue = double.NaN;
                    string trimmedPart1 = parts[1].Trim();
                    if (trimmedPart1.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                    {
                        output.Add(nameValue, true);
                        continue;
                    }
                    if (trimmedPart1.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                    {
                        output.Add(nameValue, false);
                        continue;
                    }
                    if (double.TryParse(trimmedPart1, out doubleValue))
                    {
                        output.Add(nameValue, doubleValue);
                        continue;
                    }

                    // If it is not true/false or a number it must be a string
                    output.Add(nameValue, trimmedPart1.Trim('"'));
                    continue;
                }

                // If it has more than 2 parts it must be a string
                // Put the parts back together with the missing "="
                string fullString = string.Join("=", parts, 1, parts.Length - 1).Trim('"');
                output.Add(nameValue, fullString);
            }
            return output;
        }

        public static bool GetBool(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
                return false;
            object value = dict[key];
            if (value != null && value is bool && (bool)value == true)
                return true;
            return false;
        }

        public static double GetDouble(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
                return double.NaN;
            object value = dict[key];
            if (value != null)
            {
                try
                {
                    double fpNumber = Convert.ToDouble(value);
                    return fpNumber;
                }
                catch { }
            }
            return double.NaN;
        }

        public static string GetString(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
                return null;
            object value = dict[key];
            if (value != null)
                return value.ToString();
            return null;
        }

        public static int GetInt(this Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key))
                return -666;
            object value = dict[key];
            if (value != null)
            {
                try
                {
                    int integer = Convert.ToInt32(value);
                    return integer;
                }
                catch { }
            }
            return -666;
        }

        #endregion

        #region Update Checking

        private static readonly string updateURL = "https://dl.dropboxusercontent.com/u/11805555/TMB%20Switcher/latestVersion.txt";

        /// <summary>
        /// Gets the latest version (and associated message if there is one)
        /// </summary>
        /// <param name="message">Message to be displayed if the user needs to update</param>
        /// <returns>The version number for the latest release</returns>
        public static Version getLatestVersion(out string message)
        {
            HttpWebResponse response = null;
            StreamReader dataStream = null;
            message = "";
            try
            {
                HttpWebRequest request = WebRequest.Create(updateURL) as HttpWebRequest;
                response = request.GetResponse() as HttpWebResponse;
                if (response.StatusCode != HttpStatusCode.OK)
                    return null;
                dataStream = new StreamReader(response.GetResponseStream());
                // Only need the first line since that would include the version
                string data = dataStream.ReadLine();
                if (data.Length > 9 || data.Length < 7)
                    return null;
                Version returnValue = new Version(data.Substring(0,7));
                // All other lines will be the message if there is one
                message = dataStream.ReadToEnd();
                return returnValue;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (dataStream != null)
                    dataStream.Close();
                if (response != null)
                    response.Close();
            }
        }

        #endregion
    }
}
