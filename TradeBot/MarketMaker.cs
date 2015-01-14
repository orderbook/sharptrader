/*
 * Program: Trading bot
 * Author: ICBIT team (https://icbit.se)
 * License: GPLv3 by Free Software Foundation
 * Comments: Core arbitrage trading logic implementation
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.IO;
using System.Net;
using System.Security.Cryptography;

using Newtonsoft.Json;

using WebSockets.Net;
using WebSockets.Utils;

namespace MarketMaker.Trades
{
    [Serializable]
    public class IcbitConfig
    {
        public uint UserId { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
    }

    [Serializable]
    public class BitstampConfig
    {
        public string UserId { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }
    }

    [Serializable]
    public class MarketMakerConfig
    {
        public IcbitConfig icbit { get; set; }
        public BitstampConfig bitstamp { get; set; }
    }

    // Signature is compatible between Bitstamp and ICBIT
    public class Authenticator
    {
        /*public void AddApiAuthentication(RestRequest restRequest)
        {
            var nonce = DateTime.Now.Ticks;
            var signature = GetSignature(nonce, apiKey, apiSecret, clientId);

            restRequest.AddParameter("key", apiKey);
            restRequest.AddParameter("signature", signature);
            restRequest.AddParameter("nonce", nonce);
        }*/

        public static long GetUnixTimeStamp()
        {
            DateTime time = DateTime.UtcNow;
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            // unix is number of microseconds since 1 Jan 1970
            TimeSpan epochTimespan = time.Subtract(dtDateTime);

            // ticks per microsecond = 10.
            long result = epochTimespan.Ticks / 10000000;
            return result;
        }

        public static string GetSignature(long nonce, string key, string secret, string clientId)
        {
            string msg = string.Format("{0}{1}{2}", nonce, clientId, key);

            return ByteArrayToString(SignHMACSHA256(secret, Encoding.ASCII.GetBytes(msg))).ToUpper();
        }

        private static byte[] SignHMACSHA256(String key, byte[] data)
        {
            HMACSHA256 hashMaker = new HMACSHA256(Encoding.ASCII.GetBytes(key));
            return hashMaker.ComputeHash(data);
        }

        private static string ByteArrayToString(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "");
        }
    }

    public class MarketMaker
    {
        Icbit icbit;

        MarketMakerConfig config;

        public MarketMaker(string configFilename)
        {
            // Read config file
            try
            {
                using (StreamReader sr = new StreamReader(configFilename))
                {
                    config = JsonConvert.DeserializeObject<MarketMakerConfig>(sr.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while reading config file: " + e.ToString());
                return;
            }

            Pusher p = new Pusher();
            p.Open("ws://ws.pusherapp.com/app/de504dc5763aeef9ff52?protocol=5&client=pusher-dotnet-client&version=0.0.1");
            p.Subscribe("order_book");

            // Create ICBIT exchange object
            icbit = new Icbit(config.icbit.UserId, config.icbit.ApiKey, config.icbit.ApiSecret);
            icbit.OrdersChanged += new EventHandler(IcbitOrdersChanged);
            icbit.BalanceChanged += new EventHandler(IcbitBalanceChanged);
            icbit.ConnectEvent += new EventHandler(IcbitConnected);

            // Connect to the ICBIT exchange
            icbit.Connect();
        }

        public void IcbitBalanceChanged(object sender, EventArgs e)
        {
            Console.WriteLine("ICBIT: Balance updated!");
        }

        public void IcbitOrdersChanged(object sender, EventArgs e)
        {
            Console.WriteLine("ICBIT: Orders updated!");
        }

        public void IcbitConnected(object sender, EventArgs e)
        {
            Console.WriteLine("ICBIT: Connected and ready to work!");
            //icbit.SubscribeToChannel("orderbook_BUZ3");

            //icbit.CreateOrder("BUF5", true, 182, 10);
        }

        public static string HttpPost(string uri, string parameters)
        {
            bool requestFailed = false;

            // parameters: name1=value1&name2=value2	
            //System.Net.ServicePointManager.CertificatePolicy = new MyPolicy();
            ServicePointManager.MaxServicePointIdleTime = 200;
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
            //webRequest.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US) AppleWebKit/534.21 (KHTML, like Gecko) Chrome/11.0.682.0 Safari/534.21";
            webRequest.UserAgent = "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 5.1)";

            //string ProxyString = 
            //   System.Configuration.ConfigurationManager.AppSettings
            //   [GetConfigKey("proxy")];
            //webRequest.Proxy = new WebProxy (ProxyString, true);
            //Commenting out above required change to App.Config
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(parameters);
            Stream os = null;
            try
            { // send the Post
                webRequest.ContentLength = bytes.Length;   //Count bytes to send
                os = webRequest.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);         //Send it
            }
            catch (WebException ex)
            {
                //MessageBox.Show(ex.Message, "HttpPost: Request error",
                //MessageBoxButtons.OK, MessageBoxIcon.Error);
                requestFailed = true;
            }
            finally
            {
                if (os != null)
                {
                    os.Close();
                }
            }

            if (!requestFailed)
            {
                try
                { // get the response
                    WebResponse webResponse = webRequest.GetResponse();
                    if (webResponse == null)
                    { return null; }
                    StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                    return sr.ReadToEnd().Trim();
                }
                catch (WebException ex)
                {
                    // MessageBox.Show(ex.Message, "HttpPost: Response error",
                    //   MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return null;
        }
    }
}
