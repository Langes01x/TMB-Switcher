using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace TMB_Switcher
{
    class TMB_API
    {
        private static string apiKey;
        private static readonly string baseURL = "https://pool.trademybit.com/api/{0}?key={1}";
        private static bool isActive = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">API key</param>
        public TMB_API(string key)
        {
            apiKey = key;
        }

        /// <summary>
        /// API key for accessing the pool
        /// </summary>
        public static string APIKey
        {
            set { apiKey = value; }
            get { return apiKey; }
        }

        /// <summary>
        /// Returns the status of the last command that was run
        /// </summary>
        public static bool IsActive
        {
            get { return isActive; }
        }

        /// <summary>
        /// Performs an API request to the TMB site
        /// </summary>
        /// <param name="command">API command to call</param>
        /// <returns>The result</returns>
        private string performAPICall(string command)
        {
            HttpWebResponse response = null;
            StreamReader dataStream = null;
            try
            {
                isActive = false;
                HttpWebRequest request = WebRequest.Create(string.Format(baseURL, command, apiKey)) as HttpWebRequest;
                response = request.GetResponse() as HttpWebResponse;
                if (response.StatusCode != HttpStatusCode.OK)
                    return null;
                dataStream = new StreamReader(response.GetResponseStream());
                string data = dataStream.ReadToEnd();
                if (data.Contains("Invalid API Key"))
                    return null;
                isActive = true;
                return data;
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

        /// <summary>
        /// Retrieves the profitability for the various algorithms
        /// 
        /// Keys:
        /// scrypt, nscrypt, x11, x13, x15, nist5 (and potentially any other algorithm that has been recently added)
        /// </summary>
        /// <returns>A dictionary containing the profitability info</returns>
        public Dictionary<string, double> bestAlgo()
        {
            try
            {
                string data = performAPICall("bestalgo");
                if (data == null)
                    return null;
                string[] parts = data.Split(new char[] {',', '[', ']', '{', '}'}, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length % 2 != 0)
                    return null;
                Dictionary<string, double> output = new Dictionary<string, double>();
                for (int i = 0; i < parts.Length; i+=2)
                {
                    string[] algoParts = Utilities.SplitJSON(parts[i]);
                    string[] scoreParts = Utilities.SplitJSON(parts[i+1]);
                    output.Add(algoParts[1], Convert.ToDouble(scoreParts[1]));
                }
                return output;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the pool and user hash info
        /// 
        /// Keys:
        /// pool_hash, user_hash
        /// </summary>
        /// <returns>A dictionary containing the hash info</returns>
        public Dictionary<string, double> hashInfo()
        {
            try
            {
                string data = performAPICall("hashinfo");
                if (data == null)
                    return null;
                string[] parts = data.Substring(1, data.Length - 2).Split(',');
                // Ignore worker info for now
                Dictionary<string, double> output = new Dictionary<string, double>();
                string[] hashParts = Utilities.SplitJSON(parts[0]);
                output.Add(hashParts[0], Convert.ToDouble(hashParts[1]));
                hashParts = Utilities.SplitJSON(parts[1]);
                output.Add(hashParts[0], Convert.ToDouble(hashParts[1]));
                return output;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the balances associated with the account
        /// 
        /// Keys:
        /// est_total, unexchanged, exchanged, alltime, (coin)-Confirmed, (coin)-Unconfirmed
        /// </summary>
        /// <returns>A dictionary containing the user's balances</returns>
        public Dictionary<string, double> balance()
        {
            try
            {
                string data = performAPICall("balance");
                if (data == null)
                    return null;
                string[] parts = data.Split(new string[] { "{\"autoexchange\":", ",\"coins\":" }, StringSplitOptions.RemoveEmptyEntries);
                string[] exchangeParts = parts[0].Split(new char[] { ',', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
                string[] coinParts = parts[1].Split(new char[] { ',', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
                if (coinParts.Length % 3 != 0)
                    return null;
                Dictionary<string, double> output = new Dictionary<string, double>();
                for (int i = 0; i < exchangeParts.Length; i++)
                {
                    parts = Utilities.SplitJSON(exchangeParts[i]);
                    output.Add(parts[0], Convert.ToDouble(parts[1]));
                }
                for (int i = 0; i < coinParts.Length; i+=3)
                {
                    string coinName = Utilities.SplitJSON(coinParts[i])[0];
                    double confirmed = Convert.ToDouble(Utilities.SplitJSON(coinParts[i+1])[1]);
                    double unconfirmed = Convert.ToDouble(Utilities.SplitJSON(coinParts[i+2])[1]);
                    output.Add(coinName + "-Confirmed", confirmed);
                    output.Add(coinName + "-Unconfirmed", unconfirmed);
                }
                return output;
            }
            catch
            {
                return null;
            }
        }
    }
}
