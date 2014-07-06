using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Data;

namespace TMB_Switcher
{
    class Miner_API
    {
        // Private variables
        private static string minerAddress = null;
        private static int minerPort = -1;
        private static bool isActive = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="address">Address to connect to</param>
        /// <param name="port">Port to connect to</param>
        public Miner_API(string address, int port)
        {
            minerAddress = address;
            minerPort = port;
        }

        /// <summary>
        /// Miner address - Generally 127.0.0.1
        /// </summary>
        public static string MinerAddress
        {
            set { minerAddress = value; }
            get { return minerAddress; }
        }

        /// <summary>
        /// Miner port - Generally 4028
        /// </summary>
        public static int MinerPort
        {
            set { minerPort = value; }
            get { return minerPort; }
        }

        /// <summary>
        /// Returns the status of the last command that was run
        /// </summary>
        public static bool IsActive
        {
            set { isActive = value; }
            get { return isActive; }
        }

        /// <summary>
        /// Run an API command and strip out the status header
        /// </summary>
        /// <param name="command">API command to call</param>
        /// <param name="parameters">Parameters to command</param>
        /// <returns>The result</returns>
        public string runCommand(string command, string parameters)
        {
            string apiOutput = performAPICall(command, parameters);
            if (apiOutput != null)
            {
                isActive = true;
                int startBrace = apiOutput.LastIndexOf('[');
                // Check that we haven't found the header
                if (startBrace > 12)
                {
                    int endBrace = apiOutput.LastIndexOf(']');
                    return apiOutput.Substring(startBrace + 1, endBrace - startBrace - 1);
                }
                else
                {
                    // Check status
                    string[] parts = apiOutput.Split(new string[] {"STATUS\":"}, StringSplitOptions.None);
                    if (parts.Length > 2)
                    {
                        return parts[2].Split(',')[0].Trim('"');
                    }
                }
            }
            isActive = false;
            return null;
        }

        /// <summary>
        /// Gets a list of all pools set up in the miner
        /// </summary>
        /// <returns>A list of pools and related information</returns>
        public DataTable pools()
        {
            string poolsOutput = runCommand("pools", null);
            if (poolsOutput == null)
                return null;
            string[] poolsStrings = Utilities.SplitJSONOutput(poolsOutput);
            DataTable pools = new DataTable();
            foreach (string pool in poolsStrings)
            {
                DataRow row = pools.NewRow();
                string[] poolParts = pool.Split(',');
                for (int i = 0; i < poolParts.Length; i++)
                {
                    // Since we use priority for sorting it needs to be an integer
                    string[] parts = Utilities.SplitJSON(poolParts[i]);
                    bool priorityColumn = string.Equals(parts[0], "Priority", StringComparison.InvariantCultureIgnoreCase);
                    if (!pools.Columns.Contains(parts[0]))
                    {
                        if (priorityColumn)
                            pools.Columns.Add(parts[0], typeof(int));
                        else
                            pools.Columns.Add(parts[0]);
                    }
                    string columnText = string.Join(":", parts, 1, parts.Length - 1).Replace("http:// ", "");
                    if (priorityColumn)
                        row[parts[0]] = Convert.ToInt32(columnText);
                    else
                        row[parts[0]] = columnText;
                }
                pools.Rows.Add(row);
            }
            return pools;
        }

        /// <summary>
        /// Gets a list of devices in use by the miner
        /// </summary>
        /// <returns>A list of devices and related information</returns>
        public DataTable devices()
        {
            string devsOutput = runCommand("devs", null);
            string devDetailsOutput = runCommand("devdetails", null);
            if (devDetailsOutput == null ||
                devsOutput == null)
                return null;
            string[] devsStrings = Utilities.SplitJSONOutput(devsOutput);
            string[] devDetailsStrings = Utilities.SplitJSONOutput(devDetailsOutput);
            DataTable devices =  new DataTable();
            for (int i = 0; i < devDetailsStrings.Length; i++)
            {
                DataRow row = devices.NewRow();
                string[] devParts = devDetailsStrings[i].Split(',');
                for (int j = 0; j < devParts.Length; j++)
                {
                    string[] parts = Utilities.SplitJSON(devParts[j]);
                    if (!devices.Columns.Contains(parts[0]))
                        devices.Columns.Add(parts[0]);
                    row[parts[0]] = parts[1];
                }
                devParts = devsStrings[i].Split(',');
                for (int j = 0; j < devParts.Length; j++)
                {
                    string[] parts = Utilities.SplitJSON(devParts[j]);
                    if (!devices.Columns.Contains(parts[0]))
                        devices.Columns.Add(parts[0]);
                    row[parts[0]] = parts[1];
                }
                devices.Rows.Add(row);
            }
            return devices;
        }

        /// <summary>
        /// Gets a summary of various information from the miner
        /// </summary>
        /// <returns>A list of key-value pairs containing the information</returns>
        public Dictionary<string, double> summary()
        {
            string summaryOutput = runCommand("summary", null);
            if (summaryOutput == null)
                return null;
            string[] summaryStrings = summaryOutput.Split(',');
            Dictionary<string, double> summary = new Dictionary<string, double>();
            foreach (string summaryParts in summaryStrings)
            {
                string[] parts = Utilities.SplitJSON(summaryParts);
                summary.Add(parts[0].Trim(), Convert.ToDouble(parts[1].Trim()));
            }
            return summary;
        }

        /// <summary>
        /// Performs an API request to the miner
        /// </summary>
        /// <param name="command">API command to call</param>
        /// <param name="parameters">Parameters to command</param>
        /// <returns>The result</returns>
        private string performAPICall(string command, string parameters)
        {
            TcpClient client = null;
            NetworkStream stream = null;
            StreamReader reader = null;
            StreamWriter writer = null;

            try
            {
                client = new TcpClient(minerAddress, minerPort);
                stream = client.GetStream();
                writer = new StreamWriter(stream);
                reader = new StreamReader(stream);

                StringBuilder commandString = new StringBuilder();
                commandString.Append("{\"command\":\"");
                commandString.Append(command);
                if (parameters != null)
                {
                    commandString.Append("\",\"parameter\":\"");
                    commandString.Append(parameters);
                }
                commandString.Append("\"}");

                writer.Write(commandString.ToString());
                writer.Flush();
                string data = reader.ReadToEnd();
                return data;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (writer != null)
                    writer.Close();
                if (reader != null)
                    reader.Close();
                if (stream != null)
                    stream.Close();
                if (client != null)
                    client.Close();
            }
        }
    }
}
